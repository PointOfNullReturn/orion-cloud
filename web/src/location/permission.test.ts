import { afterEach, describe, expect, it, vi } from "vitest";
import {
  readGeolocationPermission,
  watchGeolocationPermission,
} from "./permission";

// A fake PermissionStatus: mutable state + add/removeEventListener spies and a
// `fire()` helper to simulate the browser dispatching a "change".
function fakeStatus(initial: string) {
  let state = initial;
  const listeners = new Set<() => void>();
  return {
    get state() {
      return state;
    },
    addEventListener: vi.fn((type: string, cb: () => void) => {
      if (type === "change") listeners.add(cb);
    }),
    removeEventListener: vi.fn((type: string, cb: () => void) => {
      if (type === "change") listeners.delete(cb);
    }),
    fire(next: string) {
      state = next;
      listeners.forEach((cb) => cb());
    },
  };
}

// Flush the microtask queue so the internal permissions.query() promise settles.
const flush = () => Promise.resolve();

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

describe("watchGeolocationPermission", () => {
  it("invokes the callback with the new state when the permission changes", async () => {
    const status = fakeStatus("denied");
    vi.stubGlobal("navigator", { permissions: { query: async () => status } });

    const seen: string[] = [];
    watchGeolocationPermission((s) => seen.push(s));
    await flush();

    status.fire("granted");
    status.fire("prompt");
    expect(seen).toEqual(["granted", "prompt"]);
  });

  it("watches specifically the geolocation permission", async () => {
    const status = fakeStatus("prompt");
    const query = vi.fn(async () => status);
    vi.stubGlobal("navigator", { permissions: { query } });

    watchGeolocationPermission(() => {});
    await flush();

    expect(query).toHaveBeenCalledWith({ name: "geolocation" });
  });

  it("returns a cleanup that removes the change listener", async () => {
    const status = fakeStatus("granted");
    vi.stubGlobal("navigator", { permissions: { query: async () => status } });

    const stop = watchGeolocationPermission(() => {});
    await flush();
    expect(status.addEventListener).toHaveBeenCalledWith(
      "change",
      expect.any(Function),
    );

    stop();
    expect(status.removeEventListener).toHaveBeenCalledWith(
      "change",
      expect.any(Function),
    );
  });

  it("does not attach if stopped before the query resolves", async () => {
    const status = fakeStatus("denied");
    vi.stubGlobal("navigator", { permissions: { query: async () => status } });

    const stop = watchGeolocationPermission(() => {});
    stop(); // cleanup runs before the async query settles
    await flush();

    expect(status.addEventListener).not.toHaveBeenCalled();
  });

  it("is a no-op (no throw) when the Permissions API is absent", () => {
    vi.stubGlobal("navigator", {});
    const stop = watchGeolocationPermission(() => {});
    expect(() => stop()).not.toThrow();
  });

  it("does not throw or call back when the query rejects", async () => {
    vi.stubGlobal("navigator", {
      permissions: { query: () => Promise.reject(new Error("nope")) },
    });
    const cb = vi.fn();
    const stop = watchGeolocationPermission(cb);
    await flush();

    expect(cb).not.toHaveBeenCalled();
    expect(() => stop()).not.toThrow();
  });
});
