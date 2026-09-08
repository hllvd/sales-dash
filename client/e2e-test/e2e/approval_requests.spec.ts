import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';


test.describe('Approval Requests E2E', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('') + Date.now().toString().slice(-4);
  const ADMIN_EMAIL = `parent.admin.${RUN_ID}@test.com`;
  const USER_EMAIL = `request.user.${RUN_ID}@test.com`;
  const PASSWORD = 'Password123!';

  let superadminToken = '';
  let superadminId = '';

  test.beforeAll(async ({ request }) => {
    // 1. Login as SuperAdmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    superadminToken = loginData.data.token;

    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(meRes.ok()).toBeTruthy();
    superadminId = (await meRes.json()).data.id;

    // Proactive cleanup of old test users
    const getUsersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    if (getUsersRes.ok()) {
      const body = await getUsersRes.json();
      const usersList = body.data?.items || [];
      for (const u of usersList) {
        if (u.email.toLowerCase().includes('parent.admin.') || u.email.toLowerCase().includes('request.user.')) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` },
          });
        }
      }
    }

    const registerUser = async (name: string, email: string, role: string, parentUserId?: string) => {
      for (let attempt = 1; attempt <= 3; attempt++) {
        const res = await request.post('/api/users/register', {
          headers: { Authorization: `Bearer ${superadminToken}` },
          data: { name, email, password: PASSWORD, role, parentUserId },
        });
        if (res.ok()) return;
        await new Promise(r => setTimeout(r, 500));
      }
      throw new Error(`Failed to register user ${email}`);
    };

    // 2. Register a parent admin
    await registerUser('Parent Admin Test', ADMIN_EMAIL, 'admin', superadminId);
    await new Promise(r => setTimeout(r, 400));

    // 3. Register user initially under SuperAdmin
    await registerUser('User Request Test', USER_EMAIL, 'user', superadminId);
  });

  test('Superadmin can view requests page and tabs', async ({ page }) => {
    await loginAs(page);
    await page.goto('/#/requests');
    await page.waitForSelector('text=Central de Solicitações');

    await expect(page.locator('text=Solicitações Pendentes')).toBeVisible();
    await expect(page.locator('text=Minhas Solicitações')).toBeVisible();
  });

  test('User asks to change superior for the first time, parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await loginAs(page, USER_EMAIL, PASSWORD);

    // 2. User goes to Requests page to request superior
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();
    await page.click('button:has-text("Nova Solicitação")');

    await expect(page.getByRole('dialog')).toBeVisible();
    const select = page.locator('input[readonly].mantine-Select-input');
    await select.click();
    await page.click('div[role="option"]:has-text("Alteração de Superior (E-mail)")');

    const emailInput = page.locator('input[placeholder="superior@exemplo.com"]');
    await emailInput.fill(ADMIN_EMAIL);
    await page.click('button:has-text("Enviar Solicitação")');

    // Wait for modal to close
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify request is listed under Minhas Solicitações
    await expect(page.getByRole('cell', { name: 'Alteração de Superior (ParentEmail)' }).first()).toBeVisible({ timeout: 15000 });

    // 3. Target Admin logs in to accept the request
    await loginAs(page, ADMIN_EMAIL, PASSWORD);


    // Go to Requests page
    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with user and click Sim / Aprovar
    const pendingRow = page.locator('tr', { hasText: USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await pendingRow.locator('button:has-text("Sim")').click();

    // Confirm request is processed / removed from pending
    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });

    // 4. Verify user now has target Admin as superior via My Profile page (displays parent Name)
    await loginAs(page, USER_EMAIL, PASSWORD);


    await page.goto('/#/my-profile');
    await expect(page.getByText('Parent Admin Test', { exact: false })).toBeVisible({ timeout: 15000 });
  });

  test('User/Admin can request classification level change and parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await loginAs(page, USER_EMAIL, PASSWORD);

    // 2. User goes to Requests page and creates RequestClassificationLevel
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();
    await page.click('button:has-text("Nova Solicitação")');

    await expect(page.getByRole('dialog')).toBeVisible();
    const typeSelect = page.locator('input[readonly].mantine-Select-input').first();
    await typeSelect.click();
    await page.click('div[role="option"]:has-text("Solicitação de Nível de Classificação")');

    // Select level (e.g. Bronze or Prata)
    const levelSelect = page.locator('input[readonly].mantine-Select-input').nth(1);
    await levelSelect.click();
    await page.getByRole('option').first().click();

    await page.click('button:has-text("Enviar Solicitação")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify request is listed under Minhas Solicitações
    await expect(page.getByRole('cell', { name: 'Solicitação de Nível de Classificação' }).first()).toBeVisible({ timeout: 15000 });

    // 3. Parent Admin logs in to accept the level request
    await loginAs(page, ADMIN_EMAIL, PASSWORD);

    // Go to Requests page
    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with user and click Sim
    const pendingRow = page.locator('tr', { hasText: USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await expect(pendingRow.getByText('Solicitação de Nível de Classificação')).toBeVisible();
    await pendingRow.locator('button:has-text("Sim")').click();

    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });
  });

  test('User asks to be an admin, parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await loginAs(page, USER_EMAIL, PASSWORD);

    // 2. User goes to Requests page and creates RequestAdminRole
    await page.goto('/#/requests');
    await expect(page.getByRole('heading', { name: 'Central de Solicitações' })).toBeVisible();
    await page.click('button:has-text("Nova Solicitação")');

    await expect(page.getByRole('dialog')).toBeVisible();
    const select = page.locator('input[readonly].mantine-Select-input');
    await select.click();
    await page.click('div[role="option"]:has-text("Solicitação de Perfil Administrador (Role Admin)")');

    await page.click('button:has-text("Enviar Solicitação")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify request is listed under Minhas Solicitações
    await expect(page.getByRole('cell', { name: 'Solicitação de Perfil Administrador (Role Admin)' }).first()).toBeVisible({ timeout: 15000 });

    // 3. Parent Admin logs in to accept the role request
    await loginAs(page, ADMIN_EMAIL, PASSWORD);

    // Go to Requests page
    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with user and click Sim
    const pendingRow = page.locator('tr', { hasText: USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await pendingRow.locator('button:has-text("Sim")').click();

    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });

    // 4. Verify user is now an Admin (can see Contratos admin link)
    await loginAs(page, USER_EMAIL, PASSWORD);

    await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15000 });
  });
});

