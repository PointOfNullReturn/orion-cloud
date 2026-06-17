using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class GeocodingServiceTests
{
    // A realistic Open-Meteo geocoding payload (trimmed to the fields we map).
    private const string SeattleJson = """
    {
        "results": [
            {
                "id": 5809844,
                "name": "Seattle",
                "latitude": 47.60621,
                "longitude": -122.33207,
                "country_code": "US",
                "country": "United States",
                "admin1": "Washington",
                "admin2": "King"
            }
        ],
        "generationtime_ms": 0.5
    }
    """;

    [Fact]
    public async Task HappyPath_MapsFields()
    {
        var service = CreateService(FakeHttpMessageHandler.Json(SeattleJson));

        var results = await service.SearchAsync("Seattle", count: 5, default);

        var match = Assert.Single(results);
        Assert.Equal("Seattle", match.Name);
        Assert.Equal("Washington", match.Region); // admin1 -> Region
        Assert.Equal("United States", match.Country);
        Assert.Equal("US", match.CountryCode); // country_code -> CountryCode
        Assert.Equal(47.60621, match.Latitude);
        Assert.Equal(-122.33207, match.Longitude);
    }

    [Fact]
    public async Task MissingOptionalFields_MapToNull()
    {
        var json = """
        {
            "results": [
                { "name": "Nowhere", "latitude": 1.0, "longitude": 2.0 }
            ]
        }
        """;
        var service = CreateService(FakeHttpMessageHandler.Json(json));

        var match = Assert.Single(await service.SearchAsync("Nowhere", count: 5, default));
        Assert.Equal("Nowhere", match.Name);
        Assert.Null(match.Region);
        Assert.Null(match.Country);
        Assert.Null(match.CountryCode);
    }

    [Fact]
    public async Task NoResultsKey_ReturnsEmpty()
    {
        // Open-Meteo omits "results" entirely when nothing matches.
        var service = CreateService(FakeHttpMessageHandler.Json("""{ "generationtime_ms": 0.1 }"""));

        var results = await service.SearchAsync("asdfghjkl", count: 5, default);

        Assert.Empty(results);
    }

    [Fact]
    public async Task CapsResultsAtRequestedCount()
    {
        var json = """
        {
            "results": [
                { "name": "A", "latitude": 1, "longitude": 1 },
                { "name": "B", "latitude": 2, "longitude": 2 },
                { "name": "C", "latitude": 3, "longitude": 3 }
            ]
        }
        """;
        var service = CreateService(FakeHttpMessageHandler.Json(json));

        var results = await service.SearchAsync("multi", count: 2, default);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task HttpError_ReturnsEmpty()
    {
        var service = CreateService(FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        var results = await service.SearchAsync("Seattle", count: 5, default);

        Assert.Empty(results);
    }

    [Fact]
    public async Task MalformedJson_ReturnsEmpty()
    {
        var service = CreateService(FakeHttpMessageHandler.Json("not json {{{"));

        var results = await service.SearchAsync("Seattle", count: 5, default);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SendsQueryAndCountInQueryString()
    {
        var handler = FakeHttpMessageHandler.Json(SeattleJson);
        var service = CreateService(handler);

        await service.SearchAsync("San Diego", count: 5, default);

        Assert.NotNull(handler.LastRequest);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("name=San%20Diego", query); // query is URL-encoded
        Assert.Contains("count=5", query);
    }

    [Fact]
    public async Task SameQuery_IsCached_UpstreamFetchedOnce()
    {
        var cache = new TestHybridCache();
        var http = new HttpClient(FakeHttpMessageHandler.Json(SeattleJson))
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        var service = new GeocodingService(http, cache, NullLogger<GeocodingService>.Instance);

        await service.SearchAsync("Seattle", count: 5, default);
        await service.SearchAsync("Seattle", count: 5, default);

        Assert.Equal(1, cache.FactoryCallCount);
    }

    [Fact]
    public async Task UpstreamError_IsNotCached()
    {
        var cache = new TestHybridCache();
        var http = new HttpClient(FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError))
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        var service = new GeocodingService(http, cache, NullLogger<GeocodingService>.Instance);

        // Error returns empty but must NOT be cached, so a retry re-hits upstream.
        await service.SearchAsync("Seattle", count: 5, default);
        await service.SearchAsync("Seattle", count: 5, default);

        Assert.Equal(2, cache.FactoryCallCount);
    }

    private static GeocodingService CreateService(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        return new GeocodingService(http, new TestHybridCache(), NullLogger<GeocodingService>.Instance);
    }
}
