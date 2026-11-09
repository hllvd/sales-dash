import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD, ADMIN_EMAIL, ADMIN_PASSWORD } from './helpers/auth';

test.describe('My Career Plan (Meu Plano de Carreira) E2E Tests', () => {
  test.describe.configure({ mode: 'serial' });

  const timestamp = Date.now();
  const randomSuffix = Math.random().toString(36).replace(/[^a-z]/g, '').substring(0, 6);
  const userEmail = `my_career_user_${timestamp}@test.com`;
  const userName = `Consultor Meu Plano ${randomSuffix}`;
  const userPassword = 'string';
  let userId = '';

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
    expect(alefLevel).toBeDefined();

    // 3. Create regular user
    const createRes = await request.post('/api/users/register', {
      headers: authHeaders,
      data: {
        name: userName,
        email: userEmail,
        password: userPassword,
        role: 'user',
        parentUserId: superadminId,
      },
    });
    expect(createRes.ok()).toBeTruthy();
    await new Promise(r => setTimeout(r, 200));

    const usersRes = await request.get('/api/users?pageSize=1000', { headers: authHeaders });
    expect(usersRes.ok()).toBeTruthy();
    const allUsers = (await usersRes.json()).data?.items || [];
    const createdUser = allUsers.find((u: any) => u.email.toLowerCase() === userEmail.toLowerCase());
    expect(createdUser).toBeDefined();
    userId = createdUser.id;

    // 4. Assign Alef classification to user
    const assignRes = await request.post('/api/classifications/assign', {
      headers: authHeaders,
      data: {
        userId,
        levelId: alefLevel.id,
        startDate: '2026-01-01T00:00:00Z',
      },
    });
    expect(assignRes.ok()).toBeTruthy();
  });

  test('admin user should see "Plano de Carreira" group and NOT "Meu Plano de Carreira"', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as Admin
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    // 2. Verify "Meu Plano de Carreira" link is NOT present in sidebar
    const myCareerLink = page.locator('a[href="#/my-career-plan"]');
    await expect(myCareerLink).not.toBeVisible();

    // 3. Verify expandable group "Plano de Carreira" IS visible
    const careerGroup = page.getByText('Plano de Carreira', { exact: true });
    await expect(careerGroup).toBeVisible({ timeout: 15000 });

    // 4. Open "Plano de Carreira" and click "Performance do Consultor"
    await careerGroup.click();
    const perfLink = page.locator('a[href="#/career-plan/consultants"]');
    await expect(perfLink).toBeVisible();
    await perfLink.click();

    // 5. Verify page opens and renders Performance do Consultor
    await expect(page).toHaveURL(/#\/career-plan\/consultants/);
    await expect(page.getByRole('heading', { name: 'Performance do Consultor' })).toBeVisible({ timeout: 15000 });
  });

  test('superadmin should see "Plano de Carreira" group and NOT "Meu Plano de Carreira"', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as SuperAdmin
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);

    // 2. Verify "Meu Plano de Carreira" is NOT in the menu
    const myCareerLink = page.locator('a[href="#/my-career-plan"]');
    await expect(myCareerLink).not.toBeVisible();

    // 3. Verify "Plano de Carreira" parent group is visible
    const superGroup = page.getByText('Plano de Carreira', { exact: true });
    await expect(superGroup).toBeVisible();
  });

  test('regular user sees own career plan and RT contract tracking loads automatically', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as Regular User
    await loginAs(page, userEmail, userPassword);

    // 2. Verify "Meu Plano de Carreira" link is visible in sidebar and "Plano de Carreira" group is NOT
    const myCareerLink = page.locator('a[href="#/my-career-plan"]');
    await expect(myCareerLink).toBeVisible({ timeout: 15000 });

    const careerGroup = page.getByText('Plano de Carreira', { exact: true });
    await expect(careerGroup).not.toBeVisible();

    // 3. Click to navigate
    await myCareerLink.click();
    await expect(page.getByRole('heading', { name: 'Meu Plano de Carreira' })).toBeVisible({ timeout: 15000 });

    // 4. Verify Current Level shows "Alef" and Target Level shows "Beth"
    const currentCard = page.locator('.career-plan-card.current');
    await expect(currentCard).toBeVisible({ timeout: 15000 });
    await expect(currentCard.locator('.career-plan-card-name')).toContainText('Alef');

    const targetCard = page.locator('.career-plan-card.target');
    await expect(targetCard).toBeVisible({ timeout: 15000 });
    await expect(targetCard.locator('.career-plan-card-name')).toContainText('Beth');

    // 5. Verify Requirements section is visible
    await expect(page.getByText('Requisitos para Promoção')).toBeVisible({ timeout: 15000 });

    // 5b. Verify legend and highlighted metrics used by promotion requirements
    await expect(page.getByTestId('requirement-legend')).toContainText('Métrica usada em um requisito de promoção', { timeout: 15000 });
    await expect(page.locator('[data-metric-key="strictRetention15m"]')).toHaveAttribute('data-requirement', 'true');
    await expect(page.locator('[data-metric-key="commercializedContracts12mMinAge2"]')).toHaveAttribute('data-requirement', 'true');
    await expect(page.locator('[data-metric-key="activeProduction12mMinPaid2"]')).toHaveAttribute('data-requirement', 'false');
    await expect(page.locator('[data-metric-key="retention6m"]')).toHaveAttribute('data-requirement', 'false');

    // 6. Verify RT contract tracking loaded automatically
    await expect(page.getByText('Acompanhamento de Contratos (RT1 a RT6 e RT Atual)')).toBeVisible({ timeout: 15000 });
  });
});
