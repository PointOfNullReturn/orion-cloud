using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orion.Api.Configuration;
using Orion.Api.Models;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class OpenWeatherProviderTests
{
    private const double Lat = 47.6;
    private const double Lon = -122.3;
    private const string ApiKey = "test-key";

    [Fact]
    public async Task HappyPath_MapsAllFields()
    {
        // sunrise=1700000000 → 2023-11-14T22:13:20Z; timezone=-25200 (UTC-7) → 15:13:20-07:00
        var json = """
        {
            "coord": { "lat": 47.625, "lon": -122.31 },
            "weather": [ { "main": "Clouds", "description": "scattered clouds" } ],
            "main": {
                "temp": 12.5,
                "feels_like": 10.0,
                "humidity": 65,
                "pressure": 1013
            },
            "visibility": 10000,
            "wind": { "speed": 5.5, "deg": 270 },
            "clouds": { "all": 40 },
            "sys": {
                "country": "US",
                "sunrise": 1700000000,
                "sunset": 1700040000
            },
            "timezone": -25200,
            "name": "Seattle"
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Equal(47.625, result.Latitude);
        Assert.Equal(-122.31, result.Longitude);
        Assert.Equal(Units.Metric, result.Units);
        Assert.Equal(12.5, result.Temperature);
        Assert.Equal(10.0, result.FeelsLike);
        Assert.Equal(65, result.Humidity);
        Assert.Equal(1013, result.Pressure);
        Assert.Equal(5.5, result.WindSpeed);
        Assert.Equal(270, result.WindDirection);
        Assert.Equal(40, result.CloudCover);
        Assert.Equal(10000, result.Visibility);
        Assert.Equal("scattered clouds", result.Summary);
        Assert.Equal("Clouds", result.Condition);
        Assert.Equal("Seattle", result.City);
        Assert.Equal("US", result.Country);
        Assert.Equal(new[] { Provider.OpenWeather }, result.Providers);

        var expectedOffset = TimeSpan.FromHours(-7);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).ToOffset(expectedOffset), result.Sunrise);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700040000).ToOffset(expectedOffset), result.Sunset);

        // OpenWeather doesn't supply these
        Assert.Null(result.UvIndex);
        Assert.Null(result.Precipitation);
    }

    [Fact]
    public async Task MissingApiKey_ReturnsNull_WithoutCallingApi()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var provider = CreateProvider(handler, apiKey: "");

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.Null(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task WhitespaceApiKey_ReturnsNull_WithoutCallingApi()
    {
        var handler = FakeHttpMessageHandler.Json("{}");
        var provider = CreateProvider(handler, apiKey: "   ");

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.Null(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task MissingCoord_FallsBackToRequestedLatLon()
    {
        var json = """
        {
            "main": { "temp": 12.5, "feels_like": 10.0, "humidity": 65, "pressure": 1013 },
            "wind": { "speed": 5.5, "deg": 270 },
            "clouds": { "all": 40 }
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Equal(Lat, result.Latitude);
        Assert.Equal(Lon, result.Longitude);
    }

    [Fact]
    public async Task MissingRequiredField_ReturnsNull()
    {
        // wind.speed omitted
        var json = """
        {
            "main": { "temp": 12.5, "feels_like": 10.0, "humidity": 65, "pressure": 1013 },
            "wind": { "deg": 270 },
            "clouds": { "all": 40 }
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task MissingWeatherArray_OmitsSummaryAndCondition()
    {
        var json = """
        {
            "main": { "temp": 12.5, "feels_like": 10.0, "humidity": 65, "pressure": 1013 },
            "wind": { "speed": 5.5, "deg": 270 },
            "clouds": { "all": 40 }
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Null(result.Summary);
        Assert.Null(result.Condition);
    }

    [Fact]
    public async Task MissingSys_OmitsSunriseSunsetAndCountry()
    {
        var json = """
        {
            "main": { "temp": 12.5, "feels_like": 10.0, "humidity": 65, "pressure": 1013 },
            "wind": { "speed": 5.5, "deg": 270 },
            "clouds": { "all": 40 }
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Null(result.Sunrise);
        Assert.Null(result.Sunset);
        Assert.Null(result.Country);
    }

    [Fact]
    public async Task HttpError_ReturnsNull()
    {
        var provider = CreateProvider(
            FakeHttpMessageHandler.Status(HttpStatusCode.InternalServerError));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task MalformedJson_ReturnsNull()
    {
        var provider = CreateProvider(FakeHttpMessageHandler.Json("not json {{{"));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task SendsLatLonAndMetricUnitsInQueryString()
    {
        var handler = FakeHttpMessageHandler.Json("""
        {
            "main": { "temp": 12.5, "feels_like": 10.0, "humidity": 65, "pressure": 1013 },
            "wind": { "speed": 5.5, "deg": 270 },
            "clouds": { "all": 40 }
        }
        """);
        var provider = CreateProvider(handler);

        await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(handler.LastRequest);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("lat=47.6", query);
        Assert.Contains("lon=-122.3", query);
        Assert.Contains("units=metric", query);
    }

    private static OpenWeatherProvider CreateProvider(
        FakeHttpMessageHandler handler,
        string apiKey = ApiKey)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        var options = Options.Create(new WeatherOptions
        {
            OpenWeather = new OpenWeatherOptions { ApiKey = apiKey },
        });
        return new OpenWeatherProvider(http, options, NullLogger<OpenWeatherProvider>.Instance);
    }
}
