// Last-chosen location: persisted so a page reload returns to the
// current-conditions view instead of dropping back to the launcher. The user
// can still re-acquire (locate / search) from the result chrome.
//
// We persist only the location (coords + label + how it was obtained), NOT the
// weather payload — on restore we re-fetch, so the conditions are always fresh
// and we never have to migrate a stored response schema.

export type StoredLocation = {
  lat: number;
  lon: number;
  label: string | null;
  source: "location" | "search";
};

export const LAST_LOCATION_KEY = "orion-last-location";

const SOURCES: readonly StoredLocation["source"][] = ["location", "search"];

function inRange(v: unknown, max: number): v is number {
  return typeof v === "number" && Number.isFinite(v) && v >= -max && v <= max;
}

function isStoredLocation(value: unknown): value is StoredLocation {
  if (typeof value !== "object" || value === null) return false;
  const o = value as Record<string, unknown>;
  return (
    inRange(o.lat, 90) &&
    inRange(o.lon, 180) &&
    (o.label === null || typeof o.label === "string") &&
    typeof o.source === "string" &&
    (SOURCES as readonly string[]).includes(o.source)
  );
}

export function loadLastLocation(): StoredLocation | null {
  try {
    const raw = localStorage.getItem(LAST_LOCATION_KEY);
    if (!raw) return null;
    const parsed: unknown = JSON.parse(raw);
    return isStoredLocation(parsed) ? parsed : null;
  } catch {
    // Malformed JSON, or localStorage unavailable (private mode / disabled).
    return null;
  }
}

export function saveLastLocation(location: StoredLocation): void {
  try {
    localStorage.setItem(LAST_LOCATION_KEY, JSON.stringify(location));
  } catch {
    // Persistence is best-effort; the choice still applies for this session.
  }
}
