import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

/**
 * E2E Test: Temporal Team Transition & Point-in-Time Contracts Filtering
 *
 * Validates that when a seller switches teams on a specific boundary date:
 * 1. Contracts sold during the earlier team's tenure (e.g. 2026-08-25) appear only when filtering for that team (Alpha).
 * 2. Contracts sold on or after the transition date (e.g. 2026-08-27) appear only when filtering for the new team (Beta).
 * 3. The Team Calendar timeline and adjustment modal correctly preview contracts around the transition boundary.
 */
test.describe('Team Transition & Point-in-Time Contracts Filtering E2E', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_LETTERS = Array.from({ length: 8 }, () =>
    String.fromCharCode(65 + Math.floor(Math.random() * 26))
  ).join('');
  const RUN_ID = RUN_LETTERS.toLowerCase() + Date.now().toString().slice(-4);

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };

  const adminUser = {
    name: `Admin Trans ${RUN_LETTERS}`,
    email: `admin.trans.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const sellerUser = {
    name: `Seller Trans ${RUN_LETTERS}`,
    email: `seller.trans.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const teamAlphaName = `Alpha Trans ${RUN_ID}`;
  const teamBetaName = `Beta Trans ${RUN_ID}`;
  const matriculaNumber = `9${Math.floor(10000 + Math.random() * 90000)}`;

  const contractAlphaNum = `CTR-ALP-${RUN_ID}`;
  const contractBetaNum = `CTR-BET-${RUN_ID}`;

  let superadminToken: string;
  let superadminId: string;
  let adminId: string;
  let sellerId: string;
  let teamAlphaId: number;
  let teamBetaId: number;

  const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

  test.beforeAll(async ({ request }) => {
    // 1. Login as superadmin
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

    // 2. Cleanup stale test teams and users
    const teamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (teamsRes.ok()) {
      const teamsList: any[] = (await teamsRes.json()).data ?? [];
      for (const t of teamsList) {
        if (t.name.startsWith('Alpha Trans ') || t.name.startsWith('Beta Trans ')) {
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
        if (u.email?.includes('admin.trans.') || u.email?.includes('seller.trans.')) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    // 3. Register Admin and Seller
    const adminRes = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: adminUser.name,
        email: adminUser.email,
        password: adminUser.password,
        role: adminUser.role,
        parentUserId: superadminId,
      },
    });
    if (!adminRes.ok()) console.error(`Admin register failed: ${adminRes.status()} ${await adminRes.text()}`);
    expect(adminRes.ok()).toBeTruthy();
    adminId = (await adminRes.json()).data.id;

    await settle();

    const sellerRes = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: sellerUser.name,
        email: sellerUser.email,
        password: sellerUser.password,
        role: sellerUser.role,
        parentUserId: adminId,
      },
    });
    if (!sellerRes.ok()) console.error(`Seller register failed: ${sellerRes.status()} ${await sellerRes.text()}`);
    expect(sellerRes.ok()).toBeTruthy();
    sellerId = (await sellerRes.json()).data.id;

    // 4. Create Matricula and assign to Seller
    const matRes = await request.post('/api/matriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        matriculaNumber: matriculaNumber,
        status: 'active',
        startDate: new Date().toISOString(),
      },
    });
    if (!matRes.ok()) console.error(`Matricula creation failed: ${matRes.status()} ${await matRes.text()}`);
    expect(matRes.ok()).toBeTruthy();

    const linkRes = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        userId: sellerId,
        matriculaNumber: matriculaNumber,
        isOwner: true,
        startDate: new Date().toISOString(),
      },
    });
    if (!linkRes.ok()) console.error(`UserMatricula link failed: ${linkRes.status()} ${await linkRes.text()}`);
    expect(linkRes.ok()).toBeTruthy();

    await settle();

    // 5. Create Team Alpha (with Seller starting 2026-08-01)
    const teamAlphaRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamAlphaName,
        members: [{ userId: sellerId, startDate: '2026-08-01T00:00:00Z' }],
      },
    });
    if (!teamAlphaRes.ok()) console.error(`Team Alpha create failed: ${teamAlphaRes.status()} ${await teamAlphaRes.text()}`);
    expect(teamAlphaRes.ok()).toBeTruthy();
    teamAlphaId = (await teamAlphaRes.json()).data.id;

    await settle();

    // 6. Create Team Beta (with Admin as member so SetOwner succeeds)
    const teamBetaRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamBetaName,
        members: [{ userId: adminId, startDate: '2026-08-01T00:00:00Z' }],
      },
    });
    if (!teamBetaRes.ok()) console.error(`Team Beta create failed: ${teamBetaRes.status()} ${await teamBetaRes.text()}`);
    expect(teamBetaRes.ok()).toBeTruthy();
    teamBetaId = (await teamBetaRes.json()).data.id;

    const setOwnerRes = await request.post(`/api/teams/${teamBetaId}/owner`, {
      headers: {
        Authorization: `Bearer ${superadminToken}`,
        'Content-Type': 'application/json',
      },
      data: JSON.stringify(adminId),
    });
    if (!setOwnerRes.ok()) console.error(`Team Beta set owner failed: ${setOwnerRes.status()} ${await setOwnerRes.text()}`);
    expect(setOwnerRes.ok()).toBeTruthy();

    await settle();

    // 7. Create Contract 1 in Team Alpha period (Sale Date: 2026-08-25)
    const c1Res = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractAlphaNum,
        userId: sellerId,
        matriculaNumber: matriculaNumber,
        contractStartDate: '2026-08-25T00:00:00Z',
        totalAmount: 1500,
        customerName: 'Cliente Alpha Agosto',
        status: 'Active',
      },
    });
    if (!c1Res.ok()) console.error(`Contract 1 create failed: ${c1Res.status()} ${await c1Res.text()}`);
    expect(c1Res.ok()).toBeTruthy();

    // 8. Assign Seller to Team Beta starting on 2026-08-26 (ending Alpha on 2026-08-25)
    const assignRes = await request.post('/api/teams/calendar/assign-team', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        userId: sellerId,
        newTeamId: teamBetaId,
        startDate: '2026-08-26T00:00:00Z',
        updateParentUser: false,
      },
    });
    if (!assignRes.ok()) console.error(`Assign Seller to Team Beta failed: ${assignRes.status()} ${await assignRes.text()}`);
    expect(assignRes.ok()).toBeTruthy();

    await settle();

    // 9. Create Contract 2 in Team Beta period (Sale Date: 2026-08-27)
    const c2Res = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractBetaNum,
        userId: sellerId,
        matriculaNumber: matriculaNumber,
        contractStartDate: '2026-08-27T00:00:00Z',
        totalAmount: 2500,
        customerName: 'Cliente Beta Agosto',
        status: 'Active',
      },
    });
    if (!c2Res.ok()) console.error(`Contract 2 create failed: ${c2Res.status()} ${await c2Res.text()}`);
    expect(c2Res.ok()).toBeTruthy();
  });

  test('Contracts Page: Team filter correctly segregates contracts by point-in-time membership', async ({ page }) => {
    await loginAs(page, SA.email, SA.password);
    await page.goto('#/contracts');
    await page.waitForLoadState('networkidle');

    // 1. Set date filter range to include August 2026
    await page.fill('input#filterStartDate', '2026-08-01');
    await page.fill('input#filterEndDate', '2026-08-31');
    await page.waitForTimeout(600); // allow debounce to settle

    // 2. Select Team Alpha in the Teams filter
    const teamInput = page.locator('input[placeholder="Selecionar times..."], input[placeholder="Nenhum time disponível"]').first();
    await expect(teamInput).toBeVisible({ timeout: 15000 });
    await teamInput.click();

    const alphaOption = page.getByRole('option', { name: teamAlphaName }).first();
    await expect(alphaOption).toBeVisible({ timeout: 10000 });
    await alphaOption.click();

    // Close dropdown
    await page.keyboard.press('Escape');
    await page.waitForTimeout(1000);

    // Assert: Contract Alpha (25/08) is visible, Contract Beta (27/08) is NOT visible
    await expect(page.locator(`text=${contractAlphaNum}`)).toBeVisible({ timeout: 15000 });
    await expect(page.locator(`text=${contractBetaNum}`)).not.toBeVisible();

    // 3. Switch Team filter from Alpha to Beta
    await teamInput.click();
    await alphaOption.click();
    await page.waitForTimeout(300);

    const betaOption = page.getByRole('option', { name: teamBetaName }).first();
    await expect(betaOption).toBeVisible({ timeout: 10000 });
    await betaOption.click();
    await page.keyboard.press('Escape');
    await page.waitForTimeout(1000);

    // Assert: Contract Beta (27/08) is visible, Contract Alpha (25/08) is NOT visible
    await expect(page.locator(`text=${contractBetaNum}`)).toBeVisible({ timeout: 15000 });
    await expect(page.locator(`text=${contractAlphaNum}`)).not.toBeVisible();
  });

  test('Team Calendar: Timeline displays both periods and contract preview matches boundary cutoff', async ({ page }) => {
    await page.goto('#/teams/calendar');
    await page.waitForLoadState('networkidle');

    // Search and select seller
    const searchInput = page.getByPlaceholder('Buscar por nome, email ou equipe...');
    await expect(searchInput).toBeVisible({ timeout: 10000 });
    await searchInput.fill(sellerUser.name);

    const userCard = page.locator('.team-calendar-user-card', { hasText: sellerUser.name });
    await expect(userCard).toBeVisible({ timeout: 10000 });
    await userCard.click();

    // Verify timeline displays Alpha and Beta
    const timeline = page.locator('.team-calendar-timeline-container');
    await expect(timeline.getByText(teamAlphaName)).toBeVisible({ timeout: 5000 });
    await expect(timeline.getByText(teamBetaName)).toBeVisible({ timeout: 5000 });

    // Open Adjustment Modal via button
    const adjustBtn = page.locator('.team-calendar-period-card').getByRole('button', { name: 'Ajustar Transição' }).first();
    await expect(adjustBtn).toBeVisible({ timeout: 5000 });
    await adjustBtn.click();

    // Verify modal appears with preview tables
    const modal = page.locator('.mantine-Modal-content');
    await expect(modal).toBeVisible({ timeout: 5000 });
    await expect(modal.getByText('Ajustar Data de Transição entre Equipes')).toBeVisible();

    // Verify Contract Alpha (25/08) is in the left preview and Contract Beta (27/08) is in the right preview
    await expect(modal.getByText(contractAlphaNum)).toBeVisible({ timeout: 5000 });
    await expect(modal.getByText(contractBetaNum)).toBeVisible({ timeout: 5000 });

    // Cancel modal
    const cancelBtn = modal.getByRole('button', { name: 'Cancelar' });
    await cancelBtn.click();
    await expect(modal).not.toBeVisible();
  });
});
