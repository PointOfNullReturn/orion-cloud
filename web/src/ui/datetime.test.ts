import { describe, expect, it } from "vitest";
import { formatAbsolute, formatUpdatedAt, relativeTime } from "./datetime";

// A fixed reference "now" so the relative wording is deterministic.
const NOW = new Date("2026-06-20T15:45:00Z");

// Helper: an ISO instant `secondsAgo` before NOW.
function ago(seconds: number): string {
  return new Date(NOW.getTime() - seconds * 1000).toISOString();
}

describe("relativeTime", () => {
  it("reads 'just now' for anything under ~45s", () => {
    expect(relativeTime(ago(0), NOW)).toBe("just now");
    expect(relativeTime(ago(44), NOW)).toBe("just now");
  });

  it("rounds to whole minutes", () => {
    expect(relativeTime(ago(60), NOW)).toBe("1 min ago");
    expect(relativeTime(ago(4 * 60), NOW)).toBe("4 min ago");
    expect(relativeTime(ago(44 * 60), NOW)).toBe("44 min ago");
  });

  it("switches to hours past ~45 min", () => {
    expect(relativeTime(ago(60 * 60), NOW)).toBe("1 hr ago");
    expect(relativeTime(ago(3 * 60 * 60), NOW)).toBe("3 hr ago");
  });

  it("switches to days past ~22 h, pluralizing", () => {
    expect(relativeTime(ago(24 * 60 * 60), NOW)).toBe("1 day ago");
    expect(relativeTime(ago(3 * 24 * 60 * 60), NOW)).toBe("3 days ago");
  });

  it("clamps a clock-skewed future timestamp to 'just now'", () => {
    const future = new Date(NOW.getTime() + 30_000).toISOString();
    expect(relativeTime(future, NOW)).toBe("just now");
  });
});

describe("formatAbsolute", () => {
  it("renders a short, no-year date + time in the given locale/zone", () => {
    expect(
      formatAbsolute("2026-06-20T15:45:00Z", {
        locale: "en-US",
        timeZone: "UTC",
      }),
    ).toBe("Jun 20, 3:45 PM");
  });

  it("honors the supplied time zone", () => {
    // 15:45 UTC is 08:45 in Los Angeles (PDT, UTC-7).
    expect(
      formatAbsolute("2026-06-20T15:45:00Z", {
        locale: "en-US",
        timeZone: "America/Los_Angeles",
      }),
    ).toBe("Jun 20, 8:45 AM");
  });
});

describe("formatUpdatedAt", () => {
  it("composes the relative and absolute parts with a separator", () => {
    expect(
      formatUpdatedAt(ago(4 * 60), NOW, { locale: "en-US", timeZone: "UTC" }),
    ).toBe("Updated 4 min ago · Jun 20, 3:41 PM");
  });
});
