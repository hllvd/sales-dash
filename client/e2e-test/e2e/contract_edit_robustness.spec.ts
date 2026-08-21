import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Contract Edit Robustness', () => {
  test('should open edit contract modal without crashing even if duplicate users/matriculas exist', async ({ page }) => {
    test.setTimeout(60000);

    // Login as Admin (who can access Contracts page)
    await loginAs(page, 'carlosmendes@example.com', '123456');

    // Go to Contracts page directly
    await page.goto('/#/contracts');
    
    // Wait for page to load
    await expect(page.getByText('Gerenciamento de Contratos')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15000 });
    
    // Clear start date filter so historical contracts are visible
    const startDateInput = page.locator('input#filterStartDate, input[type="date"]').first();
    if (await startDateInput.isVisible().catch(() => false)) {
      await startDateInput.fill('');
      await page.waitForTimeout(500);
    }

    // If no contracts exist, create one first (filling mandatory Matricula field)
    if (await page.locator('.contracts-empty').isVisible().catch(() => false)) {
      await page.click('button:has-text("Criar")');
      await page.fill('input[required]', `TEST-${Date.now()}`);
      await page.fill('input[type="date"]', '2024-01-01');
      await page.locator('input[inputmode="decimal"]').first().fill('1000');
      const matriculaInput = page.locator('input[placeholder*="matricula"], input[placeholder*="Matrícula"]').first();
      if (await matriculaInput.isVisible().catch(() => false)) {
        await matriculaInput.fill('6111');
      }
      await page.click('button[type="submit"]');
      await expect(page.getByText('Contrato criado com sucesso')).toBeVisible({ timeout: 15000 });
      await expect(page.locator('table')).toBeVisible({ timeout: 10000 });
    }

    // Click Edit on the first contract
    await page.locator('button[title="Editar"]').first().click();

    // Verification 1: The modal should render correctly (proves the fix works)
    await expect(page.getByRole('heading', { name: 'Editar Contrato' }).first()).toBeVisible({ timeout: 15000 });

    // Verification 2: Open the "Vendedor" dropdown
    const vendedorSelect = page.getByPlaceholder('Selecione o vendedor');
    await vendedorSelect.click();

    // Verification 3: Check for options (specifically the one in the dropdown)
    await expect(page.locator('.mantine-Select-option').first()).toBeVisible({ timeout: 10000 });
  });
});
