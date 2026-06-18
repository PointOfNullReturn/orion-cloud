import type { PermissionState } from "./locationUx";

// Reads the current geolocation permission *silently* (no prompt) via the
// Permissions API. Progressive enhancement: returns "unknown" when the API is
// absent (Safari has historically lacked it for the "geolocation" name) or the
// query rejects — callers then fall back to learning from an actual attempt.
export async function readGeolocationPermission(): Promise<PermissionState> {
  const permissions = navigator.permissions;
  if (!permissions?.query) return "unknown";
  try {
    const status = await permissions.query({ name: "geolocation" });
    return status.state as PermissionState;
  } catch {
    return "unknown";
  }
}
