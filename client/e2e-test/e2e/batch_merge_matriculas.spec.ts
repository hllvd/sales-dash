import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Batch Merge Matriculas E2E', () => {

  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 6 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join('');

  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' },
    admin: { email: 'admin@salesapp.com', password: 'admin123' },
    user: { name: `Mat User ${RUN_ID}`, email: `mat.user.${RUN_ID}@test.com`, role: 'user' }
  };

  const mainMat = `MMMAIN${RUN_ID}`;
  const dupMat = `MMDUP${RUN_ID}`;

  let superadminToken: string;
  let superadminId: string;
  let userId: string;

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

    // 3. Register test user
    const resUser = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { name: users.user.name, email: users.user.email, password: 'Password123!', role: users.user.role, parentUserId: superadminId }
    });
    expect(resUser.ok()).toBeTruthy();
    const bodyUser = await resUser.json();
    userId = bodyUser.data.id;
    await settle();

    // 4. Create mainMat and dupMat by linking to user
    const linkMain = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { userEmail: users.user.email, matriculaNumber: mainMat, isOwner: true, isActive: true }
    });
    expect(linkMain.ok()).toBeTruthy();
    await settle();

    const linkDup = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { userEmail: users.user.email, matriculaNumber: dupMat, isOwner: true, isActive: true }
    });
    expect(linkDup.ok()).toBeTruthy();
    await settle();
  });

  test('should deny access to non-superadmin users', async ({ page }) => {
    await loginAs(page, users.admin.email, users.admin.password);
    await page.goto('/#/batch');
    await expect(page.getByText('Acesso Negado')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Apenas o superadmin principal')).toBeVisible();
  });

  test('should show dry-run preview for matricula consolidation', async ({ page }) => {
    // 1. Login as superadmin
    await loginAs(page, users.superadmin.email, users.superadmin.password);

    // 2. Navigate to batch page → Consolidar Matrículas tab
    await page.goto('/#/batch');
    await page.getByRole('tab', { name: 'Consolidar Matrículas' }).click();

    // 3. Fill pair using comma format
    const pairText = `${mainMat},${dupMat}`;
    await page.locator('textarea').fill(pairText);

    // 4. Click dry-run preview
    await page.click('button:has-text("Pré-visualizar Consolidação")');

    // 5. Wait for preview section to appear
    await expect(page.getByText('Pré-visualização da Consolidação')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Modo Simulação (Dry-Run)')).toBeVisible();

    // 6. Assert stat cards
    await expect(page.locator('.batch-stat-value.total')).toHaveText('1');
    await expect(page.locator('.batch-stat-value.success')).toHaveText('1');

    // 7. Assert table rows contain mainMat and dupMat
    await expect(page.getByRole('cell', { name: mainMat })).toBeVisible();
    await expect(page.getByRole('cell', { name: dupMat })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Válido' })).toBeVisible();
  });

  test('should consolidate duplicate matricula into main matricula and show Concluído', async ({ page }) => {
    // 1. Login as superadmin
    await loginAs(page, users.superadmin.email, users.superadmin.password);

    // 2. Navigate to Consolidar Matrículas tab
    await page.goto('/#/batch');
    await page.getByRole('tab', { name: 'Consolidar Matrículas' }).click();

    // 3. Fill pair and enable delete toggle
    const pairText = `${mainMat},${dupMat}`;
    await page.locator('textarea').fill(pairText);

    await page.click('text=Excluir matrícula duplicada (mat2) ao concluir?');
    const deleteSwitch = page.locator('input[type="checkbox"]').last();
    await expect(deleteSwitch).toBeChecked();

    // 4. Dry-run preview first
    await page.click('button:has-text("Pré-visualizar Consolidação")');
    const confirmButton = page.getByRole('button', { name: 'Confirmar e Executar Consolidação' });
    await expect(confirmButton).toBeVisible({ timeout: 10000 });

    // 5. Confirm and execute
    await confirmButton.click();

    // 6. Expect completion badge
    await expect(page.getByText('Resultado da Consolidação')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('Concluído', { exact: true })).toBeVisible();

    // 7. Assert result table shows the pair row with Válido status and Sim in Excluir column
    await expect(page.getByRole('cell', { name: mainMat })).toBeVisible();
    await expect(page.getByRole('cell', { name: dupMat })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Sim' })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Válido' })).toBeVisible();
  });

  test('should correctly parse pairs with spaces after comma (e.g. 6606, Kethi - 6606)', async ({ page }) => {
    await loginAs(page, users.superadmin.email, users.superadmin.password);

    await page.goto('/#/batch');
    await page.getByRole('tab', { name: 'Consolidar Matrículas' }).click();


    // Fill pair formatted with spaces after comma and inside duplicate name
    const pairText = `6606, Kethi - 6606`;
    await page.locator('textarea').fill(pairText);

    await page.click('button:has-text("Pré-visualizar Consolidação")');
    await expect(page.getByText('Pré-visualização da Consolidação')).toBeVisible({ timeout: 10000 });

    // Assert that the parsed tokens in table are "6606" and "Kethi - 6606" (not truncated to "Kethi")
    await expect(page.getByRole('cell', { name: '6606', exact: true })).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Kethi - 6606', exact: true })).toBeVisible();
  });
});

