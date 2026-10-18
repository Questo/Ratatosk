import { defineConfig, devices } from '@playwright/test'

// Runs against a stack that is already up: docker-compose's web service by default, or a Vite dev server
export default defineConfig({
  testDir: './e2e',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
