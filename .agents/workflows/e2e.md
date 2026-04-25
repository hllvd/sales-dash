---
description: e2e - Playwright E2E Specialist
---

Playwright E2E Specialist Prompt
Context:

Location: All E2E tests are located in client/e2e-test.
Test Scripts: Found in client/e2e-test/e2e.
Test Data: Found in client/e2e-test/test-data.
The "Tears" System: Tests are organized into sequential projects ("tears") in playwright.config.ts to ensure data dependency:

tear-1: Essential setup, login, and smoke tests.
tear-2: Complex logic (roles, data integrity, delimiters).
tear-3: Business rules (hierarchy visibility, filtering).
tear-4: Specialized flows (Wizard aliasing and mapping).
How to Add/Manage Tests:

Create Test: Add your_test_name.spec.ts in client/e2e-test/e2e.
Register in Config: Edit client/e2e-test/playwright.config.ts.
Project Entry: Add a new object to the projects array:
typescript
{
  name: 'tear-X',
  testMatch: ['your_test_name.spec.ts'],
  dependencies: ['tear-(X-1)'] // Maintain the chain
}
Execution:
Run specific tear: npx playwright test --project=tear-X
Run all: npx playwright test

## Temporary Files
Any files generated during test execution (e.g., downloads, enriched exports) should be stored in the `./temp/` directory.

## E2E Test Stability and Soft-Delete Handling
### Problem: The "Ghost" Record Conflict
Sequential E2E tests often fail on the second run because:
1. They "soft-delete" data during teardown (setting `IsActive = false`).
2. The `POST` create call fails on the next run because the record exists in the DB.
3. The `GET` list call shows 0 results because it filters for `IsActive = true`.
### Best Practices
#### 1. Backend "Restore" Logic (Upsert)
Create endpoints should detect inactive duplicates and restore them instead of returning a 400 error:
- If record exists AND is active -> Return 400 (Real duplicate).
- If record exists AND is inactive -> Restore (`IsActive = true`) and update with new request data.
#### 2. UI Filter Isolation
Always clear `localStorage` and reset date/text filters in `test.beforeEach` or at the start of a test. The `ContractsPage` specifically debounces and caches filters, which can cause "ghost" filtering that hides test data.
#### 3. Unique Identifiers
Always append a timestamp or random suffix to test-generated entities (Contracts, Users, Matriculas) to ensure parallel workers never collide on the same database keys.