using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Orion.Api.Models;

namespace Orion.Api.Services;

public class OpenMeteoProvider : IWeatherProvider
{
    private const string CurrentFields =
        "temperature_2m,apparent_temperature,relative_humidity_2m,pressure_msl," +
        "wind_speed_10m,wind_direction_10m,cloud_cover,uv_index,precipitation";

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenMeteoProvider> _logger;

    public OpenMeteoProvider(HttpClient httpClient, ILogger<OpenMeteoProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Provider Name => Provider.OpenMeteo;

    public async Task<WeatherResponse?> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var url = FormattableString.Invariant(
            $"forecast?latitude={latitude}&longitude={longitude}&current={CurrentFields}");

        OpenMeteoResponse? payload;
        try
        {
            payload = await _httpClient.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Open-Meteo HTTP request failed");
            return null;
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Open-Meteo response could not be parsed");
            return null;
        }

        if (payload is null)
        {
            _logger.LogWarning("Open-Meteo returned no payload");
            return null;
        }

        var current = payload.Current;
        if (current is null)
        {
            _logger.LogWarning("Open-Meteo returned no current data");
            return null;
        }

        if (current.Temperature is null ||
            current.ApparentTemperature is null ||
            current.RelativeHumidity is null ||
            current.PressureMsl is null ||
            current.WindSpeed is null ||
            current.WindDirection is null ||
            current.CloudCover is null)
        {
            _logger.LogWarning("Open-Meteo response is missing required fields");
            return null;
        }

        return new WeatherResponse(
            Latitude: payload.Latitude,
            Longitude: payload.Longitude,
            Units: Units.Metric,
            Timestamp: DateTimeOffset.UtcNow,
            Providers: new[] { Provider.OpenMeteo },

            Temperature: current.Temperature.Value,
            FeelsLike: current.ApparentTemperature.Value,
            Humidity: (int)Math.Round(current.RelativeHumidity.Value),
            Pressure: current.PressureMsl.Value,
            WindSpeed: current.WindSpeed.Value,
            WindDirection: (int)Math.Round(current.WindDirection.Value),
            CloudCover: (int)Math.Round(current.CloudCover.Value),

            UvIndex: current.UvIndex,
            Precipitation: current.Precipitation,

            Visibility: null,
            Summary: null,
            Condition: null,
            Sunrise: null,
            Sunset: null,
            City: null,
            Country: null
        );
    }

    private sealed record OpenMeteoResponse
    {
        [JsonPropertyName("latitude")] public double Latitude { get; init; }
        [JsonPropertyName("longitude")] public double Longitude { get; init; }
        [JsonPropertyName("current")] public OpenMeteoCurrent? Current { get; init; }
    }

    private sealed record OpenMeteoCurrent
    {
        [JsonPropertyName("temperature_2m")] public double? Temperature { get; init; }
        [JsonPropertyName("apparent_temperature")] public double? ApparentTemperature { get; init; }
        [JsonPropertyName("relative_humidity_2m")] public double? RelativeHumidity { get; init; }
        [JsonPropertyName("pressure_msl")] public double? PressureMsl { get; init; }
        [JsonPropertyName("wind_speed_10m")] public double? WindSpeed { get; init; }
        [JsonPropertyName("wind_direction_10m")] public double? WindDirection { get; init; }
        [JsonPropertyName("cloud_cover")] public double? CloudCover { get; init; }
        [JsonPropertyName("uv_index")] public double? UvIndex { get; init; }
        [JsonPropertyName("precipitation")] public double? Precipitation { get; init; }
    }
}
