import { useEffect, useState } from "react";
import {
  applyThemeMode,
  loadThemeMode,
  saveThemeMode,
  type ThemeMode,
} from "./theme";

function SunIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41" />
    </svg>
  );
}

function MoonIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
    </svg>
  );
}

function MonitorIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <rect x="2" y="3" width="20" height="14" rx="2" />
      <path d="M8 21h8M12 17v4" />
    </svg>
  );
}

const OPTIONS = [
  { mode: "system", tip: "Theme: System Default", Icon: MonitorIcon },
  { mode: "light", tip: "Theme: Light", Icon: SunIcon },
  { mode: "dark", tip: "Theme: Dark", Icon: MoonIcon },
] as const satisfies readonly { mode: ThemeMode; tip: string; Icon: () => React.ReactElement }[];

export function ThemeToggle() {
  const [mode, setMode] = useState<ThemeMode>(() => loadThemeMode());

  // Apply + persist whenever the choice changes (and on first mount).
  useEffect(() => {
    applyThemeMode(mode);
    saveThemeMode(mode);
  }, [mode]);

  return (
    <div className="theme-toggle" role="group" aria-label="Color theme">
      {OPTIONS.map(({ mode: m, tip, Icon }) => (
        <button
          key={m}
          type="button"
          className={mode === m ? "active" : ""}
          aria-pressed={mode === m}
          aria-label={tip}
          title={tip}
          onClick={() => setMode(m)}
        >
          <Icon />
        </button>
      ))}
    </div>
  );
}
