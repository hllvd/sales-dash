import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  // Enable parallel execution
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  // Use multiple workers to speed up independent tests
  workers: process.env.CI ? 2 : undefined,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost',
    trace: 'on-first-retry',
    viewport: { width: 1280, height: 720 },
  },
  projects: [
    {
      name: 'setup-and-import',
      testMatch: 'import_wizard.spec.ts',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'contract-updates',
      testMatch: 'import_wizard_status_update.spec.ts',
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['setup-and-import'],
    },
    {
      name: 'wizard-validations',
      testMatch: [
        'import_wizard_csv_delimiter.spec.ts',
        'import_wizard_email_mapping.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['setup-and-import'],
    },
    {
      name: 'contract-filters',
      testMatch: 'contracts_filtering.spec.ts',
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['setup-and-import'],
    },
    {
      name: 'other-tests',
      testIgnore: [
        'import_wizard.spec.ts',
        'import_wizard_status_update.spec.ts',
        'import_wizard_csv_delimiter.spec.ts',
        'import_wizard_email_mapping.spec.ts',
        'contracts_filtering.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
