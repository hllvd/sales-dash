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