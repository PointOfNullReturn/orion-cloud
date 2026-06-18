import { afterEach, describe, expect, it, vi } from "vitest";
import {
  THEME_STORAGE_KEY,
  applyThemeMode,
  loadThemeMode,
  saveThemeMode,
} from "./theme";

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

function stubDocument() {
  const el = { dataset: {} as Record<string, string> };
  vi.stubGlobal("document", { documentElement: el });
  return el;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("loadThemeMode", () => {
  it("defaults to 'system' when nothing is stored", () => {
    stubLocalStorage();
    expect(loadThemeMode()).toBe("system");
  });

  it("returns a valid stored mode", () => {
    stubLocalStorage({ [THEME_STORAGE_KEY]: "dark" });
    expect(loadThemeMode()).toBe("dark");
  });

  it("falls back to 'system' for an unrecognized stored value", () => {
    stubLocalStorage({ [THEME_STORAGE_KEY]: "banana" });
    expect(loadThemeMode()).toBe("system");
  });

  it("falls back to 'system' when localStorage is unavailable", () => {
    stubBrokenLocalStorage();
    expect(loadThemeMode()).toBe("system");
  });
});

describe("saveThemeMode", () => {
  it("persists the chosen mode", () => {
    const store = stubLocalStorage();
    saveThemeMode("light");
    expect(store.get(THEME_STORAGE_KEY)).toBe("light");
  });

  it("swallows errors when storage is unavailable", () => {
    stubBrokenLocalStorage();
    expect(() => saveThemeMode("dark")).not.toThrow();
  });

  it("round-trips with loadThemeMode", () => {
    stubLocalStorage();
    saveThemeMode("dark");
    expect(loadThemeMode()).toBe("dark");
  });
});

describe("applyThemeMode", () => {
  it("writes the mode to the documentElement's data-theme", () => {
    const el = stubDocument();
    applyThemeMode("dark");
    expect(el.dataset.theme).toBe("dark");
  });

  it("overwrites a previously applied mode", () => {
    const el = stubDocument();
    applyThemeMode("dark");
    applyThemeMode("system");
    expect(el.dataset.theme).toBe("system");
  });
});
