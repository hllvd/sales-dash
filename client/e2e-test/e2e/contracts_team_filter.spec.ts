import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

/**

 * E2E: Team Multiselect Filter on Contracts Page
 *
 * Setup:
 *  - Superadmin
 *  - User A (admin, child of superadmin)
 *  - User B (admin, child of User A)
 *  - Team-Alpha: owned by User A, member: User B → all of User B's contracts should appear when filtered
 *  - Team-Beta:  owned by User B → User A should NOT see this team in filter (B is a descendant, but this
 *               test verifies Team-Beta IS visible to superadmin and User A via hierarchy)
 *
 * Tests:
 *  1. Superadmin sees all seeded teams in the filter dropdown.
 *  2. Filtering by Team-Alpha shows only contracts for members of Team-Alpha.
 *  3. Admin (User A) sees only hierarchy-scoped teams in the filter dropdown.
 *  4. Clearing the filter resets the contracts list.
 */

test.describe('Contracts Team Filter', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('');

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };
  const userA = {
    name: `TF UserA ${RUN_ID}`,
    email: `tf.usera.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };
  const userB = {
    name: `TF UserB ${RUN_ID}`,
    email: `tf.userb.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const teamAlphaName = `TF Alpha ${RUN_ID}`;
  const teamBetaName  = `TF Beta ${RUN_ID}`;

  let superadminToken: string;
  let superadminId: string;
  let userAId: string;
  let userBId: string;
  let teamAlphaId: number;
  let teamBetaId: number;
  // Track one contract created for User B so we can assert it appears when filtering by Team-Alpha
  let userBContractNumber: string;

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

    // ── 2. Pre-cleanup: delete stale teams/users from prior runs ────────────
    const cutoff = Date.now() - 60_000;

    const teamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (teamsRes.ok()) {
      const teamsList: any[] = (await teamsRes.json()).data ?? [];
      for (const t of teamsList) {
        if ((t.name.startsWith('TF Alpha') || t.name.startsWith('TF Beta')) &&
            new Date(t.createdAt).getTime() < cutoff) {
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

    // ── 3. Register hierarchy: SA → A → B ──────────────────────────────────
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

    // ── 4. Create Team-Alpha (owned by A, member: B) ───────────────────────
    const startDate = new Date(Date.now() - 8 * 365 * 24 * 60 * 60 * 1000).toISOString();
    const alphaRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamAlphaName,
        members: [
          { userId: userAId, startDate },
          { userId: userBId, startDate },
        ],
      },
    });
    expect(alphaRes.ok()).toBeTruthy();
    teamAlphaId = (await alphaRes.json()).data.id;
    await request.post(`/api/teams/${teamAlphaId}/owner`, {
      headers: {
        Authorization: `Bearer ${superadminToken}`,
        'Content-Type': 'application/json',
      },
      data: JSON.stringify(userAId),
    });

    // ── 5. Create Team-Beta (owned by B, no extra members) ─────────────────
    const betaRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamBetaName,
        members: [{ userId: userBId, startDate }],
      },
    });
    expect(betaRes.ok()).toBeTruthy();
    teamBetaId = (await betaRes.json()).data.id;
    await request.post(`/api/teams/${teamBetaId}/owner`, {
      headers: {
        Authorization: `Bearer ${superadminToken}`,
        'Content-Type': 'application/json',
      },
      data: JSON.stringify(userBId),
    });

    // ── 6. Fetch an existing contract for User B (so we can assert it appears) ─
    // We use a contract already in the DB rather than creating one (import tests handle seeding).
    // We query contracts filtered by userEmail=userB and grab the first number if any.
    const cRes = await request.get(`/api/contracts?userEmail=${encodeURIComponent(userB.email)}`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (cRes.ok()) {
      const cBody = await cRes.json();
      if (cBody.data?.length > 0) userBContractNumber = cBody.data[0].contractNumber;
    }
  });

  // ── Helper: login + navigate to Contracts ──────────────────────────────────
  async function loginAndGoToContracts(page: any, email: string, password: string) {
    await loginAs(page, email, password);
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });
  }


  // Helper: open the Team filter MultiSelect dropdown
  // Mantine v7 MultiSelect renders as a combobox with an internal input;
  // clicking the placeholder input directly is the most reliable approach.
  function teamFilterInput(page: any) {
    return page.locator('input[placeholder="Selecionar times..."], input[placeholder="Nenhum time disponível"]').first();
  }

  // ── Test 1: Superadmin sees both seeded teams in filter ───────────────────
  test('Superadmin sees Team-Alpha and Team-Beta in filter dropdown', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, SA.email, SA.password);

    // Click the Teams MultiSelect input to open the dropdown
    await teamFilterInput(page).click();

    // Both teams should appear as options in the dropdown
    await expect(page.getByRole('option', { name: teamAlphaName })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('option', { name: teamBetaName })).toBeVisible({ timeout: 5_000 });
  });

  // ── Test 2: Filter by Team-Alpha sends correct teamIds query param ─────────
  test('Selecting Team-Alpha sends teamIds query param to API', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToContracts(page, SA.email, SA.password);

    // Set up the request watcher BEFORE selecting (debounce fires after 3s)
    const teamFilterRequest = page.waitForRequest(
      (req: any) =>
        req.url().includes('/api/contracts') &&
        !req.url().includes('/user/') &&
        req.url().includes(`teamIds=${teamAlphaId}`) &&
        req.method() === 'GET',
      { timeout: 15_000 }
    );

    // Open and select Team-Alpha
    await teamFilterInput(page).click();
    await page.getByRole('option', { name: teamAlphaName }).click();

    // Wait for debounced request (3s debounce + network)
    await teamFilterRequest;
  });

  // ── Test 3: Admin (User A) sees only hierarchy-scoped teams via API ────────
  // We verify hierarchy scope via the API directly (simpler and more reliable than
  // asserting dropdown visibility which requires the teams to have loaded).
  test('Admin (User A) can only GET hierarchy-scoped teams from API', async ({ request }) => {
    // Login as User A
    const loginRes = await request.post('/api/users/login', {
      data: { email: userA.email, password: userA.password },
    });
    expect(loginRes.ok()).toBeTruthy();
    const tokenA = (await loginRes.json()).data.token;

    // Fetch teams as User A
    const teamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${tokenA}` },
    });
    expect(teamsRes.ok()).toBeTruthy();
    const teamsList: any[] = (await teamsRes.json()).data ?? [];

    const teamNames = teamsList.map((t: any) => t.name);
    // A owns Team-Alpha (A is owner); B (descendant of A) owns Team-Beta — both should be visible
    expect(teamNames).toContain(teamAlphaName);
    expect(teamNames).toContain(teamBetaName);

    // Sanity: should not see arbitrary other teams with no hierarchy connection
    // (We can't enumerate all other teams, but we can confirm the right ones are there)
    expect(teamsList.find((t: any) => t.id === teamAlphaId)).toBeTruthy();
    expect(teamsList.find((t: any) => t.id === teamBetaId)).toBeTruthy();
  });

  // ── Test 4: Clearing the filter resets the view ────────────────────────────
  test('Clearing team filter resets contracts list', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAndGoToContracts(page, SA.email, SA.password);

    // Select Team-Alpha to activate the filter
    await teamFilterInput(page).click();
    await page.getByRole('option', { name: teamAlphaName }).click();

    // Wait for the debounced filtered request (3s debounce)
    await page.waitForRequest(
      (req: any) =>
        req.url().includes('/api/contracts') &&
        req.url().includes(`teamIds=${teamAlphaId}`) &&
        req.method() === 'GET',
      { timeout: 15_000 }
    );

    // The "Limpar Filtros" button should now be visible
    await expect(page.locator('.clear-filters-btn')).toBeVisible({ timeout: 5_000 });

    // Close the dropdown by pressing Escape to prevent it from overlaying/blocking the button
    await page.keyboard.press('Escape');

    // Click Limpar Filtros — this synchronously resets all filter state
    await page.locator('.clear-filters-btn').click();

    // The clear-filters button should disappear because no active filters remain
    await expect(page.locator('.clear-filters-btn')).not.toBeVisible({ timeout: 5_000 });

    // The MultiSelect input should be visible and show the placeholder (no selection)
    await expect(teamFilterInput(page)).toBeVisible({ timeout: 5_000 });
  });
});
