namespace Orion.Api.Configuration;

public class WeatherOptions
{
    public const string SectionName = "Weather";

    public int CacheTtlSeconds { get; set; } = 300;

    public OpenMeteoOptions OpenMeteo { get; set; } = new();
    public OpenWeatherOptions OpenWeather { get; set; } = new();
    public RateLimitOptions RateLimit { get; set; } = new();
}

public class RateLimitOptions
{
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}

public class OpenMeteoOptions
{
    public string BaseUrl { get; set; } = string.Empty;
}

public class OpenWeatherOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
