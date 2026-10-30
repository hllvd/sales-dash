import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD } from './helpers/auth';

test.describe('Career Plan Progression E2E Tests', () => {
  test.describe.configure({ mode: 'serial' });

  const timestamp = Date.now();
  const randomSuffix1 = Math.random().toString(36).replace(/[^a-z]/g, '').substring(0, 6);
  const randomSuffix2 = Math.random().toString(36).replace(/[^a-z]/g, '').substring(0, 6);
  const randomSuffix3 = Math.random().toString(36).replace(/[^a-z]/g, '').substring(0, 6);

  // User 1: Alef -> Beth (Meta 4M)
  const userEmailAlef = `career_alef_beth_${timestamp}@test.com`;
  const userNameAlef = `Consultor Alef Beth ${randomSuffix1}`;

  // User 2: Beth -> Guimel Success (10M production + 100% strict retention >= 79%)
  const userEmailBethPass = `career_beth_pass_${timestamp}@test.com`;
  const userNameBethPass = `Consultor Beth Pass ${randomSuffix2}`;

  // User 3: Beth -> Guimel Fail (50% strict retention < 79%)
  const userEmailBethFail = `career_beth_fail_${timestamp}@test.com`;
  const userNameBethFail = `Consultor Beth Fail ${randomSuffix3}`;

  const testPassword = 'string';

  test.beforeAll(async ({ request }) => {
    // 1. Authenticate as SuperAdmin via API
    const loginRes = await request.post('/api/users/login', {
      data: { email: SUPERADMIN_EMAIL, password: SUPERADMIN_PASSWORD },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginJson = await loginRes.json();
    const superadminToken = loginJson.data.token;
    const authHeaders = { Authorization: `Bearer ${superadminToken}` };

    const meRes = await request.get('/api/users/me', { headers: authHeaders });
    expect(meRes.ok()).toBeTruthy();
    const meJson = await meRes.json();
    const superadminId = meJson.data.id;

    // 2. Fetch classification levels
    const levelsRes = await request.get('/api/classifications/levels', { headers: authHeaders });
    expect(levelsRes.ok()).toBeTruthy();
    const levelsJson = await levelsRes.json();
    const levels = levelsJson.data || [];
    const alefLevel = levels.find((l: any) => l.name.toLowerCase() === 'alef');
    const bethLevel = levels.find((l: any) => l.name.toLowerCase() === 'beth');
    expect(alefLevel).toBeDefined();
    expect(bethLevel).toBeDefined();

    // Helper: Register user, assign level, and return user ID
    const registerAndAssign = async (name: string, email: string, levelId: number) => {
      const regRes = await request.post('/api/users/register', {
        headers: authHeaders,
        data: {
          name,
          email,
          password: testPassword,
          role: 'user',
          parentUserId: superadminId,
        },
      });
      if (!regRes.ok()) {
        const err = await regRes.text();
        console.error(`Register failed for ${email} (${regRes.status()}): ${err}`);
      }
      expect(regRes.ok()).toBeTruthy();
      await new Promise(r => setTimeout(r, 200));

      const usersRes = await request.get('/api/users?pageSize=1000', { headers: authHeaders });
      expect(usersRes.ok()).toBeTruthy();
      const allUsers = (await usersRes.json()).data?.items || [];
      const user = allUsers.find((u: any) => u.email.toLowerCase() === email.toLowerCase());
      expect(user).toBeDefined();

      const assignRes = await request.post('/api/classifications/assign', {
        headers: authHeaders,
        data: {
          userId: user.id,
          levelId,
          startDate: '2025-01-01T00:00:00Z',
        },
      });
      expect(assignRes.ok()).toBeTruthy();
      return user.id;
    };

    // Helper: Create contract
    const createContract = async (contractNumber: string, totalAmount: number, status: string, contractDate: string, userId: string) => {
      const contractRes = await request.post('/api/contracts', {
        headers: authHeaders,
        data: {
          contractNumber,
          totalAmount,
          status,
          contractStartDate: contractDate,
          userId,
        },
      });
      if (!contractRes.ok()) {
        const err = await contractRes.text();
        console.error(`Create contract failed (${contractRes.status()}): ${err}`);
      }
      expect(contractRes.ok()).toBeTruthy();
    };

    const now = new Date();
    // 2 months ago date
    const date2m = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 2, 10)).toISOString().split('T')[0];
    // 4 months ago date (age > 2 months)
    const date4m = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 4, 10)).toISOString().split('T')[0];

    // Setup User 1: Alef -> Beth
    const user1Id = await registerAndAssign(userNameAlef, userEmailAlef, alefLevel.id);
    await createContract(`CONT-AB-${timestamp}`, 4000000, 'Active', date2m, user1Id);

    // Setup User 2: Beth -> Guimel Pass (10M production + 100% strict retention >= 79%)
    const user2Id = await registerAndAssign(userNameBethPass, userEmailBethPass, bethLevel.id);
    await createContract(`CONT-BP-${timestamp}`, 10000000, 'Active', date4m, user2Id);

    // Setup User 3: Beth -> Guimel Fail (50% strict retention < 79%)
    const user3Id = await registerAndAssign(userNameBethFail, userEmailBethFail, bethLevel.id);
    await createContract(`CONT-BF-1-${timestamp}`, 5000000, 'Active', date4m, user3Id);
    await createContract(`CONT-BF-2-${timestamp}`, 5000000, 'Defaulted', date4m, user3Id);
  });

  test('should display Alef as current level, Beth as target with R$ 4M requirement reached (100%)', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as SuperAdmin in the browser
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);

    // 2. Navigate to the Career Plan page
    await page.goto('/#/career-plan');
    await expect(page.getByRole('heading', { name: 'Plano de Carreira' })).toBeVisible({ timeout: 15000 });

    // 3. Select User 1 from search dropdown
    const userSelectInput = page.getByRole('textbox', { name: 'Selecione um Usuário' });
    await expect(userSelectInput).toBeVisible({ timeout: 15000 });
    await expect(userSelectInput).toBeEnabled({ timeout: 15000 });
    await userSelectInput.click();
    await userSelectInput.fill(userEmailAlef);

    const option = page.locator('.mantine-Select-option', { hasText: userNameAlef });
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
    await expect(reqRow).toContainText('R$ 4.000.000,00');
    await expect(reqRow.getByText('Atingido')).toBeVisible();
    await expect(reqRow).toContainText('100%');
  });

  test('should display Beth as current level, Guimel as target with R$ 10M production and strict retention >= 79% reached (100%)', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as SuperAdmin in the browser
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);

    // 2. Navigate to the Career Plan page
    await page.goto('/#/career-plan');
    await expect(page.getByRole('heading', { name: 'Plano de Carreira' })).toBeVisible({ timeout: 15000 });

    // 3. Select User 2 (Beth -> Guimel Pass)
    const userSelectInput = page.getByRole('textbox', { name: 'Selecione um Usuário' });
    await expect(userSelectInput).toBeVisible({ timeout: 15000 });
    await expect(userSelectInput).toBeEnabled({ timeout: 15000 });
    await userSelectInput.click();
    await userSelectInput.fill(userEmailBethPass);

    const option = page.locator('.mantine-Select-option', { hasText: userNameBethPass });
    await expect(option).toBeVisible({ timeout: 10000 });
    await option.click();

    // 4. Verify Current Level card shows "Beth"
    const currentCard = page.locator('.career-plan-card.current');
    await expect(currentCard).toBeVisible({ timeout: 15000 });
    await expect(currentCard.locator('.career-plan-card-name')).toContainText('Beth');

    // 5. Verify Target Level card shows "Guimel" with Meta R$ 10.000.000,00, Retenção 79%, Prêmio CAMISA
    const targetCard = page.locator('.career-plan-card.target');
    await expect(targetCard).toBeVisible({ timeout: 15000 });
    await expect(targetCard.locator('.career-plan-card-name')).toContainText('Guimel');
    await expect(targetCard).toContainText('Meta: R$ 10.000.000,00');
    await expect(targetCard).toContainText('Retenção: 79%');
    await expect(targetCard).toContainText('Prêmio: CAMISA');

    // 6. Verify Production requirement (R$ 10M) passed
    const prodRow = page.locator('table tbody tr', { hasText: 'Produção 12 meses ≥ Meta' });
    await expect(prodRow).toBeVisible({ timeout: 15000 });
    await expect(prodRow).toContainText('R$ 10.000.000,00');
    await expect(prodRow.getByText('Atingido')).toBeVisible();
    await expect(prodRow).toContainText('100%');

    // 7. Verify Strict Retention requirement for contracts > 2 months passed (100% >= 79%)
    const retRow = page.locator('table tbody tr', { hasText: 'Retenção estrita 15 meses' });
    await expect(retRow).toBeVisible({ timeout: 15000 });
    await expect(retRow).toContainText('100');
    await expect(retRow).toContainText('79');
    await expect(retRow.getByText('Atingido')).toBeVisible();
    await expect(retRow).toContainText('100%');

    // 8. Verify Consolidated Metric Box for Active Production (>= 2 paid installments)
    const activeProdBox = page.locator('.career-plan-metric-box', { hasText: 'Prod. Ativa 12m (≥ 2 pagas)' });
    await expect(activeProdBox).toBeVisible({ timeout: 15000 });
    await expect(activeProdBox).toContainText('R$ 10.000.000,00');
  });

  test('should display strict retention as not passed when strict retention is below 79%', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as SuperAdmin in the browser
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);

    // 2. Navigate to the Career Plan page
    await page.goto('/#/career-plan');
    await expect(page.getByRole('heading', { name: 'Plano de Carreira' })).toBeVisible({ timeout: 15000 });

    // 3. Select User 3 (Beth -> Guimel Fail)
    const userSelectInput = page.getByRole('textbox', { name: 'Selecione um Usuário' });
    await expect(userSelectInput).toBeVisible({ timeout: 15000 });
    await expect(userSelectInput).toBeEnabled({ timeout: 15000 });
    await userSelectInput.click();
    await userSelectInput.fill(userEmailBethFail);

    const option = page.locator('.mantine-Select-option', { hasText: userNameBethFail });
    await expect(option).toBeVisible({ timeout: 10000 });
    await option.click();

    // 4. Verify Current Level card shows "Beth" and Target Level shows "Guimel"
    const currentCard = page.locator('.career-plan-card.current');
    await expect(currentCard).toBeVisible({ timeout: 15000 });
    await expect(currentCard.locator('.career-plan-card-name')).toContainText('Beth');

    const targetCard = page.locator('.career-plan-card.target');
    await expect(targetCard).toBeVisible({ timeout: 15000 });
    await expect(targetCard.locator('.career-plan-card-name')).toContainText('Guimel');

    // 5. Verify Strict Retention requirement shows "Não atingido" (50% < 79%)
    const retRow = page.locator('table tbody tr', { hasText: 'Retenção estrita 15 meses' });
    await expect(retRow).toBeVisible({ timeout: 15000 });
    await expect(retRow).toContainText('50');
    await expect(retRow.getByText('Não atingido')).toBeVisible();
  });
});
