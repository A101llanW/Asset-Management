import { defineConfig, devices } from '@playwright/test';

const port = (process.env.E2E_PORT ?? '').trim() || '51901';
const baseURL = (process.env.E2E_BASE_URL ?? '').trim() || `http://localhost:${port}`;

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],
  timeout: 60_000,
  expect: { timeout: 10_000 },
  globalSetup: './global-setup.ts',
  use: {
    baseURL,
    viewport: { width: 1280, height: 720 },
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: process.env.E2E_SKIP_WEBSERVER
    ? undefined
    : {
        command: 'powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/start-app.ps1',
        url: `${baseURL}/Account/Login`,
        reuseExistingServer: false,
        timeout: 180_000,
        stdout: 'pipe',
        stderr: 'pipe',
      },
});
