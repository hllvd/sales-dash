/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';

test.describe('Contract Status Update Flow', () => {
  test('should update contract statuses via CSV import', async ({ page }) => {
    test.setTimeout(90000);

    // Clear potentially stale filters from localStorage
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });

    const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);

    // 1. Login
    await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15000 });
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Navigate to Contracts page
    await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15000 });
    await page.click('a[href="#/contracts"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(2000);

    // 3. Open Bulk Import Modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible();

    // Select the standard "Contracts" template (ID 2) explicitly 
    // as the default has been changed to Dashboard (ID 3)
    // const templateSelect = page.locator('select#templateSelection');
    // if (await templateSelect.isVisible()) {
    //   await templateSelect.selectOption('2');
    // }

    // 4. Upload the update CSV
    const updateFile = getTestDataPath('contracts-update-status.csv');
    await page.setInputFiles('input#file', updateFile);

    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10000 });
    await nextBtn.click();

    // Handle "Mismatch" warning step if it appears (common for update-only CSVs)
    const proceedAnywayBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch (e) {
      // Ignore if not present
    }

    // 5. Mapping Step — auto-mapping handles all columns correctly
    // The backend already maps: Contrato→ContractNumber, Email→UserEmail, Valor→TotalAmount, STATUS→Status
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15000 });

    // Confirm import (auto-mapping is correct, button should already be enabled)
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 25000 });
    await confirmBtn.click();

    // DEBUG: capture any error shown in the modal before timing out
    await page.waitForTimeout(5000);
    const errorDiv = page.locator('.error-message');
    if (await errorDiv.isVisible()) {
      const errorText = await errorDiv.textContent();
      console.log('[DEBUG] Modal error after confirm:', errorText);
    }

    // DEBUG: log all mapped selects to see what the frontend sent
    const mappingSelects = page.locator('.mapping-row select');
    const mappingCount = await mappingSelects.count();
    for (let i = 0; i < mappingCount; i++) {
      const label = await page.locator('.mapping-row').nth(i).locator('strong').textContent();
      const value = await mappingSelects.nth(i).inputValue();
      if (value) console.log(`[DEBUG] Mapping: ${label} → ${value}`);
    }

    // 6. Wait for result — accept any processed row count
    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30000 });

    // Log actual result for debugging
    const resultText = await page.getByText(/Importados:/).textContent();
    console.log('Import result:', resultText);

    await page.click('button:has-text("Fechar")');

    // 7. Verify status in the table
    // Ensure filters are clear
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(1000);
    }

    // Filter for contract 826650 (CSV status: "Ativa" → should map to "Active" → display "Ativo")
    await page.fill('input#filterContractNumber', '826650');
    await page.waitForTimeout(6000); // 3s debounce + safety buffer

    const row826650 = page.locator('table tbody tr', { hasText: '826650' });
    await expect(row826650).toBeVisible({ timeout: 10000 });
    await expect(row826650.locator('.mantine-Badge-label')).toHaveText('Ativo', { timeout: 10000 });

    // Filter for contract 821590 (CSV status: "Cancelado" → should map to "Defaulted" → display "Cancelado")
    await page.click('button.clear-filters-btn');
    await page.waitForTimeout(2000);
    await page.fill('input#filterContractNumber', '821590');
    await page.waitForTimeout(6000);

    const row821590 = page.locator('table tbody tr', { hasText: '821590' });
    await expect(row821590).toBeVisible({ timeout: 10000 });
    await expect(row821590.locator('.mantine-Badge-label')).toHaveText('Cancelado', { timeout: 10000 });
  });
});
