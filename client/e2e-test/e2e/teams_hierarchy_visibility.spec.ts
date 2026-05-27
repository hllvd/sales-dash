import { test, expect } from '@playwright/test';

test.describe('Teams Hierarchical Visibility E2E', () => {
  // Generate letter-only unique suffix to satisfy strict backend ValidUserName validation
  const rawRunId = Date.now().toString().slice(-4);
  const charMap: Record<string, string> = {
    '0': 'a', '1': 'b', '2': 'c', '3': 'd', '4': 'e',
    '5': 'f', '6': 'g', '7': 'h', '8': 'i', '9': 'j'
  };
  const RUN_ID = rawRunId.split('').map(char => charMap[char] || char).join('');

  // User credentials
  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' },
    A: { name: `User A ${RUN_ID}`, email: `team.a.${RUN_ID}@test.com`, password: 'Password123!', role: 'admin' },
    B: { name: `User B ${RUN_ID}`, email: `team.b.${RUN_ID}@test.com`, password: 'Password123!', role: 'admin' },
    C: { name: `User C ${RUN_ID}`, email: `team.c.${RUN_ID}@test.com`, password: 'Password123!', role: 'admin' },
    D: { name: `User D ${RUN_ID}`, email: `team.d.${RUN_ID}@test.com`, password: 'Password123!', role: 'admin' },
    E: { name: `User E ${RUN_ID}`, email: `team.e.${RUN_ID}@test.com`, password: 'Password123!', role: 'admin' },
  };

  const teams = {
    A: `Team A ${RUN_ID}`,
    B: `Team B ${RUN_ID}`,
    C: `Team C ${RUN_ID}`,
    D: `Team D ${RUN_ID}`,
    E: `Team E ${RUN_ID}`,
  };

  let userAId: string;
  let userBId: string;
  let userCId: string;
  let userDId: string;
  let userEId: string;

  test.beforeAll(async ({ request }) => {
    // 1. Get Superadmin token
    const loginRes = await request.post('/api/users/login', {
      data: { email: users.superadmin.email, password: users.superadmin.password },
    });
    if (!loginRes.ok()) {
      console.error(`Login failed: Status=${loginRes.status()} Body=${await loginRes.text()}`);
    }
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    const token = loginData.data.token;

    // Get Superadmin's User ID to set as the parent for User A and User D
    // (System strictly allows only one root user without a parent)
    const meRes = await request.get('/api/users/me', {
      headers: { Authorization: `Bearer ${token}` }
    });
    if (!meRes.ok()) {
      console.error(`Fetching superadmin profile failed: Status=${meRes.status()} Body=${await meRes.text()}`);
    }
    expect(meRes.ok()).toBeTruthy();
    const meData = await meRes.json();
    const superadminId = meData.data.id;

    // Helper to register a user with retries (robust against transient SQLite database lock/concurrency issues)
    const registerUser = async (name: string, email: string, role: string, parentUserId?: string) => {
      let lastErr = '';
      for (let attempt = 1; attempt <= 3; attempt++) {
        const res = await request.post('/api/users/register', {
          headers: { Authorization: `Bearer ${token}` },
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
        lastErr = `Status=${res.status()} Body=${await res.text()}`;
        console.warn(`[Attempt ${attempt}/3] Retrying registration for ${name} (${email}): ${lastErr}. Wait 500ms...`);
        await new Promise(resolve => setTimeout(resolve, 500));
      }
      console.error(`All registration attempts failed for ${name} (${email}): ${lastErr}`);
      throw new Error(`Registration failed for ${name}: ${lastErr}`);
    };

    // Helper to create a team and assign owner with retries
    const createTeamWithOwner = async (teamName: string, ownerUserId: string) => {
      let lastErr = '';
      for (let attempt = 1; attempt <= 3; attempt++) {
        // Create team
        const res = await request.post('/api/teams', {
          headers: { Authorization: `Bearer ${token}` },
          data: {
            name: teamName,
            members: [
              { userId: ownerUserId, startDate: new Date(Date.now() - 8 * 365 * 24 * 60 * 60 * 1000).toISOString() }
            ]
          }
        });
        if (!res.ok()) {
          lastErr = `Team creation status: Status=${res.status()} Body=${await res.text()}`;
          console.warn(`[Attempt ${attempt}/3] Retrying team creation for ${teamName}: ${lastErr}. Wait 500ms...`);
          await new Promise(resolve => setTimeout(resolve, 500));
          continue;
        }

        const body = await res.json();
        const teamId = body.data.id;

        // Set owner
        const ownerRes = await request.post(`/api/teams/${teamId}/owner`, {
          headers: {
            Authorization: `Bearer ${token}`,
            'Content-Type': 'application/json'
          },
          data: JSON.stringify(ownerUserId)
        });
        if (ownerRes.ok()) {
          return; // Success
        }

        lastErr = `Setting owner status: Status=${ownerRes.status()} Body=${await ownerRes.text()}`;
        console.warn(`[Attempt ${attempt}/3] Retrying setting owner for team ${teamName}: ${lastErr}. Wait 500ms...`);
        await new Promise(resolve => setTimeout(resolve, 500));
      }

      console.error(`All team/owner creation attempts failed for ${teamName}: ${lastErr}`);
      throw new Error(`Team/owner creation failed for ${teamName}: ${lastErr}`);
    };

    // 2. Register Users in hierarchical order
    // A under superadmin
    userAId = await registerUser(users.A.name, users.A.email, users.A.role, superadminId);
    // B under A
    userBId = await registerUser(users.B.name, users.B.email, users.B.role, userAId);
    // C under B
    userCId = await registerUser(users.C.name, users.C.email, users.C.role, userBId);

    // D under superadmin
    userDId = await registerUser(users.D.name, users.D.email, users.D.role, superadminId);
    // E under D
    userEId = await registerUser(users.E.name, users.E.email, users.E.role, userDId);

    // 3. Create Teams
    await createTeamWithOwner(teams.A, userAId);
    await createTeamWithOwner(teams.B, userBId);
    await createTeamWithOwner(teams.C, userCId);
    await createTeamWithOwner(teams.D, userDId);
    await createTeamWithOwner(teams.E, userEId);
  });

  // Helper to log in to UI
  async function loginAndGoToTeams(page: any, email: string, password: string) {
    await page.goto('/');
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', password);
    await page.click('button.login-button');

    // Wait for landing page, then navigate to #/teams directly
    await page.goto('/#/teams');
    await page.waitForTimeout(2000); // Allow list to load
  }

  test('Superadmin should see all seeded teams', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToTeams(page, users.superadmin.email, users.superadmin.password);

    // Verify all Teams A, B, C, D, E are visible
    const container = page.locator('.teams-container');
    await expect(container).toContainText(teams.A);
    await expect(container).toContainText(teams.B);
    await expect(container).toContainText(teams.C);
    await expect(container).toContainText(teams.D);
    await expect(container).toContainText(teams.E);
  });

  test('User D (Admin) should see only Team D and Team E', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToTeams(page, users.D.email, users.D.password);

    // Should see D and E
    const container = page.locator('.teams-container');
    await expect(container).toContainText(teams.D);
    await expect(container).toContainText(teams.E);

    // Should NOT see A, B, or C
    const containerText = await container.innerText();
    expect(containerText).not.toContain(teams.A);
    expect(containerText).not.toContain(teams.B);
    expect(containerText).not.toContain(teams.C);
  });

  test('User A (Admin) should see Team A, B and C', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToTeams(page, users.A.email, users.A.password);

    // Should see A, B and C
    const container = page.locator('.teams-container');
    await expect(container).toContainText(teams.A);
    await expect(container).toContainText(teams.B);
    await expect(container).toContainText(teams.C);

    // Should NOT see D or E
    const containerText = await container.innerText();
    expect(containerText).not.toContain(teams.D);
    expect(containerText).not.toContain(teams.E);
  });

  test('Search field should filter teams in the table', async ({ page }) => {
    test.setTimeout(45_000);
    await loginAndGoToTeams(page, users.superadmin.email, users.superadmin.password);

    // Search for Team D specifically
    await page.fill('input[placeholder="Buscar por equipe, proprietário ou membro..."]', teams.D);
    await page.waitForTimeout(500); // Wait for input/render

    const container = page.locator('.teams-container');
    await expect(container).toContainText(teams.D);
    
    // Team A should NOT be visible under the filtered results
    const containerText = await container.innerText();
    expect(containerText).not.toContain(teams.A);
  });
});
