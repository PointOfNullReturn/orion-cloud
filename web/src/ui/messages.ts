import type { ErrorKind } from "../api/http";
import type { GeolocationErrorKind } from "../location/geolocation";

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

export function geolocationMessage(kind: GeolocationErrorKind): string {
  switch (kind) {
    case "denied":
      return "Location access was blocked. Search for your city instead.";
    case "unsupported":
      return "Location isn't available on this device. Search for your city instead.";
    case "timeout":
      return "Getting your location took too long. Search for your city instead.";
    case "unavailable":
      return "We couldn't determine your location. Search for your city instead.";
  }
}
