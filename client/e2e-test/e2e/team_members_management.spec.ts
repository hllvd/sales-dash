import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';


test.describe('Team Members Management E2E', () => {
  // Serial mode: forces all tests in this describe to run in ONE worker.
  // This is required because tests share state via beforeAll (ownerId, childAId, etc.)
  // and the hierarchy user registrations have parent-child dependencies that break
  // under parallel execution (SQLite write may not be visible before child registration).
  test.describe.configure({ mode: 'serial' });

  // Unique RUN_ID to avoid user/team name collisions
  const RUN_ID = Array.from({ length: 8 }, () => String.fromCharCode(97 + Math.floor(Math.random() * 26))).join('');

  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' },
    owner: { name: `Owner X ${RUN_ID}`, email: `team.owner.${RUN_ID}@test.com`, role: 'admin' },
    childA: { name: `Child A ${RUN_ID}`, email: `team.childa.${RUN_ID}@test.com`, role: 'admin' },
    childB: { name: `Child B ${RUN_ID}`, email: `team.childb.${RUN_ID}@test.com`, role: 'admin' },
    grandchildC: { name: `Grandchild C ${RUN_ID}`, email: `team.grandc.${RUN_ID}@test.com`, role: 'admin' },
    unrelatedD: { name: `Unrelated D ${RUN_ID}`, email: `team.unrelatedd.${RUN_ID}@test.com`, role: 'admin' },
  };

  const teamName = `Team Mgmt ${RUN_ID}`;

  let ownerId: string;
  let childAId: string;
  let childBId: string;
  let grandchildCId: string;
  let unrelatedDId: string;
  let superadminToken: string;
  let teamId: number;
  // Tracks the live team name — the rename test mutates it in the DB,
  // subsequent tests must assert against the current name, not the original const.
  let effectiveTeamName: string;

  test.beforeAll(async ({ request }) => {
    // 1. Log in superadmin
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

    // --- START CLEANUP OLD TEST DATA ---
    console.log("[Setup] Starting cleanup of old test users and teams...");

    // 1. Delete teams whose names look like they belong to our E2E runs
    const getTeamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    if (getTeamsRes.ok()) {
      const body = await getTeamsRes.json();
      const teamsList = body.data || [];
      for (const t of teamsList) {
        if (t.name.startsWith("Team Mgmt ") || t.name.startsWith("Second Team ") || t.name.startsWith("Team DateEdit ")) {
          console.log(`[Cleanup] Deleting E2E Team: ${t.name} (ID: ${t.id})`);
          await request.delete(`/api/teams/${t.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` }
          });
        }
      }
    }

    // 2. Delete users whose emails contain E2E domain patterns
    const getUsersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    if (getUsersRes.ok()) {
      const body = await getUsersRes.json();
      const usersList = body.data?.items || [];
      for (const u of usersList) {
        if (u.email.toLowerCase().includes("team.owner.") || 
            u.email.toLowerCase().includes("team.childa.") || 
            u.email.toLowerCase().includes("team.childb.") || 
            u.email.toLowerCase().includes("team.grandc.") || 
            u.email.toLowerCase().includes("team.unrelatedd.")) {
          console.log(`[Cleanup] Deleting E2E User: ${u.email} (ID: ${u.id})`);
          await request.delete(`/api/users/${u.id}`, {
            headers: { Authorization: `Bearer ${superadminToken}` }
          });
        }
      }
    }
    console.log("[Setup] Cleanup of old test data completed successfully.");
    // --- END CLEANUP OLD TEST DATA ---

    // Robust, retrying, and self-healing registerUser helper to completely bypass SQLite lock/concurrency issues
    const registerUser = async (name: string, email: string, role: string, parentUserId?: string) => {
      let lastErr = '';
      for (let attempt = 1; attempt <= 3; attempt++) {
        const res = await request.post('/api/users/register', {
          headers: { Authorization: `Bearer ${superadminToken}` },
          data: {
            name,
            email,
            password: 'Password123!',
            role,
            parentUserId
          }
        });
        if (res.ok()) {
          const body = await res.json();
          return body.data.id as string;
        }

        // Self-Healing: Check if email already exists (either active or inactive/soft-deleted)
        if (res.status() === 400) {
          const searchRes = await request.get(`/api/users?search=${encodeURIComponent(email)}`, {
            headers: { Authorization: `Bearer ${superadminToken}` }
          });
          if (searchRes.ok()) {
            const listBody = await searchRes.json();
            const usersList = listBody.data?.items || (Array.isArray(listBody.data) ? listBody.data : (Array.isArray(listBody) ? listBody : []));
            const foundUser = usersList.find((u: any) => u.email.toLowerCase() === email.toLowerCase());
            if (foundUser) {
              console.log(`[Self-Healing] User ${name} (${email}) already exists. Checking activation status...`);
              if (!foundUser.isActive) {
                const updateRes = await request.put(`/api/users/${foundUser.id}`, {
                  headers: { Authorization: `Bearer ${superadminToken}` },
                  data: { isActive: true, role, parentUserId }
                });
                expect(updateRes.ok()).toBeTruthy();
                console.log(`[Self-Healing] Successfully reactivated User ID for ${email}`);
              }
              return foundUser.id as string;
            }
          }
        }

        lastErr = `Status=${res.status()} Body=${await res.text()}`;
        console.warn(`[Attempt ${attempt}/3] Retrying registration for ${name} (${email}): ${lastErr}. Wait 500ms...`);
        await new Promise(resolve => setTimeout(resolve, 500));
      }
      console.error(`All registration attempts failed for ${name} (${email}): ${lastErr}`);
      throw new Error(`Registration failed for ${name}: ${lastErr}`);
    };

    // 2. Register Users in hierarchy — sequential with delays so SQLite commits
    // are visible before a child is created with the parent as a FK dependency.
    const settle = (ms = 300) => new Promise(r => setTimeout(r, ms));

    ownerId = await registerUser(users.owner.name, users.owner.email, users.owner.role, superadminId);
    await settle(); // let owner write commit before creating children

    childAId = await registerUser(users.childA.name, users.childA.email, users.childA.role, ownerId);
    childBId = await registerUser(users.childB.name, users.childB.email, users.childB.role, ownerId);
    await settle(); // let childA commit before creating grandchild

    grandchildCId = await registerUser(users.grandchildC.name, users.grandchildC.email, users.grandchildC.role, childAId);
    unrelatedDId = await registerUser(users.unrelatedD.name, users.unrelatedD.email, users.unrelatedD.role, superadminId);

    // 3. Create Team with Owner X and assign owner (with retries and self-healing)
    let lastTeamErr = '';
    for (let attempt = 1; attempt <= 3; attempt++) {
      const teamRes = await request.post('/api/teams', {
        headers: { Authorization: `Bearer ${superadminToken}` },
        data: {
          name: teamName,
          members: [{ userId: ownerId, startDate: new Date(Date.now() - 8 * 365 * 24 * 60 * 60 * 1000).toISOString() }]
        }
      });
      let tId: number | undefined;

      if (teamRes.ok()) {
        const teamBody = await teamRes.json();
        tId = teamBody.data.id;
        teamId = tId!;
      } else {
        const bodyText = await teamRes.text();
        if (bodyText.includes("Nome da equipe já existe")) {
          console.log(`[Self-Healing] Team ${teamName} already exists. Resolving Team ID...`);
          const listRes = await request.get('/api/teams', {
            headers: { Authorization: `Bearer ${superadminToken}` }
          });
          if (listRes.ok()) {
            const listBody = await listRes.json();
            const teamsList = listBody.data || [];
            const foundTeam = teamsList.find((t: any) => t.name.toLowerCase() === teamName.toLowerCase());
            if (foundTeam) {
              tId = foundTeam.id;
              teamId = tId!;
              console.log(`[Self-Healing] Successfully recovered Team ID for ${teamName}: ${tId}`);
            }
          }
        }

        if (!tId) {
          lastTeamErr = `Team creation status: Status=${teamRes.status()} Body=${bodyText}`;
          console.warn(`[Attempt ${attempt}/3] Retrying team creation for ${teamName}: ${lastTeamErr}. Wait 500ms...`);
          await new Promise(resolve => setTimeout(resolve, 500));
          continue;
        }
      }

      // Set Owner X as team owner
      const ownerRes = await request.post(`/api/teams/${tId}/owner`, {
        headers: { Authorization: `Bearer ${superadminToken}`, 'Content-Type': 'application/json' },
        data: JSON.stringify(ownerId)
      });
      if (ownerRes.ok()) {
        return; // Success
      }

      lastTeamErr = `Setting owner status: Status=${ownerRes.status()} Body=${await ownerRes.text()}`;
      console.warn(`[Attempt ${attempt}/3] Retrying setting owner for team ${teamName}: ${lastTeamErr}. Wait 500ms...`);
      await new Promise(resolve => setTimeout(resolve, 500));
    }

    console.error(`All team/owner creation attempts failed for ${teamName}: ${lastTeamErr}`);
    throw new Error(`Team/owner creation failed for ${teamName}: ${lastTeamErr}`);
  });

  async function loginAndGoToTeams(page: any) {
    await loginAs(page, users.superadmin.email, users.superadmin.password);
    await page.goto('/#/teams');
    await expect(page.locator('.teams-container')).toBeVisible({ timeout: 15_000 });
  }



  test('should sort available users by BFS owner hierarchy in left column', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToTeams(page);

    // Find our team in the table and click Edit (blue pen icon) to open the modal
    const searchInput = page.locator('input[placeholder="Buscar por equipe, proprietário ou membro..."]');
    await searchInput.fill(teamName);
    await page.waitForTimeout(800);

    const editBtn = page.locator('button[title="Editar"]').first();
    await editBtn.click();

    // Check modal opens successfully using custom tmc class selector
    await expect(page.locator('.tmc-layout')).toBeVisible();
    await expect(page.locator('.mantine-Modal-title')).toContainText(`Gerenciar Membros — ${teamName.toUpperCase()}`);

    // Fetch the list of visible names in the Left Column (Available Users)
    const availableCards = page.locator('.tmc-column--left .tmc-user-card__name');
    await expect(availableCards.first()).toBeVisible();

    const names = await availableCards.allInnerTexts();

    // BFS Hierarchy Order relative to Owner X should be:
    // 1. Direct children: Child A, Child B (in creation order)
    // 2. Grandchildren: Grandchild C
    // 3. Unrelated / others: Unrelated D
    const lowerNames = names.map(n => n.toLowerCase().trim());
    const indexChildA = lowerNames.indexOf(users.childA.name.toLowerCase().trim());
    const indexChildB = lowerNames.indexOf(users.childB.name.toLowerCase().trim());
    const indexGrandC = lowerNames.indexOf(users.grandchildC.name.toLowerCase().trim());
    const indexUnrelatedD = lowerNames.indexOf(users.unrelatedD.name.toLowerCase().trim());

    // Verify BFS sorted order is preserved perfectly
    expect(indexChildA).toBeLessThan(indexChildB);
    expect(indexChildB).toBeLessThan(indexGrandC);
    expect(indexGrandC).toBeLessThan(indexUnrelatedD);
  });

  test('should support adding and removing team members instantly with one click', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToTeams(page);

    // Search and open modal
    const searchInput = page.locator('input[placeholder="Buscar por equipe, proprietário ou membro..."]');
    await searchInput.fill(teamName);
    await page.waitForTimeout(800);
    await page.locator('button[title="Editar"]').first().click();

    // Verify Child A is in left column (available)
    const leftCol = page.locator('.tmc-column--left');
    const rightCol = page.locator('.tmc-column--right');

    const childANameRegex = new RegExp(users.childA.name, 'i');

    await expect(leftCol).toContainText(childANameRegex);
    await expect(rightCol).not.toContainText(childANameRegex);

    // Click Child A to add them
    await leftCol.locator('.tmc-user-card__name', { hasText: childANameRegex }).click();

    // Verify Child A is now moved to the right column
    await expect(leftCol).not.toContainText(childANameRegex);
    await expect(rightCol).toContainText(childANameRegex);

    // Click remove (IconUserMinus button) on Child A in the right column
    const childACard = rightCol.locator('.tmc-user-card', { hasText: childANameRegex });
    await childACard.locator('button[title="Remover da equipe"]').click();

    // Verify Child A moves back to available users
    await expect(leftCol).toContainText(childANameRegex);
    await expect(rightCol).not.toContainText(childANameRegex);
  });

  test('should support inline team name editing and title update', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToTeams(page);

    // Search and open modal
    const searchInput = page.locator('input[placeholder="Buscar por equipe, proprietário ou membro..."]');
    await searchInput.fill(teamName);
    await page.waitForTimeout(800);
    await page.locator('button[title="Editar"]').first().click();

    const nameInput = page.locator('input[placeholder="Digite o nome da equipe..."]');
    await expect(nameInput).toHaveValue(teamName);

    // Edit the name
    const newName = `${teamName} MOD`;
    await nameInput.fill(newName);

    // Click check icon to save
    await page.locator('button[title="Salvar novo nome"]').click();

    // Verify modal title updates reactively
    await expect(page.locator('.mantine-Modal-title')).toContainText(`Gerenciar Membros — ${newName.toUpperCase()}`);

    // Track the updated name for subsequent tests
    effectiveTeamName = newName;

    // Close modal and verify the team name updated in the table
    await page.keyboard.press('Escape');
    await page.waitForTimeout(800);

    const tableCell = page.locator('.table-container td');
    await expect(tableCell.first()).toContainText(newName.toUpperCase());
  });

  test('should handle overlapping team membership dates and warning toast', async ({ page, request }) => {
    test.setTimeout(60_000);

    // 1. Create a second team using API
    const secondTeamName = `Second Team ${RUN_ID}`;
    const secondTeamRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        name: secondTeamName,
        members: [{ userId: ownerId, startDate: new Date(Date.now() - 8 * 365 * 24 * 60 * 60 * 1000).toISOString() }]
      }
    });
    expect(secondTeamRes.ok()).toBeTruthy();
    const secondTeamBody = await secondTeamRes.json();
    const secondTeamId = secondTeamBody.data.id;

    // Set Owner X as second team owner
    const secondOwnerRes = await request.post(`/api/teams/${secondTeamId}/owner`, {
      headers: { Authorization: `Bearer ${superadminToken}`, 'Content-Type': 'application/json' },
      data: JSON.stringify(ownerId)
    });
    expect(secondOwnerRes.ok()).toBeTruthy();

    // 2. Ensure clean state: remove Child A from both teams first (ignore errors, they may not be members)
    await request.delete(`/api/teams/${teamId}/members/${childAId}`, {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    await request.delete(`/api/teams/${secondTeamId}/members/${childAId}`, {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });

    // Now add Child A to the FIRST team with a known active (open-ended) membership
    const addRes = await request.post(`/api/teams/${teamId}/members`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        members: [{ userId: childAId, startDate: '2018-01-01T00:00:00.000Z' }]
      }
    });
    expect(addRes.ok()).toBeTruthy();

    // Log in and go to teams page
    await loginAndGoToTeams(page);

    // Search and open second team's management modal
    const searchInput = page.locator('input[placeholder="Buscar por equipe, proprietário ou membro..."]');
    await searchInput.fill(secondTeamName);
    await page.waitForTimeout(800);
    await page.locator('button[title="Editar"]').first().click();

    // Verify Child A is in Available Users (left column)
    const leftCol = page.locator('.tmc-column--left');
    const childANameRegex = new RegExp(users.childA.name, 'i');
    await expect(leftCol).toContainText(childANameRegex);

    // Add Child A to the second team (should trigger warning due to active membership in first team)
    await leftCol.locator('.tmc-user-card__name', { hasText: childANameRegex }).click();

    // Assert Warning Toast is displayed using highly robust text-based selection
    const toastTitle = page.getByText('Aviso de Conflito').first();
    await expect(toastTitle).toBeVisible({ timeout: 10000 });
    // Use effectiveTeamName because the rename test may have updated the DB name
    // The database name is normalized to lowercase on save, so toastMsg has it in lowercase
    const currentTeamName = (effectiveTeamName ?? teamName).toLowerCase();
    const toastMsg = page.getByText(new RegExp(`O usuário '.*' foi removido da equipe '${currentTeamName}' por conflito de data.`, 'i')).first();
    await expect(toastMsg).toBeVisible({ timeout: 10000 });

    // Close the modal
    await page.keyboard.press('Escape');
    await page.waitForTimeout(800);

    // 3. Verify backend dates via API
    const listRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    expect(listRes.ok()).toBeTruthy();
    const listBody = await listRes.json();
    const teams = listBody.data;

    // Find the first team and verify Child A's endDate is capped (non-null)
    const firstTeam = teams.find((t: any) => t.id === teamId);
    const childAInFirst = firstTeam.members.find((m: any) => m.userId === childAId);
    expect(childAInFirst).toBeDefined();
    expect(childAInFirst.endDate).not.toBeNull();

    // Find the second team and verify Child A's endDate is active (null) and has correct startDate
    const secondTeam = teams.find((t: any) => t.id === secondTeamId);
    const childAInSecond = secondTeam.members.find((m: any) => m.userId === childAId);
    expect(childAInSecond).toBeDefined();
    expect(childAInSecond.endDate).toBeNull();
    expect(childAInSecond.startDate).not.toBeNull();
  });

  test('Test manual modification of team membership dates via Floating Popover', async ({ page, request }) => {
    const teamName = `Team DateEdit ${RUN_ID}`;

    // 1. Create a Team
    const createRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: { name: teamName }
    });
    expect(createRes.ok()).toBeTruthy();
    const createBody = await createRes.json();
    const teamId = createBody.data.id;

    // 2. Add Child A to the team
    const addRes = await request.post(`/api/teams/${teamId}/members`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        members: [{ userId: childAId, startDate: '2018-01-01T00:00:00.000Z' }]
      }
    });
    expect(addRes.ok()).toBeTruthy();

    // Log in and go to teams page
    await loginAndGoToTeams(page);

    // Search and open team's management modal
    const searchInput = page.locator('input[placeholder="Buscar por equipe, proprietário ou membro..."]');
    await searchInput.fill(teamName);
    await page.waitForTimeout(800);
    await page.locator('button[title="Editar"]').first().click();

    // Verify Child A is in Members (right column)
    const rightCol = page.locator('.tmc-column--right');
    const childANameRegex = new RegExp(users.childA.name, 'i');
    await expect(rightCol).toContainText(childANameRegex);

    // Click the "Editar datas de vigência" button in Child A's card
    const memberCard = rightCol.locator('.tmc-user-card--member', { hasText: childANameRegex });
    const editDatesBtn = memberCard.locator('button[title="Editar datas de vigência"]');
    
    // --- 1. Test Validation: Start Date > End Date ---
    await editDatesBtn.click();
    const popover = page.locator('.mantine-Popover-dropdown', { hasText: 'Editar Período na Equipe' });
    await expect(popover).toBeVisible();

    // Fill Start Date to 2022-01-01
    const startDateInput = popover.locator('input[type="date"]').first();
    await startDateInput.fill('2022-01-01');

    // Toggle "Sem data de fim" off to enable End Date input
    const noEndDateCheckbox = popover.locator('input[type="checkbox"]');
    await noEndDateCheckbox.uncheck();

    // Fill End Date to 2020-01-01 (Invalid because Start > End)
    const endDateInput = popover.locator('input[type="date"]').nth(1);
    await endDateInput.fill('2020-01-01');

    // Click Save
    const saveBtn = popover.locator('button[title="Salvar datas"]');
    await saveBtn.click();

    // Assert that the local validation error is shown in the Popover
    await expect(popover.locator('text=Início deve ser anterior ao Fim')).toBeVisible();

    // Click Cancel to dismiss
    const cancelBtn = popover.locator('button[title="Cancelar"]');
    await cancelBtn.click();
    await expect(popover).not.toBeVisible();

    // --- 2. Test Success: Valid Date Range ---
    await editDatesBtn.click();
    await expect(popover).toBeVisible();

    // Set Start Date to 2020-05-15
    await startDateInput.fill('2020-05-15');

    // Toggle "Sem data de fim" off to enable End Date input
    await noEndDateCheckbox.uncheck();

    // Set End Date to 2021-08-20
    await endDateInput.fill('2021-08-20');

    // Click Save
    await saveBtn.click();

    // Assert Toast notification of success
    await expect(page.getByText('Datas de vigência de').first()).toBeVisible({ timeout: 5000 });

    // Close the modal
    await page.keyboard.press('Escape');
    await page.waitForTimeout(800);

    // 3. Verify backend dates via API
    const listRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${superadminToken}` }
    });
    expect(listRes.ok()).toBeTruthy();
    const listBody = await listRes.json();
    const teams = listBody.data;

    const team = teams.find((t: any) => t.id === teamId);
    const childAMember = team.members.find((m: any) => m.userId === childAId);
    expect(childAMember).toBeDefined();

    // Start date must match 2020-05-15
    expect(childAMember.startDate).toContain('2020-05-15');
    // End date must match 2021-08-20
    expect(childAMember.endDate).toContain('2021-08-20');
  });
});
