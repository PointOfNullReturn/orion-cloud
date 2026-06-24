// Build identity shown on the card back: release version + the commit it was
// built from (e.g. "v1.0.0 · a1b2c3d").
//
// The raw inputs are injected at build time via Vite `define` (see
// vite.config.ts): `version` from package.json, `sha` from git / CI. This
// module is just the pure formatter, kept separate so it's unit-testable
// without the build-time globals.

export function buildLabel(version: string, sha: string | null): string {
  return `v${version} · ${sha || "local"}`;
}
