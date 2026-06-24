import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { execSync } from 'node:child_process'
import { readFileSync } from 'node:fs'

const pkg = JSON.parse(
  readFileSync(new URL('./package.json', import.meta.url), 'utf8'),
)

function gitShortSha(): string | null {
  try {
    return execSync('git rev-parse --short HEAD', {
      stdio: ['ignore', 'pipe', 'ignore'],
    })
      .toString()
      .trim()
  } catch {
    // No git (e.g. a tarball build) — fall back to null → label reads "local".
    return null
  }
}

// CI sets GITHUB_SHA (full hash); locally we shell out to git for the short form.
const sha = process.env.GITHUB_SHA?.slice(0, 7) ?? gitShortSha()

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // Baked into the bundle at build time; consumed via src/ui/version.ts.
  define: {
    __APP_VERSION__: JSON.stringify(pkg.version),
    __BUILD_SHA__: JSON.stringify(sha),
  },
})
