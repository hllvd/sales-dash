import { test, expect } from '@playwright/test';

test.describe('Team Members Management E2E', () => {
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

        // Self-Healing: Check if email already exists
        if (res.status() === 400) {
          const bodyText = await res.text();
          if (bodyText.includes("Email já existe")) {
            console.log(`[Self-Healing] User ${name} (${email}) was already registered by a previous try. Retrieving User ID...`);
            const listRes = await request.get('/api/users?pageSize=100', {
              headers: { Authorization: `Bearer ${superadminToken}` }
            });
            if (listRes.ok()) {
              const listBody = await listRes.json();
              const usersList = listBody.data?.items || (Array.isArray(listBody.data) ? listBody.data : (Array.isArray(listBody) ? listBody : []));
              const foundUser = usersList.find((u: any) => u.email.toLowerCase() === email.toLowerCase());
              if (foundUser) {
                console.log(`[Self-Healing] Successfully recovered User ID for ${email}: ${foundUser.id}`);
                return foundUser.id as string;
              }
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

    // 2. Register Users in hierarchy
    // owner under superadmin
    ownerId = await registerUser(users.owner.name, users.owner.email, users.owner.role, superadminId);
    // childA under owner
    childAId = await registerUser(users.childA.name, users.childA.email, users.childA.role, ownerId);
    // childB under owner (created after childA)
    childBId = await registerUser(users.childB.name, users.childB.email, users.childB.role, ownerId);
    // grandchildC under childA
    grandchildCId = await registerUser(users.grandchildC.name, users.grandchildC.email, users.grandchildC.role, childAId);
    // unrelatedD under superadmin
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
    await page.goto('/');
    await page.fill('input[type="email"]', users.superadmin.email);
    await page.fill('input[type="password"]', users.superadmin.password);
    await page.click('button.login-button');
    await page.goto('/#/teams');
    await page.waitForTimeout(1500); // Allow list to fully render
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
    await expect(page.locator('.mantine-Modal-title')).toContainText(`Gerenciar Membros — ${teamName}`);

    // Fetch the list of visible names in the Left Column (Available Users)
    const availableCards = page.locator('.tmc-column--left .tmc-user-card__name');
    await expect(availableCards.first()).toBeVisible();

    const names = await availableCards.allInnerTexts();

    // BFS Hierarchy Order relative to Owner X should be:
    // 1. Direct children: Child A, Child B (in creation order)
    // 2. Grandchildren: Grandchild C
    // 3. Unrelated / others: Unrelated D
    const indexChildA = names.indexOf(users.childA.name);
    const indexChildB = names.indexOf(users.childB.name);
    const indexGrandC = names.indexOf(users.grandchildC.name);
    const indexUnrelatedD = names.indexOf(users.unrelatedD.name);

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

    await expect(leftCol).toContainText(users.childA.name);
    await expect(rightCol).not.toContainText(users.childA.name);

    // Click Child A to add them
    await leftCol.locator('.tmc-user-card__name', { hasText: users.childA.name }).click();

    // Verify Child A is now moved to the right column
    await expect(leftCol).not.toContainText(users.childA.name);
    await expect(rightCol).toContainText(users.childA.name);

    // Click remove (IconUserMinus button) on Child A in the right column
    const childACard = rightCol.locator('.tmc-user-card', { hasText: users.childA.name });
    await childACard.locator('button[title="Remover da equipe"]').click();

    // Verify Child A moves back to available users
    await expect(leftCol).toContainText(users.childA.name);
    await expect(rightCol).not.toContainText(users.childA.name);
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
    await expect(page.locator('.mantine-Modal-title')).toContainText(`Gerenciar Membros — ${newName}`);

    // Close modal and verify the team name updated in the table
    await page.keyboard.press('Escape');
    await page.waitForTimeout(800);

    const tableCell = page.locator('.table-container td');
    await expect(tableCell.first()).toContainText(newName);
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

    // 2. Ensure Child A is in the first team (Team Mgmt) first
    const addRes = await request.post(`/api/teams/${teamId}/members`, {
      headers: { Authorization: `Bearer ${superadminToken}` },
      data: {
        members: [{ userId: childAId, startDate: new Date(Date.now() - 8 * 365 * 24 * 60 * 60 * 1000).toISOString() }]
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
    await expect(leftCol).toContainText(users.childA.name);

    // Add Child A to the second team (should trigger warning due to active membership in first team)
    await leftCol.locator('.tmc-user-card__name', { hasText: users.childA.name }).click();

    // Assert Warning Toast is displayed
    const toast = page.locator('.mantine-Notification-root', { hasText: 'Aviso de Conflito' });
    await expect(toast).toBeVisible({ timeout: 10000 });
    await expect(toast).toContainText(`O usuário '${users.childA.name}' foi removido da equipe '${teamName}' por conflito de data.`);

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
});
