import { describe, expect, it } from "vitest";
import type { ErrorKind } from "../api/http";
import type { GeolocationErrorKind } from "../location/geolocation";
import { apiErrorMessage, geolocationMessage } from "./messages";

describe("apiErrorMessage", () => {
  it("gives rate-limited its own distinct, gentle message", () => {
    const rateLimited = apiErrorMessage("rate-limited");
    expect(rateLimited).not.toEqual(apiErrorMessage("network"));
    expect(rateLimited.toLowerCase()).toMatch(/moment|too (quickly|many|fast)/);
  });

  it("never exposes the raw kind or a status code to the user", () => {
    const kinds: ErrorKind[] = [
      "bad-request",
      "rate-limited",
      "upstream-failed",
      "network",
      "unknown",
    ];
    for (const kind of kinds) {
      const msg = apiErrorMessage(kind);
      expect(msg).not.toContain(kind);
      expect(msg).not.toMatch(/\d{3}/); // no "400"/"429"/"502"
      expect(msg.length).toBeGreaterThan(0);
    }
  });

  it("collapses all non-rate-limited kinds to one generic message", () => {
    const generic = apiErrorMessage("network");
    expect(apiErrorMessage("unknown")).toEqual(generic);
    expect(apiErrorMessage("upstream-failed")).toEqual(generic);
    expect(apiErrorMessage("bad-request")).toEqual(generic);
  });
});

describe("geolocationMessage", () => {
  it("returns fallback-guiding copy for every kind", () => {
    const kinds: GeolocationErrorKind[] = [
      "denied",
      "unavailable",
      "timeout",
      "unsupported",
    ];
    for (const kind of kinds) {
      const msg = geolocationMessage(kind);
      expect(msg.length).toBeGreaterThan(0);
      expect(msg.toLowerCase()).toContain("city"); // points user to manual search
    }
  });
});
