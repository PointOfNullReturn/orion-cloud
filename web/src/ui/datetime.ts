// Formatting for the "last current conditions" timestamp on the weather card.
// The API stamps `timestamp` (UTC) at the moment data is fetched from the
// providers and freezes it in cache for the TTL, so on a cache hit this reads a
// few minutes old — a genuine freshness signal.

export interface AbsoluteOptions {
  // Default to the viewer's locale/zone in production by leaving these
  // undefined; tests pin them for deterministic output.
  locale?: string;
  timeZone?: string;
}

// Bucket thresholds (seconds), mirroring the common relative-time cutoffs:
// under 45s reads "just now", under 45 min is minutes, under 22 h is hours.
const JUST_NOW = 45;
const MINUTES_MAX = 45 * 60;
const HOURS_MAX = 22 * 60 * 60;

const MINUTE = 60;
const HOUR = 60 * 60;
const DAY = 24 * 60 * 60;

// "4 min ago" — coarse, friendly, and deterministic given `now`. Future
// timestamps (client clock skew) clamp to "just now" rather than "in -1 min".
export function relativeTime(iso: string, now: Date): string {
  const deltaSec = Math.max(0, (now.getTime() - new Date(iso).getTime()) / 1000);

  if (deltaSec < JUST_NOW) return "just now";
  if (deltaSec < MINUTES_MAX) return `${Math.round(deltaSec / MINUTE)} min ago`;
  if (deltaSec < HOURS_MAX) return `${Math.round(deltaSec / HOUR)} hr ago`;

  const days = Math.round(deltaSec / DAY);
  return `${days} day${days === 1 ? "" : "s"} ago`;
}

// "Jun 20, 3:45 PM" — short, no year (current conditions are ~today). Delegates
// to Intl so the viewer sees their own locale + time zone.
export function formatAbsolute(iso: string, opts: AbsoluteOptions = {}): string {
  return new Intl.DateTimeFormat(opts.locale, {
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZone: opts.timeZone,
  }).format(new Date(iso));
}

// True when `iso` is at least `maxAgeMs` old relative to `now` — the cue to
// refresh on tab-refocus instead of waiting out the interval. A future
// (clock-skewed) or unparseable timestamp counts as fresh, so we never trigger a
// spurious refresh on bad input; the regular interval still covers the live case.
export function isStale(iso: string, now: Date, maxAgeMs: number): boolean {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return false;
  return now.getTime() - then >= maxAgeMs;
}

// "Updated 4 min ago · Jun 20, 3:45 PM"
export function formatUpdatedAt(
  iso: string,
  now: Date,
  opts: AbsoluteOptions = {},
): string {
  return `Updated ${relativeTime(iso, now)} · ${formatAbsolute(iso, opts)}`;
}
