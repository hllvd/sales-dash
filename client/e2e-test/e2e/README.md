# E2E Test Execution Guide

This directory contains end-to-end tests for the SalesApp. Due to the stateful nature of the application and the shared database, tests are structured to follow a specific execution order managed in `playwright.config.ts`.

## Parallel Startup Tests

The following tests are independent and are configured to start immediately and run in parallel:
- `import_wizard.spec.ts`: Handles the main data import flow.
- `login.spec.ts`: Verifies basic authentication.
- `smoke.spec.ts`: Verifies basic page availability.

## Sequential / Dependent Tests

Most other tests in this suite depend on the data being correctly imported and mapped by the `import_wizard`. Therefore, they are configured to wait for the `import_wizard` to complete before they start:

1. **Contract Filters** (`contracts_filtering.spec.ts`): Relies on imported contracts to verify filtering logic.
2. **Contract Updates** (`import_wizard_status_update.spec.ts`): Relies on existing contracts to verify status updates.
3. **Wizard Validations**:
   - `import_wizard_csv_delimiter.spec.ts`
   - `import_wizard_email_mapping.spec.ts`

## Configuration

The execution order is enforced using Playwright **Projects** and **Dependencies**. 

- The `setup-and-import` project runs the wizard.
- Other projects (like `contract-filters` or `contract-updates`) list `setup-and-import` in their `dependencies` array.
- Independent tests run in the `other-tests` project which has no dependencies.

To run all tests:
```bash
npx playwright test
```
