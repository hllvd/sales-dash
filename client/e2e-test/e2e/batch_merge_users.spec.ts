import { test, expect } from '@playwright/test';

test.describe('Batch Merge Users E2E', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 6 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join('');

  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' },
    main: { name: `Main User ${RUN_ID}`, email: `main.merge.${RUN_ID}@test.com`, role: 'user' },
    duplicate: { name: `Duplicate User ${RUN_ID}`, email: `dup.merge.${RUN_ID}@test.com`, role: 'user' },
  };

  let superadminToken: string;

  test.beforeAll(async ({ request }) => {
    // 1. Log in as superadmin
    const loginRes = await request.post('/api/users/login', {
      data: { email: users.superadmin.email, password: users.superadmin.password },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    superadminToken = loginData.data.token;

    // Get superadmin's User ID
    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    expect(meRes.ok()).toBeTruthy();
    const meData = await meRes.json();
    const superadminId = meData.data.id;

    // Register main and duplicate users
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


    const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

    await registerUser(users.main.name, users.main.email, users.main.role);
    await settle();

    await registerUser(users.duplicate.name, users.duplicate.email, users.duplicate.role);
    await settle();
  });

  test('should consolidate duplicate user into main user via batch page', async ({ page }) => {
    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', users.superadmin.email);
    await page.fill('input[type="password"]', users.superadmin.password);
    await page.click('button.login-button');

    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });

    // Navigate to batch page
    await page.goto('/#/batch');

    // Click "Consolidar Usuários" tab
    await page.getByRole('tab', { name: 'Consolidar Usuários' }).click();

    // Fill pair in textarea
    const pairText = `${users.main.email},${users.duplicate.email}`;
    await page.locator('textarea').fill(pairText);

    // Click "Pré-visualizar Consolidação"
    await page.click('button:has-text("Pré-visualizar Consolidação")');

    // Expect preview header and table
    await expect(page.getByText('Pré-visualização da Consolidação')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Modo Simulação (Dry-Run)')).toBeVisible();

    // Confirm and execute consolidation
    await page.click('button:has-text("Confirmar e Executar Consolidação")');

    // Expect completion badge
    await expect(page.getByText('Resultado da Consolidação')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Concluído', { exact: true })).toBeVisible();
  });
});
