/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

/**
 * [TEAR-2] Contract Dashboard Import Parameterized Update Options
 * Tests the updateMatriculaOnExisting and updateTotalAmountOnExisting checkboxes.
 */

const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };

async function login(page: Page) {
  await page.goto('/');
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await page.goto('/');
  await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15000 });
  await page.fill('input[type="email"]', ADMIN.email);
  await page.fill('input[type="password"]', ADMIN.password);
  await page.click('button.login-button');
  await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15000 });
}

test.describe('Contract Dashboard Import Update Options', () => {
  test.describe.configure({ mode: 'serial' });

  test('Checkboxes for Matricula and TotalAmount update exist and behave correctly', async ({ page }) => {
    await login(page);

    // Navigate to Contracts page
    await page.click('a[href="#/contracts"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(2000);

    // Open import modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10000 });

    // Create a temporary dummy CSV file for contractDashboard
    const tempCsvPath = path.join(__dirname, '../temp/test_update_options.csv');
    if (!fs.existsSync(path.dirname(tempCsvPath))) {
      fs.mkdirSync(path.dirname(tempCsvPath), { recursive: true });
    }
    fs.writeFileSync(tempCsvPath, 'Cota,Total,SaleStartDate,Status,Matricula\nG1;999;C1;Cust1;CNT-E2E-OPT-1,1000,2024-01-01,Active,MAT-E2E-OPT-1\n');

    // Upload file
    await page.setInputFiles('input#file', tempCsvPath);

    // Click Next ("Próximo")
    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10000 });
    await nextBtn.click();

    // Handle optional mismatch step
    const proceedBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedBtn.isVisible({ timeout: 3000 })) {
        await proceedBtn.click();
      }
    } catch { /* ignore if not present */ }

    // Wait for step 2 (mapping step)
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('#updateMatriculaOnExisting')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('#updateTotalAmountOnExisting')).toBeVisible({ timeout: 15000 });

    // Assert default states
    const updateMatriculaCb = page.locator('#updateMatriculaOnExisting');
    const updateTotalAmountCb = page.locator('#updateTotalAmountOnExisting');

    await expect(updateMatriculaCb).not.toBeChecked(); // Default OFF
    await expect(updateTotalAmountCb).toBeChecked();    // Default ON

    // Clean up temporary file
    if (fs.existsSync(tempCsvPath)) {
      fs.unlinkSync(tempCsvPath);
    }
  });
});
