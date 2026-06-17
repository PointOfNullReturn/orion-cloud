namespace Orion.Api.Models;

// A single place match from the geocoding search. Name + coords are always
// present; region/country/code are nullable because Open-Meteo omits them for
// some results (e.g. countries, oceans). Serializes camelCase like everything else.
public record GeocodeResult(
    string Name,
    string? Region,
    string? Country,
    string? CountryCode,
    double Latitude,
    double Longitude
);
