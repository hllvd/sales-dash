import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD } from './helpers/auth';

test.describe('Career Plan Progression E2E Tests', () => {
  test.describe.configure({ mode: 'serial' });

  const timestamp = Date.now();
  const randomSuffix = Math.random().toString(36).replace(/[^a-z]/g, '').substring(0, 6);
  const testUserEmail = `career_alef_beth_${timestamp}@test.com`;
  const testUserName = `Consultor Alef Beth ${randomSuffix}`;
  const testPassword = 'string';
  const contractNumber = `CONT-ALEF-BETH-${timestamp}`;

  let superadminToken = '';
  let testUserId = '';

  test.beforeAll(async ({ request }) => {
    // 1. Authenticate as SuperAdmin via API
    const loginRes = await request.post('/api/users/login', {
      data: { email: SUPERADMIN_EMAIL, password: SUPERADMIN_PASSWORD },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginJson = await loginRes.json();
    superadminToken = loginJson.data.token;
    const authHeaders = { Authorization: `Bearer ${superadminToken}` };

    const meRes = await request.get('/api/users/me', { headers: authHeaders });
    expect(meRes.ok()).toBeTruthy();
    const meJson = await meRes.json();
    const superadminId = meJson.data.id;

    // 2. Fetch classification levels to get the ID for "Alef"
    const levelsRes = await request.get('/api/classifications/levels', { headers: authHeaders });
    expect(levelsRes.ok()).toBeTruthy();
    const levelsJson = await levelsRes.json();
    const levels = levelsJson.data || [];
    const alefLevel = levels.find((l: any) => l.name.toLowerCase() === 'alef');
    expect(alefLevel).toBeDefined();

    // 3. Register the test user
    const registerRes = await request.post('/api/users/register', {
      headers: authHeaders,
      data: {
        name: testUserName,
        email: testUserEmail,
        password: testPassword,
        role: 'user',
        parentUserId: superadminId,
      },
    });
    if (!registerRes.ok()) {
      const err = await registerRes.text();
      console.error(`Register failed (${registerRes.status()}): ${err}`);
    }
    expect(registerRes.ok()).toBeTruthy();

    // Small delay to ensure DB commit
    await new Promise(r => setTimeout(r, 300));

    // 4. Resolve the newly registered user's ID
    const usersRes = await request.get('/api/users?pageSize=1000', { headers: authHeaders });
    expect(usersRes.ok()).toBeTruthy();
    const usersJson = await usersRes.json();
    const allUsers = usersJson.data?.items || [];
    const createdUser = allUsers.find((u: any) => u.email.toLowerCase() === testUserEmail.toLowerCase());
    expect(createdUser).toBeDefined();
    testUserId = createdUser.id;

    // 5. Assign the user to the "Alef" classification level
    const assignRes = await request.post('/api/classifications/assign', {
      headers: authHeaders,
      data: {
        userId: testUserId,
        levelId: alefLevel.id,
        startDate: '2025-01-01T00:00:00Z',
      },
    });
    expect(assignRes.ok()).toBeTruthy();

    // 6. Create an eligible contract with R$ 4.000.000,00 total amount
    // Pick a date 2 months ago in UTC (safely within the 12 closed months window)
    const now = new Date();
    const contractDate = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 2, 10))
      .toISOString()
      .split('T')[0];

    const contractRes = await request.post('/api/contracts', {
      headers: authHeaders,
      data: {
        contractNumber,
        totalAmount: 4000000,
        status: 'Active',
        contractStartDate: contractDate,
        userId: testUserId,
      },
    });
    if (!contractRes.ok()) {
      const err = await contractRes.text();
      console.error(`Create contract failed (${contractRes.status()}): ${err}`);
    }
    expect(contractRes.ok()).toBeTruthy();
  });

  test('should display Alef as current level, Beth as target with R$ 4M requirement reached (100%)', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as SuperAdmin in the browser
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);

    // 2. Navigate to the Career Plan page
    await page.goto('/#/career-plan');
    await expect(page.getByRole('heading', { name: 'Plano de Carreira' })).toBeVisible({ timeout: 15000 });

    // 3. Select the test user from the search dropdown
    const userSelectInput = page.getByRole('textbox', { name: 'Selecione um Usuário' });
    await expect(userSelectInput).toBeVisible({ timeout: 15000 });
    await expect(userSelectInput).toBeEnabled({ timeout: 15000 });
    await userSelectInput.click();
    await userSelectInput.fill(testUserEmail);

    const option = page.locator('.mantine-Select-option', { hasText: testUserName });
    await expect(option).toBeVisible({ timeout: 10000 });
    await option.click();

    // 4. Verify Current Level card shows "Alef"
    const currentCard = page.locator('.career-plan-card.current');
    await expect(currentCard).toBeVisible({ timeout: 15000 });
    await expect(currentCard.locator('.career-plan-card-name')).toContainText('Alef');

    // 5. Verify Target Level card shows "Beth" with Meta R$ 4.000.000,00
    const targetCard = page.locator('.career-plan-card.target');
    await expect(targetCard).toBeVisible({ timeout: 15000 });
    await expect(targetCard.locator('.career-plan-card-name')).toContainText('Beth');
    await expect(targetCard).toContainText('Meta: R$ 4.000.000,00');

    // 6. Verify Requirements Table has the 4M sales production requirement as passed (100%)
    const reqRow = page.locator('table tbody tr', { hasText: 'Produção 12 meses ≥ Meta' });
    await expect(reqRow).toBeVisible({ timeout: 15000 });

    // Current value and Target cells show R$ 4.000.000,00
    await expect(reqRow).toContainText('R$ 4.000.000,00');

    // Status badge shows "Atingido"
    await expect(reqRow.getByText('Atingido')).toBeVisible();

    // Progress shows 100%
    await expect(reqRow).toContainText('100%');
  });
});
