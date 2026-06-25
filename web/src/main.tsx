import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource/sora/700.css' // self-hosted brand wordmark (only weight used)
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
