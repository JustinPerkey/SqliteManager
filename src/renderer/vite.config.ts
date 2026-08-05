import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  // Sqlm.App serves this build from https://app.sqlm/ via SetVirtualHostNameToFolderMapping
  // (PLAN.md §4.1) — relative asset URLs so the build isn't tied to a specific host path.
  base: './',
  build: {
    outDir: 'dist',
    // The renderer bundle must be verifiably self-contained (PLAN.md §2.2): CI greps this output
    // for http(s):// asset references and fails the build on any hit.
    sourcemap: true,
  },
})
