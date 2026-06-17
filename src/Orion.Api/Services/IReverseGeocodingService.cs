using Orion.Api.Models;

namespace Orion.Api.Services;

public interface IReverseGeocodingService
{
    // Best-effort: returns null (never throws to the caller) when no place name
    // resolves or the upstream fails, mirroring the other location services.
    Task<GeocodeResult?> ReverseAsync(
        double latitude,
        double longitude,
        CancellationToken cancellationToken);
}
