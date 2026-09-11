import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

/**
 * E2E: Team MultiSelect Filter on My Contracts Page (`/#/my-contracts`)
 *
 * Setup:
 *   - Seller user created fresh for this run
 *   - Team Alpha: seller member from 2026-07-01 → ends 2026-07-31 (transitions to Beta)
 *   - Team Beta:  seller member from 2026-08-01 → open-ended
 *   - Contract Alpha: SaleStartDate 2026-07-15 → belongs to Alpha period
 *   - Contract Beta:  SaleStartDate 2026-08-15 → belongs to Beta period
 *
 * Tests:
 *   1. API — GET /users/me/teams returns only the seller's teams (Alpha + Beta)
 *   2. UI  — MultiSelect renders with "Todos" placeholder and lists Alpha + Beta as options
 *   3. UI/API — Selecting Alpha dispatches GET /contracts/user/{id}?teamIds={alphaId}
 *   4. Temporal — Filter by Alpha: Contract Alpha visible, Contract Beta absent
 *   5. Temporal — Filter by Beta:  Contract Beta visible, Contract Alpha absent
 *   6. Clear  — "Limpar Filtros" resets the MultiSelect and hides the button
 */

test.describe('My Contracts — Team Filter', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_LETTERS = Array.from({ length: 8 }, () =>
    String.fromCharCode(65 + Math.floor(Math.random() * 26))
  ).join('');
  const RUN_ID = RUN_LETTERS.toLowerCase() + Date.now().toString().slice(-4);

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };

  const seller = {
    name: `MCF Seller ${RUN_LETTERS}`,
    email: `mcf.seller.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const teamAlphaName = `MCF Alpha ${RUN_ID}`;
  const teamBetaName  = `MCF Beta ${RUN_ID}`;

  const contractAlphaNum = `MCF-ALP-${RUN_ID}`.slice(0, 20);
  const contractBetaNum  = `MCF-BET-${RUN_ID}`.slice(0, 20);

  const matriculaNumber = `8${Math.floor(10000 + Math.random() * 90000)}`;

  let superadminToken: string;
  let superadminId: string;
  let sellerId: string;
  let sellerToken: string;
  let teamAlphaId: number;
  let teamBetaId: number;

  const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

  // Locator for the team MultiSelect input on /#/my-contracts
  function teamMultiSelectInput(page: any) {
    return page.locator('input[placeholder="Todos"]').first();
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

    // 2. Cleanup stale data from previous runs
    const teamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (teamsRes.ok()) {
      const teams: any[] = (await teamsRes.json()).data ?? [];
      for (const t of teams) {
        if (t.name.startsWith('MCF Alpha') || t.name.startsWith('MCF Beta')) {
          await request.delete(`/api/teams/${t.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    const usersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (usersRes.ok()) {
      const users: any[] = (await usersRes.json()).data?.items ?? [];
      for (const u of users) {
        if (u.email?.includes('mcf.seller.')) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    // 3. Create seller user
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

    // 4. Create matricula and link to seller
    const matRes = await request.post('/api/matriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { matriculaNumber, status: 'active', startDate: '2026-01-01T00:00:00Z' },
    });
    if (!matRes.ok()) console.error(`Matricula create failed: ${matRes.status()} ${await matRes.text()}`);
    expect(matRes.ok()).toBeTruthy();

    const linkRes = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { userId: sellerId, matriculaNumber, isOwner: true, startDate: '2026-01-01T00:00:00Z' },
    });
    if (!linkRes.ok()) console.error(`UserMatricula link failed: ${linkRes.status()} ${await linkRes.text()}`);
    expect(linkRes.ok()).toBeTruthy();
    await settle();

    // 5. Create Team Alpha — seller member from 2026-07-01
    const alphaRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamAlphaName,
        members: [{ userId: sellerId, startDate: '2026-07-01T00:00:00Z' }],
      },
    });
    if (!alphaRes.ok()) console.error(`Team Alpha create failed: ${alphaRes.status()} ${await alphaRes.text()}`);
    expect(alphaRes.ok()).toBeTruthy();
    teamAlphaId = (await alphaRes.json()).data.id;
    await settle();

    // 6. Create Team Beta (superadmin as placeholder member) then set owner
    const betaRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamBetaName,
        members: [{ userId: superadminId, startDate: '2026-07-01T00:00:00Z' }],
      },
    });
    if (!betaRes.ok()) console.error(`Team Beta create failed: ${betaRes.status()} ${await betaRes.text()}`);
    expect(betaRes.ok()).toBeTruthy();
    teamBetaId = (await betaRes.json()).data.id;

    await request.post(`/api/teams/${teamBetaId}/owner`, {
      headers: { Authorization: `Bearer ${superadminToken}`, 'Content-Type': 'application/json' },
      data: JSON.stringify(superadminId),
    });
    await settle();

    // Transition seller from Alpha to Beta starting 2026-08-01
    // (Alpha membership automatically ends on 2026-07-31)
    const assignRes = await request.post('/api/teams/calendar/assign-team', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        userId: sellerId,
        newTeamId: teamBetaId,
        startDate: '2026-08-01T00:00:00Z',
        updateParentUser: false,
      },
    });
    if (!assignRes.ok()) console.error(`Assign to Beta failed: ${assignRes.status()} ${await assignRes.text()}`);
    expect(assignRes.ok()).toBeTruthy();
    await settle();

    // 7. Create Contract Alpha (SaleStartDate 2026-07-15 → within Alpha period)
    const c1Res = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractAlphaNum,
        userId: sellerId,
        matriculaNumber,
        contractStartDate: '2026-07-15T00:00:00Z',
        totalAmount: 1000,
        customerName: 'Cliente Alpha Julho',
        status: 'Active',
      },
    });
    if (!c1Res.ok()) console.error(`Contract Alpha create failed: ${c1Res.status()} ${await c1Res.text()}`);
    expect(c1Res.ok()).toBeTruthy();

    // 8. Create Contract Beta (SaleStartDate 2026-08-15 → within Beta period)
    const c2Res = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractBetaNum,
        userId: sellerId,
        matriculaNumber,
        contractStartDate: '2026-08-15T00:00:00Z',
        totalAmount: 2000,
        customerName: 'Cliente Beta Agosto',
        status: 'Active',
      },
    });
    if (!c2Res.ok()) console.error(`Contract Beta create failed: ${c2Res.status()} ${await c2Res.text()}`);
    expect(c2Res.ok()).toBeTruthy();

    // 9. Login as seller and cache token for API tests
    const sellerLoginRes = await request.post('/api/users/login', {
      data: { email: seller.email, password: seller.password },
    });
    expect(sellerLoginRes.ok()).toBeTruthy();
    sellerToken = (await sellerLoginRes.json()).data.token;
  });

  // ── Test 1: API endpoint returns both teams ────────────────────────────────────
  test('GET /users/me/teams returns both teams the seller has been part of', async ({ request }) => {
    const res = await request.get('/api/users/me/teams', {
      headers: { Authorization: `Bearer ${sellerToken}` },
    });
    expect(res.ok()).toBeTruthy();

    const body = await res.json();
    expect(body.success).toBe(true);

    const names: string[] = (body.data ?? []).map((t: any) => t.name);
    expect(names).toContain(teamAlphaName);
    expect(names).toContain(teamBetaName);

    expect(body.data.find((t: any) => t.id === teamAlphaId)).toBeTruthy();
    expect(body.data.find((t: any) => t.id === teamBetaId)).toBeTruthy();
  });

  // ── Test 2: MultiSelect appears with correct options ──────────────────────────
  test('MultiSelect renders on /#/my-contracts with the seller teams as options', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });

    const input = teamMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });

    await input.click();
    await expect(page.getByRole('option', { name: teamAlphaName })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('option', { name: teamBetaName })).toBeVisible({ timeout: 5_000 });

    await page.keyboard.press('Escape');
  });

  // ── Test 3: Selecting a team dispatches teamIds to the API ────────────────────
  test('Selecting Team Alpha sends teamIds query param to /contracts/user/{id}', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });

    const filteredRequest = page.waitForRequest(
      (req: any) =>
        req.url().includes('/api/contracts/user/') &&
        req.url().includes(`teamIds=${teamAlphaId}`) &&
        req.method() === 'GET',
      { timeout: 15_000 }
    );

    const input = teamMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.click();
    await page.getByRole('option', { name: teamAlphaName }).click();
    await page.keyboard.press('Escape');

    await filteredRequest;
  });

  // ── Test 4: Temporal filter — Alpha period ────────────────────────────────────
  test('Filter by Alpha: Contract Alpha visible, Contract Beta not visible', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });
    await page.waitForLoadState('networkidle');

    const input = teamMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.click();

    const filteredResponse = page.waitForResponse(
      (res: any) =>
        res.url().includes('/api/contracts/user/') &&
        res.url().includes(`teamIds=${teamAlphaId}`) &&
        res.ok(),
      { timeout: 15_000 }
    );

    await page.getByRole('option', { name: teamAlphaName }).click();
    await page.keyboard.press('Escape');

    await filteredResponse;

    await expect(page.locator(`text=${contractAlphaNum}`)).toBeVisible({ timeout: 10_000 });
    await expect(page.locator(`text=${contractBetaNum}`)).not.toBeVisible();
  });

  // ── Test 5: Temporal filter — Beta period ─────────────────────────────────────
  test('Filter by Beta: Contract Beta visible, Contract Alpha not visible', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });
    await page.waitForLoadState('networkidle');

    const input = teamMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.click();

    const filteredResponse = page.waitForResponse(
      (res: any) =>
        res.url().includes('/api/contracts/user/') &&
        res.url().includes(`teamIds=${teamBetaId}`) &&
        res.ok(),
      { timeout: 15_000 }
    );

    await page.getByRole('option', { name: teamBetaName }).click();
    await page.keyboard.press('Escape');

    await filteredResponse;

    await expect(page.locator(`text=${contractBetaNum}`)).toBeVisible({ timeout: 10_000 });
    await expect(page.locator(`text=${contractAlphaNum}`)).not.toBeVisible();
  });

  // ── Test 6: Limpar Filtros resets the MultiSelect ─────────────────────────────
  test('Limpar Filtros resets team MultiSelect and hides the clear button', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAs(page, seller.email, seller.password);
    await page.goto('/#/my-contracts');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15_000 });

    // No filter active → button must not exist
    await expect(page.locator('button:has-text("Limpar Filtros")')).not.toBeVisible();

    // Activate filter by selecting Alpha
    const input = teamMultiSelectInput(page);
    await expect(input).toBeVisible({ timeout: 10_000 });
    await input.click();
    await page.getByRole('option', { name: teamAlphaName }).click();
    await page.keyboard.press('Escape');

    // Button must appear
    await expect(page.locator('button:has-text("Limpar Filtros")')).toBeVisible({ timeout: 5_000 });

    // Click it
    await page.locator('button:has-text("Limpar Filtros")').click();

    // Button must disappear (no active filter)
    await expect(page.locator('button:has-text("Limpar Filtros")')).not.toBeVisible({ timeout: 5_000 });

    // MultiSelect must still be visible, with no selected pills
    await expect(teamMultiSelectInput(page)).toBeVisible({ timeout: 5_000 });
    await expect(page.locator('.mantine-MultiSelect-pill')).not.toBeAttached();
  });
});
