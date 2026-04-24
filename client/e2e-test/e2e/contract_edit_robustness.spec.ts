import { test, expect } from '@playwright/test';

test.describe('Contract Edit Robustness', () => {
  test.beforeEach(async ({ page }) => {
    // Login as Superadmin for full access
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');
    // Wait for initial load
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15000 });
  });

  test('should open edit contract modal without crashing even if duplicate users/matriculas exist', async ({ page }) => {
    // Catch console errors
    const errors: string[] = [];
    page.on('console', msg => {
      if (msg.type() === 'error') errors.push(msg.text());
    });

    // Go to Contracts page directly
    await page.goto('/#/contracts');
    
    // Wait for page to load
    await expect(page.getByText('Gerenciamento de Contratos')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15000 });
    
    // If no contracts exist, create one first
    if (await page.locator('.contracts-empty').isVisible()) {
      await page.click('button:has-text("Criar")');
      await page.fill('input[required]', `TEST-${Date.now()}`);
      await page.fill('input[type="date"]', '2024-01-01');
      await page.locator('input[inputmode="decimal"]').fill('1000');
      await page.click('button[type="submit"]');
      await expect(page.getByText('Contrato criado com sucesso')).toBeVisible();
      await expect(page.locator('table')).toBeVisible({ timeout: 10000 });
    }

    // Click Edit on the first contract
    await page.locator('button[title="Editar"]').first().click();

    // Verification 1: The modal should render correctly (proves the fix works)
    await expect(page.getByRole('heading', { name: 'Editar Contrato' }).first()).toBeVisible();

    // Verification 2: Open the "Vendedor" dropdown
    const vendedorSelect = page.getByPlaceholder('Selecione o vendedor');
    await vendedorSelect.click();

    // Verification 3: Check for options (specifically the one in the dropdown)
    // We target the default option to avoid ambiguity with background filters
    const option = page.getByRole('option', { name: 'Sem vendedor atribuído' });
    await expect(option).toBeVisible({ timeout: 10000 });
    
    // Verification 4: Ensure no Mantine "Duplicate options" error was thrown
    const duplicateError = errors.find(e => e.includes('Duplicate options are not supported'));
    expect(duplicateError).toBeUndefined();

    // Select an option and save
    await option.click();
    await page.click('button:has-text("Salvar Alterações")');

    // Final verification
    await expect(page.getByText('Contrato atualizado com sucesso')).toBeVisible();
  });
});
