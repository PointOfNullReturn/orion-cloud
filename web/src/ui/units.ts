// Units preference: persisted user choice (metric / imperial).
//
// Mirrors theme.ts. Unlike theme there's no DOM to apply to — the choice is a
// fetch parameter and a render flag — so this is just load/save.

import type { Units } from "../api/weather";

export const UNITS_STORAGE_KEY = "orion-units";

const VALUES: readonly Units[] = ["metric", "imperial"];

export function loadUnits(): Units {
  try {
    const stored = localStorage.getItem(UNITS_STORAGE_KEY);
    if (stored && (VALUES as readonly string[]).includes(stored)) {
      return stored as Units;
    }
  } catch {
    // localStorage can throw (private mode / disabled) — fall through.
  }
  return "metric";
}

export function saveUnits(units: Units): void {
  try {
    localStorage.setItem(UNITS_STORAGE_KEY, units);
  } catch {
    // Persistence is best-effort; the choice still applies for this session.
  }
}
