using Orion.Api.Models;

namespace Orion.Tests.Helpers;

internal static class WeatherResponses
{
    public static WeatherResponse Metric(
        double latitude = 47.6,
        double longitude = -122.3,
        DateTimeOffset? timestamp = null,
        Provider[]? providers = null,
        double temperature = 20,
        double feelsLike = 18,
        int humidity = 60,
        double pressure = 1013,
        double windSpeed = 5,
        int windDirection = 270,
        int cloudCover = 40,
        double? uvIndex = null,
        double? precipitation = null,
        int? visibility = null,
        string? summary = null,
        string? condition = null,
        DateTimeOffset? sunrise = null,
        DateTimeOffset? sunset = null,
        string? city = null,
        string? country = null) =>
        new WeatherResponse(
            Latitude: latitude,
            Longitude: longitude,
            Units: Units.Metric,
            Timestamp: timestamp ?? DateTimeOffset.UnixEpoch,
            Providers: providers ?? Array.Empty<Provider>(),
            Temperature: temperature,
            FeelsLike: feelsLike,
            Humidity: humidity,
            Pressure: pressure,
            WindSpeed: windSpeed,
            WindDirection: windDirection,
            CloudCover: cloudCover,
            UvIndex: uvIndex,
            Precipitation: precipitation,
            Visibility: visibility,
            Summary: summary,
            Condition: condition,
            Sunrise: sunrise,
            Sunset: sunset,
            City: city,
            Country: country
        );
}
