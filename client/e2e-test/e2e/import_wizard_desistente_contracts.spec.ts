/// <reference types="node" />
import { test, expect, type Page } from '@playwright/test';
import path from 'path';

// ── Helpers ──────────────────────────────────────────────────────────────────

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

async function loginAndGoToWizard(page: Page): Promise<void> {
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
  await expect(page.locator('a[href="#/import-wizard"]')).toBeVisible({ timeout: 15_000 });
  await page.click('a[href="#/import-wizard"]');
  await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible();
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

    // ── Assert warning is visible on Step 1 and blocks advancement ───────────────
    const desistenteAlert = page.locator('[data-testid="desistente-warning"]');
    await expect(desistenteAlert).toBeVisible({ timeout: 20_000 });
    await expect(desistenteAlert).toContainText('We\'ve detected some contract with status "desistente", we won\'t import it');
    await expect(desistenteAlert).toContainText('TEST-DES-001');

    // "Avançar para Passo 2" should not be visible until checkbox is checked
    const advanceBtn = page.locator('button:has-text("Avançar para Passo 2")');
    await expect(advanceBtn).not.toBeVisible();

    // Check the box
    const allowCheckbox = page.locator('#wiz-allow-desistentes');
    await expect(allowCheckbox).toBeVisible();
    await allowCheckbox.check();
    await expect(allowCheckbox).toBeChecked();

    // Now advance should be visible
    await expect(advanceBtn).toBeVisible();
    await advanceBtn.click();

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

    // Verify desistente skipped alert shows
    const skippedAlert = page.locator('[data-testid="desistente-skipped-warning"]');
    await expect(skippedAlert).toBeVisible({ timeout: 10_000 });
    await expect(skippedAlert).toContainText('TEST-DES-001');

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
    await page.waitForTimeout(6000); // safety buffer for search debounce
    const rowOk = page.locator('table tbody tr', { hasText: 'TEST-OK-002' });
    await expect(rowOk).toBeVisible({ timeout: 10_000 });

    // Clear filters and search for TEST-DES-001 (should NOT exist)
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(1000);
    }
    
    await page.fill('input#filterContractNumber', 'TEST-DES-001');
    await page.waitForTimeout(6000); // debounce wait
    const rowDesistente = page.locator('table tbody tr', { hasText: 'TEST-DES-001' });
    await expect(rowDesistente).not.toBeVisible();
  });
});
