import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchGeocode, type GeocodeResult } from "./geocode";

const sample: GeocodeResult[] = [
  {
    name: "Seattle",
    region: "Washington",
    country: "United States",
    countryCode: "US",
    latitude: 47.60621,
    longitude: -122.33207,
  },
];

function stubFetch(status: number, body: unknown) {
  const fake = vi.fn().mockResolvedValue(
    new Response(JSON.stringify(body), { status }),
  );
  vi.stubGlobal("fetch", fake);
  return fake;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchGeocode", () => {
  it("returns ok:true with results on 200", async () => {
    stubFetch(200, sample);
    const result = await fetchGeocode("Seattle");
    expect(result).toEqual({ ok: true, results: sample });
  });

  it("returns ok:true with an empty array when nothing matches", async () => {
    stubFetch(200, []);
    const result = await fetchGeocode("asdfghjkl");
    expect(result).toEqual({ ok: true, results: [] });
  });

  it("sends the query URL-encoded as q to /geocode", async () => {
    const fake = stubFetch(200, sample);
    await fetchGeocode("San Diego");
    const calledUrl = String(fake.mock.calls[0][0]);
    expect(calledUrl).toContain("/geocode?");
    expect(calledUrl).toContain("q=San+Diego");
  });

  it("maps 429 to rate-limited", async () => {
    stubFetch(429, {});
    const result = await fetchGeocode("Seattle");
    expect(result).toEqual({ ok: false, kind: "rate-limited" });
  });

  it("maps 400 to bad-request", async () => {
    stubFetch(400, {});
    const result = await fetchGeocode("");
    expect(result).toEqual({ ok: false, kind: "bad-request" });
  });

  it("maps a thrown fetch (network failure) to network", async () => {
    const fake = vi.fn().mockRejectedValue(new TypeError("Failed to fetch"));
    vi.stubGlobal("fetch", fake);
    const result = await fetchGeocode("Seattle");
    expect(result).toEqual({ ok: false, kind: "network" });
  });
});
