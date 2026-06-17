import { BASE_URL, statusToKind, type ErrorKind } from "./http";

// Mirrors the C# GeocodeResult record (src/Orion.Api/Models/GeocodeResult.cs).
// Name + coords always present; region/country/code may be null.
export interface GeocodeResult {
  name: string;
  region: string | null;
  country: string | null;
  countryCode: string | null;
  latitude: number;
  longitude: number;
}

export type GeocodeResultSet =
  | { ok: true; results: GeocodeResult[] } // results may be empty (no matches)
  | { ok: false; kind: ErrorKind };

export async function fetchGeocode(query: string): Promise<GeocodeResultSet> {
  const params = new URLSearchParams({ q: query });

  let response: Response;
  try {
    response = await fetch(`${BASE_URL}/geocode?${params}`);
  } catch {
    return { ok: false, kind: "network" };
  }

  if (response.ok) {
    const results = (await response.json()) as GeocodeResult[];
    return { ok: true, results };
  }

  return { ok: false, kind: statusToKind(response.status) };
}
