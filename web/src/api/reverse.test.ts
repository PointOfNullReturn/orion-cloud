import { afterEach, describe, expect, it, vi } from "vitest";
import type { GeocodeResult } from "./geocode";
import { fetchReverse } from "./reverse";

// A representative BigDataCloud reverse-geocode-client payload.
const bdcSeattle = {
  city: "Seattle",
  locality: "Downtown Seattle",
  principalSubdivision: "Washington",
  countryName: "United States of America (the)",
  countryCode: "US",
};

const seattle: GeocodeResult = {
  name: "Seattle",
  region: "Washington",
  country: "United States of America (the)",
  countryCode: "US",
  latitude: 47.6,
  longitude: -122.3,
};

function stubFetch(status: number, body: unknown) {
  const fake = vi
    .fn()
    .mockResolvedValue(new Response(JSON.stringify(body), { status }));
  vi.stubGlobal("fetch", fake);
  return fake;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchReverse", () => {
  it("maps a BigDataCloud response to a GeocodeResult with the input coords", async () => {
    stubFetch(200, bdcSeattle);
    expect(await fetchReverse(47.6, -122.3)).toEqual(seattle);
  });

  it("falls back to locality when city is empty", async () => {
    stubFetch(200, { ...bdcSeattle, city: "" });
    const result = await fetchReverse(47.6, -122.3);
    expect(result?.name).toBe("Downtown Seattle");
  });

  it("maps absent region/country fields to null", async () => {
    stubFetch(200, { city: "Nowhere" });
    expect(await fetchReverse(1, 2)).toEqual({
      name: "Nowhere",
      region: null,
      country: null,
      countryCode: null,
      latitude: 1,
      longitude: 2,
    });
  });

  it("returns null when no city or locality is resolved (e.g. open ocean)", async () => {
    stubFetch(200, { city: "", locality: "", countryName: "" });
    expect(await fetchReverse(0, 0)).toBeNull();
  });

  it("returns null on a non-ok response (best-effort)", async () => {
    stubFetch(500, {});
    expect(await fetchReverse(47.6, -122.3)).toBeNull();
  });

  it("returns null when fetch throws (network)", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockRejectedValue(new TypeError("Failed to fetch")),
    );
    expect(await fetchReverse(47.6, -122.3)).toBeNull();
  });

  it("calls BigDataCloud's client endpoint directly, not the Orion API", async () => {
    const fake = stubFetch(200, bdcSeattle);
    await fetchReverse(47.6, -122.3);
    const url = String(fake.mock.calls[0][0]);
    expect(url).toContain("bigdatacloud.net");
    expect(url).toContain("reverse-geocode-client");
    expect(url).toContain("latitude=47.6");
    expect(url).toContain("longitude=-122.3");
    expect(url).not.toContain("/reverse?"); // no longer proxied through Orion
  });
});
