// Promisifies the browser Geolocation API into a discriminated-union outcome
// that always resolves (never rejects), so callers handle one shape. The browser
// picks GPS/WiFi/cell internally; enableHighAccuracy nudges toward GPS.

export type GeolocationErrorKind =
  | "denied" // PERMISSION_DENIED (1) — user refused
  | "unavailable" // POSITION_UNAVAILABLE (2) — no fix available
  | "timeout" // TIMEOUT (3) — took too long
  | "unsupported"; // navigator.geolocation not present (insecure context / old browser)

export type GeolocationOutcome =
  | { ok: true; latitude: number; longitude: number }
  | { ok: false; kind: GeolocationErrorKind };

const DEFAULT_OPTIONS: PositionOptions = {
  enableHighAccuracy: true,
  timeout: 10_000,
};

export function getCurrentPosition(
  options: PositionOptions = DEFAULT_OPTIONS,
): Promise<GeolocationOutcome> {
  if (!("geolocation" in navigator)) {
    return Promise.resolve({ ok: false, kind: "unsupported" });
  }

  return new Promise((resolve) => {
    navigator.geolocation.getCurrentPosition(
      (position) =>
        resolve({
          ok: true,
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
        }),
      (error) => resolve({ ok: false, kind: codeToKind(error.code) }),
      options,
    );
  });
}

function codeToKind(code: number): GeolocationErrorKind {
  switch (code) {
    case 1:
      return "denied";
    case 3:
      return "timeout";
    default:
      return "unavailable"; // 2 and any unexpected code
  }
}
