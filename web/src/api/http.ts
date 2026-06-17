// Shared HTTP plumbing for the Orion API clients (weather, geocode).

// Machine-readable outcome of a failed request. The UI maps these to friendly
// copy; the clients stay precise so failures are testable and loggable.
export type ErrorKind =
  | "bad-request" // 400 — invalid input (UI validates first; this is a safety net)
  | "rate-limited" // 429 — per-IP limit hit
  | "upstream-failed" // 502 — all upstream providers failed
  | "network" // fetch() threw — offline / DNS / CORS
  | "unknown"; // any other non-ok status

// Empty base => relative paths, which is fine for tests. Real value comes from
// VITE_API_URL: localhost:5204 in dev, Container App in prod.
export const BASE_URL = import.meta.env.VITE_API_URL ?? "";

export function statusToKind(status: number): ErrorKind {
  switch (status) {
    case 400:
      return "bad-request";
    case 429:
      return "rate-limited";
    case 502:
      return "upstream-failed";
    default:
      return "unknown";
  }
}
