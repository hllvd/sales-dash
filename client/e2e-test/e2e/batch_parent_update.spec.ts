import { test, expect } from '@playwright/test';

test.describe('Batch Parent Update E2E', () => {
  // Serial mode to ensure state is shared and run sequentially in one worker
  test.describe.configure({ mode: 'serial' });

  // Generate unique run ID to avoid email and team collisions
  const RUN_ID = Array.from({ length: 6 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join('');

  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' },
    admin: { email: 'admin@salesapp.com', password: 'admin123' },
    parent: { name: `Batch Parent ${RUN_ID}`, email: `batch.parent.${RUN_ID}@test.com`, role: 'admin' },
    child1: { name: `Batch Child A ${RUN_ID}`, email: `batch.child1.${RUN_ID}@test.com`, role: 'user' },
    child2: { name: `Batch Child B ${RUN_ID}`, email: `batch.child2.${RUN_ID}@test.com`, role: 'user' },
    child3: { name: `Batch Child C ${RUN_ID}`, email: `batch.child3.${RUN_ID}@test.com`, role: 'user' },
  };

  const teamName = `Batch Team E2E ${RUN_ID}`;
  const matriculaVal = `matbatch${RUN_ID}`;
  let teamId: number;
  let parentId: string;
  let child1Id: string;
  let child2Id: string;
  let child3Id: string;
  let superadminToken: string;

  test.beforeAll(async ({ request }) => {
    // 1. Log in as superadmin to get token
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

    // --- CLEANUP Routine at start ---
    console.log("[Setup] Cleaning up any old batch test data...");
    const getTeamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    if (getTeamsRes.ok()) {
      const body = await getTeamsRes.json();
      const teamsList = body.data || [];
      for (const t of teamsList) {
        if (t.name.startsWith("Batch Team E2E ")) {
          await request.delete(`/api/teams/${t.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` }
          });
        }
      }
    }

    const getUsersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    if (getUsersRes.ok()) {
      const body = await getUsersRes.json();
      const usersList = body.data?.items || [];
      for (const u of usersList) {
        if (u.email.includes("batch.parent.") || u.email.includes("batch.child1.") || u.email.includes("batch.child2.") || u.email.includes("batch.child3.")) {
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` }
          });
        }
      }
    }
    // --- END CLEANUP ---

    // Register test users
    const registerUser = async (name: string, email: string, role: string, parentUserId?: string) => {
      const res = await request.post('/api/users/register', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: { name, email, password: 'Password123!', role, parentUserId }
      });
      expect(res.ok()).toBeTruthy();
      const body = await res.json();
      return body.data.id as string;
    };

    const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

    parentId = await registerUser(users.parent.name, users.parent.email, users.parent.role, superadminId);
    await settle();

    child1Id = await registerUser(users.child1.name, users.child1.email, users.child1.role, superadminId);
    await settle();

    child2Id = await registerUser(users.child2.name, users.child2.email, users.child2.role, superadminId);
    await settle();

    child3Id = await registerUser(users.child3.name, users.child3.email, users.child3.role, superadminId);
    await settle();

    // Link child3 to matricula
    const linkRes = await request.post('/api/usermatriculas', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        userEmail: users.child3.email,
        matriculaNumber: matriculaVal,
        isOwner: true,
        isActive: true
      }
    });
    expect(linkRes.ok()).toBeTruthy();
    await settle();

    // Create Team
    const teamRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { name: teamName }
    });
    expect(teamRes.ok()).toBeTruthy();
    const teamData = await teamRes.json();
    teamId = teamData.data.id;

    // Add child users as team members
    const addMembersRes = await request.post(`/api/teams/${teamId}/members`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        members: [
          { userId: child1Id, startDate: '2020-01-01T00:00:00.000Z' },
          { userId: child2Id, startDate: '2020-01-01T00:00:00.000Z' }
        ]
      }
    });
    expect(addMembersRes.ok()).toBeTruthy();
  });

  test('should return access denied for regular admins', async ({ page }) => {
    // Login as regular admin
    await page.goto('/');
    await page.fill('input[type="email"]', users.admin.email);
    await page.fill('input[type="password"]', users.admin.password);
    await page.click('button.login-button');

    // Wait for login redirection
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });

    // Verify that 'Ferramentas Admin' is not visible in the menu for regular admins
    await expect(page.getByText('Ferramentas Admin')).not.toBeVisible();

    // Try to access batch page directly via hash URL
    await page.goto('/#/batch');
    
    // Expect Access Denied card to be visible
    await expect(page.getByText('Acesso Negado')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Apenas o superadmin principal')).toBeVisible();
  });

  test('should allow access and successfully run bulk updates for superadmin', async ({ page }) => {
    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', users.superadmin.email);
    await page.fill('input[type="password"]', users.superadmin.password);
    await page.click('button.login-button');

    // Wait for login redirection
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });

    // Verify that 'Ferramentas Admin' is visible in the menu for superadmin
    await expect(page.getByText('Ferramentas Admin')).toBeVisible();

    // Navigate to batch page
    await page.goto('/#/batch');

    // Expect correct page headers
    await expect(page.getByRole('heading', { name: 'Modificação em Lote', exact: true })).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Acesso Negado')).not.toBeVisible();

    // 2. Select team from dropdown
    const selectCombobox = page.locator('input[placeholder="Selecione uma equipe"]').first();
    await selectCombobox.click();
    await page.getByRole('option', { name: teamName }).click();

    // Fill new superior email
    await page.fill('input[placeholder="exemplo@salesapp.com"]', users.parent.email);

    // Toggle the override switch
    await page.click('text=Sobrescrever superior existente?');

    // Click submit
    await page.click('button:has-text("Aplicar Alterações")');

    // 3. Verify Results stats cards are displayed
    await expect(page.getByRole('heading', { name: 'Resultado da Operação' })).toBeVisible({ timeout: 15000 });
    
    // Matched: 2 (Child1 & Child2), Success: 2, Ignored: 0
    await expect(page.locator('.batch-stat-value.total')).toHaveText('2');
    await expect(page.locator('.batch-stat-value.success')).toHaveText('2');
    await expect(page.locator('.batch-stat-value.skipped')).toHaveText('0');

    // Expect the table to show updated children names
    await expect(page.getByRole('tab', { name: 'Atualizados (2)' })).toBeVisible();
    await expect(page.locator('table.batch-table td >> text=' + users.child1.name).first()).toBeVisible();
    await expect(page.locator('table.batch-table td >> text=' + users.child2.name).first()).toBeVisible();
  });

  test('should allow assigning children of parent to a team for superadmin', async ({ page }) => {
    page.on('console', msg => console.log(`[Browser Console] ${msg.type()}: ${msg.text()}`));
    page.on('pageerror', err => console.log(`[Browser Error] ${err.message}`));
    page.on('response', response => {
      console.log(`[Browser Network] ${response.request().method()} ${response.url()} -> ${response.status()}`);
    });

    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', users.superadmin.email);
    await page.fill('input[type="password"]', users.superadmin.password);
    await page.click('button.login-button');

    // Wait for login redirection
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });

    // Navigate to batch page
    await page.goto('/#/batch');

    // Click "Atribuir a Equipe" tab
    await page.getByRole('tab', { name: 'Atribuir a Equipe' }).click();

    // Fill E-mail do Superior (pai)
    await page.getByLabel('E-mail do Superior (pai)').fill(users.parent.email);

    // Select team from dropdown
    const selectCombobox = page.locator('input[placeholder="Selecione uma equipe"]').last();
    await selectCombobox.click();
    await page.getByRole('option', { name: teamName }).click();

    // 2. Submit with overrideExisting = false (should skip existing members)
    await page.click('button[type="submit"]:has-text("Atribuir a Equipe")');

    // Verify results: matched: 2, added: 0, skipped: 2
    await expect(page.getByRole('heading', { name: 'Resultado da Operação' })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.batch-stat-value.total')).toHaveText('2');
    await expect(page.locator('.batch-stat-value.success')).toHaveText('0');
    await expect(page.locator('.batch-stat-value.skipped')).toHaveText('2');

    // Expect skipped table to show children
    await page.getByRole('tab', { name: 'Ignorados (2)' }).click();
    await expect(page.locator('table.batch-table td >> text=' + users.child1.name).first()).toBeVisible();
    await expect(page.locator('table.batch-table td >> text=' + users.child2.name).first()).toBeVisible();

    // 3. Submit with overrideExisting = true (should update/add successfully)
    await page.click('text=Sobrescrever membros existentes?');
    await page.click('button[type="submit"]:has-text("Atribuir a Equipe")');

    // Verify results: matched: 2, added: 2, skipped: 0
    await expect(page.locator('.batch-stat-value.total')).toHaveText('2');
    await expect(page.locator('.batch-stat-value.success')).toHaveText('2');
    await expect(page.locator('.batch-stat-value.skipped')).toHaveText('0');

    // Expect added table to show children
    await page.getByRole('tab', { name: 'Adicionados (2)' }).click();
    await expect(page.locator('table.batch-table td >> text=' + users.child1.name).first()).toBeVisible();
    await expect(page.locator('table.batch-table td >> text=' + users.child2.name).first()).toBeVisible();
  });

  test('should allow assigning a user to a team by matricula for superadmin', async ({ page }) => {
    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', users.superadmin.email);
    await page.fill('input[type="password"]', users.superadmin.password);
    await page.click('button.login-button');

    // Wait for login redirection
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });

    // Navigate to batch page
    await page.goto('/#/batch');

    // Click "Atribuir a Equipe" tab
    await page.getByRole('tab', { name: 'Atribuir a Equipe' }).click();

    // Fill Matrícula
    await page.getByLabel('Matrícula').fill(matriculaVal);

    // Select team from dropdown
    const selectCombobox = page.locator('input[placeholder="Selecione uma equipe"]').last();
    await selectCombobox.click();
    await page.getByRole('option', { name: teamName }).click();

    // Submit with overrideExisting = false (should succeed for child3 since child3 is not in team)
    await page.click('button[type="submit"]:has-text("Atribuir a Equipe")');

    // Verify results: matched: 1, added: 1, skipped: 0
    await expect(page.getByRole('heading', { name: 'Resultado da Operação' })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.batch-stat-value.total')).toHaveText('1');
    await expect(page.locator('.batch-stat-value.success')).toHaveText('1');
    await expect(page.locator('.batch-stat-value.skipped')).toHaveText('0');

    // Expect added table to show child3
    await page.getByRole('tab', { name: 'Adicionados (1)' }).click();
    await expect(page.locator('table.batch-table td >> text=' + users.child3.name).first()).toBeVisible();
  });
});
