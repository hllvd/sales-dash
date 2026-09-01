import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD } from './helpers/auth';

/**
 * [TEAR 3] Sibling Isolation Test
 *
 * Verifies that two users who are NOT in a parent-child relationship
 * ("siblings" in the hierarchy) cannot see each other's contracts.
 */
test.describe('[TEAR 3] Sibling Isolation', () => {
  // Use a unique ID for each run to avoid any soft-delete or persistence conflicts
  const RUN_ID = Date.now().toString().slice(-6);
  const SIBLING_CONTRACT = `ISO${RUN_ID}`;
  const SIBLING_MATRICULA = '10134';

  // ── Seed: create the probe contract ───────────────────────────────────────
  test.beforeAll(async ({ playwright, baseURL }) => {
    const ctx = await playwright.request.newContext({ baseURL });

    const loginRes = await ctx.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    expect(loginRes.ok()).toBeTruthy();
    const { data: { token } } = await loginRes.json();
    const headers = { Authorization: `Bearer ${token}` };

    const createRes = await ctx.post('/api/contracts', {
      headers,
      data: {
        contractNumber: SIBLING_CONTRACT,
        totalAmount: 10000,
        status: 'Active',
        matriculaNumber: SIBLING_MATRICULA,
        contractStartDate: '2000-01-01',
      },
    });

    const status = createRes.status();
    if (!createRes.ok()) {
      const err = await createRes.text();
      console.error(`>>> API ERROR creating probe contract ${SIBLING_CONTRACT}: HTTP ${status} - Body: ${err}`);
    } else {
      const json = await createRes.json();
      console.log(`>>> Probe contract ready: ${SIBLING_CONTRACT} (id=${json?.data?.id})`);
    }
    expect(createRes.ok()).toBeTruthy();

    await ctx.dispose();
  });

  // ── Teardown: clean up ─────────────────────────────────────────────────────
  test.afterAll(async ({ playwright, baseURL }) => {
    const ctx = await playwright.request.newContext({ baseURL });
    const loginRes = await ctx.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    if (!loginRes.ok()) { await ctx.dispose(); return; }
    const { data: { token } } = await loginRes.json();
    const headers = { Authorization: `Bearer ${token}` };

    const listRes = await ctx.get(`/api/contracts?contractNumber=${SIBLING_CONTRACT}`, { headers });
    if (listRes.ok()) {
      const body = await listRes.json();
      const contracts: Array<{ id: number }> = body?.data?.items ?? body?.data ?? [];
      for (const contract of contracts) {
        await ctx.delete(`/api/contracts/${contract.id}`, { headers });
      }
    }
    await ctx.dispose();
  });

  test('superadmin can see the isolation probe contract', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });

    await page.fill('input#filterStartDate', '');
    
    const searchPromise = page.waitForResponse(resp =>
      resp.url().includes('/api/contracts') &&
      resp.url().includes(SIBLING_CONTRACT) &&
      resp.status() === 200
    );

    await page.fill('input#filterContractNumber', SIBLING_CONTRACT);
    await searchPromise;

    await expect(page.locator('table tbody tr').filter({ hasText: SIBLING_CONTRACT })).toBeVisible({ timeout: 15_000 });
  });

  test('Carlos Mendes cannot see contracts belonging to unrelated matricula 10134', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAs(page, 'carlosmendes@example.com', '123456');
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });


    await page.fill('input#filterStartDate', '');

    const searchPromiseNeg = page.waitForResponse(resp =>
      resp.url().includes('/api/contracts') &&
      resp.url().includes(SIBLING_CONTRACT) &&
      resp.status() === 200
    );

    await page.fill('input#filterContractNumber', SIBLING_CONTRACT);
    await searchPromiseNeg;

    const rowCount = await page.locator('table tbody tr').count();
    expect(rowCount).toBe(0);
  });
});
