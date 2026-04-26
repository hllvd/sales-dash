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
      name: 'tear-1-setup-and-import',
      testMatch: [
        'import_wizard.spec.ts',
        'login.spec.ts',
        'smoke.spec.ts',
        'responsive_menu.spec.ts',
        'circular_hierarchy_prevention.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'tear-2-roles-testing',
      testMatch: [
        'import_wizard_status_update.spec.ts',
        'user_role_management.spec.ts',
        //'import_wizard_csv_delimiter.spec.ts',
        'import_wizard_email_mapping.spec.ts',
        'import_wizard_data_integrity.spec.ts',
        'import_wizard_validation.spec.ts',
        'scrape_credentials.spec.ts',
        'matricula_ownership.spec.ts',
        'powerbi_credentials.spec.ts',
        'contract_edit_robustness.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['tear-1-setup-and-import']
    },
    {
      name: 'tear-3',
      testMatch: [
        'hierarchy_contract_visibility.spec.ts',
        'hierarchy_sibling_isolation.spec.ts',
        'hierarchy_deep_visibility.spec.ts',
        'contracts_filtering.spec.ts',
        'contract_export.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['tear-2-roles-testing']
    },
    {
      name: 'tear-4-aliases-testing',
      testMatch: ['import_wizard_aliases.spec.ts'],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['tear-3']
    },

  ],
});
