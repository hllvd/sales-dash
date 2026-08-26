import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  globalSetup: './global-setup.ts',
  testDir: './e2e',
  // Enable parallel execution
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 1,
  // Use multiple workers to speed up independent tests
  workers: 4,
  // Increase default test timeout - many tests have login + multi-step flows
  timeout: 60000,
  reporter: 'html',
  use: {
    baseURL: 'http://localhost',
    storageState: '.auth/superadmin.json',
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
      name: 'tear-2a-import',
      testMatch: [
        'import_wizard_status_update.spec.ts',
        'import_status_validation.spec.ts',
        'import_wizard_email_mapping.spec.ts',
        'import_wizard_data_integrity.spec.ts',
        'import_wizard_validation.spec.ts',
        'import_wizard_email_duplicates.spec.ts',
        'import_wizard_duplicate_contracts.spec.ts',
        'import_wizard_desistente_contracts.spec.ts',
        'import_wizard_blank_contracts.spec.ts',
        'import_wizard_outliers.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['tear-1-setup-and-import']
    },
    {
      name: 'tear-2b-roles',
      testMatch: [
        'user_role_management.spec.ts',
        'stores_crud.spec.ts',
        'contract_dashboard_desistente.spec.ts',
        'scrape_credentials.spec.ts',
        'matricula_ownership.spec.ts',
        'matricula_edit_normalization.spec.ts',
        'powerbi_credentials.spec.ts',
        'contract_edit_robustness.spec.ts',
        'import_wizard_verification.spec.ts',
        'import_dashboard_matricula_change.spec.ts',
        'import_error_csv_download.spec.ts',
        'contract_dashboard_bem_pend_1_atr.spec.ts',
        'import_dashboard_update_options.spec.ts',
        'import_dashboard_missing_total_amount.spec.ts',
        'import_dashboard_cota_field.spec.ts',
        'import_dashboard_upsert_robustness.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['tear-1-setup-and-import']
    },
    {
      name: 'tear-3a-hierarchy',
      testMatch: [
        'admin_assign_contract_matricula.spec.ts',
        'batch_parent_update.spec.ts',
        'user_tree_hierarchy.spec.ts',
        'hierarchy_contract_visibility.spec.ts',
        'hierarchy_sibling_isolation.spec.ts',
        'hierarchy_deep_visibility.spec.ts',
        'contracts_filtering.spec.ts',
        'contracts_ui_enhancements.spec.ts',
        'contracts_team_filter.spec.ts',
        'contracts_users_filter.spec.ts',
        'contract_export.spec.ts',
        'matricula_health_monitoring.spec.ts',
        'teams_hierarchy_visibility.spec.ts',
        'team_report_setup.spec.ts',
        'team_members_management.spec.ts',
        'contracts_matricula_multiselect.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['tear-2a-import', 'tear-2b-roles']
    },
    {
      name: 'tear-3b-admin',
      testMatch: [
        'classification_management.spec.ts',
        'classification_next_level.spec.ts',
        'classification_members_modal.spec.ts',
        'user_classification_and_views.spec.ts',
        'import_wizard_aliases.spec.ts',
        'pending_contract_claims.spec.ts',
        'user_metadata.spec.ts',
        'admin_registration.spec.ts',
        'admin_permissions.spec.ts',
        'matricula_admin_access.spec.ts',
        'matricula_request_approval.spec.ts',
        'equipe_admin_permission.spec.ts',
        'delete_user_migration.spec.ts',
        'approval_requests.spec.ts',
        'batch_merge_users.spec.ts',
        'batch_merge_matriculas.spec.ts',
        'admin_create_user_gestor_defaults.spec.ts'
      ],
      use: { ...devices['Desktop Chrome'] },
      dependencies: ['tear-2a-import', 'tear-2b-roles']
    }
  ],
});
