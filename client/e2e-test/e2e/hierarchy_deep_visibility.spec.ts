import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

/**
 * [TEAR 3] Deep Hierarchy Contract Visibility Test
 *
 * Verifies that a user can see contracts of descendants at ANY depth, not just
 * direct children. Also verifies the inverse: a leaf node cannot see ancestor contracts.
 *
 * Chain under test (4 levels):
 *   A: Carlos Mendes     (carlosmendes@example.com) — Admin  ← LOGIN USER
 *   └── B: Julio Mota    (juliomota@example.com)    matricula: 9999
 *       └── C: Patricia  (patrician3@example.com)  matricula: 7777  [2 levels below A]
 *           └── D: Diego (diegon4@example.com)     matricula: 8888  [3 levels below A]
 *
 * Expected visibility for A (Carlos):
 *   ✓ B contracts (direct child, 1 level deep)
 *   ✓ C contracts (grandchild, 2 levels deep)   — contract 1100004909
 *   ✓ D contracts (great-grandchild, 3 levels deep) — contract 1100005807
 *
 * This test relies on data seeded via the official Import Wizard (TEAR 1).
 */
test.describe('[TEAR 3] Deep Hierarchy Contract Visibility', () => {
  const CHAIN = {
    A_EMAIL: 'carlosmendes@example.com',
    A_PASSWORD: '123456',
    B_MATRICULA: '9999',    // Julio Mota — direct child of A
    C_MATRICULA: '7777',    // Patricia   — grandchild (2 levels)
    D_EMAIL: 'diegon4@example.com',
    D_PASSWORD: 'diego123',
    D_MATRICULA: '8888',    // Diego      — great-grandchild (3 levels)
  };

  // Official contract numbers added to contracts_historial.xlsx by user
  const CONTRACT_L3 = '1100004909'; // Patricia's contract (level C)
  const CONTRACT_L4 = '1100005807'; // Diego's contract    (level D)

  // ── Helper: login + navigate to contracts page ────────────────────────────
  async function loginAndGoToContracts(
    page: any,
    email: string,
    password: string,
    isAdmin = true,
  ) {
    await loginAs(page, email, password);

    if (isAdmin) {
      await page.goto('/#/contracts');
      await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });

      // Clear filters if any are active (clearing default 15-month date filter)
      const clearFiltersBtn = page.locator('button.clear-filters-btn');
      if (await clearFiltersBtn.isVisible()) {
        await clearFiltersBtn.click();
        await page.waitForTimeout(1000);
      }
    } else {
      // Regular users land on Meus Contratos
      await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });
    }
  }

  // ── Test 1: A sees B (Level 1, direct child) ─────────────────────────────
  test('A sees direct child B contracts (1 level deep)', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, CHAIN.A_EMAIL, CHAIN.A_PASSWORD);

    await page.fill('#filterMatricula', CHAIN.B_MATRICULA);

    // Wait for loading to clear
    await page.waitForSelector('.contracts-loading', { state: 'hidden', timeout: 20_000 });

    const matching = page.locator('table tbody tr').filter({ hasText: CHAIN.B_MATRICULA });
    await expect(matching.first()).toBeVisible({ timeout: 15_000 });
    console.log(`>>> A sees B (9999) contracts ✓`);
  });

  // ── Test 2: A sees C (Level 2, grandchild) ───────────────────────────────
  test('A sees grandchild C contracts (2 levels deep)', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, CHAIN.A_EMAIL, CHAIN.A_PASSWORD);

    // Filter by the official contract number
    await page.fill('input#filterContractNumber', CONTRACT_L3);
    await page.waitForTimeout(6000); // Wait for debounce

    // Wait for any existing rows to disappear if filtering isn't instant
    await expect(page.locator('table tbody tr')).toHaveCount(1, { timeout: 20_000 });
    await expect(page.locator('table tbody tr').filter({ hasText: CHAIN.C_MATRICULA })).toBeVisible({ timeout: 15_000 });
    console.log(`>>> A sees C (7777) contract at depth 2 ✓`);
  });

  // ── Test 3: A sees D (Level 3, great-grandchild) ─────────────────────────
  test('A sees great-grandchild D contracts (3 levels deep)', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, CHAIN.A_EMAIL, CHAIN.A_PASSWORD);

    // Filter by the official contract number
    await page.fill('input#filterContractNumber', CONTRACT_L4);
    await page.waitForTimeout(6000); // Wait for debounce

    await expect(page.locator('table tbody tr')).toHaveCount(1, { timeout: 20_000 });
    await expect(page.locator('table tbody tr').filter({ hasText: CHAIN.D_MATRICULA })).toBeVisible({ timeout: 15_000 });
    console.log(`>>> A sees D (8888) contract at depth 3 ✓`);
  });

  // ── Test 4: D (leaf) cannot see C (parent) contracts — upward isolation ───
  test('D (leaf) cannot see parent C contracts — upward isolation', async ({ page }) => {
    test.setTimeout(45_000);
    
    // Use API context to verify background scope directly
    const apiCtx = await page.context().request;

    // Login as Diego (Level D)
    const loginRes = await apiCtx.post('/api/users/login', {
      data: { email: CHAIN.D_EMAIL, password: CHAIN.D_PASSWORD },
    });
    expect(loginRes.ok()).toBeTruthy();
    const { data: { token } } = await loginRes.json();

    // Directly query the contracts API as Diego for Patricia's contract (Level C)
    const contractsRes = await apiCtx.get(`/api/contracts?contractNumber=${CONTRACT_L3}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    expect(contractsRes.ok()).toBeTruthy();
    const contractsBody = await contractsRes.json();

    // D (Diego) must see 0 results for Patricia's contract
    expect(contractsBody.data).toHaveLength(0);
    console.log(`>>> Upward isolation confirmed: D sees 0 results for parent C's contract ✓`);
  });
});
