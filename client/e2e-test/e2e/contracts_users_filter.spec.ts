import { test, expect } from '@playwright/test';

/**
 * E2E: Users Multiselect Filter on Contracts Page
 *
 * Setup:
 *  - Superadmin
 *  - User A (admin, child of superadmin)
 *  - User B (admin, child of User A)
 *  - User C (admin, child of superadmin, NOT a descendant of User A)
 *
 * Tests:
 *  1. Superadmin sees all users in the filter dropdown.
 *  2. Admin (User A) sees only their descendants (User B) and themselves in the filter dropdown (no User C or Superadmin).
 *  3. Group filter is removed from UI.
 *  4. Selecting a user sends correct query param.
 *  5. Clearing the filter resets the list.
 */

test.describe('Contracts Users Filter', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('');

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };
  const userA = {
    name: `UF UserA ${RUN_ID}`,
    email: `uf.usera.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };
  const userB = {
    name: `UF UserB ${RUN_ID}`,
    email: `uf.userb.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };
  const userC = {
    name: `UF UserC ${RUN_ID}`,
    email: `uf.userc.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  let superadminToken: string;
  let superadminId: string;
  let userAId: string;
  let userBId: string;
  let userCId: string;

  const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

  test.beforeAll(async ({ request }) => {
    // ── 1. Login as superadmin ──────────────────────────────────────────────
    const loginRes = await request.post('/api/users/login', {
      data: { email: SA.email, password: SA.password },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginBody = await loginRes.json();
    superadminToken = loginBody.data.token;

    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(meRes.ok()).toBeTruthy();
    superadminId = (await meRes.json()).data.id;

    // ── 2. Pre-cleanup: delete stale test users from prior runs ────────────
    const cutoff = Date.now() - 60_000;

    const usersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (usersRes.ok()) {
      const usersList: any[] = (await usersRes.json()).data?.items ?? [];
      for (const u of usersList) {
        if (u.email?.endsWith('@test.com') && new Date(u.createdAt).getTime() < cutoff) {
          for (let p = 0; p < 3; p++) {
            await request.delete(`/api/users/${u.id}`, {
              headers: { Authorization: `Bearer ${superadminToken}` },
            });
          }
        }
      }
    }

    // ── 3. Register hierarchy: SA → A → B, SA → C ─────────────────────────
    const registerUser = async (name: string, email: string, role: string, parentId?: string) => {
      const res = await request.post('/api/users/register', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: { name, email, password: 'Password123!', role, parentUserId: parentId },
      });
      if (res.ok()) return (await res.json()).data.id as string;
      // Self-heal: already exists (either active or inactive/soft-deleted)
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
      throw new Error(`User registration failed: ${await res.text()}`);
    };

    userAId = await registerUser(userA.name, userA.email, userA.role, superadminId);
    await settle();
    userBId = await registerUser(userB.name, userB.email, userB.role, userAId);
    await settle();
    userCId = await registerUser(userC.name, userC.email, userC.role, superadminId);
    await settle();
  });

  // ── Helper: login + navigate to Contracts ──────────────────────────────────
  async function loginAndGoToContracts(page: any, email: string, password: string) {
    await page.goto('/');
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.click('button.login-button');
    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });
  }

  function userFilterInput(page: any) {
    return page.locator('input[placeholder="Selecionar usuários..."], input[placeholder="Nenhum usuário disponível"]').first();
  }

  // ── Test 1: Superadmin sees all users ─────────────────────────────────────
  test('Superadmin sees User A, User B, and User C in users filter', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, SA.email, SA.password);

    await userFilterInput(page).click();

    // Verify all users appear as options
    await expect(page.getByRole('option', { name: userA.name, exact: false })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('option', { name: userB.name, exact: false })).toBeVisible({ timeout: 5_000 });
    await expect(page.getByRole('option', { name: userC.name, exact: false })).toBeVisible({ timeout: 5_000 });
  });

  // ── Test 2: Admin A sees only hierarchy-scoped users ───────────────────────
  test('Admin (User A) sees only descendant users and self in filter dropdown', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, userA.email, userA.password);

    await userFilterInput(page).click();

    // User A should see themselves and their child User B
    await expect(page.getByRole('option', { name: userA.name, exact: false })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('option', { name: userB.name, exact: false })).toBeVisible({ timeout: 5_000 });

    // User A should NOT see User C or Superadmin
    await expect(page.getByRole('option', { name: userC.name, exact: false })).not.toBeVisible();
    await expect(page.getByRole('option', { name: 'Super Admin', exact: false })).not.toBeVisible();
  });

  // ── Test 3: Remove Grupo filter from UI ────────────────────────────────────
  test('Group filter is removed from the UI', async ({ page }) => {
    test.setTimeout(30_000);
    await loginAndGoToContracts(page, SA.email, SA.password);

    // Assert that the label / combobox for Grupo is not present in the DOM
    await expect(page.locator('label[for="filterGroup"]')).not.toBeVisible();
    await expect(page.locator('select#filterGroup')).not.toBeVisible();
  });

  // ── Test 4: Selecting a user sends correct query param ─────────────────────
  test('Selecting a user sends userIds query param to contracts API', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, SA.email, SA.password);

    // Set up request listener before selecting (3s debounce)
    const userFilterRequest = page.waitForRequest(
      (req: any) =>
        req.url().includes('/api/contracts') &&
        !req.url().includes('/user/') &&
        req.url().includes(`userIds=${userBId}`) &&
        req.method() === 'GET',
      { timeout: 15_000 }
    );

    await userFilterInput(page).click();
    await page.getByRole('option', { name: userB.name, exact: false }).click();

    await userFilterRequest;
  });

  // ── Test 5: Clearing the filter resets the list ────────────────────────────
  test('Clearing users filter resets the list', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAndGoToContracts(page, SA.email, SA.password);

    await userFilterInput(page).click();
    await page.getByRole('option', { name: userB.name, exact: false }).click();

    await expect(page.locator('.clear-filters-btn')).toBeVisible({ timeout: 5_000 });

    await page.keyboard.press('Escape');

    await page.locator('.clear-filters-btn').click();

    await expect(page.locator('.clear-filters-btn')).not.toBeVisible({ timeout: 5_000 });
    await expect(userFilterInput(page)).toBeVisible({ timeout: 5_000 });
  });
});
