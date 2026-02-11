/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';

test.describe('Import Wizard Flow', () => {
  test('should complete the full import process and verify retention metric', async ({ page }) => {
    // Set timeout for this specific test
    test.setTimeout(50000);
    // Helper to get absolute path for test data
    const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);

    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Verify no contracts are present initially
    await page.click('a[href="#/contracts"]');
    await expect(page.getByRole('heading', { name: 'Contratos' })).toBeVisible();
    
    // Check if table is empty
    const rows = page.locator('table tbody tr');
    // It might take a moment to load
    await expect(rows).toHaveCount(0, { timeout: 10000 });

    // 3. Go to Import Wizard
    await page.click('a[href="#/import-wizard"]');
    await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible();

    // 4. Step 1: Upload historical contracts
    const historicalFile = getTestDataPath('historical_contracts.xlsx');
    await page.waitForTimeout(10000); 
    await page.setInputFiles('input[type="file"]', historicalFile);
    await page.click('button:has-text("Próximo Passo")');

    // 5. Step 2: Upload filled users.csv
    await page.waitForTimeout(10000); 
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible();
    const usersFile = getTestDataPath('users-demo.csv');
    await page.setInputFiles('input[type="file"]', usersFile);
    await page.click('button:has-text("Importar Usuários e Avançar")');

    // 6. Navigate to Contracts for final import
    await expect(page.getByText('Download de Contratos')).toBeVisible({ timeout: 15000 });
    await page.click('button:has-text("Ir para Mapeamento")');

    // 7. Bulk Import Modal
    await expect(page.getByRole('heading', { name: 'Contratos' })).toBeVisible();
    await page.click('button:has-text("Importar")');
    
    const finalContractFile = getTestDataPath('contract.csv');
    await page.setInputFiles('input#file', finalContractFile);
    
    // Select template "Dashboard" (ID 3 matches ContractDashboard)
    await page.selectOption('select#templateSelection', { label: 'Dashboard' }); 
    await page.click('button:has-text("Próximo")');

    // Mappings
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 10000 });
    // Aggressive wait for auto-mapping to settle
    await page.waitForTimeout(1000); 
    await page.click('button:has-text("Confirmar e Importar")');

    // 8. Verify Retention Metric
    await expect(page.locator('.success-message')).toBeVisible({ timeout: 60000 });
    await page.click('button:has-text("Fechar")');

    // Verify 95.03% on Contracts page
    await expect(page.locator('.aggregation-summary')).toBeVisible();
    await expect(page.locator('.aggregation-chart')).toContainText('95.03%', { timeout: 10000 });
  });
});
