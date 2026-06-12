using Orion.Api.Models;

namespace Orion.Api.Services;

public interface IWeatherAggregatorService
{
    Task<WeatherResponse?> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        Provider? provider,
        CancellationToken cancellationToken);
}
