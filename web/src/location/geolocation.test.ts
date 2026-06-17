import { afterEach, describe, expect, it, vi } from "vitest";
import { getCurrentPosition } from "./geolocation";

// Install a fake navigator.geolocation whose getCurrentPosition invokes the
// success or error callback we configure.
function stubGeolocation(
  impl: (
    success: PositionCallback,
    error: PositionErrorCallback,
  ) => void,
) {
  vi.stubGlobal("navigator", { geolocation: { getCurrentPosition: impl } });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("getCurrentPosition", () => {
  it("resolves ok:true with coordinates on success", async () => {
    stubGeolocation((success) =>
      success({
        coords: { latitude: 47.6, longitude: -122.3 },
      } as GeolocationPosition),
    );

    const result = await getCurrentPosition();

    expect(result).toEqual({ ok: true, latitude: 47.6, longitude: -122.3 });
  });

  it("maps PERMISSION_DENIED (code 1) to denied", async () => {
    stubGeolocation((_, error) =>
      error({ code: 1 } as GeolocationPositionError),
    );
    expect(await getCurrentPosition()).toEqual({ ok: false, kind: "denied" });
  });

  it("maps POSITION_UNAVAILABLE (code 2) to unavailable", async () => {
    stubGeolocation((_, error) =>
      error({ code: 2 } as GeolocationPositionError),
    );
    expect(await getCurrentPosition()).toEqual({
      ok: false,
      kind: "unavailable",
    });
  });

  it("maps TIMEOUT (code 3) to timeout", async () => {
    stubGeolocation((_, error) =>
      error({ code: 3 } as GeolocationPositionError),
    );
    expect(await getCurrentPosition()).toEqual({ ok: false, kind: "timeout" });
  });

  it("resolves unsupported when geolocation is absent", async () => {
    vi.stubGlobal("navigator", {});
    expect(await getCurrentPosition()).toEqual({
      ok: false,
      kind: "unsupported",
    });
  });
});
