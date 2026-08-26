import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD } from './helpers/auth';

test.describe('[TEAR 3] Admin Create User Gestor Defaults (Parent, Matricula & Team)', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('');

  const adminUser = {
    name: `Admin Gestor ${RUN_ID}`,
    email: `admin.gestor.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const newUser1 = {
    name: `Child User One ${RUN_ID}`,
    email: `child.user1.${RUN_ID}@test.com`,
    password: 'Password123!',
  };

  const newUser2 = {
    name: `Child User Two ${RUN_ID}`,
    email: `child.user2.${RUN_ID}@test.com`,
    password: 'Password123!',
  };

  const teamName = `Equipe Gestor ${RUN_ID}`;
  const MAT1 = `MAT1${RUN_ID}`;
  const MAT2 = `MAT2${RUN_ID}`;

  let superadminToken: string;
  let superadminId: string;
  let adminUserId: string;
  let teamId: number;

  const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

  test.beforeAll(async ({ request }) => {
    // 1. Login as superadmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: SUPERADMIN_EMAIL, password: SUPERADMIN_PASSWORD },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginBody = await loginRes.json();
    superadminToken = loginBody.data.token;

    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(meRes.ok()).toBeTruthy();
    superadminId = (await meRes.json()).data.id;

    // 2. Register Admin User
    const regAdminRes = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: adminUser.name,
        email: adminUser.email,
        password: adminUser.password,
        role: adminUser.role,
        parentUserId: superadminId,
        matriculaNumber: MAT1,
        isMatriculaOwner: true,
      },
    });
    expect(regAdminRes.ok()).toBeTruthy();
    adminUserId = (await regAdminRes.json()).data.id;
    await settle();

    // 3. Create Team for Admin and set as Owner
    const teamRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: teamName,
        members: [
          {
            userId: adminUserId,
            startDate: new Date().toISOString(),
          },
        ],
      },
    });
    expect(teamRes.ok()).toBeTruthy();
    const teamBody = await teamRes.json();
    teamId = teamBody.data.id;

    await request.post(`/api/teams/${teamId}/owner`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: `"${adminUserId}"`,
    });
    await settle();
  });

  test('Admin creates user with auto-filled parent, default manager matricula, and team checked', async ({ page, request }) => {
    // Login as the created Admin
    await loginAs(page, adminUser.email, adminUser.password);

    await page.goto('/#/users');
    await expect(page.locator('h2:has-text("Gerenciamento de Usuários")')).toBeVisible({ timeout: 15000 });

    // Open Create User Modal
    await page.click('button:has-text("Criar")');
    await expect(page.locator('.mantine-Modal-title:has-text("Criar Novo Usuário")')).toBeVisible();

    // Verify Parent User Autocomplete is prefilled with admin's email/name
    const parentInput = page.locator('input[placeholder="Digite para buscar..."]');
    await expect(parentInput).toHaveValue(new RegExp(adminUser.email, 'i'));

    // Verify "Usar matrícula do gestor" checkbox is checked by default
    const matriculaCheckbox = page.getByRole('checkbox', { name: 'Usar matrícula do gestor' });
    await expect(matriculaCheckbox).toBeChecked({ timeout: 10000 });

    // Verify MAT1 is shown as read-only / auto-selected
    const matriculaInput = page.locator('input[value="' + MAT1 + '"]');
    await expect(matriculaInput).toBeVisible();

    // Verify "Participar da equipe [TeamName]" checkbox is visible and checked by default
    const teamCheckbox = page.getByRole('checkbox', { name: new RegExp(`Participar da equipe ${teamName}`, 'i') });
    await expect(teamCheckbox).toBeVisible();
    await expect(teamCheckbox).toBeChecked();

    // Fill new user details
    await page.fill('input[placeholder="Nome completo"]', newUser1.name);
    await page.fill('input[placeholder="email@exemplo.com"]', newUser1.email);
    await page.fill('input[placeholder="Senha"]', newUser1.password);

    // Submit
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.locator('.mantine-Modal-title:has-text("Criar Novo Usuário")')).toBeHidden({ timeout: 15000 });

    // Verify user was created via API and possesses parent, team, and matricula
    await settle();
    const listRes = await request.get(`/api/users?search=${encodeURIComponent(newUser1.email)}`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(listRes.ok()).toBeTruthy();
    const listData = await listRes.json();
    expect(listData.data.items.length).toBeGreaterThan(0);
    const created = listData.data.items.find((u: any) => u.email.toLowerCase() === newUser1.email.toLowerCase());
    expect(created).toBeTruthy();
    expect(created.parentUserId).toBe(adminUserId);
    expect(created.currentTeamName).toBe(teamName);
    expect(created.activeMatriculas?.length).toBeGreaterThan(0);
    expect(created.activeMatriculas[0].matriculaNumber).toBe(MAT1);
    expect(created.activeMatriculas[0].isOwner).toBe(false);
    expect(created.isMatriculaOwner).toBe(false);
  });

  test('Admin with multiple owned matriculas shows select dropdown to choose from', async ({ page, request }) => {
    // Assign 2nd owned matricula to Admin via UserMatriculas endpoint
    // 1. Create or get matricula 2
    const mat2Res = await request.post('/api/matriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { matriculaNumber: MAT2, status: 'active', startDate: new Date().toISOString() },
    });
    let mat2Id: number;
    if (mat2Res.ok()) {
      mat2Id = (await mat2Res.json()).data.id;
    } else {
      const getMat = await request.get(`/api/matriculas?search=${MAT2}`, {
        headers: { Authorization: `Bearer ${superadminToken}` },
      });
      mat2Id = (await getMat.json()).data.items[0].id;
    }

    // 2. Link Admin to MAT2 as owner
    const linkRes = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        userId: adminUserId,
        matriculaNumber: MAT2,
        isOwner: true,
        startDate: new Date().toISOString(),
      },
    });
    expect(linkRes.ok()).toBeTruthy();
    await settle();

    // Login as Admin
    await loginAs(page, adminUser.email, adminUser.password);
    await page.goto('/#/users');
    await expect(page.locator('h2:has-text("Gerenciamento de Usuários")')).toBeVisible({ timeout: 15000 });

    // Open Create User Modal
    await page.click('button:has-text("Criar")');
    await expect(page.locator('.mantine-Modal-title:has-text("Criar Novo Usuário")')).toBeVisible();

    // Verify "Usar matrícula do gestor" is checked
    const matriculaCheckbox = page.getByRole('checkbox', { name: 'Usar matrícula do gestor' });
    await expect(matriculaCheckbox).toBeChecked({ timeout: 10000 });

    // Multi-matricula selection should be rendered
    const matriculaSelect = page.locator('input[placeholder="Escolha uma matrícula"]');
    await expect(matriculaSelect).toBeVisible();

    // Select MAT2
    await matriculaSelect.click();
    await page.click(`.mantine-Select-option:has-text("${MAT2}")`);

    // Fill details for 2nd user
    await page.fill('input[placeholder="Nome completo"]', newUser2.name);
    await page.fill('input[placeholder="email@exemplo.com"]', newUser2.email);
    await page.fill('input[placeholder="Senha"]', newUser2.password);

    // Submit
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.locator('.mantine-Modal-title:has-text("Criar Novo Usuário")')).toBeHidden({ timeout: 15000 });

    // Verify user 2 was created with MAT2
    await settle();
    const listRes = await request.get(`/api/users?search=${encodeURIComponent(newUser2.email)}`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(listRes.ok()).toBeTruthy();
    const listData = await listRes.json();
    const created = listData.data.items.find((u: any) => u.email.toLowerCase() === newUser2.email.toLowerCase());
    expect(created).toBeTruthy();
    expect(created.activeMatriculas?.length).toBeGreaterThan(0);
    expect(created.activeMatriculas[0].matriculaNumber).toBe(MAT2);
    expect(created.activeMatriculas[0].isOwner).toBe(false);
    expect(created.isMatriculaOwner).toBe(false);
  });

  test('Superadmin creation starts with empty parent and dynamically loads manager details on selection', async ({ page }) => {
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);
    await page.goto('/#/users');
    await expect(page.locator('h2:has-text("Gerenciamento de Usuários")')).toBeVisible({ timeout: 15000 });

    // Open Create User Modal
    await page.click('button:has-text("Criar")');
    await expect(page.locator('.mantine-Modal-title:has-text("Criar Novo Usuário")')).toBeVisible();

    // Parent input should be empty for superadmin
    const parentInput = page.locator('input[placeholder="Digite para buscar..."]');
    await expect(parentInput).toHaveValue('');

    // Initially, no manager team checkbox
    await expect(page.getByRole('checkbox', { name: /Participar da equipe/i })).toBeHidden();

    // Select the admin user as parent
    await parentInput.fill(adminUser.email);
    const option = page.locator(`.mantine-Autocomplete-option:has-text("${adminUser.email}")`).first();
    await expect(option).toBeVisible({ timeout: 10000 });
    await option.click();

    // Dynamic loading should trigger and display "Usar matrícula do gestor" and team checkbox
    await expect(page.getByRole('checkbox', { name: 'Usar matrícula do gestor' })).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('checkbox', { name: new RegExp(`Participar da equipe ${teamName}`, 'i') })).toBeVisible();
  });
});