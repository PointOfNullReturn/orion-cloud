import { BASE_URL, statusToKind, type ErrorKind } from "./http";

export type { ErrorKind } from "./http";

// Mirrors the C# WeatherResponse record (src/Orion.Api/Models/WeatherResponse.cs).
// Three mapping rules from the API:
//   1. JSON keys are camelCase (System.Text.Json default), so properties are camelCase here.
//   2. Enums serialize lowercase (Program.cs JSON policy), so they're lowercase string unions.
//   3. Nullable C# fields (double?) are always PRESENT in JSON but may be null — the API
//      guarantees a stable schema ("missing source field -> null"). So they're `T | null`,
//      NOT optional (`T?`). The key always exists; the value may be null.

export type Units = "metric" | "imperial";
export type Provider = "openmeteo" | "openweather";

export interface WeatherResponse {
  latitude: number;
  longitude: number;
  units: Units;
  timestamp: string; // ISO 8601 (DateTimeOffset serializes to a string)
  providers: Provider[]; // which sources actually answered

  temperature: number;
  feelsLike: number;
  humidity: number;
  pressure: number;
  windSpeed: number;
  windDirection: number;
  cloudCover: number;

  uvIndex: number | null;
  precipitation: number | null;

  visibility: number | null;
  summary: string | null;
  condition: string | null;
  sunrise: string | null;
  sunset: string | null;
  city: string | null;
  country: string | null;
}

export type WeatherResult =
  | { ok: true; data: WeatherResponse }
  | { ok: false; kind: ErrorKind };

export async function fetchWeather(
  lat: number,
  lon: number,
  units: Units,
): Promise<WeatherResult> {
  const params = new URLSearchParams({
    lat: String(lat),
    lon: String(lon),
    units,
  });

  let response: Response;
  try {
    response = await fetch(`${BASE_URL}/weather?${params}`);
  } catch {
    // fetch only rejects on network-level failures, not HTTP error statuses.
    return { ok: false, kind: "network" };
  }

  if (response.ok) {
    const data = (await response.json()) as WeatherResponse;
    return { ok: true, data };
  }

  return { ok: false, kind: statusToKind(response.status) };
}
