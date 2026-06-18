import type { GeolocationErrorKind } from "./geolocation";

// Which launcher layout to show. Location leads when it can; search takes over
// when location is blocked, unsupported, or has just failed. This is pure
// policy — the component reads it to pick a layout and the right hint copy.

export type LocationMode =
  | "location-first" // location is the hero; search is the quiet fallback
  | "search-retry" // transient failure: search leads, location offered as a retry
  | "search-only"; // location can't work here (unsupported / blocked)

// Mirrors the Permissions API states, plus "unknown" for when that API is
// absent (e.g. Safari) and we haven't learned anything from an attempt yet.
export type PermissionState = "granted" | "prompt" | "denied" | "unknown";

export interface LocationUxInput {
  supported: boolean;
  permission: PermissionState;
  lastFailure: GeolocationErrorKind | null;
}

export function decideLocationMode({
  supported,
  permission,
  lastFailure,
}: LocationUxInput): LocationMode {
  // Device can't do geolocation at all — never offer it.
  if (!supported) return "search-only";

  // Known-denied (read silently via the Permissions API): the browser won't
  // re-prompt, so the location button would be a dead end.
  if (permission === "denied") return "search-only";

  // Otherwise react to the outcome of the last attempt.
  // Hard failures behave like a block; transient ones stay retryable.
  if (lastFailure === "denied" || lastFailure === "unsupported") return "search-only";
  if (lastFailure === "timeout" || lastFailure === "unavailable") return "search-retry";

  // Supported, not blocked, nothing failed → lead with location.
  return "location-first";
}
