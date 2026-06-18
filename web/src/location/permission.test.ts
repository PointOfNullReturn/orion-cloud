import { afterEach, describe, expect, it, vi } from "vitest";
import { readGeolocationPermission } from "./permission";

// Fake navigator.permissions.query with a resolving or rejecting impl.
function stubPermissions(
  query: (descriptor: { name: string }) => Promise<{ state: string }>,
) {
  vi.stubGlobal("navigator", { permissions: { query } });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("readGeolocationPermission", () => {
  it("passes through the Permissions API state", async () => {
    stubPermissions(async () => ({ state: "granted" }));
    expect(await readGeolocationPermission()).toBe("granted");

    stubPermissions(async () => ({ state: "denied" }));
    expect(await readGeolocationPermission()).toBe("denied");

    stubPermissions(async () => ({ state: "prompt" }));
    expect(await readGeolocationPermission()).toBe("prompt");
  });

  it("queries specifically for geolocation", async () => {
    const query = vi.fn(async () => ({ state: "granted" }));
    vi.stubGlobal("navigator", { permissions: { query } });
    await readGeolocationPermission();
    expect(query).toHaveBeenCalledWith({ name: "geolocation" });
  });

  it("returns unknown when the Permissions API is absent (e.g. Safari)", async () => {
    vi.stubGlobal("navigator", {});
    expect(await readGeolocationPermission()).toBe("unknown");
  });

  it("returns unknown when the query rejects (some browsers reject for geolocation)", async () => {
    stubPermissions(() => Promise.reject(new Error("not supported")));
    expect(await readGeolocationPermission()).toBe("unknown");
  });
});
