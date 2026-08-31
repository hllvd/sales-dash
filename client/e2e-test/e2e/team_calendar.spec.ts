import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Team Calendar Timeline & Assignment Wizard E2E', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('');

  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' },
    admin: { name: `Admin Cal ${RUN_ID}`, email: `admin.cal.${RUN_ID}@test.com`, role: 'admin' },
    child: { name: `Child Cal ${RUN_ID}`, email: `child.cal.${RUN_ID}@test.com`, role: 'admin' },
  };

  const teamAlphaName = `Cal Alpha ${RUN_ID}`;
  const teamBetaName = `Cal Beta ${RUN_ID}`;
  const teamGammaName = `Cal Gamma ${RUN_ID}`;

  let superadminToken: string;
  let adminId: string;
  let childId: string;
  let teamAlphaId: number;
  let teamBetaId: number;
  let teamGammaId: number;

  test.beforeAll(async ({ request }) => {
    // 1. Login superadmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: users.superadmin.email, password: users.superadmin.password },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    superadminToken = loginData.data.token;

    // 2. Proactive Cleanup
    const getTeamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (getTeamsRes.ok()) {
      const body = await getTeamsRes.json();
      const teamsList = body.data || [];
      for (const t of teamsList) {
        if (t.name.startsWith('Cal Alpha ') || t.name.startsWith('Cal Beta ') || t.name.startsWith('Cal Gamma ')) {
          await request.delete(`/api/teams/${t.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    const getUsersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (getUsersRes.ok()) {
      const body = await getUsersRes.json();
      const usersList = body.data?.items || [];
      for (const u of usersList) {
        if (u.email.toLowerCase().includes('admin.cal.') || u.email.toLowerCase().includes('child.cal.')) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    // 3. Register test admin and child user
    const createAdminRes = await request.post('/api/users', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: users.admin.name,
        email: users.admin.email,
        password: 'password123',
        role: users.admin.role,
      },
    });
    expect(createAdminRes.ok()).toBeTruthy();
    const adminData = await createAdminRes.json();
    adminId = adminData.data.id;

    // Settle before child registration
    await new Promise(r => setTimeout(r, 400));

    const createChildRes = await request.post('/api/users', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: users.child.name,
        email: users.child.email,
        password: 'password123',
        role: users.child.role,
        parentUserId: adminId,
      },
    });
    expect(createChildRes.ok()).toBeTruthy();
    const childData = await createChildRes.json();
    childId = childData.data.id;

    // 4. Create teams and assign child user with 2 periods
    const createTeamAlpha = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamAlphaName,
        members: [{ userId: childId, startDate: '2024-01-01T00:00:00Z' }],
      },
    });
    expect(createTeamAlpha.ok()).toBeTruthy();
    const teamAlphaData = await createTeamAlpha.json();
    teamAlphaId = teamAlphaData.data.id;

    await new Promise(r => setTimeout(r, 400));

    const createTeamBeta = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamBetaName,
        members: [{ userId: childId, startDate: '2024-06-01T00:00:00Z' }],
      },
    });
    expect(createTeamBeta.ok()).toBeTruthy();
    const teamBetaData = await createTeamBeta.json();
    teamBetaId = teamBetaData.data.id;

    await new Promise(r => setTimeout(r, 400));

    const createTeamGamma = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamGammaName,
        ownerUserId: adminId,
      },
    });
    expect(createTeamGamma.ok()).toBeTruthy();
    const teamGammaData = await createTeamGamma.json();
    teamGammaId = teamGammaData.data.id;
  });

  test('Navigation: Should expand Equipes submenu and open Calendário', async ({ page }) => {
    await loginAs(page, users.superadmin.email, users.superadmin.password);

    // Locate Equipes menu item
    const equipesParent = page.getByRole('link', { name: 'Equipes', exact: true });
    await expect(equipesParent).toBeVisible({ timeout: 15000 });

    // Click to ensure submenu is open
    const calendarioLink = page.locator('a[href="#/teams/calendar"]');
    if (!(await calendarioLink.isVisible())) {
      await equipesParent.click();
    }

    await expect(calendarioLink).toBeVisible({ timeout: 5000 });
    await calendarioLink.click();

    // Verify URL and title
    await expect(page).toHaveURL(/#\/teams\/calendar/);
    await expect(page.getByRole('heading', { name: 'Calendário de Equipes' })).toBeVisible({ timeout: 10000 });
  });

  test('User List & Right Pane Hero Card: Should display current active team', async ({ page }) => {
    await page.goto('#/teams/calendar');
    await page.waitForLoadState('networkidle');

    // Search for child user
    const searchInput = page.getByPlaceholder('Buscar por nome, email ou equipe...');
    await expect(searchInput).toBeVisible({ timeout: 10000 });
    await searchInput.fill(users.child.name);

    // Locate user card
    const userCard = page.locator('.team-calendar-user-card', { hasText: users.child.name });
    await expect(userCard).toBeVisible({ timeout: 10000 });
    await userCard.click();

    // Verify Hero Card displays current team
    const heroCard = page.locator('.team-calendar-current-card');
    await expect(heroCard).toBeVisible();
    await expect(heroCard.getByText(teamBetaName, { exact: false })).toBeVisible();
    await expect(heroCard.getByRole('button', { name: 'Atribuir Nova Equipe' })).toBeVisible();
  });

  test('Wizard: Should assign user to a new team via 4-step wizard and sync parent user', async ({ page }) => {
    await page.goto('#/teams/calendar');
    await page.waitForLoadState('networkidle');

    // Search and select child user
    const searchInput = page.getByPlaceholder('Buscar por nome, email ou equipe...');
    await searchInput.fill(users.child.name);
    const userCard = page.locator('.team-calendar-user-card', { hasText: users.child.name });
    await expect(userCard).toBeVisible({ timeout: 10000 });
    await userCard.click();

    // Click "Atribuir Nova Equipe" button in Hero Card
    const assignBtn = page.getByRole('button', { name: 'Atribuir Nova Equipe' });
    await expect(assignBtn).toBeVisible({ timeout: 5000 });
    await assignBtn.click();

    // Verify Wizard Modal opens
    const modal = page.locator('.mantine-Modal-content');
    await expect(modal).toBeVisible({ timeout: 5000 });
    await expect(modal.getByText('Atribuir Nova Equipe')).toBeVisible();

    // Step 0: Select new team (teamGammaName)
    const select = modal.locator('.mantine-Select-input');
    await select.click();
    const dropdownOption = page.locator('.mantine-Select-option', { hasText: teamGammaName });
    await expect(dropdownOption).toBeVisible({ timeout: 5000 });
    await dropdownOption.click();

    // Verify parent sync checkbox is visible and checked by default
    const parentCheckbox = modal.locator('input[type="checkbox"]');
    await expect(parentCheckbox).toBeVisible();
    await expect(parentCheckbox).toBeChecked();

    // Click "Próximo" to advance to Step 1 (Data)
    const nextBtn = modal.getByRole('button', { name: 'Próximo' });
    await nextBtn.click();

    // Step 1: Set Date
    const dateInput = modal.locator('input[type="date"]');
    await expect(dateInput).toBeVisible();
    await dateInput.fill('2025-01-01');

    // Click "Próximo" to advance to Step 2 (Preview)
    await nextBtn.click();

    // Step 2: Contract Preview
    await expect(modal.getByText('Preview dos Contratos Afetados')).toBeVisible({ timeout: 5000 });

    // Click "Próximo" to advance to Step 3 (Confirmação)
    await nextBtn.click();
    await expect(modal.getByText('Confirmação de Mudança')).toBeVisible({ timeout: 5000 });

    // Click "Confirmar Atribuição"
    const confirmBtn = modal.getByRole('button', { name: 'Confirmar Atribuição' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();

    // Modal closes and Hero Card shows new team
    await expect(modal).not.toBeVisible({ timeout: 8000 });
    const heroCard = page.locator('.team-calendar-current-card');
    await expect(heroCard.getByText(teamGammaName, { exact: false })).toBeVisible({ timeout: 8000 });
  });

  test('Adjustment Modal: Should allow quick transition adjustments', async ({ page }) => {
    await page.goto('#/teams/calendar');
    await page.waitForLoadState('networkidle');

    // Search and select child user
    const searchInput = page.getByPlaceholder('Buscar por nome, email ou equipe...');
    await searchInput.fill(users.child.name);
    const userCard = page.locator('.team-calendar-user-card', { hasText: users.child.name });
    await expect(userCard).toBeVisible({ timeout: 10000 });
    await userCard.click();

    // Click "Ajustar Transição" on one of the historical period cards
    const adjustBtn = page.locator('.team-calendar-period-card').getByRole('button', { name: 'Ajustar Transição' }).first();
    await expect(adjustBtn).toBeVisible({ timeout: 5000 });
    await adjustBtn.click();

    // Verify modal appears
    const modal = page.locator('.mantine-Modal-content');
    await expect(modal).toBeVisible({ timeout: 5000 });
    await expect(modal.getByText('Ajustar Data de Transição entre Equipes')).toBeVisible();

    // Cancel modal
    const cancelBtn = modal.getByRole('button', { name: 'Cancelar' });
    await cancelBtn.click();
    await expect(modal).not.toBeVisible();
  });
});
