/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';

test.describe('Import Wizard Flow', () => {
  test('should complete the full import process including contract import from wizard', async ({ page }) => {
    test.setTimeout(120_000);

    const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);

    // ── 1. Navigate directly to Import Wizard (pre-authenticated) ───────────
    await page.goto('/#/import-wizard');
    await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible();


    // ── 3. Step 1: Upload historical contracts file ───────────────────────────
    const historicalFile = getTestDataPath('historical_contracts.xlsx');
    await page.waitForTimeout(3000);
    await page.setInputFiles('input[type="file"]', historicalFile);
    await page.click('button:has-text("Próximo Passo")');

    // ── 4. Step 2: Upload filled users file ───────────────────────────────────
    await page.waitForTimeout(5000);
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible();
    const usersFile = getTestDataPath('users-demo.csv');
    await page.setInputFiles('input[type="file"]', usersFile);
    await page.click('button:has-text("Importar Usuários e Avançar")');

    // ── 5. Step 3: Import contracts directly from wizard ─────────────────────
    // Wait for the Step 3 content to be visible (Opções de Importação)
    await expect(page.getByText('Opções de Importação')).toBeVisible({ timeout: 20000 });

    // Verify the import options checkboxes are visible and ON by default
    await expect(page.locator('#wiz-skip-missing')).toBeChecked();
    await expect(page.locator('#wiz-auto-groups')).toBeChecked();
    await expect(page.locator('#wiz-auto-pvs')).toBeChecked();

    // Click "Importar Contratos" — this generates the temp file and runs the import
    await page.click('button:has-text("Importar Contratos")');

    // ── 6. Wait for result ────────────────────────────────────────────────────
    await expect(
      page.locator('.mantine-Alert-root').filter({ hasText: /Contratos importados|Importação com erros/ })
    ).toBeVisible({ timeout: 60000 });

    // ── 7. Navigate to Contracts page and verify ──────────────────────────────
    await page.click('button:has-text("Ir para Lista de Contratos")');
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 20000 });
    await expect(page.locator('table tbody tr').first()).toBeVisible({ timeout: 20000 });

    // Verify aggregation chart (Smoke Check)
    await expect(page.locator('.aggregation-summary')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('.aggregation-chart').first()).toContainText(/[0-9]+%/, { timeout: 15000 });
  });
});
