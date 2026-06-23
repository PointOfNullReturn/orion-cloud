import type { GeocodeResult } from "./geocode";

// BigDataCloud's free client-side reverse-geocoding endpoint. Called DIRECTLY
// from the browser (never server-side) with the device's own coordinates from
// the HTML5 Geolocation API — the only use its fair-use policy permits, and the
// reason this lives client-side instead of behind the Orion API. Keyless, and
// no attribution is required for client-side use.
// https://www.bigdatacloud.com/support/fair-use-policy-for-free-client-side-reverse-geocoding-api
const BIGDATACLOUD_URL =
  "https://api.bigdatacloud.net/data/reverse-geocode-client";

interface BigDataCloudResponse {
  city?: string;
  locality?: string;
  principalSubdivision?: string;
  countryName?: string;
  countryCode?: string;
}

const cleaned = (value: string | undefined): string | null =>
  value && value.trim() ? value : null;

// Best-effort place name for coordinates. Reverse geocoding is enrichment, not
// critical path — any failure (network, error status, ocean with no name) just
// yields null and the UI falls back to showing the coordinates.
export async function fetchReverse(
  lat: number,
  lon: number,
): Promise<GeocodeResult | null> {
  const params = new URLSearchParams({
    latitude: String(lat),
    longitude: String(lon),
    localityLanguage: "en",
  });

  let payload: BigDataCloudResponse;
  try {
    const response = await fetch(`${BIGDATACLOUD_URL}?${params}`);
    if (!response.ok) return null;
    payload = (await response.json()) as BigDataCloudResponse;
  } catch {
    return null;
  }

  const name = cleaned(payload.city) ?? cleaned(payload.locality);
  if (name === null) return null; // nothing worth showing (e.g. open ocean)

  return {
    name,
    region: cleaned(payload.principalSubdivision),
    country: cleaned(payload.countryName),
    countryCode: cleaned(payload.countryCode),
    latitude: lat,
    longitude: lon,
  };
}
