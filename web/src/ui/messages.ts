import type { ErrorKind } from "../api/http";
import type { GeolocationErrorKind } from "../location/geolocation";
import type { PermissionState } from "../location/locationUx";

// Turns machine-readable failure kinds into user-facing copy. The user never
// sees a raw kind or a status code — only rate-limiting gets a distinct nudge;
// everything else collapses to one calm "try again" message.

const GENERIC = "We couldn't load that right now. Please try again shortly.";

export function apiErrorMessage(kind: ErrorKind): string {
  if (kind === "rate-limited") {
    return "You're checking a bit too quickly — give it a moment and try again.";
  }
  return GENERIC;
}

// Explains, when relevant, *why* location isn't leading — so a missing or
// demoted location action isn't a mystery. Returns null when location is fine
// (the happy path, where no note is needed). Copy no longer says "search
// instead": in every non-null case search is already the obvious hero.
export function locationNote(
  supported: boolean,
  permission: PermissionState,
  lastFailure: GeolocationErrorKind | null,
): string | null {
  if (!supported || lastFailure === "unsupported") {
    return "Location isn't available on this device.";
  }
  if (permission === "denied" || lastFailure === "denied") {
    return "Location is blocked — you can re-enable it in your browser's site settings.";
  }
  if (lastFailure === "timeout") {
    return "Getting your location took too long.";
  }
  if (lastFailure === "unavailable") {
    return "We couldn't determine your location.";
  }
  return null;
}
