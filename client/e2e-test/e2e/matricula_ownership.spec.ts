import { test, expect } from '@playwright/test';

test.describe('Matricula Ownership Enforcement (TEAR 2)', () => {
  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';
  const user1 = 'carlosmendes@example.com';
  const user1Name = 'Carlos Mendes';
  const user2 = 'mariaeduarda@example.com';
  const user2Name = 'Maria Eduarda';
  const testMatricula = 'OWNERSHIP-TEST-001';

  test.beforeEach(async ({ page }) => {
    // Login as Admin
    await page.goto('/');
    await page.fill('input[type="email"]', adminEmail);
    await page.fill('input[type="password"]', adminPassword);
    await page.click('button.login-button');

    // Go to Matriculas page
    await page.click('a[href="#/matriculas"]', { timeout: 15000 });
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Matrículas' })).toBeVisible({ timeout: 15000 });
  });

  test('should enforce that only one user can be owner of a matricula', async ({ page }) => {
    console.log('>>> Step 1: Create matricula with first owner');

    await page.click('button:has-text("Nova Matrícula")');

    // Search and select User 1
    await page.fill('input[placeholder="Digite para buscar um usuário"]', user1);
    // Wait for the 3s debounce + API call
    await page.waitForTimeout(4000);
    await page.click(`div[role="option"]:has-text("${user1}")`);

    await page.fill('input[placeholder="Ex: MAT-001"]', testMatricula);

    // Mark as Owner
    const ownerCheckbox = page.locator('label:has-text("Proprietário da Matrícula")').locator('..').locator('input[type="checkbox"]');
    await ownerCheckbox.check();

    await page.click('button:has-text("Criar Matrícula")');

    // Wait for the modal to disappear
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // Verify it appeared in the list — filter by name (table shows name, not email)
    const user1Row = page.locator('tr', { hasText: testMatricula }).filter({ hasText: user1Name });
    await expect(user1Row).toContainText('Proprietário');
    console.log(`>>> Success: ${user1Name} is now owner of ${testMatricula}`);

    console.log('>>> Step 2: Try to assign another user as owner of the SAME matricula');

    await page.click('button:has-text("Nova Matrícula")');

    // Search and select User 2
    await page.fill('input[placeholder="Digite para buscar um usuário"]', user2);
    await page.waitForTimeout(6000);
    await page.click(`div[role="option"]:has-text("${user2}")`);
    await page.waitForTimeout(6000);
    await page.fill('input[placeholder="Ex: MAT-001"]', testMatricula);

    // Try to mark as Owner again
    await ownerCheckbox.check();

    await page.click('button:has-text("Criar Matrícula")');
    
    // ✅ NEW BEHAVIOR: The assignment should succeed because we now support ownership transfer!
    // Verify the modal closes and the second user is now the owner
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
    
    // Verify User 2 is now owner — filter by name (table shows name, not email)
    const user2Row = page.locator('tr', { hasText: testMatricula }).filter({ hasText: user2Name });
    await expect(user2Row).toContainText('Proprietário', { timeout: 10000 });
    
    // Verify User 1 is no longer owner
    const user1RowAfter = page.locator('tr', { hasText: testMatricula }).filter({ hasText: user1Name });
    await expect(user1RowAfter).not.toContainText('Proprietário');
    
    console.log(`>>> Success: Ownership was successfully transferred to ${user2Name}.`);

    // Cleanup: Delete all matricula records created for this test.
    // We wait for the count to decrease after each deletion to handle multiple rows.
    const testRows = page.locator('tr', { hasText: testMatricula });
    let count = await testRows.count();
    
    while (count > 0) {
      console.log(`>>> Cleaning up: ${count} rows remaining for ${testMatricula}`);
      await testRows.first().locator('button[title="Excluir"], .tabler-icon-trash').click();
      await page.getByRole('dialog').getByRole('button', { name: 'Excluir' }).click();
      
      // Wait for the count to decrease
      await expect(testRows).toHaveCount(count - 1, { timeout: 10000 });
      count = await testRows.count();
    }

    await expect(page.locator('tr', { hasText: testMatricula })).not.toBeVisible();
    console.log('>>> Cleanup complete.');
  });
});
