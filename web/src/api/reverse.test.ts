import { afterEach, describe, expect, it, vi } from "vitest";
import type { GeocodeResult } from "./geocode";
import { fetchReverse } from "./reverse";

const seattle: GeocodeResult = {
  name: "Seattle",
  region: "Washington",
  country: "United States of America (the)",
  countryCode: "US",
  latitude: 47.6,
  longitude: -122.3,
};

function stubFetch(status: number, body: unknown) {
  const fake = vi.fn().mockResolvedValue(
    new Response(status === 404 ? "" : JSON.stringify(body), { status }),
  );
  vi.stubGlobal("fetch", fake);
  return fake;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("fetchReverse", () => {
  it("returns the place on 200", async () => {
    stubFetch(200, seattle);
    expect(await fetchReverse(47.6, -122.3)).toEqual(seattle);
  });

  it("returns null on 404 (no name resolved)", async () => {
    stubFetch(404, null);
    expect(await fetchReverse(0, 0)).toBeNull();
  });

  it("returns null on any other error status (best-effort)", async () => {
    stubFetch(429, {});
    expect(await fetchReverse(47.6, -122.3)).toBeNull();
  });

  it("returns null when fetch throws (network)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    expect(await fetchReverse(47.6, -122.3)).toBeNull();
  });

  it("sends lat and lon as query params to /reverse", async () => {
    const fake = stubFetch(200, seattle);
    await fetchReverse(47.6, -122.3);
    const url = String(fake.mock.calls[0][0]);
    expect(url).toContain("/reverse?");
    expect(url).toContain("lat=47.6");
    expect(url).toContain("lon=-122.3");
  });
});
