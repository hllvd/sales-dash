import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  // Disable parallel execution globally for these state-dependent tests
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  // Use a single worker to prevent race conditions on the shared database
  workers: 1,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost',
    trace: 'on-first-retry',
    viewport: { width: 1280, height: 720 },
  },
  projects: [
    {
      name: 'setup-and-import',
      testMatch: /import_wizard\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'contract-updates',
      testMatch: /import_wizard_status_update\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['setup-and-import'],
    },
    {
      name: 'remaining-tests',
      testIgnore: [/import_wizard\.spec\.ts/, /import_wizard_status_update\.spec\.ts/],
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
