import { test, expect } from '@playwright/test';

test.describe('Admin Scoped Permissions (TEAR 3)', () => {
  // Use serial mode to maintain DB state cleanly across sequential verification steps
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Date.now().toString().slice(-4);
  const letters = 'abcdefghij';
  const RUN_LETTERS = RUN_ID.split('').map(digit => letters[parseInt(digit, 10)]).join('').toUpperCase();
  
  const ADMIN_EMAIL = `admin.scope.${RUN_ID}@test.com`;
  const CHILD_EMAIL = `child.scope.${RUN_ID}@test.com`;
  const OTHER_EMAIL = `other.scope.${RUN_ID}@test.com`;
  
  // Orphan criteria test users
  const NOPARENT_EMAIL = `noparent.scope.${RUN_ID}@test.com`;
  const NOTEAM_EMAIL = `noteam.scope.${RUN_ID}@test.com`;
  const INELIGIBLE_EMAIL = `ineligible.scope.${RUN_ID}@test.com`; // Has parent and has team

  let adminUserId = '';
  let childUserId = '';
  let otherUserId = '';
  let noparentUserId = '';
  let noteamUserId = '';
  let ineligibleUserId = '';
  let teamId = 0;

  // Cleanup helper to run at start and end
  async function cleanupUsers(page: any) {
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // Wait for the login and page navigation to complete
    await expect(page.locator('a[href="#/users"]')).toBeVisible({ timeout: 10000 });

    await page.evaluate(async (emails) => {
      const token = localStorage.getItem("token");
      
      // Get all users
      const res = await fetch('/api/users?page=1&pageSize=1000', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      if (!data.success || !data.data) return;

      for (const u of data.data.items) {
        if (emails.some(email => u.email.includes(email))) {
          // Delete/deactivate user
          await fetch(`/api/users/${u.id}`, {
            method: 'DELETE',
            headers: { 'Authorization': `Bearer ${token}` }
          });
        }
      }

      // Cleanup teams owned by Admin EE
      const teamsRes = await fetch('/api/teams', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const teamsData = await teamsRes.json();
      if (teamsData.success && teamsData.data) {
        for (const t of teamsData.data) {
          if (t.name.includes('Equipe EE') || (t.owner && t.owner.userEmail.includes('admin.scope')) || t.name.includes('ee ')) {
            await fetch(`/api/teams/${t.id}`, {
              method: 'DELETE',
              headers: { 'Authorization': `Bearer ${token}` }
            });
          }
        }
      }
    }, [ADMIN_EMAIL, CHILD_EMAIL, OTHER_EMAIL, NOPARENT_EMAIL, NOTEAM_EMAIL, INELIGIBLE_EMAIL]);
    
    await page.evaluate(() => localStorage.clear());
  }

  async function loginAsAdmin(page: any) {
    await page.goto('/');
    await page.fill('input[type="email"]', ADMIN_EMAIL);
    await page.fill('input[type="password"]', 'password123');
    await page.click('button.login-button');
    await expect(page.locator('a[href="#/users"]')).toBeVisible({ timeout: 10000 });
  }

  test.beforeAll(async ({ browser }) => {
    const page = await browser.newPage();
    await cleanupUsers(page);
    await page.close();
  });

  test.afterAll(async ({ browser }) => {
    const page = await browser.newPage();
    await cleanupUsers(page);
    await page.close();
  });

  test('1. Setup: SuperAdmin creates Admin, Child, and other test users', async ({ page }) => {
    test.setTimeout(60000);
    // Login as SuperAdmin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // Go to Users page
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // 1.1 Create Admin User
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `Admin EE ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', ADMIN_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    await page.click('input[readonly].mantine-Select-input');
    await page.click('div[role="option"]:has-text("Administrador")');
    // Set parent to SuperAdmin so they are not a root user
    await page.fill('input[placeholder="Digite para buscar..."]', 'superadmin@salesapp.com');
    const adminParentOpt = page.locator('div[role="option"]', { hasText: 'superadmin@salesapp.com' });
    await expect(adminParentOpt).toBeVisible({ timeout: 5000 });
    await adminParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Resolve Admin User ID
    const usersMap1 = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, ADMIN_EMAIL);
    adminUserId = usersMap1;
    expect(adminUserId).not.toBe('');
    console.log(`>>> Admin User ID: ${adminUserId}`);

    // 1.2 Create Child User (under Admin)
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `Child EE ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', CHILD_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    // Set parent to Admin EE
    await page.fill('input[placeholder="Digite para buscar..."]', ADMIN_EMAIL);
    const childParentOpt = page.locator('div[role="option"]', { hasText: ADMIN_EMAIL });
    await expect(childParentOpt).toBeVisible({ timeout: 5000 });
    await childParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Resolve Child User ID
    childUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, CHILD_EMAIL);
    expect(childUserId).not.toBe('');

    // 1.3 Create Other User (independent, no child)
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `Other EE ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', OTHER_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    // Set parent to SuperAdmin so they are not a root user
    await page.fill('input[placeholder="Digite para buscar..."]', 'superadmin@salesapp.com');
    const otherParentOpt = page.locator('div[role="option"]', { hasText: 'superadmin@salesapp.com' });
    await expect(otherParentOpt).toBeVisible({ timeout: 5000 });
    await otherParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Resolve Other User ID
    otherUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, OTHER_EMAIL);
    expect(otherUserId).not.toBe('');

    // 1.4 Create Noparent User (no parent, eligible for Admin's team)
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `NoParent EE ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', NOPARENT_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    // Set parent to SuperAdmin so they are not a root user
    await page.fill('input[placeholder="Digite para buscar..."]', 'superadmin@salesapp.com');
    const noparentParentOpt = page.locator('div[role="option"]', { hasText: 'superadmin@salesapp.com' });
    await expect(noparentParentOpt).toBeVisible({ timeout: 5000 });
    await noparentParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Resolve Noparent User ID
    noparentUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, NOPARENT_EMAIL);
    expect(noparentUserId).not.toBe('');

    // 1.5 Create Noteam User (has parent but no team, eligible for Admin's team)
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `NoTeam EE ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', NOTEAM_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    // Set parent to SuperAdmin so they have a parent
    await page.fill('input[placeholder="Digite para buscar..."]', 'superadmin@salesapp.com');
    const noteamParentOpt = page.locator('div[role="option"]', { hasText: 'superadmin@salesapp.com' });
    await expect(noteamParentOpt).toBeVisible({ timeout: 5000 });
    await noteamParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Resolve Noteam User ID
    noteamUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, NOTEAM_EMAIL);
    expect(noteamUserId).not.toBe('');

    // 1.6 Create Ineligible User (has parent AND has team, ineligible for Admin's team)
    await page.click('button:has-text("Criar")');
    await page.fill('input[placeholder="Nome completo"]', `Ineligible EE ${RUN_LETTERS}`);
    await page.fill('input[placeholder="email@exemplo.com"]', INELIGIBLE_EMAIL);
    await page.fill('input[placeholder="Senha"]', 'password123');
    // Set parent to SuperAdmin
    await page.fill('input[placeholder="Digite para buscar..."]', 'superadmin@salesapp.com');
    const ineligibleParentOpt = page.locator('div[role="option"]', { hasText: 'superadmin@salesapp.com' });
    await expect(ineligibleParentOpt).toBeVisible({ timeout: 5000 });
    await ineligibleParentOpt.click();
    await page.click('button:has-text("Criar Usuário")');
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(500);

    // Resolve Ineligible User ID
    ineligibleUserId = await page.evaluate(async (email) => {
      const token = localStorage.getItem("token");
      const res = await fetch(`/api/users?page=1&pageSize=100&search=${email}`, {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      return data.data.items[0]?.id || '';
    }, INELIGIBLE_EMAIL);
    expect(ineligibleUserId).not.toBe('');
 
    // 1.7 Create a Team for the Ineligible user so they are in a team
    await page.click('a[href="#/teams"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Equipes (Equipes)' })).toBeVisible();
    await page.click('button:has-text("Nova Equipe")');
    await page.fill('input[placeholder="Ex: Equipe Fênix"]', `outra ${RUN_ID}`);
    await page.click('button:has-text("Criar e Gerenciar")');
    await page.waitForTimeout(500);
    // Close the members modal
    await page.click('button.mantine-Modal-close');
    await page.waitForTimeout(200);
 
    // Get the other team ID and assign the Ineligible user to it
    const otherTeamId = await page.evaluate(async (teamName) => {
      const token = localStorage.getItem("token");
      const res = await fetch('/api/teams', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      const team = data.data.find((t: any) => t.name === teamName);
      return team?.id || 0;
    }, `outra ${RUN_ID}`);
    expect(otherTeamId).toBeGreaterThan(0);

    await page.evaluate(async ({ teamId, userId }) => {
      const token = localStorage.getItem("token");
      await fetch(`/api/teams/${teamId}/members`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          members: [{ userId, startDate: new Date().toISOString() }]
        })
      });
    }, { teamId: otherTeamId, userId: ineligibleUserId });

    // 1.8 Create Admin's own Team (owned by ADMIN_EMAIL)
    await page.click('button:has-text("Nova Equipe")');
    await page.fill('input[placeholder="Ex: Equipe Fênix"]', `ee ${RUN_ID}`);
    await page.click('button:has-text("Criar e Gerenciar")');
    await page.waitForTimeout(500);
    // Close the members modal
    await page.click('button.mantine-Modal-close');
    await page.waitForTimeout(200);
 
    teamId = await page.evaluate(async ({ teamName, adminId }) => {
      const token = localStorage.getItem("token");
      // Find the team
      const res = await fetch('/api/teams', {
        headers: { 'Authorization': `Bearer ${token}` }
      });
      const data = await res.json();
      const team = data.data.find((t: any) => t.name === teamName);
      if (!team) return 0;
 
      // Add Admin EE as member first
      await fetch(`/api/teams/${team.id}/members`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          members: [{ userId: adminId, startDate: new Date().toISOString() }]
        })
      });
 
      // Make Admin EE the owner
      await fetch(`/api/teams/${team.id}/owner`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify(adminId)
      });
 
      return team.id;
    }, { teamName: `ee ${RUN_ID}`, adminId: adminUserId });
 
    expect(teamId).toBeGreaterThan(0);
    console.log(`>>> Admin's Team ID: ${teamId}`);

    // Wait for DB commits to settle
    await page.waitForTimeout(1000);
    await page.evaluate(() => localStorage.clear());
  });

  test('2. Admin sees all users, but can edit/delete only descendants', async ({ page }) => {
    test.setTimeout(60000);
    await loginAsAdmin(page);

    // Go to Users page
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // 2.1 Verify other (non-child) user shows "Apenas leitura"
    await page.fill('input[placeholder="Buscar por nome ou email..."]', OTHER_EMAIL);
    await page.waitForTimeout(800);
    const otherRow = page.locator('table tbody tr').filter({ hasText: OTHER_EMAIL });
    await expect(otherRow).toBeVisible();
    await expect(otherRow.locator('text=Apenas leitura')).toBeVisible();
    await expect(otherRow.locator('button[title="Editar"]')).not.toBeVisible();
    await expect(otherRow.locator('button[title="Excluir"]')).not.toBeVisible();

    // 2.2 Verify child user shows Edit/Delete buttons
    await page.fill('input[placeholder="Buscar por nome ou email..."]', CHILD_EMAIL);
    await page.waitForTimeout(800);
    const childRow = page.locator('table tbody tr').filter({ hasText: CHILD_EMAIL });
    await expect(childRow).toBeVisible();
    await expect(childRow.locator('button[title="Editar"]')).toBeVisible();
    await expect(childRow.locator('button[title="Excluir"]')).toBeVisible();
    await expect(childRow.locator('text=Apenas leitura')).not.toBeVisible();
  });

  test('3. Backend API enforces Admin descendant constraints', async ({ page }) => {
    await loginAsAdmin(page);
    // We execute API calls from the Admin's page context using their JWT token
    const patchResult = await page.evaluate(async ({ otherId, childId }) => {
      const token = localStorage.getItem("token");

      // Attempt to update non-child user -> Should fail with 403 Forbidden
      const resOther = await fetch(`/api/users/${otherId}`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ name: 'Hacked Name' })
      });

      // Attempt to update child user -> Should succeed with 200 OK
      const resChild = await fetch(`/api/users/${childId}`, {
        method: 'PUT',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ name: 'Updated Child Name' })
      });

      // Attempt to delete non-child user -> Should fail with 403 Forbidden
      const resDelOther = await fetch(`/api/users/${otherId}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${token}` }
      });

      return {
        otherStatus: resOther.status,
        childStatus: resChild.status,
        deleteOtherStatus: resDelOther.status
      };
    }, { otherId: otherUserId, childId: childUserId });

    expect(patchResult.otherStatus).toBe(403);
    expect(patchResult.childStatus).toBe(200);
    expect(patchResult.deleteOtherStatus).toBe(403);
  });

  test('4. Admin registration form role and parent dropdown restrictions', async ({ page }) => {
    await loginAsAdmin(page);
    // Still logged in as Admin from test 2
    await page.goto('/#/users');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    await page.click('button:has-text("Criar")');
    await expect(page.getByRole('dialog')).toBeVisible();

    // 4.1 Verify Role selector is disabled and set to User (Usuário)
    const roleSelect = page.locator('input[readonly].mantine-Select-input');
    await expect(roleSelect).toBeDisabled();
    await expect(roleSelect).toHaveValue('Usuário');

    // 4.2 Verify Parent User autocomplete contains Admin and Child, but not Other
    await page.fill('input[placeholder="Digite para buscar..."]', RUN_ID);
    await page.waitForTimeout(500);

    const dropdownOptions = page.locator('div[role="option"]');
    const optionsText = await dropdownOptions.allInnerTexts();

    // Should contain Admin EE and Child EE
    expect(optionsText.some(t => t.includes(ADMIN_EMAIL))).toBe(true);
    expect(optionsText.some(t => t.includes(CHILD_EMAIL))).toBe(true);
    // Should NOT contain Other EE
    expect(optionsText.some(t => t.includes(OTHER_EMAIL))).toBe(false);

    await page.click('button:has-text("Cancelar")');
  });

  test('5. Admin Matricula scoping verification', async ({ page }) => {
    await loginAsAdmin(page);
    // Verify Admin can add matricula to descendant user, but gets 403 on non-descendant user
    const matriculaResult = await page.evaluate(async ({ otherId, childId, runId }) => {
      const token = localStorage.getItem("token");

      // 5.1 POST child matricula link -> 201 Created
      const resChild = await fetch('/api/usermatriculas', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          userId: childId,
          matriculaNumber: `MATCHILD${runId}`,
          isOwner: true,
          status: 'active',
          startDate: new Date().toISOString()
        })
      });

      // 5.2 POST other matricula link -> 403 Forbidden
      const resOther = await fetch('/api/usermatriculas', {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          userId: otherId,
          matriculaNumber: `MATOTHER${runId}`,
          isOwner: true,
          status: 'active',
          startDate: new Date().toISOString()
        })
      });

      return {
        childPostStatus: resChild.status,
        otherPostStatus: resOther.status
      };
    }, { otherId: otherUserId, childId: childUserId, runId: RUN_ID });

    expect(matriculaResult.childPostStatus).toBe(201);
    expect(matriculaResult.otherPostStatus).toBe(403);
  });

  test('6. Admin Team scoping and orphan criteria', async ({ page }) => {
    await loginAsAdmin(page);
    // Go to teams page
    await page.goto('/#/teams');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Equipes (Equipes)' })).toBeVisible();

    // Search for the Admin's team
    await page.fill('input[placeholder="Buscar por equipe, proprietário ou membro..."]', `ee ${RUN_ID}`);
    const teamRow = page.locator('table tbody tr').filter({ hasText: `EE ${RUN_ID}` });
    await expect(teamRow).toBeVisible();

    // Click manage members
    await teamRow.locator('button[title="Editar"]').click();
    await expect(page.getByRole('heading', { name: /Gerenciar Membros/ }).first()).toBeVisible();

    // Check available members search input
    const searchInput = page.locator('input[placeholder="Buscar usuário..."]');
    await expect(searchInput).toBeVisible();

    // 6.1 Child user (CHILD_EMAIL) -> Should be present/available
    await searchInput.fill(CHILD_EMAIL);
    await page.waitForTimeout(500);
    await expect(page.locator('.tmc-user-card', { hasText: CHILD_EMAIL })).toBeVisible();

    // 6.2 User with no parent (carlosmendes@example.com) -> Should NOT be present/available (Orphan but not descendant)
    await searchInput.fill('carlosmendes@example.com');
    await page.waitForTimeout(500);
    await expect(page.locator('.tmc-user-card', { hasText: 'carlosmendes@example.com' })).not.toBeVisible();

    // 6.3 User with no team (NOTEAM_EMAIL) -> Should NOT be present/available (Orphan but not descendant)
    await searchInput.fill(NOTEAM_EMAIL);
    await page.waitForTimeout(500);
    await expect(page.locator('.tmc-user-card', { hasText: NOTEAM_EMAIL })).not.toBeVisible();

    // 6.4 Ineligible user with parent AND team (INELIGIBLE_EMAIL) -> Should NOT be present/available
    await searchInput.fill(INELIGIBLE_EMAIL);
    await page.waitForTimeout(500);
    await expect(page.locator('.tmc-user-card', { hasText: INELIGIBLE_EMAIL })).not.toBeVisible();

    // 6.5 Verify backend rejects adding ineligible user to team
    const addResult = await page.evaluate(async ({ teamId, ineligibleId, childId }) => {
      const token = localStorage.getItem("token");

      // Add child user -> 200 OK
      const resChild = await fetch(`/api/teams/${teamId}/members`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          members: [{ userId: childId, startDate: new Date().toISOString() }]
        })
      });

      // Add ineligible user -> 400 Bad Request
      const resIneligible = await fetch(`/api/teams/${teamId}/members`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          members: [{ userId: ineligibleId, startDate: new Date().toISOString() }]
        })
      });

      return {
        childStatus: resChild.status,
        ineligibleStatus: resIneligible.status
      };
    }, { teamId, ineligibleId: ineligibleUserId, childId: childUserId });

    expect(addResult.childStatus).toBe(200);
    expect(addResult.ineligibleStatus).toBe(400);

    // Close members modal
    await page.click('button.mantine-Modal-close');
  });

  test('7. Verify user list order (SuperAdmin first, Admin second, User third)', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/#/users');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // Get all functions (Badges) from the first page in order
    const badges = page.locator('table tbody tr td span.mantine-Badge-label');
    const badgeTexts = await badges.allInnerTexts();

    // Map each badge to its role precedence: Super Admin = 1, Admin = 2, Usuário = 3
    const rolePrecedences = badgeTexts
      .filter(text => ['Super Admin', 'Admin', 'Usuário'].includes(text))
      .map(text => {
        if (text === 'Super Admin') return 1;
        if (text === 'Admin') return 2;
        return 3;
      });

    // Verify rolePrecedences is sorted ascending
    for (let i = 0; i < rolePrecedences.length - 1; i++) {
      expect(rolePrecedences[i]).toBeLessThanOrEqual(rolePrecedences[i + 1]);
    }
  });
});
