using Orion.Api.Models;

namespace Orion.Api.Services;

public interface IWeatherProvider
{
    Provider Name { get; }

    Task<WeatherResponse?> GetCurrentWeatherAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
