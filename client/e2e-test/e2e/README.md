# E2E Test Execution Guide (TEARS)

This directory contains end-to-end tests for the SalesApp. Due to the stateful nature of the application and the shared database, tests are structured into **sequential tiers (TEARS)** managed in `playwright.config.ts`.

## Execution Tiers

### 1. TEAR 1 (Import Wizard Group)
**Purpose**: Handles the initial data load into the system.
- Includes: `import_wizard.spec.ts`, `import_wizard_status_update.spec.ts`, etc.
- **Smart Check**: If data is already detected (e.g., 95.03% retention is found), the import wizard steps are skipped to save time.

### 2. TEAR 2 (User & Role Setup)
**Purpose**: Configures account-specific states required for functional tests.
- Includes: `user_role_management.spec.ts`.
- **Smart Check**: Verifies if the target user (e.g., Carlos Mendes) already has the required role (Admin) before attempting promotion.
- **Depends on**: TEAR 1.

### 3. TEAR 3 (Operational Tests)
**Purpose**: Verifies general business logic once data and roles are established.
- Includes: `login.spec.ts`, `smoke.spec.ts`, `contracts_filtering.spec.ts`.
- **Depends on**: TEAR 2.

## How to execute

To run all tiers in sequence:
```bash
npx playwright test
```

To run a specific tier without re-running long setup steps (if already completed):
```bash
# This will trigger dependencies, but they will "skip" if already done
npx playwright test --project tear-3
```

