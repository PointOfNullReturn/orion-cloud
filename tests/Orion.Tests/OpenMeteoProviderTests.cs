using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orion.Api.Models;
using Orion.Api.Services;
using Orion.Tests.Helpers;

namespace Orion.Tests;

public class OpenMeteoProviderTests
{
    private const double Lat = 47.6;
    private const double Lon = -122.3;

    [Fact]
    public async Task HappyPath_MapsAllFields()
    {
        var json = """
        {
            "latitude": 47.625,
            "longitude": -122.3,
            "current": {
                "temperature_2m": 12.5,
                "apparent_temperature": 10.0,
                "relative_humidity_2m": 65,
                "pressure_msl": 1013.2,
                "wind_speed_10m": 5.5,
                "wind_direction_10m": 270,
                "cloud_cover": 40,
                "uv_index": 3.5,
                "precipitation": 0.2
            }
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Equal(47.625, result.Latitude);
        Assert.Equal(-122.3, result.Longitude);
        Assert.Equal(Units.Metric, result.Units);
        Assert.Equal(12.5, result.Temperature);
        Assert.Equal(10.0, result.FeelsLike);
        Assert.Equal(65, result.Humidity);
        Assert.Equal(1013.2, result.Pressure);
        Assert.Equal(5.5, result.WindSpeed);
        Assert.Equal(270, result.WindDirection);
        Assert.Equal(40, result.CloudCover);
        Assert.Equal(3.5, result.UvIndex);
        Assert.Equal(0.2, result.Precipitation);
        Assert.Equal(new[] { Provider.OpenMeteo }, result.Providers);

        Assert.Null(result.Visibility);
        Assert.Null(result.Summary);
        Assert.Null(result.Condition);
        Assert.Null(result.Sunrise);
        Assert.Null(result.Sunset);
        Assert.Null(result.City);
        Assert.Null(result.Country);
    }

    [Fact]
    public async Task HappyPath_OptionalUvAndPrecipitation_AreNullable()
    {
        var json = """
        {
            "latitude": 47.6,
            "longitude": -122.3,
            "current": {
                "temperature_2m": 12.5,
                "apparent_temperature": 10.0,
                "relative_humidity_2m": 65,
                "pressure_msl": 1013.2,
                "wind_speed_10m": 5.5,
                "wind_direction_10m": 270,
                "cloud_cover": 40
            }
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(result);
        Assert.Null(result.UvIndex);
        Assert.Null(result.Precipitation);
    }

    [Fact]
    public async Task MissingRequiredField_ReturnsNull()
    {
        var json = """
        {
            "latitude": 47.6,
            "longitude": -122.3,
            "current": {
                "apparent_temperature": 10.0,
                "relative_humidity_2m": 65,
                "pressure_msl": 1013.2,
                "wind_speed_10m": 5.5,
                "wind_direction_10m": 270,
                "cloud_cover": 40
            }
        }
        """;
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.Null(result);
    }

    [Fact]
    public async Task MissingCurrentSection_ReturnsNull()
    {
        var json = """{"latitude": 47.6, "longitude": -122.3}""";
        var provider = CreateProvider(FakeHttpMessageHandler.Json(json));

        var result = await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.Null(result);
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
    public async Task SendsLatLonInQueryString()
    {
        var handler = FakeHttpMessageHandler.Json("""
        {
            "latitude": 47.6,
            "longitude": -122.3,
            "current": {
                "temperature_2m": 12.5, "apparent_temperature": 10.0,
                "relative_humidity_2m": 65, "pressure_msl": 1013.2,
                "wind_speed_10m": 5.5, "wind_direction_10m": 270,
                "cloud_cover": 40
            }
        }
        """);
        var provider = CreateProvider(handler);

        await provider.GetCurrentWeatherAsync(Lat, Lon, default);

        Assert.NotNull(handler.LastRequest);
        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("latitude=47.6", query);
        Assert.Contains("longitude=-122.3", query);
    }

    private static OpenMeteoProvider CreateProvider(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://example.test/"),
        };
        return new OpenMeteoProvider(http, NullLogger<OpenMeteoProvider>.Instance);
    }
}
