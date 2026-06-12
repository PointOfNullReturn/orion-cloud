using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Orion.Api.Configuration;
using Orion.Api.Models;

namespace Orion.Api.Services;

public class OpenWeatherProvider : IWeatherProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenWeatherProvider> _logger;
    private readonly string _apiKey;

    public OpenWeatherProvider(
        HttpClient httpClient,
        IOptions<WeatherOptions> options,
        ILogger<OpenWeatherProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = options.Value.OpenWeather.ApiKey;
    }

    public Provider Name => Provider.OpenWeather;

    public async Task<WeatherResponse?> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("OpenWeather API key is not configured; skipping provider");
            return null;
        }

        var url = FormattableString.Invariant(
            $"weather?lat={latitude}&lon={longitude}&units=metric");

        OpenWeatherResponse? payload;
        try
        {
            payload = await _httpClient.GetFromJsonAsync<OpenWeatherResponse>(url, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "OpenWeather HTTP request failed");
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "OpenWeather response could not be parsed");
            return null;
        }

        if (payload is null)
        {
            _logger.LogWarning("OpenWeather returned no payload");
            return null;
        }

        var main = payload.Main;
        var wind = payload.Wind;
        var clouds = payload.Clouds;

        if (main?.Temp is null ||
            main.FeelsLike is null ||
            main.Humidity is null ||
            main.Pressure is null ||
            wind?.Speed is null ||
            wind.Deg is null ||
            clouds?.All is null)
        {
            _logger.LogWarning("OpenWeather response is missing required fields");
            return null;
        }

        var timezoneOffset = TimeSpan.FromSeconds(payload.Timezone ?? 0);
        var firstWeather = payload.Weather?.FirstOrDefault();

        return new WeatherResponse(
            Latitude: payload.Coord?.Lat ?? latitude,
            Longitude: payload.Coord?.Lon ?? longitude,
            Units: Units.Metric,
            Timestamp: DateTimeOffset.UtcNow,
            Providers: new[] { Provider.OpenWeather },

            Temperature: main.Temp.Value,
            FeelsLike: main.FeelsLike.Value,
            Humidity: (int)Math.Round(main.Humidity.Value),
            Pressure: main.Pressure.Value,
            WindSpeed: wind.Speed.Value,
            WindDirection: (int)Math.Round(wind.Deg.Value),
            CloudCover: (int)Math.Round(clouds.All.Value),

            UvIndex: null,
            Precipitation: null,

            Visibility: payload.Visibility is { } v ? (int)Math.Round(v) : null,
            Summary: firstWeather?.Description,
            Condition: firstWeather?.Main,
            Sunrise: payload.Sys?.Sunrise is { } sunrise
                ? DateTimeOffset.FromUnixTimeSeconds(sunrise).ToOffset(timezoneOffset)
                : null,
            Sunset: payload.Sys?.Sunset is { } sunset
                ? DateTimeOffset.FromUnixTimeSeconds(sunset).ToOffset(timezoneOffset)
                : null,
            City: payload.Name,
            Country: payload.Sys?.Country
        );
    }

    private sealed record OpenWeatherResponse
    {
        [JsonPropertyName("coord")] public Coord? Coord { get; init; }
        [JsonPropertyName("weather")] public List<Weather>? Weather { get; init; }
        [JsonPropertyName("main")] public Main? Main { get; init; }
        [JsonPropertyName("visibility")] public double? Visibility { get; init; }
        [JsonPropertyName("wind")] public Wind? Wind { get; init; }
        [JsonPropertyName("clouds")] public Clouds? Clouds { get; init; }
        [JsonPropertyName("sys")] public Sys? Sys { get; init; }
        [JsonPropertyName("timezone")] public int? Timezone { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
    }

    private sealed record Coord
    {
        [JsonPropertyName("lat")] public double Lat { get; init; }
        [JsonPropertyName("lon")] public double Lon { get; init; }
    }

    private sealed record Weather
    {
        [JsonPropertyName("main")] public string? Main { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
    }

    private sealed record Main
    {
        [JsonPropertyName("temp")] public double? Temp { get; init; }
        [JsonPropertyName("feels_like")] public double? FeelsLike { get; init; }
        [JsonPropertyName("pressure")] public double? Pressure { get; init; }
        [JsonPropertyName("humidity")] public double? Humidity { get; init; }
    }

    private sealed record Wind
    {
        [JsonPropertyName("speed")] public double? Speed { get; init; }
        [JsonPropertyName("deg")] public double? Deg { get; init; }
    }

    private sealed record Clouds
    {
        [JsonPropertyName("all")] public double? All { get; init; }
    }

    private sealed record Sys
    {
        [JsonPropertyName("country")] public string? Country { get; init; }
        [JsonPropertyName("sunrise")] public long? Sunrise { get; init; }
        [JsonPropertyName("sunset")] public long? Sunset { get; init; }
    }
}
