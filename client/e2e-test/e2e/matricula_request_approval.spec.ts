import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Matrícula Request and Approval Flow (TEAR 3)', () => {

  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Date.now().toString().slice(-4);
  const USER_EMAIL = `req.approve.${RUN_ID}@test.com`;
  const REQ_MATR = `55${RUN_ID}`;

  let testUserId = '';
  let superadminId = '';
  let superadminToken = '';

  // Cleanup helper to run at start
  async function cleanup(request: any) {
    const loginRes = await request.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    if (!loginRes.ok()) return;
    const token = (await loginRes.json()).data.token;

    const res = await request.get('/api/users?page=1&pageSize=1000', {
      headers: { 'Authorization': `Bearer ${token}` }
    });
    const data = await res.json();
    if (data.success && data.data) {
      for (const u of data.data.items) {
        if (u.email.includes('req.approve.')) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
          });
        }
      }
    }

    const matRes = await request.get('/api/usermatriculas', {
      headers: { 'Authorization': `Bearer ${token}` }
    });
    const matData = await matRes.json();
    if (matData.success && matData.data) {
      for (const m of matData.data) {
        if (m.matriculaNumber.startsWith('REQ-APP-')) {
          await request.delete(`/api/usermatriculas/${m.id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
          });
        }
      }
    }
  }

  test.beforeAll(async ({ request }) => {
    await cleanup(request);

    // 1. Login as SuperAdmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: 'superadmin@salesapp.com', password: 'string' },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginBody = await loginRes.json();
    superadminToken = loginBody.data.token;

    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` },
    });
    expect(meRes.ok()).toBeTruthy();
    superadminId = (await meRes.json()).data.id;

    // 2. Register test user
    const res = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: 'User Req Approve',
        email: USER_EMAIL,
        password: 'Password123!',
        role: 'user',
        parentUserId: superadminId
      },
    });
    expect(res.ok()).toBeTruthy();
    testUserId = (await res.json()).data.id;
  });

  test('should allow user to request a matrícula, and admin to activate it', async ({ page }) => {
    test.setTimeout(90000);
    // ── STEP 1: User requests matrícula from their profile ──
    await loginAs(page, USER_EMAIL, 'Password123!');

    // Go to My Profile page
    await page.click('a[href="#/my-profile"]', { timeout: 15000 });
    await expect(page.getByRole('heading', { name: 'Meu Perfil' })).toBeVisible({ timeout: 15000 });

    // Request new matrícula
    await page.click('button:has-text("Solicitar Matrícula")');
    await expect(page.getByRole('dialog').getByText('Solicitar Nova Matrícula')).toBeVisible();

    await page.fill('input[placeholder="Digite o número da matrícula"]', REQ_MATR);
    await page.getByRole('dialog').getByRole('button', { name: /^Solicitar$/ }).click();

    // Verify modal closes
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // ── STEP 2: Admin activates the requested matrícula ──
    await loginAs(page, 'superadmin@salesapp.com', 'string');

    // Go to Matriculas page
    await page.click('a[href="#/matriculas"]', { timeout: 15000 });
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Matrículas' })).toBeVisible({ timeout: 15000 });

    // Search for the requested matrícula
    await page.fill('input[placeholder="Buscar por número de matrícula (ou separadas por vírgula) ou usuário..."]', REQ_MATR);
    await page.waitForTimeout(1500); // Wait for search debounce

    const adminRow = page.locator('tr', { hasText: REQ_MATR });
    await expect(adminRow).toBeVisible({ timeout: 10000 });
    await expect(adminRow).toContainText('Pendente');

    // Click to edit
    await adminRow.locator('button[title="Editar"], .tabler-icon-edit, button:has(.tabler-icon-edit)').first().click();
    await expect(page.getByRole('dialog')).toBeVisible();


    // Activate the matrícula
    const activeCheckbox = page.getByRole('dialog').locator('input[type="checkbox"]').first();
    if (!(await activeCheckbox.isChecked())) {
      await activeCheckbox.check();
    }
    const statusSelect = page.getByRole('dialog').locator('input[readonly]:not([disabled]), .mantine-Select-input').first();
    await statusSelect.click();
    await page.getByRole('option', { name: 'Ativo' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Salvar Alterações' }).click();



    // Verify modal closes
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // Verify it now shows as Ativa on the admin list
    await expect(adminRow).toContainText('Ativa');

    // ── STEP 3: User verifies matrícula is now Ativa ──
    await loginAs(page, USER_EMAIL, 'Password123!');

    // Go to My Profile page
    await page.click('a[href="#/my-profile"]', { timeout: 15000 });
    await expect(page.getByRole('heading', { name: 'Meu Perfil' })).toBeVisible({ timeout: 15000 });

    // Verify matrícula shows as Ativa
    const userMatRow = page.locator('tr', { hasText: REQ_MATR });
    await expect(userMatRow).toBeVisible({ timeout: 10000 });
    await expect(userMatRow).toContainText('Ativa');
  });
});
