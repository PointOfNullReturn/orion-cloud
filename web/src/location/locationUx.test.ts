import { describe, expect, it } from "vitest";
import { decideLocationMode, type LocationUxInput } from "./locationUx";

// Convenience: a fully-capable starting point; override per case.
function input(over: Partial<LocationUxInput> = {}): LocationUxInput {
  return { supported: true, permission: "prompt", lastFailure: null, ...over };
}

describe("decideLocationMode", () => {
  it("leads with location when supported, not blocked, nothing failed", () => {
    expect(decideLocationMode(input({ permission: "prompt" }))).toBe("location-first");
    expect(decideLocationMode(input({ permission: "granted" }))).toBe("location-first");
    expect(decideLocationMode(input({ permission: "unknown" }))).toBe("location-first");
  });

  it("falls to search-only when the device can't do geolocation", () => {
    expect(decideLocationMode(input({ supported: false }))).toBe("search-only");
  });

  it("falls to search-only when permission is known-denied (read silently)", () => {
    expect(decideLocationMode(input({ permission: "denied" }))).toBe("search-only");
  });

  it("falls to search-only after a hard failure (denied / unsupported)", () => {
    expect(decideLocationMode(input({ lastFailure: "denied" }))).toBe("search-only");
    expect(decideLocationMode(input({ lastFailure: "unsupported" }))).toBe("search-only");
  });

  it("offers a retry after a transient failure (timeout / unavailable)", () => {
    expect(decideLocationMode(input({ lastFailure: "timeout" }))).toBe("search-retry");
    expect(decideLocationMode(input({ lastFailure: "unavailable" }))).toBe("search-retry");
  });

  it("treats unsupported as overriding everything else", () => {
    expect(
      decideLocationMode({ supported: false, permission: "granted", lastFailure: "timeout" }),
    ).toBe("search-only");
  });

  it("treats a known-denied permission as overriding a transient failure", () => {
    expect(
      decideLocationMode({ supported: true, permission: "denied", lastFailure: "timeout" }),
    ).toBe("search-only");
  });
});
