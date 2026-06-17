using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Orion.Api.Models;

namespace Orion.Api.Services;

public class BigDataCloudReverseGeocoder : IReverseGeocodingService
{
    private readonly HttpClient _httpClient;
    private readonly HybridCache _cache;
    private readonly ILogger<BigDataCloudReverseGeocoder> _logger;

    public BigDataCloudReverseGeocoder(
        HttpClient httpClient,
        HybridCache cache,
        ILogger<BigDataCloudReverseGeocoder> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GeocodeResult?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken)
    {
        var key = BuildCacheKey(latitude, longitude);

        try
        {
            // Successful lookups (including a resolved null "no name") are cached;
            // upstream errors throw out of the factory so they are not cached.
            return await _cache.GetOrCreateAsync(
                key,
                factory: ct => FetchAsync(latitude, longitude, ct),
                cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "BigDataCloud reverse-geocode HTTP request failed");
            return null;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "BigDataCloud reverse-geocode response could not be parsed");
            return null;
        }
    }

    private async ValueTask<GeocodeResult?> FetchAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var url = FormattableString.Invariant(
            $"reverse-geocode-client?latitude={latitude}&longitude={longitude}&localityLanguage=en");

        var payload = await _httpClient.GetFromJsonAsync<BigDataCloudResponse>(url, ct);

        var name = FirstNonEmpty(payload?.City, payload?.Locality);
        if (name is null)
        {
            return null; // nothing worth displaying (e.g. open ocean)
        }

        return new GeocodeResult(
            Name: name,
            Region: NullIfEmpty(payload?.PrincipalSubdivision),
            Country: NullIfEmpty(payload?.CountryName),
            CountryCode: NullIfEmpty(payload?.CountryCode),
            Latitude: latitude,
            Longitude: longitude);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.Select(NullIfEmpty).FirstOrDefault(v => v is not null);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string BuildCacheKey(double latitude, double longitude)
    {
        // ~100m precision is plenty for a city name and improves cache hits.
        var lat = Math.Round(latitude, 3);
        var lon = Math.Round(longitude, 3);
        return FormattableString.Invariant($"reverse:{lat}:{lon}");
    }

    private sealed record BigDataCloudResponse
    {
        [JsonPropertyName("city")] public string? City { get; init; }
        [JsonPropertyName("locality")] public string? Locality { get; init; }
        [JsonPropertyName("principalSubdivision")] public string? PrincipalSubdivision { get; init; }
        [JsonPropertyName("countryName")] public string? CountryName { get; init; }
        [JsonPropertyName("countryCode")] public string? CountryCode { get; init; }
    }
}
