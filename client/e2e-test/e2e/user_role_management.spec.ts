import { test, expect } from '@playwright/test';

test.describe('User Role Management (TEAR 2)', () => {
  const targetUser = 'carlosmendes@example.com';
  const targetPassword = '123456';
  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';

  test('should verify and promote carlosmendes to admin role', async ({ page }) => {
    test.setTimeout(60000);
    // 1. Initial Check: Login as Carlos to see current state
    console.log(`>>> Checking initial role for ${targetUser}`);
    await page.goto('/');
    await page.fill('input[type="email"]', targetUser);
    await page.fill('input[type="password"]', targetPassword);
    await page.click('button.login-button');

    // If he's a user, he shouldn't see the "Contratos" (admin) nav link
    const contractsLink = page.locator('a[href="#/contracts"]');
    const isAlreadyAdmin = await contractsLink.isVisible();

    if (isAlreadyAdmin) {
      console.log(`>>> [Tear 2] ${targetUser} is already an Admin. Skipping promotion.`);
    } else {
      console.log(`>>> ${targetUser} is currently a regular User. Proceeding to promotion.`);

      // 2. Promotion: Login as Admin to change role
      await page.evaluate(() => localStorage.clear());
      await page.goto('/');
      await page.fill('input[type="email"]', adminEmail);
      await page.fill('input[type="password"]', adminPassword);
      await page.click('button.login-button');

      // Go to Users page
      await page.click('a[href="#/users"]');
      await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

      // Search for Carlos
      await page.fill('input[placeholder="Buscar por nome ou email..."]', targetUser);
      await page.waitForTimeout(1000); // Wait for debounce

      // Find the row and click Edit
      const userRow = page.locator('tr', { hasText: targetUser });
      await userRow.locator('button[title="Editar"]').click();

      // Change role to Administrador
      await page.click('input[readonly].mantine-Select-input'); // Click select to open dropdown
      await page.click('div[role="option"]:has-text("Administrador")');

      await page.click('button:has-text("Salvar Alterações")');
      
      // Wait for the modal to disappear to ensure the update call and list refresh have completed
      await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
      
      // Verification: Check the row directly for the "Admin" badge text
      // This is more robust than selecting by specific Mantine internal classes
      await expect(userRow).toContainText('Admin', { ignoreCase: true, timeout: 20000 });
      console.log(`>>> ${targetUser} promoted to Admin successfully.`);
      
      // Wait for SQLite database to commit and settle
      await page.waitForTimeout(1500);
      
      await page.evaluate(() => localStorage.clear());
    }

    // 3. Verification: Login as Carlos and verify Admin access
    console.log(`>>> Verifying Admin access for ${targetUser}`);
    await page.goto('/');
    await page.fill('input[type="email"]', targetUser);
    await page.fill('input[type="password"]', targetPassword);
    await page.click('button.login-button');

    // Now he SHOULD see the "Contratos" link
    await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible();

    console.log(`>>> [Tear 2] Verification complete. ${targetUser} has Admin access.`);
  });
});
