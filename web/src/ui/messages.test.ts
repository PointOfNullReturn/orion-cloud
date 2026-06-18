import { describe, expect, it } from "vitest";
import type { ErrorKind } from "../api/http";
import { apiErrorMessage, locationNote } from "./messages";

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

describe("locationNote", () => {
  it("returns null on the happy path (supported, not blocked, no failure)", () => {
    expect(locationNote(true, "prompt", null)).toBeNull();
    expect(locationNote(true, "granted", null)).toBeNull();
    expect(locationNote(true, "unknown", null)).toBeNull();
  });

  it("explains an unsupported device", () => {
    expect(locationNote(false, "unknown", null)?.toLowerCase()).toContain("device");
    expect(locationNote(true, "unknown", "unsupported")?.toLowerCase()).toContain("device");
  });

  it("points a blocked user to browser settings (proactive or after a tap)", () => {
    const proactive = locationNote(true, "denied", null);
    const reactive = locationNote(true, "prompt", "denied");
    expect(proactive?.toLowerCase()).toContain("settings");
    expect(reactive).toEqual(proactive);
  });

  it("gives transient failures their own calm copy", () => {
    const timeout = locationNote(true, "prompt", "timeout");
    const unavailable = locationNote(true, "prompt", "unavailable");
    expect(timeout?.length).toBeGreaterThan(0);
    expect(unavailable?.length).toBeGreaterThan(0);
    expect(timeout).not.toEqual(unavailable);
  });

  it("never tells the user to 'search instead' — search is already the hero", () => {
    const notes = [
      locationNote(false, "unknown", null),
      locationNote(true, "denied", null),
      locationNote(true, "prompt", "timeout"),
      locationNote(true, "prompt", "unavailable"),
    ];
    for (const note of notes) {
      expect(note?.toLowerCase()).not.toContain("instead");
    }
  });
});
