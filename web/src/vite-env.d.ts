/// <reference types="vite/client" />

// Strongly-type our custom env vars so `import.meta.env.VITE_API_URL` is
// `string | undefined` instead of `any` (the default from vite/client).
interface ImportMetaEnv {
  readonly VITE_API_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
