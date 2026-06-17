using Orion.Api.Models;

namespace Orion.Api.Services;

public interface IGeocodingService
{
    // Best-effort: returns an empty list (never null) on no matches or upstream
    // failure, mirroring how IWeatherProvider returns null rather than throwing.
    Task<IReadOnlyList<GeocodeResult>> SearchAsync(
        string query,
        int count,
        CancellationToken cancellationToken);
}
