import { test, expect } from '@playwright/test';

test.describe('Stores CRUD Management (TEAR 2)', () => {
  test.describe.configure({ mode: 'serial' });

  const testStoreName = `Loja Teste E2E ${Date.now()}`;
  const updatedStoreName = `${testStoreName} Alt`;

  test('should create, edit, and delete a store as superadmin', async ({ page }) => {
    test.setTimeout(60000);

    // 1. Navigate to Stores page
    await page.goto('/#/stores');
    await expect(page.getByRole('heading', { name: 'Lojas' })).toBeVisible({ timeout: 15000 });

    // 2. Click "Nova Loja"
    await page.click('button:has-text("Nova Loja")');
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();

    // 3. Fill store form
    await page.fill('input[placeholder="Ex: BALNEARIO CAMBORIU"]', testStoreName);

    // Select state "SC"
    await modal.locator('.mantine-Select-input').click();
    await page.click('div[role="option"]:has-text("Santa Catarina (SC)")');

    // Submit form
    await page.click('button:has-text("Criar Loja")');

    // Modal should close and store should appear in table
    await expect(modal).not.toBeVisible({ timeout: 10000 });
    await expect(page.locator('tr', { hasText: testStoreName })).toBeVisible({ timeout: 15000 });

    // 4. Edit Store
    const row = page.locator('tr', { hasText: testStoreName });
    await row.locator('button[title="Edit"], button:has(.tabler-icon-edit)').click();
    await expect(modal).toBeVisible();

    // Change name
    await page.fill('input[placeholder="Ex: BALNEARIO CAMBORIU"]', updatedStoreName);
    await page.click('button:has-text("Salvar Alterações")');
    await expect(modal).not.toBeVisible({ timeout: 10000 });

    // Verify updated store in table
    await expect(page.locator('tr', { hasText: updatedStoreName })).toBeVisible({ timeout: 15000 });

    // 5. Delete Store
    const updatedRow = page.locator('tr', { hasText: updatedStoreName });
    await updatedRow.locator('button[title="Delete"], button:has(.tabler-icon-trash)').click();

    // Confirm deletion modal
    const confirmModal = page.getByRole('dialog', { name: 'Confirmar Exclusão' });
    await expect(confirmModal).toBeVisible();
    await confirmModal.locator('button:has-text("Excluir")').click();

    // Verify removal
    await expect(page.locator('tr', { hasText: updatedStoreName })).not.toBeVisible({ timeout: 10000 });
  });
});
