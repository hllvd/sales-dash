import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Admin Equipe Scoped Permissions (TEAR 3)', () => {

  // Use serial mode to maintain DB state cleanly across sequential verification steps
  test.describe.configure({ mode: 'serial' });

  const RUN_LETTERS = Array.from({ length: 8 }, () =>
    String.fromCharCode(65 + Math.floor(Math.random() * 26))
  ).join('');
  const RUN_ID = RUN_LETTERS.toLowerCase() + Date.now().toString().slice(-4);
  
  const ADMIN_A_EMAIL = `admin.a.scope.${RUN_ID}@test.com`;
  const ADMIN_B_EMAIL = `admin.b.scope.${RUN_ID}@test.com`;
  const CHILD_A_EMAIL = `child.a.scope.${RUN_ID}@test.com`;
  const CHILD_B_EMAIL = `child.b.scope.${RUN_ID}@test.com`;
  
  let adminAUserId = '';
  let adminBUserId = '';
  let childAUserId = '';
  let childBUserId = '';
  
  let teamAId = 0;
  let teamBId = 0;

  // Cleanup helper to run at start and end
  async function cleanup(page: any) {
    await loginAs(page);
    await expect(page.locator('a[href="#/users"]').first()).toBeVisible({ timeout: 10000 });


    await page.evaluate(async ({ emails, runLetters }) => {
      const token = localStorage.getItem("token");
      
      // Delete teams if they match names
      const teamsRes = await fetch('/api/teams', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (teamsRes.ok) {
        const teamsData = await teamsRes.json();
        const teamsList = Array.isArray(teamsData) ? teamsData : (teamsData.items || (teamsData.data && teamsData.data.items) || teamsData.data || []);
        if (Array.isArray(teamsList)) {
          for (const t of teamsList) {
            if (t.name && t.name.includes(runLetters)) {
              await fetch(`/api/teams/${t.id}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
              });
            }
          }
        }
      }

      // Get all users
      const res = await fetch('/api/users?page=1&pageSize=1000', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      if (res.ok) {
        const data = await res.json();
        const usersList = Array.isArray(data) ? data : (data.items || (data.data && data.data.items) || data.data || []);
        if (Array.isArray(usersList)) {
          for (const u of usersList) {
            if (u.email && emails.some(email => u.email.includes(email))) {
              await fetch(`/api/users/${u.id}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
              });
            }
          }
        }
      }
    }, { emails: [ADMIN_A_EMAIL, ADMIN_B_EMAIL, CHILD_A_EMAIL, CHILD_B_EMAIL], runLetters: RUN_LETTERS });

    
    await page.evaluate(() => localStorage.clear());
  }

  async function loginAsAdminA(page: any) {
    await loginAs(page, ADMIN_A_EMAIL, 'password123');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 10000 });
  }

  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();
    await cleanup(page);
    await page.close();
  });

  test.afterAll(async ({ browser }) => {
    const page = await browser.newPage();
    await cleanup(page);
    await page.close();
  });

  test('1. Setup users, teams and RBAC matrix', async ({ page }) => {
    test.setTimeout(60000);
    // Login as SuperAdmin
    await loginAs(page);

    // Go to Users page
    await page.goto('/#/users');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();
    await expect(page.locator('a[href="#/users"]').first()).toBeVisible({ timeout: 10000 });

    // 1.1 Create Admin A
    await page.click('button:has-text("Criar")');

    await page.fill('input[placeholder="Nome completo"]', `Admin A ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', ADMIN_A_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    await page.getByRole('dialog').locator('.mantine-Select-input').first().click();
    await page.click('div[role="option"]:has-text("Administrador")');
    await page.fill('input[placeholder="Digite para buscar..."]', 'superadmin@salesapp.com');
    const adminAParentOpt = page.locator('div[role="option"]', { hasText: 'superadmin@salesapp.com' });
    await expect(adminAParentOpt).toBeVisible({ timeout: 5000 });
    await adminAParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Get Admin A user ID
    adminAUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, ADMIN_A_EMAIL);
    expect(adminAUserId).not.toBe('');

    // 1.2 Create Admin B
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `Admin B ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', ADMIN_B_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    await page.getByRole('dialog').locator('.mantine-Select-input').first().click();
    await page.click('div[role="option"]:has-text("Administrador")');
    await page.fill('input[placeholder="Digite para buscar..."]', 'superadmin@salesapp.com');
    const adminBParentOpt = page.locator('div[role="option"]', { hasText: 'superadmin@salesapp.com' });
    await expect(adminBParentOpt).toBeVisible({ timeout: 5000 });
    await adminBParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Get Admin B user ID
    adminBUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, ADMIN_B_EMAIL);
    expect(adminBUserId).not.toBe('');

    // 1.3 Create Child A (under Admin A)
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `Child A ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', CHILD_A_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    await page.fill('input[placeholder="Digite para buscar..."]', ADMIN_A_EMAIL);
    const childAParentOpt = page.locator('div[role="option"]', { hasText: ADMIN_A_EMAIL });
    await expect(childAParentOpt).toBeVisible({ timeout: 5000 });
    await childAParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    childAUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, CHILD_A_EMAIL);
    expect(childAUserId).not.toBe('');

    // 1.4 Create Child B (under Admin B)
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `Child B ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', CHILD_B_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    await page.fill('input[placeholder="Digite para buscar..."]', ADMIN_B_EMAIL);
    const childBParentOpt = page.locator('div[role="option"]', { hasText: ADMIN_B_EMAIL });
    await expect(childBParentOpt).toBeVisible({ timeout: 5000 });
    await childBParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    childBUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, CHILD_B_EMAIL);
    expect(childBUserId).not.toBe('');

    // 1.5 Create Team A (Admin A as Owner)
    await page.click('a[href="#/teams"]');
    await page.click('button:has-text("Nova Equipe")');
    await page.fill('input[placeholder="Ex: Equipe Fênix"]', `admin a ${RUN_ID}`);
    await page.click('button:has-text("Criar e Gerenciar")');
    await page.waitForTimeout(500);
    await page.click('button.mantine-Modal-close');

    teamAId = await page.evaluate(async ({ teamName, adminId }) => {
      const token = localStorage.getItem("token");
      const res = await fetch('/api/teams', { headers: { 'Authorization': `Bearer ${token}` } });
      const data = await res.json();
      const team = data.data.find((t: any) => t.name === teamName);
      if (!team) return 0;
      
      // Add Admin as member
      await fetch(`/api/teams/${team.id}/members`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ members: [{ userId: adminId, startDate: new Date().toISOString() }] })
      });

      // Set owner
      await fetch(`/api/teams/${team.id}/owner`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(adminId)
      });
      
      return team.id;
    }, { teamName: `admin a ${RUN_ID}`, adminId: adminAUserId });
    expect(teamAId).toBeGreaterThan(0);

    // 1.6 Create Team B (Admin B as Owner)
    await page.click('button:has-text("Nova Equipe")');
    await page.fill('input[placeholder="Ex: Equipe Fênix"]', `admin b ${RUN_ID}`);
    await page.click('button:has-text("Criar e Gerenciar")');
    await page.waitForTimeout(500);
    await page.click('button.mantine-Modal-close');

    teamBId = await page.evaluate(async ({ teamName, adminId, childBId }) => {
      const token = localStorage.getItem("token");
      const res = await fetch('/api/teams', { headers: { 'Authorization': `Bearer ${token}` } });
      const data = await res.json();
      const team = data.data.find((t: any) => t.name === teamName);
      if (!team) return 0;
      
      // Add Admin as member
      await fetch(`/api/teams/${team.id}/members`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ members: [{ userId: adminId, startDate: new Date().toISOString() }] })
      });

      // Add Child B as member to make them ineligible (has parent AND has team)
      await fetch(`/api/teams/${team.id}/members`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify({ members: [{ userId: childBId, startDate: new Date().toISOString() }] })
      });

      // Set owner
      await fetch(`/api/teams/${team.id}/owner`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${token}`, 'Content-Type': 'application/json' },
        body: JSON.stringify(adminId)
      });
      
      return team.id;
    }, { teamName: `admin b ${RUN_ID}`, adminId: adminBUserId, childBId: childBUserId });
    expect(teamBId).toBeGreaterThan(0);

    // 1.7 Toggle teams:manage permission ON for Admin role in RBAC
    await page.click('a[href="#/access-control"]');
    await expect(page.getByRole('heading', { name: 'Controle de Acesso (RBAC)' })).toBeVisible();
    await page.fill('input[placeholder="Pesquisar endpoints..."]', 'teams:manage');
    await page.waitForTimeout(500);

    // Check the matrix to make sure it's enabled for Admin role
    const checkbox = page.locator(`table tbody tr`).filter({ hasText: 'teams:manage' }).locator('input[type="checkbox"]').nth(0);
    const checked = await checkbox.isChecked();
    if (!checked) {
      await checkbox.click();
      await page.waitForTimeout(1000);
    }
  });

  test('2. Admin A sees Equipes menu and can navigate to it', async ({ page }) => {
    await loginAsAdminA(page);
    
    // Check navigation menu
    const equipesLink = page.locator('a[href="#/teams"]');
    await expect(equipesLink).toBeVisible();
    
    // Click on Equipes
    await equipesLink.click();
    await expect(page.locator('.teams-container')).toBeVisible();
  });

  test('3. Scoping: Admin A sees Team A but not Team B', async ({ page }) => {
    await loginAsAdminA(page);
    await page.goto('/#/teams');
    await expect(page.locator('.teams-container')).toBeVisible();

    const container = page.locator('.teams-container');
    await expect(container).toContainText(new RegExp(`ADMIN A ${RUN_ID}`, 'i'));
    
    const containerText = await container.innerText();
    expect(containerText.toUpperCase()).not.toContain(`ADMIN B ${RUN_ID}`.toUpperCase());
  });

  test('4. UI buttons visibility on TeamsPage for Admin A', async ({ page }) => {
    await loginAsAdminA(page);
    await page.goto('/#/teams');
    await expect(page.locator('.teams-container')).toBeVisible();

    // 4.1 Nova Equipe button should NOT be visible
    const createBtn = page.locator('button:has-text("Nova Equipe")');
    await expect(createBtn).not.toBeVisible();

    // 4.2 Delete button on row should NOT be visible
    const teamRow = page.locator('table tbody tr').filter({ hasText: new RegExp(`ADMIN A ${RUN_ID}`, 'i') });
    await expect(teamRow.locator('button[title="Excluir"]')).not.toBeVisible();
  });

  test('5. API Guards check: Admin A cannot Create or Delete teams directly via API', async ({ page }) => {
    await loginAsAdminA(page);
    
    const apiResult = await page.evaluate(async ({ teamAId, runId }) => {
      const token = localStorage.getItem("token");

      // Try creating team -> should return 403
      const createRes = await fetch('/api/teams', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          name: `Equipe Hacked ${runId}`,
          members: []
        })
      });

      // Try deleting team -> should return 403
      const deleteRes = await fetch(`/api/teams/${teamAId}`, {
        method: 'DELETE',
        headers: {
          'Authorization': `Bearer ${token}`
        }
      });

      return {
        createStatus: createRes.status,
        deleteStatus: deleteRes.status
      };
    }, { teamAId, runId: RUN_ID });

    expect(apiResult.createStatus).toBe(403);
    expect(apiResult.deleteStatus).toBe(403);
  });

  test('6. Member scoping check on backend API for Admin A', async ({ page }) => {
    await loginAsAdminA(page);

    const memberResult = await page.evaluate(async ({ teamAId, childAId, childBId }) => {
      const token = localStorage.getItem("token");

      // Adding Child A (descendant of Admin A) -> should return 200
      const childARes = await fetch(`/api/teams/${teamAId}/members`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          members: [{ userId: childAId, startDate: new Date().toISOString() }]
        })
      });

      // Adding Child B (descendant of Admin B) -> should return 400
      const childBRes = await fetch(`/api/teams/${teamAId}/members`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          members: [{ userId: childBId, startDate: new Date().toISOString() }]
        })
      });

      return {
        childAStatus: childARes.status,
        childBStatus: childBRes.status
      };
    }, { teamAId, childAId: childAUserId, childBId: childBUserId });

    expect(memberResult.childAStatus).toBe(200);
    expect(memberResult.childBStatus).toBe(400);
  });
});
