import { afterEach, describe, expect, it, vi } from "vitest";
import { UNITS_STORAGE_KEY, loadUnits, saveUnits } from "./units";

// Stub a Map-backed localStorage (default test env is node — no DOM globals).
function stubLocalStorage(seed?: Record<string, string>) {
  const store = new Map<string, string>(Object.entries(seed ?? {}));
  vi.stubGlobal("localStorage", {
    getItem: (k: string) => store.get(k) ?? null,
    setItem: (k: string, v: string) => void store.set(k, v),
    removeItem: (k: string) => void store.delete(k),
  });
  return store;
}

// Stub a throwing localStorage (private mode / disabled storage).
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

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("loadUnits", () => {
  it("defaults to 'metric' when nothing is stored", () => {
    stubLocalStorage();
    expect(loadUnits()).toBe("metric");
  });

  it("returns a valid stored value", () => {
    stubLocalStorage({ [UNITS_STORAGE_KEY]: "imperial" });
    expect(loadUnits()).toBe("imperial");
  });

  it("falls back to 'metric' for an unrecognized stored value", () => {
    stubLocalStorage({ [UNITS_STORAGE_KEY]: "kelvin" });
    expect(loadUnits()).toBe("metric");
  });

  it("falls back to 'metric' when localStorage is unavailable", () => {
    stubBrokenLocalStorage();
    expect(loadUnits()).toBe("metric");
  });
});

describe("saveUnits", () => {
  it("persists the chosen units", () => {
    const store = stubLocalStorage();
    saveUnits("imperial");
    expect(store.get(UNITS_STORAGE_KEY)).toBe("imperial");
  });

  it("swallows errors when storage is unavailable", () => {
    stubBrokenLocalStorage();
    expect(() => saveUnits("imperial")).not.toThrow();
  });

  it("round-trips with loadUnits", () => {
    stubLocalStorage();
    saveUnits("imperial");
    expect(loadUnits()).toBe("imperial");
  });
});
