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

// Subscribes to live geolocation permission changes (e.g. the user flips the
// site setting while the widget is open) and calls `onChange` with the new
// state. Returns a cleanup that detaches the listener. Progressive enhancement:
// where the Permissions API is absent or rejects there's nothing to watch, so
// the returned cleanup is a safe no-op. `query` is async, so a cleanup that runs
// before it settles cancels the pending subscription rather than attaching.
export function watchGeolocationPermission(
  onChange: (state: PermissionState) => void,
): () => void {
  const permissions = navigator.permissions;
  if (!permissions?.query) return () => {};

  let status: PermissionStatus | null = null;
  let cancelled = false;
  const handler = () => {
    if (status) onChange(status.state as PermissionState);
  };

  permissions
    .query({ name: "geolocation" })
    .then((s) => {
      if (cancelled) return;
      status = s;
      s.addEventListener("change", handler);
    })
    .catch(() => {
      // API present but rejects for "geolocation" (some browsers) — nothing to watch.
    });

  return () => {
    cancelled = true;
    if (status) status.removeEventListener("change", handler);
  };
}
