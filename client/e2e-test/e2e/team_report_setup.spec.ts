import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Teams Setup for Reports E2E', () => {

  const users = {
    superadmin: { email: 'superadmin@salesapp.com', password: 'string' }
  };

  test('should retrieve pre-seeded users and group them into Equipe Alpha and Equipe Beta', async ({ request, page }) => {
    test.setTimeout(60_000);

    // 1. Get Superadmin token
    const loginRes = await request.post('/api/users/login', {
      data: { email: users.superadmin.email, password: users.superadmin.password },
    });
    expect(loginRes.ok()).toBeTruthy();
    const loginData = await loginRes.json();
    const token = loginData.data.token;

    // 2. Fetch all users in the system to resolve IDs
    const usersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(usersRes.ok()).toBeTruthy();
    const usersBody = await usersRes.json();
    const usersList = usersBody.data?.items || (Array.isArray(usersBody.data) ? usersBody.data : []);

    const findUserByEmail = (email: string) => {
      const u = usersList.find((x: any) => x.email.toLowerCase() === email.toLowerCase());
      if (!u) {
        throw new Error(`User with email "${email}" not found! Make sure the tear-1 import_wizard spec has run successfully.`);
      }
      return u;
    };

    // Retrieve imported user objects
    const carlos = findUserByEmail('carlosmendes@example.com');
    const julio = findUserByEmail('juliomota@example.com');
    const arthur = findUserByEmail('arthurterplak@example.com');
    const bryan = findUserByEmail('bryanlopes@example.com');
    const richard = findUserByEmail('richardwesley@example.com');
    const paulo = findUserByEmail('paulocarvalho@example.com');
    const carla = findUserByEmail('carlafranciele@example.com');

    console.log('>>> Successfully found all target users in the system.');

    // 3. Cleanup existing "Equipe Alpha" and "Equipe Beta" to allow clean repeat runs
    const teamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(teamsRes.ok()).toBeTruthy();
    const teamsBody = await teamsRes.json();
    const teamsList = teamsBody.data || [];

    const existingAlpha = teamsList.find((t: any) => t.name === 'Equipe Alpha');
    if (existingAlpha) {
      console.log(`>>> Deleting existing Equipe Alpha (ID: ${existingAlpha.id})`);
      await request.delete(`/api/teams/${existingAlpha.id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
    }

    const existingBeta = teamsList.find((t: any) => t.name === 'Equipe Beta');
    if (existingBeta) {
      console.log(`>>> Deleting existing Equipe Beta (ID: ${existingBeta.id})`);
      await request.delete(`/api/teams/${existingBeta.id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
    }

    // 4. Create "Equipe Alpha" (Owner: Carlos Mendes, members: Julio Mota, Arthur Terplak, Bryan Lopes)
    console.log('>>> Creating Equipe Alpha...');
    const alphaMembers = [
      { userId: carlos.id, startDate: new Date('2020-01-01').toISOString() },
      { userId: julio.id, startDate: new Date('2020-01-01').toISOString() },
      { userId: arthur.id, startDate: new Date('2020-01-01').toISOString() },
      { userId: bryan.id, startDate: new Date('2020-01-01').toISOString() }
    ];
    const alphaCreateRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${token}` },
      data: { name: 'Equipe Alpha', members: alphaMembers }
    });
    expect(alphaCreateRes.ok()).toBeTruthy();
    const alphaData = await alphaCreateRes.json();
    const alphaId = alphaData.data.id;

    // Set Owner for Equipe Alpha
    const alphaOwnerRes = await request.post(`/api/teams/${alphaId}/owner`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: JSON.stringify(carlos.id)
    });
    expect(alphaOwnerRes.ok()).toBeTruthy();
    console.log('>>> Equipe Alpha created and owner assigned.');

    // 5. Create "Equipe Beta" (Owner: Julio Mota, members: Richard Wesley, Paulo Carvalho, Carla Franciele)
    console.log('>>> Creating Equipe Beta...');
    const betaMembers = [
      { userId: julio.id, startDate: new Date('2020-01-01').toISOString() },
      { userId: richard.id, startDate: new Date('2020-01-01').toISOString() },
      { userId: paulo.id, startDate: new Date('2020-01-01').toISOString() },
      { userId: carla.id, startDate: new Date('2020-01-01').toISOString() }
    ];
    const betaCreateRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${token}` },
      data: { name: 'Equipe Beta', members: betaMembers }
    });
    expect(betaCreateRes.ok()).toBeTruthy();
    const betaData = await betaCreateRes.json();
    const betaId = betaData.data.id;

    // Set Owner for Equipe Beta
    const betaOwnerRes = await request.post(`/api/teams/${betaId}/owner`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: JSON.stringify(julio.id)
    });
    expect(betaOwnerRes.ok()).toBeTruthy();
    console.log('>>> Equipe Beta created and owner assigned.');

    // 6. Navigate to UI and verify both Teams exist visually
    console.log('>>> Logging into UI to verify teams...');
    await loginAs(page, users.superadmin.email, users.superadmin.password);

    // Go to Teams page
    await page.goto('/#/teams');

    await page.waitForTimeout(2000); // Allow list to load

    const container = page.locator('.teams-container');
    await expect(container).toContainText('EQUIPE ALPHA');
    await expect(container).toContainText('EQUIPE BETA');

    console.log('>>> SUCCESS: Both teams created successfully and verified in the UI!');
  });
});
