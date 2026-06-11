namespace Orion.Api.Models;

public enum Units
{
    Metric,
    Imperial
}

public enum Provider
{
    OpenMeteo,
    OpenWeather
}

public record WeatherResponse(
    double Latitude,
    double Longitude,
    Units Units,
    DateTimeOffset Timestamp,
    Provider[] Providers,

    double Temperature,
    double FeelsLike,
    int Humidity,
    double Pressure,
    double WindSpeed,
    int WindDirection,
    int CloudCover,

    double? UvIndex,
    double? Precipitation,

    int? Visibility,
    string? Summary,
    DateTimeOffset? Sunrise,
    DateTimeOffset? Sunset,
    string? City,
    string? Country
);
