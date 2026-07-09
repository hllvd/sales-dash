import { test, expect } from '@playwright/test';

test.describe('[TEAR 3] Admin Assign Contract and Matricula Guards', () => {
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('');

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };

  const adminA = {
    name: `Admin A ${RUN_ID}`,
    email: `admin.a.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'admin',
  };

  const userB = {
    name: `User B ZeroMat ${RUN_ID}`,
    email: `user.b.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'user',
  };

  const userC = {
    name: `User C OneMat ${RUN_ID}`,
    email: `user.c.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'user',
  };

  const userD = {
    name: `User D TwoMat ${RUN_ID}`,
    email: `user.d.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'user',
  };

  const userE = {
    name: `User E Outer ${RUN_ID}`,
    email: `user.e.${RUN_ID}@test.com`,
    password: 'Password123!',
    role: 'user',
  };

  let superadminToken: string;
  let superadminId: string;
  let adminAId: string;
  let userBId: string;
  let userCId: string;
  let userDId: string;
  let userEId: string;

  const MAT_C = `MATC${RUN_ID}`;
  const MAT_D1 = `MATD1${RUN_ID}`;
  const MAT_D2 = `MATD2${RUN_ID}`;
  const MAT_E = `MATE${RUN_ID}`;

  const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

  test.beforeAll(async ({ request }) => {
    // ── 1. Login as superadmin ──────────────────────────────────────────────
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

    // ── 2. Register users in hierarchy ─────────────────────────────────────
    const registerUser = async (name: string, email: string, role: string, parentId?: string) => {
      const res = await request.post('/api/users/register', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: { name, email, password: 'Password123!', role, parentUserId: parentId },
      });
      if (!res.ok()) {
        console.error(`FAILED to register user ${email}: status=${res.status()}, error=${await res.text()}`);
      }
      expect(res.ok()).toBeTruthy();
      return (await res.json()).data.id as string;
    };

    adminAId = await registerUser(adminA.name, adminA.email, adminA.role, superadminId);
    await settle();
    userBId = await registerUser(userB.name, userB.email, userB.role, adminAId);
    await settle();
    userCId = await registerUser(userC.name, userC.email, userC.role, adminAId);
    await settle();
    userDId = await registerUser(userD.name, userD.email, userD.role, adminAId);
    await settle();
    userEId = await registerUser(userE.name, userE.email, userE.role, superadminId);
    await settle();

    // ── 3. Assign Matriculas ────────────────────────────────────────────────
    const assignMatricula = async (userId: string, matriculaNumber: string, isOwner = true) => {
      const res = await request.post('/api/usermatriculas', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: {
          userId,
          matriculaNumber,
          isOwner,
          isActive: true,
          startDate: new Date().toISOString()
        }
      });
      expect(res.ok()).toBeTruthy();
    };

    // User C has 1 owner matricula
    await assignMatricula(userCId, MAT_C, true);
    await settle();

    // User D has 2 owner matriculas
    await assignMatricula(userDId, MAT_D1, true);
    await settle();
    await assignMatricula(userDId, MAT_D2, true);
    await settle();

    // User E has 1 owner matricula (outer user)
    await assignMatricula(userEId, MAT_E, true);
    await settle();
  });

  // ── Helper: Login + Go to Contracts ────────────────────────────────────────
  async function loginAndGoToContracts(page: any, email: string, password: string) {
    await page.goto('/');
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.click('button.login-button');
    await page.getByTestId('nav-contracts').click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15000 });
  }

  test('should restrict user list to descendants for Admin A and enforce matricula validations', async ({ page }) => {
    test.setTimeout(60000);
    await loginAndGoToContracts(page, adminA.email, adminA.password);

    // Click on create contract
    await page.getByRole('button', { name: 'Criar', exact: true }).click();
    await expect(page.getByRole('dialog')).toBeVisible();

    // Click on Vendedor dropdown to open it
    const getSelectByLabel = (labelText: string) =>
      page.getByRole('dialog').locator('label').filter({ hasText: labelText }).locator('..').locator('input').first();

    const vendedorSelect = getSelectByLabel('Vendedor');
    await vendedorSelect.click();

    // Verify descendants and self are visible
    await expect(page.getByRole('option', { name: adminA.name, exact: false })).toBeVisible();
    await expect(page.getByRole('option', { name: userB.name, exact: false })).toBeVisible();
    await expect(page.getByRole('option', { name: userC.name, exact: false })).toBeVisible();
    await expect(page.getByRole('option', { name: userD.name, exact: false })).toBeVisible();

    // Verify outer user and superadmin are NOT visible
    await expect(page.getByRole('option', { name: userE.name, exact: false })).not.toBeVisible();
    await expect(page.getByRole('option', { name: 'Super Admin', exact: false })).not.toBeVisible();

    // ── Scenario 1: Select User B (0 matriculas) ─────────────────────────────
    console.log('>>> Selecting User B (0 matriculas)');
    await page.getByRole('option', { name: userB.name, exact: false }).click();
    await expect(page.getByRole('option', { name: userB.name, exact: false })).not.toBeVisible();

    // Verify red warning is shown
    const warningText = page.locator('text=Este usuário não possui matrícula');
    await expect(warningText).toBeVisible();

    // Verify submit button is disabled
    const submitBtn = page.getByRole('button', { name: 'Criar Contrato', exact: true });
    await expect(submitBtn).toBeDisabled();

    // ── Scenario 2: Select User C (1 owner matricula) ────────────────────────
    console.log('>>> Selecting User C (1 owner matricula)');
    await vendedorSelect.click();
    await page.getByRole('option', { name: userC.name, exact: false }).click();
    await expect(page.getByRole('option', { name: userC.name, exact: false })).not.toBeVisible();

    // Verify warning is hidden
    await expect(warningText).not.toBeVisible();

    // Verify matricula select auto-filled with MAT_C
    const matriculaSelect = getSelectByLabel('Número da Matrícula');
    await expect(matriculaSelect).toHaveValue(`${MAT_C} (Dona)`);

    // Verify submit button is enabled (subject to other fields being filled)
    await expect(submitBtn).toBeEnabled();

    // ── Scenario 3: Select User D (2 owner matriculas) ───────────────────────
    await expect(vendedorSelect).toHaveValue(new RegExp(userC.name));
    await vendedorSelect.click();
    await page.getByRole('option', { name: userD.name, exact: false }).click();
    await expect(page.getByRole('option', { name: userD.name, exact: false })).not.toBeVisible();
    await expect(vendedorSelect).toHaveValue(new RegExp(userD.name));

    // Verify warning is hidden
    await expect(warningText).not.toBeVisible();

    // Verify matricula select is empty by default
    await expect(matriculaSelect).toHaveValue('');

    // Fill in required contract details
    await page.getByRole('dialog').locator('form > div').filter({ has: page.locator('label', { hasText: 'Número do Contrato' }) }).locator('input').fill(`CN-${RUN_ID}`);
    const todayStr = new Date().toISOString().split('T')[0];
    await page.getByRole('dialog').locator('input[type="date"]').fill(todayStr);
    
    // Total Amount input is a Mantine NumberInput inside the "Valor Total" field container
    const amountInput = page.getByRole('dialog').locator('form > div').filter({ has: page.locator('label', { hasText: 'Valor Total' }) }).locator('input');
    await amountInput.fill('1500');

    // Try to submit without selecting matricula
    await submitBtn.click();

    // Toast or notification error should be visible
    const toastError = page.locator('text=Por favor, selecione uma matrícula').first();
    await expect(toastError).toBeVisible({ timeout: 10000 });

    // Open matricula dropdown and select MAT_D1
    await matriculaSelect.click();
    await page.getByRole('option', { name: `${MAT_D1} (Dona)`, exact: false }).click();

    // Submit form
    await submitBtn.click();

    // Dialog should close on success
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 15000 });

    // Verify contract created successfully
    await page.fill('#filterContractNumber', `CN-${RUN_ID}`);
    await page.waitForTimeout(4000); // Wait for debounce
    await expect(page.locator('table tbody tr').filter({ hasText: `CN-${RUN_ID}` }).first()).toBeVisible();
    console.log('>>> E2E Contract Assignment and Matricula validation verified successfully!');
  });
});
