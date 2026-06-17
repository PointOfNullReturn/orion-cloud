import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchWeather, type WeatherResponse } from "./weather";

// A minimal valid response body the fake API can return on success.
const sample: WeatherResponse = {
  latitude: 47.6,
  longitude: -122.3,
  units: "metric",
  timestamp: "2026-06-17T12:00:00+00:00",
  providers: ["openmeteo", "openweather"],
  temperature: 18.2,
  feelsLike: 17.9,
  humidity: 64,
  pressure: 1013,
  windSpeed: 3.1,
  windDirection: 210,
  cloudCover: 40,
  uvIndex: null,
  precipitation: 0,
  visibility: null,
  summary: null,
  condition: null,
  sunrise: null,
  sunset: null,
  city: null,
  country: null,
};

// Build a fake fetch that returns a given status + body, and lets us inspect the call.
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

describe("fetchWeather", () => {
  it("returns ok:true with parsed data on 200", async () => {
    stubFetch(200, sample);
    const result = await fetchWeather(47.6, -122.3, "metric");
    expect(result).toEqual({ ok: true, data: sample });
  });

  it("sends lat, lon, and units as query params to /weather", async () => {
    const fake = stubFetch(200, sample);
    await fetchWeather(47.6, -122.3, "imperial");
    const calledUrl = String(fake.mock.calls[0][0]);
    expect(calledUrl).toContain("/weather?");
    expect(calledUrl).toContain("lat=47.6");
    expect(calledUrl).toContain("lon=-122.3");
    expect(calledUrl).toContain("units=imperial");
  });

  it("maps 400 to bad-request", async () => {
    stubFetch(400, { detail: "bad coords" });
    const result = await fetchWeather(999, 999, "metric");
    expect(result).toEqual({ ok: false, kind: "bad-request" });
  });

  it("maps 429 to rate-limited", async () => {
    stubFetch(429, {});
    const result = await fetchWeather(47.6, -122.3, "metric");
    expect(result).toEqual({ ok: false, kind: "rate-limited" });
  });

  it("maps 502 to upstream-failed", async () => {
    stubFetch(502, {});
    const result = await fetchWeather(47.6, -122.3, "metric");
    expect(result).toEqual({ ok: false, kind: "upstream-failed" });
  });

  it("maps any other non-ok status to unknown", async () => {
    stubFetch(500, {});
    const result = await fetchWeather(47.6, -122.3, "metric");
    expect(result).toEqual({ ok: false, kind: "unknown" });
  });

  it("maps a thrown fetch (network failure) to network", async () => {
    const fake = vi.fn().mockRejectedValue(new TypeError("Failed to fetch"));
    vi.stubGlobal("fetch", fake);
    const result = await fetchWeather(47.6, -122.3, "metric");
    expect(result).toEqual({ ok: false, kind: "network" });
  });
});
