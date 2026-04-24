import { test, expect } from '@playwright/test';

test.describe('Matricula Ownership Enforcement (TEAR 2)', () => {
  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';
  const user1 = 'carlosmendes@example.com';
  const user2 = 'mariaeduarda@example.com';
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

    // Verify it appeared in the list
    await expect(page.locator('tr', { hasText: testMatricula })).toContainText('Proprietário');
    console.log(`>>> Success: ${user1} is now owner of ${testMatricula}`);

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

    // Expect an error message (this depends on how the backend returns errors)
    // The AppDbContext uses a unique index, so the backend should return a 400 or 500
    // and the frontend should display the error message.
    const errorMessage = page.locator('.error-message, [style*="color: red"]');
    await expect(errorMessage).toBeVisible({ timeout: 10000 });

    console.log('>>> Success: Second owner assignment was blocked as expected.');

    // Cleanup: Delete the first one so test can be re-run
    await page.click('button:has-text("Cancelar")'); // Close modal

    const userRow = page.locator('tr', { hasText: testMatricula });
    await userRow.locator('button[title="Excluir"], .tabler-icon-trash').click();
    await page.click('button:has-text("Confirmar")');

    await expect(userRow).not.toBeVisible();
    console.log('>>> Cleanup complete.');
  });
});
