import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD } from './helpers/auth';

test.describe('User Tree Hierarchy & Submenu Navigation', () => {

  const superadminEmail = 'superadmin@salesapp.com';
  const superadminPassword = 'string';
  const adminEmail = 'admin@salesapp.com';
  const adminPassword = 'admin123';

  test.beforeAll(async ({ request }) => {
    // 1. Get Superadmin token
    const loginRes = await request.post('/api/users/login', {
      data: { email: superadminEmail, password: superadminPassword },
    });
    console.log('>>> E2E SETUP: login status =', loginRes.status());
    if (!loginRes.ok()) return;
    const loginData = await loginRes.json();
    const token = loginData.data.token;

    // 2. Fetch all users to resolve Carlos Mendes ID
    const usersRes = await request.get('/api/users?pageSize=1000', {
      headers: { Authorization: `Bearer ${token}` }
    });
    console.log('>>> E2E SETUP: getUsers status =', usersRes.status());
    if (!usersRes.ok()) return;
    const usersBody = await usersRes.json();
    const usersList = usersBody.data?.items || [];
    console.log('>>> E2E SETUP: users list count =', usersList.length);
    const carlos = usersList.find((x: any) => x.email.toLowerCase() === 'carlosmendes@example.com');
    console.log('>>> E2E SETUP: Carlos Mendes user found =', !!carlos);
    if (!carlos) return;

    // 3. Cleanup existing "Equipe Alpha"
    const teamsRes = await request.get('/api/teams', {
      headers: { Authorization: `Bearer ${token}` }
    });
    if (teamsRes.ok()) {
      const teamsBody = await teamsRes.json();
      const teamsList = teamsBody.data || [];
      const existingAlpha = teamsList.find((t: any) => t.name === 'Equipe Alpha');
      if (existingAlpha) {
        console.log('>>> E2E SETUP: deleting existing Equipe Alpha ID =', existingAlpha.id);
        await request.delete(`/api/teams/${existingAlpha.id}`, {
          headers: { Authorization: `Bearer ${token}` }
        });
      }
    }

    // 4. Create "Equipe Alpha"
    const alphaCreateRes = await request.post('/api/teams', {
      headers: { Authorization: `Bearer ${token}` },
      data: { name: 'Equipe Alpha', members: [{ userId: carlos.id, startDate: new Date('2020-01-01').toISOString() }] }
    });
    console.log('>>> E2E SETUP: createTeam status =', alphaCreateRes.status());
    if (!alphaCreateRes.ok()) return;
    const alphaData = await alphaCreateRes.json();
    const alphaId = alphaData.data.id;

    // Set Owner
    const ownerRes = await request.post(`/api/teams/${alphaId}/owner`, {
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      data: JSON.stringify(carlos.id)
    });
    console.log('>>> E2E SETUP: setOwner status =', ownerRes.status());
  });

  test('should navigate to Users and Tree views, check components, and test role-based Select visibility', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Login as standard Admin first (to verify Select is not visible)
    console.log('>>> Logging in as standard Admin...');
    await loginAs(page, adminEmail, adminPassword);

    // 2. Locate and check the navigation sidebar
    console.log('>>> Checking navigation submenu...');
    const usersMenuLink = page.getByRole('link', { name: 'Usuários', exact: true });
    await expect(usersMenuLink).toBeVisible();

    // Click to navigate and expand
    await usersMenuLink.click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // Verify sub-links are visible
    const listSubmenu = page.getByRole('link', { name: 'Lista', exact: true });
    const treeSubmenu = page.getByRole('link', { name: 'Árvore', exact: true });
    await expect(listSubmenu).toBeVisible();
    await expect(treeSubmenu).toBeVisible();

    // Navigate to Tree page
    await treeSubmenu.click();
    await expect(page.getByRole('heading', { name: 'Árvore de Usuários' })).toBeVisible();

    // Verify target user selector dropdown is NOT visible for standard admin
    await expect(page.locator('input[placeholder="Selecione um usuário (Padrão: Minha Árvore)"]')).not.toBeVisible();

    // 3. Clear storage and login as Superadmin
    console.log('>>> Logging in as Superadmin...');
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);


    // Navigate directly to Tree page
    await page.goto('#/users/tree');
    await expect(page.getByRole('heading', { name: 'Árvore de Usuários' })).toBeVisible();

    // Verify target user selector dropdown IS visible for superadmin
    const userSelectInput = page.locator('input[placeholder="Selecione um usuário (Padrão: Minha Árvore)"]');
    await expect(userSelectInput).toBeVisible();

    // Verify basic tree nodes display
    const rootNode = page.locator('.tree-node-group').first();
    await expect(rootNode).toBeVisible();

    // 4. Test Search Highlighting
    console.log('>>> Testing Search Highlighting...');
    const rootName = await rootNode.locator('.tree-node-name').textContent();
    expect(rootName).toBeTruthy();

    const searchInput = page.locator('input[placeholder="Buscar por nome, email ou função..."]');
    await searchInput.fill(rootName!);
    await page.waitForTimeout(500); // Wait for input to propagate

    // Check if matching node is highlighted
    const highlightedNode = page.locator('.tree-node-group.tree-node-highlighted');
    await expect(highlightedNode).toBeVisible();
    await expect(highlightedNode.locator('.tree-node-name')).toHaveText(rootName!);

    // Clear search
    await searchInput.fill('');

    // 5. Test Profile details modal popup
    console.log('>>> Testing profile details modal popup...');
    // Hover over the root node to reveal the action buttons
    await rootNode.hover();
    // Click the eye icon on the root node
    await rootNode.locator('.view-details-btn').click();

    // The user details modal should open
    const profileModalTitle = page.getByRole('dialog').getByRole('heading', { name: 'Perfil do Usuário' }).first();
    await expect(profileModalTitle).toBeVisible({ timeout: 10000 });

    // Close the details modal
    await page.getByRole('dialog').locator('.mantine-Modal-close').click();
    await expect(page.getByRole('dialog')).not.toBeVisible();

    // 6. Test Select other user's tree hierarchy (select Carlos Mendes who owns a team)
    console.log('>>> Testing select user Carlos Mendes who owns Equipe Alpha...');
    // Click select input to open options
    await userSelectInput.click();
    
    // Select Carlos Mendes
    const option = page.locator('.mantine-Select-option').filter({ hasText: 'Carlos Mendes' }).first();
    await expect(option).toBeVisible();
    await option.click();

    // Wait for the tree to refresh and check if Carlos Mendes is now the root of the hierarchy
    const newRootNode = page.locator('.tree-node-group').first();
    await expect(newRootNode.locator('.tree-node-name')).toContainText('Carlos Mendes');

    // 7. Verify Team Ownership Badge
    console.log('>>> Checking Team Ownership Badge...');
    const teamBadge = newRootNode.locator('.mantine-Badge-root', { hasText: 'Dono: Equipe Alpha' });
    await expect(teamBadge).toBeVisible();

    // 8. Test Edit User Modal
    console.log('>>> Testing Edit User Modal...');
    // Hover over Carlos Mendes node to reveal the action buttons
    await newRootNode.hover();

    // Locate the edit button
    const editAction = newRootNode.locator('.edit-user-btn');
    await editAction.click();

    // Verify Edit User modal opens
    const editModalTitle = page.getByRole('dialog').getByRole('heading', { name: 'Editar Usuário' }).first();
    await expect(editModalTitle).toBeVisible({ timeout: 10000 });

    // Close/Cancel the modal
    await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();
    await expect(page.getByRole('dialog')).not.toBeVisible();

    // 9. Test Team Navigation Link
    console.log('>>> Testing Team Navigation Link...');
    // Hover again to show action icons
    await newRootNode.hover();
    
    // Team navigation button
    const teamAction = newRootNode.locator('.team-nav-btn');
    await teamAction.click();

    // Verify navigation to Teams Page with query parameter
    await expect(page).toHaveURL(/.*#\/teams\?search=Equipe.*/);
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Equipes' })).toBeVisible({ timeout: 10000 });

    // Verify that the search input is initialized to Equipe Alpha and the table filters
    const teamsSearchInput = page.locator('input[placeholder="Buscar por equipe, proprietário ou membro..."]');
    await expect(teamsSearchInput).toHaveValue('Equipe Alpha');
    
    // Check that only Equipe Alpha is visible in the table
    const tableRow = page.locator('table tbody tr');
    await expect(tableRow).toHaveCount(1);
    await expect(tableRow.first().locator('td').first()).toHaveText('EQUIPE ALPHA');

    console.log('>>> User Tree Hierarchy, Submenu, User Edit, and Team Navigation E2E checks completed successfully!');
  });
});
