using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class ReverseGeocodingServiceTests
{
    private const double Lat = 47.6;
    private const double Lon = -122.3;

    // A trimmed BigDataCloud reverse-geocode-client payload.
    private const string SeattleJson = """
    {
        "latitude": 47.6062,
        "longitude": -122.3321,
        "countryName": "United States of America (the)",
        "countryCode": "US",
        "principalSubdivision": "Washington",
        "city": "Seattle",
        "locality": "Seattle"
    }
    """;

    [Fact]
    public async Task HappyPath_MapsFields_UsingInputCoords()
    {
        var service = CreateService(FakeHttpMessageHandler.Json(SeattleJson));

        var result = await service.ReverseAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Equal("Seattle", result.Name);
        Assert.Equal("Washington", result.Region); // principalSubdivision -> Region
        Assert.Equal("United States of America (the)", result.Country);
        Assert.Equal("US", result.CountryCode);
        Assert.Equal(Lat, result.Latitude); // echoes the input coords, not the payload's
        Assert.Equal(Lon, result.Longitude);
    }

    [Fact]
    public async Task EmptyCity_FallsBackToLocality()
    {
        var json = """
        { "city": "", "locality": "Greater London", "countryName": "United Kingdom", "countryCode": "GB" }
        """;
        var service = CreateService(FakeHttpMessageHandler.Json(json));

        var result = await service.ReverseAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Equal("Greater London", result.Name);
    }

    [Fact]
    public async Task NoCityOrLocality_ReturnsNull()
    {
        // Open ocean / unnamed place: nothing to display.
        var json = """{ "city": "", "locality": "", "countryName": "", "countryCode": "" }""";
        var service = CreateService(FakeHttpMessageHandler.Json(json));

        var result = await service.ReverseAsync(Lat, Lon, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task HttpError_ReturnsNull()
    {
        var service = CreateService(FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        Assert.Null(await service.ReverseAsync(Lat, Lon, default));
    }

    [Fact]
    public async Task MalformedJson_ReturnsNull()
    {
        var service = CreateService(FakeHttpMessageHandler.Json("not json {{{"));

        Assert.Null(await service.ReverseAsync(Lat, Lon, default));
    }

    [Fact]
    public async Task SendsLatLonInQueryString()
    {
        var handler = FakeHttpMessageHandler.Json(SeattleJson);
        var service = CreateService(handler);

        await service.ReverseAsync(Lat, Lon, default);

        Assert.NotNull(handler.LastRequest);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("latitude=47.6", query);
        Assert.Contains("longitude=-122.3", query);
    }

    [Fact]
    public async Task SameCoords_IsCached_UpstreamFetchedOnce()
    {
        var cache = new TestHybridCache();
        var http = new HttpClient(FakeHttpMessageHandler.Json(SeattleJson))
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        var service = new BigDataCloudReverseGeocoder(http, cache, NullLogger<BigDataCloudReverseGeocoder>.Instance);

        await service.ReverseAsync(Lat, Lon, default);
        await service.ReverseAsync(Lat, Lon, default);

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
        var service = new BigDataCloudReverseGeocoder(http, cache, NullLogger<BigDataCloudReverseGeocoder>.Instance);

        await service.ReverseAsync(Lat, Lon, default);
        await service.ReverseAsync(Lat, Lon, default);

        Assert.Equal(2, cache.FactoryCallCount);
    }

    private static BigDataCloudReverseGeocoder CreateService(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        return new BigDataCloudReverseGeocoder(http, new TestHybridCache(), NullLogger<BigDataCloudReverseGeocoder>.Instance);
    }
}
