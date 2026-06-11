import { test, expect } from '@playwright/test';

test.describe('User Tree Hierarchy & Submenu Navigation', () => {
  const superadminEmail = 'superadmin@salesapp.com';
  const superadminPassword = 'string';
  const adminEmail = 'admin@salesapp.com';
  const adminPassword = 'admin123';

  test('should navigate to Users and Tree views, check components, and test role-based Select visibility', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Login as standard Admin first (to verify Select is not visible)
    console.log('>>> Logging in as standard Admin...');
    await page.goto('/');
    await page.fill('input[type="email"]', adminEmail);
    await page.fill('input[type="password"]', adminPassword);
    await page.click('button.login-button');

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
    await page.evaluate(() => localStorage.clear());
    await page.goto('/');
    await page.fill('input[type="email"]', superadminEmail);
    await page.fill('input[type="password"]', superadminPassword);
    await page.click('button.login-button');

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
    // Click the eye icon on the root node
    await rootNode.locator('.tree-node-action').first().click();

    // The user details modal should open
    const profileModalTitle = page.getByRole('dialog').getByRole('heading', { name: 'Perfil do Usuário' }).first();
    await expect(profileModalTitle).toBeVisible({ timeout: 10000 });

    // Close the details modal
    await page.getByRole('dialog').locator('.mantine-Modal-close').click();
    await expect(page.getByRole('dialog')).not.toBeVisible();

    // 6. Test Select other user's tree hierarchy
    console.log('>>> Testing select other user tree...');
    // Click select input to open options
    await userSelectInput.click();
    
    // Select one seeded user, e.g. "Bryan Lopes" or "Gabriela Loreto"
    const option = page.locator('.mantine-Select-option').filter({ hasText: 'Bryan Lopes' }).first();
    await expect(option).toBeVisible();
    await option.click();

    // Wait for the tree to refresh and check if Bryan Lopes is now the root of the hierarchy
    const newRootNode = page.locator('.tree-node-group').first();
    await expect(newRootNode.locator('.tree-node-name')).toContainText('Bryan Lopes');

    console.log('>>> User Tree Hierarchy and Submenu E2E checks completed successfully!');
  });
});
