/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';

test.describe('Contract Status Update Flow', () => {
  test('should update contract statuses via CSV import', async ({ page }) => {
    // Set timeout for this specific test as it involves complex operations
    test.setTimeout(60000);
    
    // Helper to get absolute path for test data
    const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);

    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Navigate to Contracts page
    await page.click('a[href="#/contracts"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible();

    // 3. Open Bulk Import Modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible();

    // 4. Upload the update CSV
    const updateFile = getTestDataPath('contracts-update-status.csv');
    await page.setInputFiles('input#file', updateFile);
    await page.click('button:has-text("Próximo")');

    // 5. Mapping Step
    // Wait for the mapping screen to appear
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15000 });
    
    // Explicitly mapping fields to ensure the 'Confirmar e Importar' button is enabled
    // We expect 4 columns: Contrato, Email, Valor, STATUS
    // We'll map them to: ContractNumber, UserEmail, TotalAmount, Status
    
    const mapField = async (sourceCol: string, targetField: string) => {
      const row = page.locator('.mapping-row', { hasText: sourceCol });
      await row.locator('select').selectOption(targetField);
    };

    await mapField('Contrato', 'ContractNumber');
    await mapField('Email', 'UserEmail');
    await mapField('Valor', 'TotalAmount');
    await mapField('STATUS', 'Status');

    // Small wait for the state to update
    await page.waitForTimeout(1000); 

    // Confirm and Import
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 5000 });
    await confirmBtn.click();

    // 6. Verify Import Result
    // After clicking mapping, it goes to results. Let's look for "Importados: 2"
    await expect(page.getByText(/Importados: \d+/)).toBeVisible({ timeout: 25000 });
    await page.click('button:has-text("Fechar")');

    // 7. Verify updates in the table
    // We'll use the search filter to check specific contracts
    
    // Check Contract 826650 (CSV says "Ativa" -> UI should show "Ativo")
    await page.fill('input#filterContractNumber', '826650');
    // Wait for debounce (3s in ContractsPage.tsx) + stability
    await page.waitForTimeout(5000);
    // Find the row for 826650 specifically to be safe
    const row826650 = page.locator('table tbody tr', { hasText: '826650' });
    await expect(row826650).toContainText('Ativo', { timeout: 10000 });

    // Check Contract 821590 (CSV says "Cancelado" -> UI should show "Cancelado")
    await page.click('button.clear-filters-btn');
    await page.waitForTimeout(1000);
    await page.fill('input#filterContractNumber', '821590');
    await page.waitForTimeout(5000);
    const row821590 = page.locator('table tbody tr', { hasText: '821590' });
    await expect(row821590).toContainText('Cancelado', { timeout: 10000 });
  });
});
