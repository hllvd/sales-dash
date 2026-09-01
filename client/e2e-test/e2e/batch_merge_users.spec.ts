import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Batch Merge Users E2E', () => {

  test.describe.configure({ mode: 'serial' });

  const RUN_LETTERS = Array.from({ length: 6 }, () => String.fromCharCode(65 + Math.floor(Math.random() * 26))).join('');
  const RUN_ID = RUN_LETTERS.toLowerCase() + Date.now().toString().slice(-4);

  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' },
    admin: { email: 'admin@salesapp.com', password: 'admin123' },
    main: { name: `Main User ${RUN_LETTERS}`, email: `main.merge.${RUN_ID}@test.com`, role: 'user' },
    duplicate: { name: `Duplicate User ${RUN_LETTERS}`, email: `dup.merge.${RUN_ID}@test.com`, role: 'user' },
  };

  let superadminToken: string;
  let superadminId: string;
  let mainUserId: string;
  let duplicateUserId: string;

  test.beforeAll(async ({ request }) => {
    const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

    // 1. Log in as superadmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: users.superadmin.email, password: users.superadmin.password },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    superadminToken = loginData.data.token;

    // 2. Get superadmin's User ID
    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    expect(meRes.ok()).toBeTruthy();
    const meData = await meRes.json();
    superadminId = meData.data.id;

    // 3. Cleanup: delete any leftover test users from previous runs (by email pattern)
    const getUsersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    if (getUsersRes.ok()) {
      const body = await getUsersRes.json();
      const usersList = body.data?.items || [];
      for (const u of usersList) {
        if (u.email.includes('main.merge.') || u.email.includes('dup.merge.')) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` }
          });
        }
      }
    }

    // 4. Register main and duplicate users
    const registerUser = async (name: string, email: string, role: string) => {
      const res = await request.post('/api/users/register', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: { name, email, password: 'Password123!', role, parentUserId: superadminId }
      });
      if (!res.ok()) {
        console.error(`Register user failed for ${email}: ${res.status()} ${await res.text()}`);
      }
      expect(res.ok()).toBeTruthy();
      const body = await res.json();
      return body.data.id as string;
    };

    mainUserId = await registerUser(users.main.name, users.main.email, users.main.role);
    await settle();

    duplicateUserId = await registerUser(users.duplicate.name, users.duplicate.email, users.duplicate.role);
    await settle();
  });

  test('should deny access to non-superadmin users', async ({ page }) => {
    await loginAs(page, users.admin.email, users.admin.password);
    await page.goto('/#/batch');
    await expect(page.getByText('Acesso Negado')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Apenas o superadmin principal')).toBeVisible();
  });

  test('should show dry-run preview with correct stats before confirming', async ({ page }) => {
    // 1. Login as superadmin
    await loginAs(page, users.superadmin.email, users.superadmin.password);

    // 2. Navigate to batch page → Consolidar Usuários tab
    await page.goto('/#/batch');
    await page.getByRole('tab', { name: 'Consolidar Usuários' }).click();

    // 3. Fill pair using comma format
    const pairText = `${users.main.email},${users.duplicate.email}`;
    await page.locator('textarea').fill(pairText);

    // 4. Verify deactivate toggle default is OFF (unchecked)
    const deactivateSwitch = page.locator('input[type="checkbox"]').last();
    await expect(deactivateSwitch).not.toBeChecked();

    // 5. Click dry-run preview
    await page.click('button:has-text("Pré-visualizar Consolidação")');

    // 6. Wait for preview section to appear
    await expect(page.getByText('Pré-visualização da Consolidação')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Modo Simulação (Dry-Run)')).toBeVisible();

    // 7. Assert stat cards: 1 total pair, 1 valid, 0 errors
    await expect(page.locator('.batch-stat-value.total')).toHaveText('1');
    await expect(page.locator('.batch-stat-value.success')).toHaveText('1');
    await expect(page.locator('.batch-stat-value.skipped')).toHaveText('0');

    // 8. Assert table rows contain the correct emails and valid status
    await expect(page.getByRole('cell', { name: users.main.email })).toBeVisible();
    await expect(page.getByRole('cell', { name: users.duplicate.email })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Válido' })).toBeVisible();

    // 9. "Não" in Desativar column (since toggle is OFF)
    await expect(page.getByRole('cell', { name: 'Não' })).toBeVisible();
  });

  test('should consolidate duplicate user into main user and show Concluído', async ({ page }) => {
    // 1. Login as superadmin
    await loginAs(page, users.superadmin.email, users.superadmin.password);

    // 2. Navigate to Consolidar Usuários tab
    await page.goto('/#/batch');
    await page.getByRole('tab', { name: 'Consolidar Usuários' }).click();


    // 3. Fill pair and enable deactivate toggle
    const pairText = `${users.main.email},${users.duplicate.email}`;
    await page.locator('textarea').fill(pairText);

    // Enable deactivate duplicate toggle
    await page.click('text=Desativar usuário duplicado (email2) ao concluir?');
    const deactivateSwitch = page.locator('input[type="checkbox"]').last();
    await expect(deactivateSwitch).toBeChecked();

    // 4. Dry-run preview first
    await page.click('button:has-text("Pré-visualizar Consolidação")');
    const confirmButton = page.getByRole('button', { name: 'Confirmar e Executar Consolidação' });
    await expect(confirmButton).toBeVisible({ timeout: 10000 });

    // 5. Confirm and execute
    await confirmButton.click();

    // 6. Expect completion badge
    await expect(page.getByText('Resultado da Consolidação')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('Concluído', { exact: true })).toBeVisible();

    // 7. Assert result table shows the pair row with Válido status
    await expect(page.getByRole('cell', { name: users.main.email })).toBeVisible();
    await expect(page.getByRole('cell', { name: users.duplicate.email })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Válido' })).toBeVisible();

    // 8. "Sim" in Desativar column (since toggle was ON)
    await expect(page.getByRole('cell', { name: 'Sim' })).toBeVisible();
  });

  test('should verify duplicate user is deactivated after merge via API', async ({ request }) => {
    // After the previous test ran the actual merge with deactivation ON,
    // confirm via API that the duplicate user's IsActive is now false.
    const res = await request.get(`/api/users/${duplicateUserId}`, {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    expect(res.ok()).toBeTruthy();
    const body = await res.json();
    expect(body.data.isActive).toBe(false);
  });
});
