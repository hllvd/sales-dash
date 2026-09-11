import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

/**
 * E2E: Matricula MultiSelect Autocomplete Filter & Refresh Button on My Contracts Page (`/#/my-contracts`)
 *
 * Setup:
 *   - Seller user created fresh for this run
 *   - 2 active matriculas created and linked to seller (Mat A and Mat B)
 *   - Contract A with Mat A
 *   - Contract B with Mat B
 *
 * Tests:
 *   1. UI: MultiSelect renders on /#/my-contracts with seller's active matriculas as options
 *   2. Filter: Selecting Mat A sends matriculas param and displays only Contract A
 *   3. Filter: Selecting both Mat A and Mat B displays both contracts
 *   4. Refresh: Clicking "Atualizar" button reloads all data successfully
 *   5. Clear: "Limpar Filtros" resets the matricula MultiSelect and hides the clear button
 */

test.describe('My Contracts — Matricula Autocomplete & Refresh', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_LETTERS = Array.from({ length: 8 }, () =>
    String.fromCharCode(65 + Math.floor(Math.random() * 26))
  ).join('');
  const RUN_ID = RUN_LETTERS.toLowerCase() + Date.now().toString().slice(-4);

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };

  const seller = {
    name: `MCM Seller ${RUN_LETTERS}`,
    email: `mcm.seller.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const matANum = `7${Math.floor(10000 + Math.random() * 90000)}`;
  const matBNum = `6${Math.floor(10000 + Math.random() * 90000)}`;

  const contractANum = `MCM-A-${RUN_ID}`.slice(0, 20);
  const contractBNum = `MCM-B-${RUN_ID}`.slice(0, 20);

  let superadminToken: string;
  let superadminId: string;
  let sellerId: string;

  const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

  function matriculaMultiSelectInput(page: any) {
    return page.locator('input#matriculaFilter');
  }

  test.beforeAll(async ({ request }) => {
    // 1. Login as superadmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: SA.email, password: SA.password },
    });
    expect(loginRes.ok()).toBeTruthy();
    superadminToken = (await loginRes.json()).data.token;

    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(meRes.ok()).toBeTruthy();
    superadminId = (await meRes.json()).data.id;

    // 2. Pre-cleanup any stale user
    const usersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (usersRes.ok()) {
      const users: any[] = (await usersRes.json()).data?.items ?? [];
      for (const u of users) {
        if (u.email?.includes('mcm.seller.')) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    // 3. Register seller user (self-healing)
    const registerUser = async (name: string, email: string, role: string, parentId?: string) => {
      const res = await request.post('/api/users/register', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: { name, email, password: 'Password123!', role, parentUserId: parentId },
      });
      if (res.ok()) return (await res.json()).data.id as string;
      if (res.status() === 400) {
        const searchRes = await request.get(`/api/users?search=${encodeURIComponent(email)}`, {
          headers: { Authorization: `Bearer ${superadminToken}` },
        });
        if (searchRes.ok()) {
          const listBody = await searchRes.json();
          const found = (listBody.data?.items ?? []).find((u: any) => u.email.toLowerCase() === email.toLowerCase());
          if (found) {
            if (!found.isActive) {
              const updateRes = await request.put(`/api/users/${found.id}`, {
                headers: { Authorization: `Bearer ${superadminToken}` },
                data: { isActive: true, role, parentUserId: parentId },
              });
              expect(updateRes.ok()).toBeTruthy();
            }
            return found.id as string;
          }
        }
      }
      throw new Error(`Seller registration failed: ${res.status()} ${await res.text()}`);
    };

    sellerId = await registerUser(seller.name, seller.email, seller.role, superadminId);
    await settle();

    // 4. Create Matricula A and link to seller
    const matARes = await request.post('/api/matriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { matriculaNumber: matANum, status: 'active', startDate: '2026-01-01T00:00:00Z' },
    });
    expect(matARes.ok()).toBeTruthy();

    const linkARes = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { userId: sellerId, matriculaNumber: matANum, isOwner: true, startDate: '2026-01-01T00:00:00Z' },
    });
    expect(linkARes.ok()).toBeTruthy();
    await settle();

    // 5. Create Matricula B and link to seller
    const matBRes = await request.post('/api/matriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { matriculaNumber: matBNum, status: 'active', startDate: '2026-01-01T00:00:00Z' },
    });
    expect(matBRes.ok()).toBeTruthy();

    const linkBRes = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { userId: sellerId, matriculaNumber: matBNum, isOwner: false, startDate: '2026-01-01T00:00:00Z' },
    });
    expect(linkBRes.ok()).toBeTruthy();
    await settle();

    // 6. Create Contract A with Mat A
    const cARes = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractANum,
        userId: sellerId,
        matriculaNumber: matANum,
        contractStartDate: '2026-08-10T00:00:00Z',
        totalAmount: 1500,
        customerName: 'Cliente Mat A',
        status: 'Active',
      },
    });
    expect(cARes.ok()).toBeTruthy();

    // 7. Create Contract B with Mat B
    const cBRes = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractBNum,
        userId: sellerId,
        matriculaNumber: matBNum,
        contractStartDate: '2026-08-20T00:00:00Z',
        totalAmount: 2500,
        customerName: 'Cliente Mat B',
        status: 'Active',
      },
    });
    expect(cBRes.ok()).toBeTruthy();
  });

  // ── Test 1: MultiSelect renders with seller's matricula options ───────────────
  test('Matricula MultiSelect renders options for current user active matriculas', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });

    const input = matriculaMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });

    // Click to open dropdown
    await input.click();

    // Both matricula numbers should appear in the options list
    await expect(page.getByRole('option', { name: new RegExp(matANum) })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('option', { name: new RegExp(matBNum) })).toBeVisible({ timeout: 5_000 });

    await page.keyboard.press('Escape');
  });

  // ── Test 2: Filter by Mat A displays only Contract A ─────────────────────────
  test('Filter by Mat A: Contract A visible, Contract B not visible', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });
    await page.waitForLoadState('networkidle');

    const input = matriculaMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.click();

    const filteredResponse = page.waitForResponse(
      (res: any) =>
        res.url().includes('/api/contracts/user/') &&
        res.url().includes(`matriculas=${matANum}`) &&
        res.ok(),
      { timeout: 15_000 }
    );

    await page.getByRole('option', { name: new RegExp(matANum) }).click();
    await page.keyboard.press('Escape');

    await filteredResponse;

    await expect(page.locator(`text=${contractANum}`)).toBeVisible({ timeout: 10_000 });
    await expect(page.locator(`text=${contractBNum}`)).not.toBeVisible();
  });

  // ── Test 3: Filter by both Mat A and Mat B displays both contracts ───────────
  test('Filter by both Mat A and Mat B displays both contracts', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });
    await page.waitForLoadState('networkidle');

    // First select Mat A
    const input = matriculaMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.click();

    let filteredResponse = page.waitForResponse(
      (res: any) =>
        res.url().includes('/api/contracts/user/') &&
        res.url().includes(`matriculas=${matANum}`) &&
        res.ok(),
      { timeout: 15_000 }
    );
    await page.getByRole('option', { name: new RegExp(matANum) }).click();
    await filteredResponse;

    // Now select Mat B as well
    filteredResponse = page.waitForResponse(
      (res: any) =>
        res.url().includes('/api/contracts/user/') &&
        res.url().includes(`matriculas=${matBNum}`) &&
        res.ok(),
      { timeout: 15_000 }
    );
    await page.getByRole('option', { name: new RegExp(matBNum) }).click();
    await page.keyboard.press('Escape');
    await filteredResponse;

    // Both contracts must be visible
    await expect(page.locator(`text=${contractANum}`)).toBeVisible({ timeout: 10_000 });
    await expect(page.locator(`text=${contractBNum}`)).toBeVisible({ timeout: 10_000 });
  });

  // ── Test 4: Refresh button updates data ──────────────────────────────────────
  test('Clicking Atualizar button reloads all data and displays notification', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });

    const refreshBtn = page.getByRole('button', { name: 'Atualizar' });
    await expect(refreshBtn).toBeVisible({ timeout: 10_000 });

    const refreshResponse = page.waitForResponse(
      (res: any) => res.url().includes('/api/contracts/user/') && res.ok(),
      { timeout: 15_000 }
    );

    await refreshBtn.click();
    await refreshResponse;

    // Verify success notification or state
    await expect(page.locator('text=Dados atualizados com sucesso')).toBeVisible({ timeout: 10_000 });
  });

  // ── Test 5: Limpar Filtros resets matricula filter ────────────────────────────
  test('Limpar Filtros resets matricula MultiSelect and hides clear button', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });

    // Select Mat A to activate filter
    const input = matriculaMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.click();
    await page.getByRole('option', { name: new RegExp(matANum) }).click();
    await page.keyboard.press('Escape');

    // "Limpar Filtros" must appear
    const clearBtn = page.locator('button:has-text("Limpar Filtros")');
    await expect(clearBtn).toBeVisible({ timeout: 5_000 });

    // Click it
    await clearBtn.click();

    // Button should disappear
    await expect(clearBtn).not.toBeVisible({ timeout: 5_000 });

    // No pills should remain in matricula MultiSelect
    await expect(page.locator('.filter-group:has(#matriculaFilter) .mantine-MultiSelect-pill')).not.toBeAttached();
  });
});
