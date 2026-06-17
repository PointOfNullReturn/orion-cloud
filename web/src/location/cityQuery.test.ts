import { describe, expect, it } from "vitest";
import type { GeocodeResult } from "../api/geocode";
import { parseCityQuery, prioritizeByHint } from "./cityQuery";

function place(name: string, region: string | null, country: string, code: string): GeocodeResult {
  return { name, region, country, countryCode: code, latitude: 0, longitude: 0 };
}

const sheffieldUK = place("Sheffield", "England", "United Kingdom", "GB");
const sheffieldAL = place("Sheffield", "Alabama", "United States", "US");

describe("parseCityQuery", () => {
  it("splits a name and region/country hint on the first comma", () => {
    expect(parseCityQuery("Sheffield, AL")).toEqual({ name: "Sheffield", hint: "AL" });
  });

  it("trims whitespace around both parts", () => {
    expect(parseCityQuery("  Paris ,  France ")).toEqual({ name: "Paris", hint: "France" });
  });

  it("returns a null hint when there is no comma", () => {
    expect(parseCityQuery("Sheffield")).toEqual({ name: "Sheffield", hint: null });
  });

  it("treats a trailing comma as no hint", () => {
    expect(parseCityQuery("Sheffield,")).toEqual({ name: "Sheffield", hint: null });
  });
});

describe("prioritizeByHint", () => {
  it("returns results unchanged when there is no hint", () => {
    const results = [sheffieldUK, sheffieldAL];
    expect(prioritizeByHint(results, null)).toEqual(results);
  });

  it("floats a US-state-abbreviation match to the top", () => {
    const ordered = prioritizeByHint([sheffieldUK, sheffieldAL], "AL");
    expect(ordered[0]).toBe(sheffieldAL);
  });

  it("matches a full region name", () => {
    const ordered = prioritizeByHint([sheffieldUK, sheffieldAL], "Alabama");
    expect(ordered[0]).toBe(sheffieldAL);
  });

  it("matches a country name", () => {
    const ordered = prioritizeByHint([sheffieldUK, sheffieldAL], "United Kingdom");
    expect(ordered[0]).toBe(sheffieldUK);
  });

  it("matches a country code", () => {
    const ordered = prioritizeByHint([sheffieldUK, sheffieldAL], "US");
    expect(ordered[0]).toBe(sheffieldAL);
  });

  it("preserves relative order among non-matches (stable)", () => {
    const ordered = prioritizeByHint([sheffieldUK, sheffieldAL], "Texas");
    expect(ordered).toEqual([sheffieldUK, sheffieldAL]);
  });
});
