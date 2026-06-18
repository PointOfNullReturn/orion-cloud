// Theme preference: persisted user choice + applying it to the DOM.
//
// "system" follows the OS via the CSS @media (prefers-color-scheme) rules in
// index.css — there is no JS listener; the media query reacts to OS changes on
// its own. We only ever set a data-theme attribute; CSS does the resolution.

export type ThemeMode = "system" | "light" | "dark";

export const THEME_STORAGE_KEY = "orion-theme";

const MODES: readonly ThemeMode[] = ["system", "light", "dark"];

export function loadThemeMode(): ThemeMode {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    if (stored && (MODES as readonly string[]).includes(stored)) {
      return stored as ThemeMode;
    }
  } catch {
    // localStorage can throw (private mode / disabled) — fall through.
  }
  return "system";
}

export function saveThemeMode(mode: ThemeMode): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, mode);
  } catch {
    // Persistence is best-effort; the choice still applies for this session.
  }
}

export function applyThemeMode(mode: ThemeMode): void {
  document.documentElement.dataset.theme = mode;
}
