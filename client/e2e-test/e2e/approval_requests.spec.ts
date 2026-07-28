import { test, expect } from '@playwright/test';

test.describe('Approval Requests E2E', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Date.now().toString().slice(-4);
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

    // 2. Register a parent admin
    const adminRes = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: 'Parent Admin Test',
        email: ADMIN_EMAIL,
        password: PASSWORD,
        role: 'admin',
        parentUserId: superadminId,
      },
    });
    if (!adminRes.ok()) {
      console.error(`FAILED admin register: status=${adminRes.status()}, error=${await adminRes.text()}`);
    }
    expect(adminRes.ok()).toBeTruthy();

    // 3. Register user initially under SuperAdmin
    const userRes = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: 'User Request Test',
        email: USER_EMAIL,
        password: PASSWORD,
        role: 'user',
        parentUserId: superadminId,
      },
    });
    if (!userRes.ok()) {
      console.error(`FAILED user register: status=${userRes.status()}, error=${await userRes.text()}`);
    }
    expect(userRes.ok()).toBeTruthy();
  });

  test('Superadmin can view requests page and tabs', async ({ page }) => {
    await page.goto('/#/login');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button[type="submit"]');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 15000 });

    await page.goto('/#/requests');
    await page.waitForSelector('text=Central de Solicitações');

    await expect(page.locator('text=Solicitações Pendentes')).toBeVisible();
    await expect(page.locator('text=Minhas Solicitações')).toBeVisible();
  });

  test('User asks to change superior for the first time, parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await page.goto('/#/login');
    await page.fill('input[type="email"]', USER_EMAIL);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('button[type="submit"]');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 15000 });

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
    await page.evaluate(() => localStorage.clear());
    await page.goto('/#/login');
    await page.fill('input[type="email"]', ADMIN_EMAIL);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('button[type="submit"]');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 15000 });

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
    await page.evaluate(() => localStorage.clear());
    await page.goto('/#/login');
    await page.fill('input[type="email"]', USER_EMAIL);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('button[type="submit"]');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 15000 });

    await page.goto('/#/my-profile');
    await expect(page.getByText('Parent Admin Test', { exact: false })).toBeVisible({ timeout: 15000 });
  });

  test('User asks to be an admin, parent admin accepts it', async ({ page }) => {
    test.setTimeout(60000);

    // 1. User logs in
    await page.goto('/#/login');
    await page.fill('input[type="email"]', USER_EMAIL);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('button[type="submit"]');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 15000 });

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
    await page.evaluate(() => localStorage.clear());
    await page.goto('/#/login');
    await page.fill('input[type="email"]', ADMIN_EMAIL);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('button[type="submit"]');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 15000 });

    // Go to Requests page
    await page.goto('/#/requests');
    await page.click('text=Solicitações Pendentes');

    // Find row with user and click Sim
    const pendingRow = page.locator('tr', { hasText: USER_EMAIL });
    await expect(pendingRow).toBeVisible({ timeout: 15000 });
    await pendingRow.locator('button:has-text("Sim")').click();

    await expect(pendingRow).not.toBeVisible({ timeout: 15000 });

    // 4. Verify user is now an Admin (can see Contratos admin link)
    await page.evaluate(() => localStorage.clear());
    await page.goto('/#/login');
    await page.fill('input[type="email"]', USER_EMAIL);
    await page.fill('input[type="password"]', PASSWORD);
    await page.click('button[type="submit"]');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 15000 });

    await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15000 });
  });
});
