import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

/**
 * Dedicated E2E Test Suite: Inactive User Contracts Visibility & Hierarchy Scope
 *
 * Verifies that:
 * 1. An Admin can see contracts of their direct/indirect subordinates even after they are deactivated.
 * 2. An Admin CANNOT see contracts of deactivated users belonging to other hierarchies/networks.
 * 3. Superadmin can see contracts of deactivated users from any hierarchy.
 * 4. ContractForm seller dropdown respects the user's `IncludeInactiveUsersInFilter` preference.
 */
test.describe('Inactive User Contracts Visibility & Scope', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_LETTERS = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('');
  const RUN_ID = RUN_LETTERS + Date.now().toString().slice(-4);

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };

  // Hierarchy A: Admin A -> Subordinate B (deactivated)
  const adminA = {
    name: `Admin Alpha ${RUN_LETTERS}`,
    email: `adm.alpha.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const subB = {
    name: `Consultant Beta ${RUN_LETTERS}`,
    email: `con.beta.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'user',
  };

  // Hierarchy X: Admin X -> Subordinate Y (deactivated)
  const adminX = {
    name: `Admin Xray ${RUN_LETTERS}`,
    email: `adm.xray.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const subY = {
    name: `Consultant Yankee ${RUN_LETTERS}`,
    email: `con.yankee.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'user',
  };

  const contractBNum = `CTR-INAC-B-${RUN_ID}`;
  const contractYNum = `CTR-INAC-Y-${RUN_ID}`;

  let superadminToken: string;
  let superadminId: string;
  let adminAId: string;
  let subBId: string;
  let adminXId: string;
  let subYId: string;

  const settle = (ms = 300) => new Promise((r) => setTimeout(r, ms));

  test.beforeAll(async ({ request }) => {
    // 1. Login as Superadmin
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

    // 2. Register Admin A & Subordinate B
    const regAdminA = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: adminA.name,
        email: adminA.email,
        password: adminA.password,
        role: adminA.role,
        parentUserId: superadminId,
      },
    });
    expect(regAdminA.ok()).toBeTruthy();
    adminAId = (await regAdminA.json()).data.id;

    await settle();

    const regSubB = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: subB.name,
        email: subB.email,
        password: subB.password,
        role: subB.role,
        parentUserId: adminAId,
      },
    });
    expect(regSubB.ok()).toBeTruthy();
    subBId = (await regSubB.json()).data.id;

    await settle();

    // 3. Register Admin X & Subordinate Y
    const regAdminX = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: adminX.name,
        email: adminX.email,
        password: adminX.password,
        role: adminX.role,
        parentUserId: superadminId,
      },
    });
    expect(regAdminX.ok()).toBeTruthy();
    adminXId = (await regAdminX.json()).data.id;

    await settle();

    const regSubY = await request.post('/api/users/register', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: subY.name,
        email: subY.email,
        password: subY.password,
        role: subY.role,
        parentUserId: adminXId,
      },
    });
    expect(regSubY.ok()).toBeTruthy();
    subYId = (await regSubY.json()).data.id;

    await settle();

    // 4. Create Contract for Subordinate B
    const cBRes = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractBNum,
        userId: subBId,
        contractStartDate: '2026-01-15T00:00:00Z',
        totalAmount: 2500,
        customerName: 'Cliente Beta Teste',
        status: 'Active',
      },
    });
    expect(cBRes.ok()).toBeTruthy();

    // 5. Create Contract for Subordinate Y
    const cYRes = await request.post('/api/contracts', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        contractNumber: contractYNum,
        userId: subYId,
        contractStartDate: '2026-01-20T00:00:00Z',
        totalAmount: 4200,
        customerName: 'Cliente Yankee Teste',
        status: 'Active',
      },
    });
    expect(cYRes.ok()).toBeTruthy();

    await settle();

    // 6. Deactivate Subordinate B and Subordinate Y
    const deactB = await request.put(`/api/users/${subBId}`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { isActive: false },
    });
    expect(deactB.ok()).toBeTruthy();

    const deactY = await request.put(`/api/users/${subYId}`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { isActive: false },
    });
    expect(deactY.ok()).toBeTruthy();

    await settle();
  });

  // ── Test 1: Admin A sees deactivated subordinate B's contract in /#/contracts
  test('Admin A sees deactivated subordinate B contract in /#/contracts', async ({ page }) => {
    test.setTimeout(45_000);

    await loginAs(page, adminA.email, adminA.password);
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15_000 });

    // Search specifically for contractBNum
    await page.fill('input[placeholder="Buscar por número..."]', contractBNum);
    await page.waitForTimeout(1000);

    const row = page.locator('table tbody tr').filter({ hasText: contractBNum });
    await expect(row).toBeVisible({ timeout: 15_000 });
    await expect(row.getByText(subB.name)).toBeVisible();
  });

  // ── Test 2: Admin A cannot see deactivated user Y from another hierarchy
  test('Admin A cannot see deactivated user Y contract from outside hierarchy', async ({ page }) => {
    test.setTimeout(45_000);

    await loginAs(page, adminA.email, adminA.password);
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15_000 });

    // Search for contractYNum (belonging to Yankee under Admin X)
    await page.fill('input[placeholder="Buscar por número..."]', contractYNum);
    await page.waitForTimeout(1000);

    const row = page.locator('table tbody tr').filter({ hasText: contractYNum });
    await expect(row).not.toBeVisible();
  });

  // ── Test 3: Superadmin sees contracts from all deactivated users
  test('Superadmin sees contracts from all deactivated users', async ({ page }) => {
    test.setTimeout(45_000);

    await loginAs(page, SA.email, SA.password);
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15_000 });

    // Check Contract B
    await page.fill('input[placeholder="Buscar por número..."]', contractBNum);
    await page.waitForTimeout(1000);
    const rowB = page.locator('table tbody tr').filter({ hasText: contractBNum });
    await expect(rowB).toBeVisible({ timeout: 15_000 });
    await expect(rowB.getByText(subB.name)).toBeVisible();

    // Check Contract Y
    await page.fill('input[placeholder="Buscar por número..."]', contractYNum);
    await page.waitForTimeout(1000);
    const rowY = page.locator('table tbody tr').filter({ hasText: contractYNum });
    await expect(rowY).toBeVisible({ timeout: 15_000 });
    await expect(rowY.getByText(subY.name)).toBeVisible();
  });

  // ── Test 4: ContractForm seller dropdown respects IncludeInactiveUsersInFilter preference
  test('ContractForm seller dropdown respects user preference for inactive users', async ({ page, request }) => {
    test.setTimeout(60_000);

    // 1. Ensure Admin A's preference is OFF (default)
    const loginAdminARes = await request.post('/api/users/login', {
      data: { email: adminA.email, password: adminA.password },
    });
    const adminAToken = (await loginAdminARes.json()).data.token;

    await request.put('/api/users/me/preferences', {
      headers: { Authorization: `Bearer ${adminAToken}` },
      data: { includeInactiveUsersInFilter: false },
    });

    // 2. Login as Admin A and open Create Contract modal
    await loginAs(page, adminA.email, adminA.password);
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15_000 });

    await page.click('button:has-text("Criar")');
    const contractModal = page.getByRole('dialog');
    await expect(contractModal).toBeVisible();

    const vendedorSelect = contractModal.getByPlaceholder('Selecione o vendedor');
    await vendedorSelect.click();
    await vendedorSelect.fill('');
    await page.waitForTimeout(500);

    // Subordinate B is inactive, so with preference OFF, B must NOT be an option
    const optionBOff = page.getByRole('option').filter({ hasText: subB.name });
    await expect(optionBOff).toHaveCount(0);

    // Close modal
    await contractModal.getByRole('button', { name: 'Cancelar' }).click();
    await expect(contractModal).not.toBeVisible();

    // 3. Switch preference ON via Settings modal in UI to test full integration
    await page.click('button:has-text("Configurações")');
    const settingsModal = page.locator('.mantine-Modal-content');
    await expect(settingsModal).toBeVisible({ timeout: 10_000 });

    const prefUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/users/me/preferences') && r.request().method() === 'PUT' && r.status() === 200
    );
    await settingsModal.getByText("Incluir usuários desativados no filtro Usuário").click();
    await prefUpdatePromise;

    await expect(page.getByText('Preferência de filtro atualizada')).toBeVisible({ timeout: 5000 });
    await settingsModal.getByRole('button', { name: 'Concluir' }).click();
    await expect(settingsModal).not.toBeVisible();

    // 4. Open Create Contract modal again
    await page.click('button:has-text("Criar")');
    const contractModal2 = page.getByRole('dialog');
    await expect(contractModal2).toBeVisible();

    const vendedorSelect2 = contractModal2.getByPlaceholder('Selecione o vendedor');
    await vendedorSelect2.click();
    await page.waitForTimeout(500);

    // Subordinate B is now visible because preference is ON
    const optionBOn = page.getByRole('option').filter({ hasText: subB.name });
    await expect(optionBOn.first()).toBeVisible({ timeout: 10_000 });

    await contractModal2.getByRole('button', { name: 'Cancelar' }).click();
  });
});
