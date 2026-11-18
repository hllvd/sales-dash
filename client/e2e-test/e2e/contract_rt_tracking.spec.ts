import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD } from './helpers/auth';

test.describe('Contract RT Tracking & Consórcio Installments E2E Tests', () => {
  test.describe.configure({ mode: 'serial' });

  const timestamp = Date.now();
  const randomSuffix = Math.random().toString(36).replace(/[^a-z]/g, '').substring(0, 6);
  const userEmail = `rt_tracking_user_${timestamp}@test.com`;
  const userName = `Consultor RT Tracking ${randomSuffix}`;
  const userPassword = 'string';

  const contractNumMaria = `1100945941-${timestamp}`.substring(0, 20);
  const contractNumZago = `1101011234-${timestamp}`.substring(0, 20);
  const contractNumRonaldo = `1100937573-${timestamp}`.substring(0, 20);

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
    const levels = (await levelsRes.json()).data || [];
    const alefLevel = levels.find((l: any) => l.name.toLowerCase() === 'alef');
    expect(alefLevel).toBeDefined();

    // 3. Register regular consultant user
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
    await new Promise((r) => setTimeout(r, 200));

    const usersRes = await request.get('/api/users?pageSize=1000', { headers: authHeaders });
    expect(usersRes.ok()).toBeTruthy();
    const allUsers = (await usersRes.json()).data?.items || [];
    const createdUser = allUsers.find((u: any) => u.email.toLowerCase() === userEmail.toLowerCase());
    expect(createdUser).toBeDefined();
    const userId = createdUser.id;

    // 4. Assign Alef level
    const assignRes = await request.post('/api/classifications/assign', {
      headers: authHeaders,
      data: {
        userId,
        levelId: alefLevel.id,
        startDate: '2025-01-01T00:00:00Z',
      },
    });
    expect(assignRes.ok()).toBeTruthy();

    // 5. Seed test contracts with realistic sales dates:
    // Today is after day 15 (e.g. 18th of current month).
    // Previous month sales should have 2 installments due: RT1 and RT2.
    const now = new Date();
    const currentYear = now.getUTCFullYear();
    const currentMonth = now.getUTCMonth(); // 0-based

    // Contract 1: Maria - Early previous month (e.g. 8th), Active
    const prevMonthEarly = new Date(Date.UTC(currentYear, currentMonth - 1, 8)).toISOString().split('T')[0];
    // Contract 2: Zago - Late previous month (e.g. 28th), Active
    const prevMonthLate = new Date(Date.UTC(currentYear, currentMonth - 1, 28)).toISOString().split('T')[0];
    // Contract 3: Ronaldo - Early previous month (e.g. 5th), Late1
    const prevMonthRonaldo = new Date(Date.UTC(currentYear, currentMonth - 1, 5)).toISOString().split('T')[0];

    const createContract = async (contractNumber: string, customerName: string, amount: number, status: string, date: string) => {
      const res = await request.post('/api/contracts', {
        headers: authHeaders,
        data: {
          contractNumber,
          customerName,
          totalAmount: amount,
          status,
          contractStartDate: date,
          userId,
        },
      });
      if (!res.ok()) {
        const err = await res.text();
        console.error(`Failed to create contract ${contractNumber} (${res.status()}): ${err}`);
      }
      expect(res.ok()).toBeTruthy();
    };

    await createContract(contractNumMaria, 'MARIA DA CONCEICAO MENDES', 300000, 'Active', prevMonthEarly);
    await createContract(contractNumZago, 'ZAGO INVESTIMENTOS LTDA', 152261.58, 'Active', prevMonthLate);
    await createContract(contractNumRonaldo, 'RONALDO DE MOURA', 100000, 'Late1', prevMonthRonaldo);
  });

  test('RT tracking table should correctly show 2 paid installments for Active and 1 paid + 1 unpaid for Late1', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Log in as the consultant
    await loginAs(page, userEmail, userPassword);

    // 2. Navigate to "Meu Plano de Carreira"
    const myCareerLink = page.locator('a[href="#/my-career-plan"]');
    await expect(myCareerLink).toBeVisible({ timeout: 15000 });
    await myCareerLink.click();

    // 3. Verify heading and RT table load
    await expect(page.getByRole('heading', { name: 'Meu Plano de Carreira' })).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('Acompanhamento de Contratos (RT1 a RT6 e RT Atual)')).toBeVisible({ timeout: 15000 });

    // 4. Validate Maria da Conceição row (Active, early previous month)
    const rowMaria = page.locator('tr').filter({ hasText: contractNumMaria });
    await expect(rowMaria).toBeVisible({ timeout: 15000 });

    // RT1 (index 5) should be Paid (green)
    const mariaRt1 = rowMaria.locator('td').nth(5).locator('span');
    await expect(mariaRt1).toHaveCSS('background-color', 'rgb(34, 197, 94)'); // #22c55e

    // RT2 (index 6) should be Paid (green)
    const mariaRt2 = rowMaria.locator('td').nth(6).locator('span');
    await expect(mariaRt2).toHaveCSS('background-color', 'rgb(34, 197, 94)'); // #22c55e

    // RT3 (index 7) should be Blank (white)
    const mariaRt3 = rowMaria.locator('td').nth(7).locator('span');
    await expect(mariaRt3).toHaveCSS('background-color', 'rgb(255, 255, 255)');

    // RT Atual (index 11) should be Up to date (green)
    const mariaCurrent = rowMaria.locator('td').nth(11).locator('span');
    await expect(mariaCurrent).toHaveCSS('background-color', 'rgb(34, 197, 94)');

    // 5. Validate Zago Investimentos row (Active, late previous month - entire month counts)
    const rowZago = page.locator('tr').filter({ hasText: contractNumZago });
    await expect(rowZago).toBeVisible({ timeout: 15000 });

    const zagoRt1 = rowZago.locator('td').nth(5).locator('span');
    await expect(zagoRt1).toHaveCSS('background-color', 'rgb(34, 197, 94)');

    const zagoRt2 = rowZago.locator('td').nth(6).locator('span');
    await expect(zagoRt2).toHaveCSS('background-color', 'rgb(34, 197, 94)');

    const zagoRt3 = rowZago.locator('td').nth(7).locator('span');
    await expect(zagoRt3).toHaveCSS('background-color', 'rgb(255, 255, 255)');

    // 6. Validate Ronaldo de Moura row (Late1, previous month)
    const rowRonaldo = page.locator('tr').filter({ hasText: contractNumRonaldo });
    await expect(rowRonaldo).toBeVisible({ timeout: 15000 });

    // RT1 should be Paid (green, first installment paid at closing)
    const ronaldoRt1 = rowRonaldo.locator('td').nth(5).locator('span');
    await expect(ronaldoRt1).toHaveCSS('background-color', 'rgb(34, 197, 94)');

    // RT2 should be Unpaid (yellow/amber, late 1 in 2nd month)
    const ronaldoRt2 = rowRonaldo.locator('td').nth(6).locator('span');
    await expect(ronaldoRt2).toHaveCSS('background-color', 'rgb(234, 179, 8)'); // #eab308

    // RT3 should be Blank (white)
    const ronaldoRt3 = rowRonaldo.locator('td').nth(7).locator('span');
    await expect(ronaldoRt3).toHaveCSS('background-color', 'rgb(255, 255, 255)');

    // RT Atual should be Overdue (red)
    const ronaldoCurrent = rowRonaldo.locator('td').nth(11).locator('span');
    await expect(ronaldoCurrent).toHaveCSS('background-color', 'rgb(239, 68, 68)'); // #ef4444

    // 7. Test Simulator: toggle Ronaldo RT Atual and verify dynamic recalculation
    const simButton = page.getByRole('button', { name: /Simulador/i });
    await expect(simButton).toBeVisible();
    await simButton.click();

    // Verify instructional alert appears
    await expect(page.getByText('Modo Simulador Ativo')).toBeVisible();

    // Toggle Ronaldo from red to green
    const ronaldoToggle = rowRonaldo.locator('button[aria-label*="Simular status do contrato"]');
    await expect(ronaldoToggle).toBeVisible();
    await ronaldoToggle.click();

    // Verify row receives modified class and RT Atual becomes green
    await expect(rowRonaldo).toHaveClass(/rt-row-modified/);
    const ronaldoCurrentSimulated = rowRonaldo.locator('td').nth(11).locator('span');
    await expect(ronaldoCurrentSimulated).toHaveCSS('background-color', 'rgb(34, 197, 94)');

    // Verify "Limpar (1)" button appears in the table header
    const clearButton = page.getByRole('button', { name: /Limpar \(\d+\)/i });
    await expect(clearButton).toBeVisible();

    // Verify reset simulation via Limpar button
    await clearButton.click();
    await expect(clearButton).not.toBeVisible();
    await expect(rowRonaldo).not.toHaveClass(/rt-row-modified/);

    // RT Atual returns to red (overdue)
    await expect(ronaldoCurrent).toHaveCSS('background-color', 'rgb(239, 68, 68)');
  });
});
