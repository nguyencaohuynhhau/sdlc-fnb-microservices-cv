import { defineConfig } from '@playwright/test'

// Chạy trên stack compose đang sống: nginx (web) ở 5173 proxy sang gateway 8080. Không có webServer.
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  reporter: 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'retain-on-failure',
  },
})
