using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Orion.Api.Configuration;
using Orion.Api.Models;

namespace Orion.Api.Services;

public class WeatherAggregatorService : IWeatherAggregatorService
{
    private readonly IReadOnlyList<IWeatherProvider> _providers;
    private readonly HybridCache _cache;
    private readonly ILogger<WeatherAggregatorService> _logger;
    private readonly TimeSpan _cacheTtl;

    public WeatherAggregatorService(
        IEnumerable<IWeatherProvider> providers,
        HybridCache cache,
        IOptions<WeatherOptions> options,
        ILogger<WeatherAggregatorService> logger)
    {
        _providers = providers.ToList();
        _cache = cache;
        _logger = logger;
        _cacheTtl = TimeSpan.FromSeconds(options.Value.CacheTtlSeconds);
    }

    public async Task<WeatherResponse?> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        Provider? provider,
        CancellationToken cancellationToken)
    {
        var key = BuildCacheKey(latitude, longitude, provider);
        var entryOptions = new HybridCacheEntryOptions { Expiration = _cacheTtl };

        try
        {
            return await _cache.GetOrCreateAsync(
                key,
                factory: async ct =>
                {
                    _logger.LogDebug("Cache miss for {CacheKey}; fetching from providers", key);
                    return await FetchOrThrowAsync(latitude, longitude, provider, ct);
                },
                options: entryOptions,
                cancellationToken: cancellationToken);
        }
        catch (ProviderUnavailableException)
        {
            _logger.LogWarning(
                "All providers failed for ({Lat}, {Lon}); returning null",
                latitude, longitude);
            return null;
        }
    }

    private async Task<WeatherResponse> FetchOrThrowAsync(
        double latitude,
        double longitude,
        Provider? provider,
        CancellationToken ct)
    {
        var result = provider.HasValue
            ? await FetchSingleAsync(latitude, longitude, provider.Value, ct)
            : await FetchAggregateAsync(latitude, longitude, ct);

        return result ?? throw new ProviderUnavailableException();
    }

    private async Task<WeatherResponse?> FetchSingleAsync(
        double latitude,
        double longitude,
        Provider provider,
        CancellationToken ct)
    {
        var p = _providers.FirstOrDefault(x => x.Name == provider);
        if (p is null)
        {
            _logger.LogWarning("No registered provider matches {Provider}", provider);
            return null;
        }

        var response = await p.GetCurrentWeatherAsync(latitude, longitude, ct);
        return response is null ? null : response with { Latitude = latitude, Longitude = longitude };
    }

    private async Task<WeatherResponse?> FetchAggregateAsync(
        double latitude,
        double longitude,
        CancellationToken ct)
    {
        var tasks = _providers.Select(p => p.GetCurrentWeatherAsync(latitude, longitude, ct)).ToArray();
        var responses = await Task.WhenAll(tasks);
        var successful = responses.Where(r => r is not null).Cast<WeatherResponse>().ToList();

        if (successful.Count == 0) return null;
        if (successful.Count == 1) return successful[0] with { Latitude = latitude, Longitude = longitude };
        return Merge(latitude, longitude, successful);
    }

    private static WeatherResponse Merge(
        double latitude,
        double longitude,
        IReadOnlyList<WeatherResponse> responses)
    {
        var uv = responses.FirstOrDefault(r => r.UvIndex.HasValue)?.UvIndex;
        var precipitation = responses.FirstOrDefault(r => r.Precipitation.HasValue)?.Precipitation;
        var visibility = responses.FirstOrDefault(r => r.Visibility.HasValue)?.Visibility;
        var summary = responses.FirstOrDefault(r => r.Summary is not null)?.Summary;
        var condition = responses.FirstOrDefault(r => r.Condition is not null)?.Condition;
        var sunrise = responses.FirstOrDefault(r => r.Sunrise.HasValue)?.Sunrise;
        var sunset = responses.FirstOrDefault(r => r.Sunset.HasValue)?.Sunset;
        var city = responses.FirstOrDefault(r => r.City is not null)?.City;
        var country = responses.FirstOrDefault(r => r.Country is not null)?.Country;

        var providers = responses.SelectMany(r => r.Providers).Distinct().ToArray();

        return new WeatherResponse(
            Latitude: latitude,
            Longitude: longitude,
            Units: Units.Metric,
            Timestamp: DateTimeOffset.UtcNow,
            Providers: providers,

            Temperature: responses.Average(r => r.Temperature),
            FeelsLike: responses.Average(r => r.FeelsLike),
            Humidity: (int)Math.Round(responses.Average(r => (double)r.Humidity)),
            Pressure: responses.Average(r => r.Pressure),
            WindSpeed: responses.Average(r => r.WindSpeed),
            WindDirection: VectorMeanDegrees(responses.Select(r => r.WindDirection)),
            CloudCover: (int)Math.Round(responses.Average(r => (double)r.CloudCover)),

            UvIndex: uv,
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

    // Edge case: when providers disagree by ~180°, atan2(0,0)=0 reports false "north". See PRODUCTION_NOTES.md.
    private static int VectorMeanDegrees(IEnumerable<int> degrees)
    {
        double sumSin = 0, sumCos = 0;
        int count = 0;
        foreach (var d in degrees)
        {
            var r = d * Math.PI / 180.0;
            sumSin += Math.Sin(r);
            sumCos += Math.Cos(r);
            count++;
        }
        if (count == 0) return 0;

        var mean = Math.Atan2(sumSin, sumCos) * 180.0 / Math.PI;
        if (mean < 0) mean += 360;
        return (int)Math.Round(mean) % 360;
    }

    private static string BuildCacheKey(double latitude, double longitude, Provider? provider)
    {
        var roundedLat = Math.Round(latitude, 4);
        var roundedLon = Math.Round(longitude, 4);
        return FormattableString.Invariant(
            $"weather:{roundedLat}:{roundedLon}:{provider?.ToString() ?? "all"}");
    }

    private sealed class ProviderUnavailableException : Exception
    {
    }
}
