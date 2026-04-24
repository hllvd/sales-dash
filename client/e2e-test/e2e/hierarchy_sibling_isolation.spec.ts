import { test, expect } from '@playwright/test';

/**
 * [TEAR 3] Sibling Isolation Test
 *
 * Verifies that two users who are NOT in a parent-child relationship
 * ("siblings" in the hierarchy) cannot see each other's contracts.
 *
 * Chain under test:
 *   [Root Level]
 *   ├── Carlos Mendes   (carlosmendes@example.com) — Admin  ← LOGIN USER
 *   │   └── Julio Mota  (juliomota@example.com)             ← Child of Carlos
 *   └── Leonardo Bandieri (leonardobandieri@example.com)    ← Unrelated (sibling-level)
 *       [uses matricula 10134]
 *
 * Carlos's allowed matriculas: {6111, 11177, 9999, 7777, 8888}
 * 10134 is NOT in that set → Carlos must see 0 contracts for 10134 contracts.
 *
 * The test also verifies from the positive side (superadmin CAN see it)
 * to guarantee the contract actually exists and the test is not trivially passing.
 */
test.describe('[TEAR 3] Sibling Isolation', () => {
  const SIBLING_CONTRACT = 'ISOLATION-10134-001';
  const SIBLING_MATRICULA = '10134';

  // ── Seed: create one contract for matricula 10134 via superadmin API ──────
  test.beforeAll(async ({ playwright, baseURL }) => {
    const ctx = await playwright.request.newContext({ baseURL });

    const loginRes = await ctx.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    expect(loginRes.ok()).toBeTruthy();
    const { data: { token } } = await loginRes.json();
    const headers = { Authorization: `Bearer ${token}` };

    // Create the isolation probe contract (idempotent: 400 = already exists)
    const createRes = await ctx.post('/api/contracts', {
      headers,
      data: {
        contractNumber: SIBLING_CONTRACT,
        totalAmount: 10000,
        status: 'Active',
        matriculaNumber: SIBLING_MATRICULA,
      },
    });
    expect([200, 201, 400]).toContain(createRes.status());

    await ctx.dispose();
  });

  // ── Positive: superadmin can see the contract (confirms it exists) ─────────
  test('superadmin can see the isolation probe contract', async ({ page }) => {
    test.setTimeout(30_000);

    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10_000 });

    await page.fill('input#filterContractNumber', SIBLING_CONTRACT);
    await page.waitForTimeout(4000); // debounce

    await expect(page.locator('table tbody tr')).toHaveCount(1, { timeout: 10_000 });
  });

  // ── Negative: Carlos CANNOT see 10134 contracts (sibling isolation) ────────
  test('Carlos Mendes cannot see contracts belonging to unrelated matricula 10134', async ({ page }) => {
    test.setTimeout(30_000);

    await page.goto('/');
    await page.fill('input[type="email"]', 'carlosmendes@example.com');
    await page.fill('input[type="password"]', '123456');
    await page.click('button.login-button');

    // Admin sees "Gerenciamento de Contratos", not "Meus Contratos"
    await page.getByTestId('nav-contracts').click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });

    // Filter by the probe contract number created via superadmin
    await page.fill('input#filterContractNumber', SIBLING_CONTRACT);
    await page.waitForTimeout(4000); // debounce

    // Carlos must see 0 rows — 10134 is not in his descendant tree
    const rows = page.locator('table tbody tr');
    const emptyState = page.locator('.contracts-empty, [data-testid="no-results"]');

    const rowCount = await rows.count();
    expect(rowCount).toBe(0);
    console.log(`>>> Sibling isolation confirmed: Carlos sees ${rowCount} rows for contract ${SIBLING_CONTRACT}`);
  });
});
