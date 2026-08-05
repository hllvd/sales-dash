/// <reference types="node" />
import { test, expect, type Page } from '@playwright/test';
import path from 'path';

// ── Helpers ──────────────────────────────────────────────────────────────────

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

async function loginAndGoToWizard(page: Page): Promise<void> {
  await page.goto('/#/import-wizard');
  await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible({ timeout: 15_000 });
}


// ── Test Suite ────────────────────────────────────────────────────────────────

test.describe('Import Wizard — Desistente Status Handling', () => {
  test.describe.configure({ mode: 'serial' });

  test('should detect desistente status contracts, warn, skip, and omit them from contract lists', async ({ page }) => {
    test.setTimeout(120_000);

    // ── Step 1: Upload contracts file with DESISTENTE status row ──────────────────
    await loginAndGoToWizard(page);

    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('contracts_with_desistente.csv'));

    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // ── Assert warning is NOT visible on Step 1 and user advances directly to Step 2 ───────────────
    const desistenteAlert = page.locator('[data-testid="desistente-warning"]');
    await expect(desistenteAlert).not.toBeVisible({ timeout: 5_000 });

    // ── Step 2: Upload users file ────────────────────────────────────────────────
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible({ timeout: 10_000 });
    const step2Input = page.locator('#wizard-step2-input');
    await expect(step2Input).toBeAttached();
    await step2Input.setInputFiles(getTestDataPath('users-demo.csv'));
    await page.click('button:has-text("Importar Usuários e Avançar")');

    // ── Step 3: Configure and Import ─────────────────────────────────────────────
    await expect(page.getByText('Opções de Importação')).toBeVisible({ timeout: 20_000 });
    await page.click('button:has-text("Importar Contratos")');

    // Wait for the results screen
    await expect(
      page.locator('.mantine-Alert-root').filter({ hasText: /Contratos importados|Importação com erros/ })
    ).toBeVisible({ timeout: 60_000 });

    // Verify desistente skipped alert does NOT show
    const skippedAlert = page.locator('[data-testid="desistente-skipped-warning"]');
    await expect(skippedAlert).not.toBeVisible({ timeout: 5_000 });

    // ── Verification: Contracts list ────────────────────────────────────────────
    await page.click('button:has-text("Ir para Lista de Contratos")');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });

    // Clear filters if any are active
    const clearFiltersBtn = page.locator('button.clear-filters-btn');
    if (await clearFiltersBtn.isVisible()) {
      await clearFiltersBtn.click();
      await page.waitForTimeout(1000);
    }

    // Search for TEST-OK-002 (should exist)
    await page.fill('input#filterContractNumber', 'TEST-OK-002');
    await page.waitForTimeout(1000);
    const rowOk = page.locator('table tbody tr', { hasText: 'TEST-OK-002' });
    await expect(rowOk).toBeVisible({ timeout: 10_000 });

    // Clear filters and search for TEST-DES-001 without status filter (should NOT exist in default list)
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(1000);
    }
    
    await page.fill('input#filterContractNumber', 'TEST-DES-001');
    await page.waitForTimeout(1000);
    const rowDesistente = page.locator('table tbody tr', { hasText: 'TEST-DES-001' });
    await expect(rowDesistente).not.toBeVisible();

    // Select Desistente status filter for SuperAdmin
    const statusSelect = page.locator('input#filterStatus');
    await statusSelect.click();
    await page.click('.mantine-MultiSelect-option:has-text("Desistente"), .mantine-Select-option:has-text("Desistente")');

    // Now TEST-DES-001 should be visible
    await expect(rowDesistente).toBeVisible({ timeout: 10_000 });
  });
});
