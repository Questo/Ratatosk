import react from '@vitejs/plugin-react'
import { defineConfig } from 'vitest/config'

// Read on the Node side only; client code always calls the relative /api path.
const apiUrl = process.env.API_URL ?? 'http://localhost:5000'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      // Same origin as the app, like nginx in docker-compose (ADR 0003)
      '/api/': {
        target: apiUrl,
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
  },
})
