using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Orion.Api.Models;

namespace Orion.Api.Services;

public class GeocodingService : IGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly HybridCache _cache;
    private readonly ILogger<GeocodingService> _logger;

    public GeocodingService(HttpClient httpClient, HybridCache cache, ILogger<GeocodingService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeocodeResult>> SearchAsync(
        string query,
        int count,
        CancellationToken cancellationToken)
    {
        var key = BuildCacheKey(query, count);

        try
        {
            // A successful lookup (including a legitimate empty "no matches") is cached.
            // Upstream errors throw out of the factory so they are NOT cached and retry next time.
            return await _cache.GetOrCreateAsync(
                key,
                factory: ct => FetchAsync(query, count, ct),
                cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Open-Meteo geocoding HTTP request failed");
            return Array.Empty<GeocodeResult>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Open-Meteo geocoding response could not be parsed");
            return Array.Empty<GeocodeResult>();
        }
    }

    private async ValueTask<GeocodeResult[]> FetchAsync(string query, int count, CancellationToken ct)
    {
        var url = FormattableString.Invariant(
            $"search?name={Uri.EscapeDataString(query)}&count={count}&language=en&format=json");

        var payload = await _httpClient.GetFromJsonAsync<OpenMeteoGeocodingResponse>(url, ct);

        // No "results" key => no matches. Not an error; a cacheable empty answer.
        if (payload?.Results is not { Count: > 0 } results)
        {
            return Array.Empty<GeocodeResult>();
        }

        return results
            .Take(count)
            .Select(r => new GeocodeResult(
                Name: r.Name,
                Region: r.Admin1,
                Country: r.Country,
                CountryCode: r.CountryCode,
                Latitude: r.Latitude,
                Longitude: r.Longitude))
            .ToArray();
    }

    private static string BuildCacheKey(string query, int count) =>
        FormattableString.Invariant($"geocode:{query.Trim().ToLowerInvariant()}:{count}");

    private sealed record OpenMeteoGeocodingResponse
    {
        [JsonPropertyName("results")] public List<GeocodingMatch>? Results { get; init; }
    }

    private sealed record GeocodingMatch
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("latitude")] public double Latitude { get; init; }
        [JsonPropertyName("longitude")] public double Longitude { get; init; }
        [JsonPropertyName("country")] public string? Country { get; init; }
        [JsonPropertyName("country_code")] public string? CountryCode { get; init; }
        [JsonPropertyName("admin1")] public string? Admin1 { get; init; }
    }
}
