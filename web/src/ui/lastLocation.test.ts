import { afterEach, describe, expect, it, vi } from "vitest";
import {
  LAST_LOCATION_KEY,
  loadLastLocation,
  saveLastLocation,
  type StoredLocation,
} from "./lastLocation";

function stubLocalStorage(seed?: Record<string, string>) {
  const store = new Map<string, string>(Object.entries(seed ?? {}));
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => store.get(k) ?? null,
    setItem: (k: string, v: string) => void store.set(k, v),
    removeItem: (k: string) => void store.delete(k),
  });
  return store;
}

function stubBrokenLocalStorage() {
  vi.stubGlobal("localStorage", {
    getItem: () => {
      throw new Error("denied");
    },
    setItem: () => {
      throw new Error("denied");
    },
  });
}

const SAMPLE: StoredLocation = {
  lat: 34.74,
  lon: -87.66,
  label: "Muscle Shoals, Alabama, US",
  source: "search",
};

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("loadLastLocation", () => {
  it("returns null when nothing is stored", () => {
    stubLocalStorage();
    expect(loadLastLocation()).toBeNull();
  });

  it("returns a valid stored location", () => {
    stubLocalStorage({ [LAST_LOCATION_KEY]: JSON.stringify(SAMPLE) });
    expect(loadLastLocation()).toEqual(SAMPLE);
  });

  it("accepts a null label (geolocation result before reverse lands)", () => {
    const located = { ...SAMPLE, label: null, source: "location" as const };
    stubLocalStorage({ [LAST_LOCATION_KEY]: JSON.stringify(located) });
    expect(loadLastLocation()).toEqual(located);
  });

  it("returns null for malformed JSON", () => {
    stubLocalStorage({ [LAST_LOCATION_KEY]: "{not json" });
    expect(loadLastLocation()).toBeNull();
  });

  it("returns null when coordinates are out of range", () => {
    stubLocalStorage({
      [LAST_LOCATION_KEY]: JSON.stringify({ ...SAMPLE, lat: 200 }),
    });
    expect(loadLastLocation()).toBeNull();
  });

  it("returns null for a missing or unrecognized source", () => {
    stubLocalStorage({
      [LAST_LOCATION_KEY]: JSON.stringify({ ...SAMPLE, source: "guess" }),
    });
    expect(loadLastLocation()).toBeNull();
  });

  it("returns null when a coordinate is non-finite", () => {
    // JSON has no NaN literal; a stored "null" coordinate models the same risk.
    stubLocalStorage({
      [LAST_LOCATION_KEY]: JSON.stringify({ ...SAMPLE, lon: null }),
    });
    expect(loadLastLocation()).toBeNull();
  });

  it("returns null when localStorage is unavailable", () => {
    stubBrokenLocalStorage();
    expect(loadLastLocation()).toBeNull();
  });
});

describe("saveLastLocation", () => {
  it("persists the location as JSON", () => {
    const store = stubLocalStorage();
    saveLastLocation(SAMPLE);
    expect(store.get(LAST_LOCATION_KEY)).toBe(JSON.stringify(SAMPLE));
  });

  it("swallows errors when storage is unavailable", () => {
    stubBrokenLocalStorage();
    expect(() => saveLastLocation(SAMPLE)).not.toThrow();
  });

  it("round-trips with loadLastLocation", () => {
    stubLocalStorage();
    saveLastLocation(SAMPLE);
    expect(loadLastLocation()).toEqual(SAMPLE);
  });
});
