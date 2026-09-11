/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';

test.describe('Import Wizard Flow', () => {
  test('should complete the full import process including contract import from wizard', async ({ page }) => {
    test.setTimeout(120_000);

    const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);

    // ── 1. Navigate directly to Import Wizard (pre-authenticated) ───────────
    await page.goto('/#/import-wizard');
    await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible({ timeout: 15000 });

    // ── 3. Step 1: Upload historical contracts file ───────────────────────────
    const historicalFile = getTestDataPath('historical_contracts.xlsx');
    await page.locator('#wizard-step1-input').setInputFiles(historicalFile);

    // Click "Próximo Passo" to upload and process Step 1
    const nextStepBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextStepBtn).toBeVisible({ timeout: 15000 });
    await nextStepBtn.click();

    // Handle possible 'Modelo Divergente' or 'Avançar para Passo 2' warnings
    const mismatchProceed = page.locator('button:has-text("Prosseguir assim mesmo")');
    const advanceWithWarningsBtn = page.locator('button:has-text("Avançar para Passo 2")');

    await Promise.race([
      page.getByText('Preenchimento de Usuários').waitFor({ state: 'visible', timeout: 15000 }).catch(() => {}),
      mismatchProceed.waitFor({ state: 'visible', timeout: 10000 }).catch(() => {}),
      advanceWithWarningsBtn.waitFor({ state: 'visible', timeout: 10000 }).catch(() => {})
    ]);

    if (await mismatchProceed.isVisible().catch(() => false)) {
      await mismatchProceed.click();
    } else if (await advanceWithWarningsBtn.isVisible().catch(() => false)) {
      await advanceWithWarningsBtn.click();
    }

    // ── 4. Step 2: Upload filled users file ───────────────────────────────────
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible({ timeout: 30000 });
    const usersFile = getTestDataPath('users-demo.csv');
    // Target the specific Step 2 file input ID
    await page.locator('#wizard-step2-input').setInputFiles(usersFile);

    const advanceBtnStep2 = page.locator('button:has-text("Importar Usuários e Avançar")');
    await expect(advanceBtnStep2).toBeEnabled({ timeout: 15000 });
    await advanceBtnStep2.click();

    // ── 5. Step 3: Import contracts directly from wizard ─────────────────────
    // Wait for the Step 3 content to be visible (Opções de Importação)
    await expect(page.getByText('Opções de Importação')).toBeVisible({ timeout: 60000 });

    // Verify the import options checkboxes are visible and ON by default
    await expect(page.locator('#wiz-skip-missing')).toBeChecked({ timeout: 10000 });
    await expect(page.locator('#wiz-auto-groups')).toBeChecked({ timeout: 10000 });
    await expect(page.locator('#wiz-auto-pvs')).toBeChecked({ timeout: 10000 });

    // Click "Importar Contratos" — this generates the temp file and runs the import
    const importContractsBtn = page.locator('button:has-text("Importar Contratos")');
    await importContractsBtn.click();

    // Explicitly wait for step3-import API response
    await page.waitForResponse(
      resp => resp.url().includes('/api/wizard/step3-import/') && resp.status() === 200,
      { timeout: 60000 }
    );

    // ── 6. Wait for result ────────────────────────────────────────────────────
    await expect(
      page.locator('.mantine-Alert-root').filter({ hasText: /Contratos importados|Importação com erros/ })
    ).toBeVisible({ timeout: 60000 });

    // ── 7. Navigate to Contracts page and verify ──────────────────────────────
    const goToContractsBtn = page.locator('button:has-text("Ir para Lista de Contratos")');
    await expect(goToContractsBtn).toBeVisible({ timeout: 15000 });
    await goToContractsBtn.click();
    await page.goto('/#/contracts');

    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 20000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 20000 });
    await expect(page.locator('table tbody tr').first()).toBeVisible({ timeout: 20000 });

    // Verify aggregation chart (Smoke Check)
    await expect(page.locator('.aggregation-summary')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('.aggregation-chart').first()).toContainText(/[0-9]+%/, { timeout: 15000 });
  });
});
