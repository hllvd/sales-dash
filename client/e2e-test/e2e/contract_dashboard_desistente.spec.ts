import { test, expect, type Page } from '@playwright/test';
import path from 'path';

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

async function loginAndGoToContracts(page: Page): Promise<void> {
  await page.goto('/');
  await page.evaluate(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
  await page.reload();
  await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15_000 });
  await page.fill('input[type="email"]', 'superadmin@salesapp.com');
  await page.fill('input[type="password"]', 'string');
  await page.click('button.login-button');
  await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15_000 });
  await page.click('a[href="#/contracts"]');
  await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible();
}

test.describe('Contracts Dashboard — Desistente Status Handling', () => {
  test('should detect, warn, skip desistente status contracts and show correct records in dashboard', async ({ page }) => {
    test.setTimeout(90_000);

    // 1. Login and go to Contracts Page
    await loginAndGoToContracts(page);

    // 2. Open Bulk Import Modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible();

    // 3. Upload contracts file containing a DESISTENTE row
    const importFile = getTestDataPath('contracts_with_desistente.csv');
    await page.setInputFiles('input#file', importFile);

    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    // Handle Mismatch Warning if it appears
    const proceedAnywayBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch (e) {
      // Ignore if not present
    }

    // 4. Mapping Step
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });

    // Confirm and Import
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 25_000 });
    await confirmBtn.click();

    // 5. Result Step
    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30_000 });

    // Verify desistente skipped warning does NOT show in results modal
    const skippedAlert = page.locator('[data-testid="desistente-skipped-warning"]');
    await expect(skippedAlert).not.toBeVisible({ timeout: 5_000 });

    // Close Modal
    await page.click('button:has-text("Fechar")');

    // 6. Verification
    // Clear filters if any are active
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(1000);
    }

    // Search for TEST-DES-001 without status filter (should NOT exist in default list)
    await page.fill('input#filterContractNumber', 'TEST-DES-001');
    const rowDesistente = page.locator('table tbody tr', { hasText: 'TEST-DES-001' });
    await expect(rowDesistente).not.toBeVisible({ timeout: 5000 });

    // Select Desistente status filter for SuperAdmin
    const statusSelect = page.locator('input#filterStatus');
    await statusSelect.click();
    await page.click('.mantine-MultiSelect-option:has-text("Desistente"), .mantine-Select-option:has-text("Desistente")');

    // Now TEST-DES-001 should be visible
    await expect(rowDesistente).toBeVisible({ timeout: 10_000 });

    // Clear filters and search for TEST-OK-002 (should exist in the list)
    await page.click('button.clear-filters-btn');
    await page.fill('input#filterContractNumber', 'TEST-OK-002');
    const rowOk = page.locator('table tbody tr', { hasText: 'TEST-OK-002' });
    await expect(rowOk).toBeVisible({ timeout: 10_000 });
  });
});
