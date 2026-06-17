import { BASE_URL } from "./http";
import type { GeocodeResult } from "./geocode";

// Best-effort place name for coordinates. Reverse geocoding is enrichment, not
// critical path — any failure (404 no-name, rate-limit, network) just yields
// null and the UI falls back to showing the coordinates.
export async function fetchReverse(
  lat: number,
  lon: number,
): Promise<GeocodeResult | null> {
  const params = new URLSearchParams({ lat: String(lat), lon: String(lon) });
  try {
    const response = await fetch(`${BASE_URL}/reverse?${params}`);
    if (!response.ok) return null;
    return (await response.json()) as GeocodeResult;
  } catch {
    return null;
  }
}
