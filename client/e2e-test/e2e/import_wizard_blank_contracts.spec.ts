import { test, expect, type Page } from '@playwright/test';
import path from 'path';
import { loginAs } from './helpers/auth';

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

async function loginAndGoToWizard(page: Page): Promise<void> {
  await loginAs(page);
  await page.goto('/#/import-wizard');
  await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible();
}


test.describe('[TEAR 4] Import Wizard — Blank and Short Contract Number Pre-Validation', () => {
  test.describe.configure({ mode: 'serial' });

  test('should hard block advancement when blank contract numbers are detected', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAndGoToWizard(page);

    // 1. Upload the contracts file with empty contract numbers
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('contracts_with_blanks.xlsx'));

    // Trigger upload validation
    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // Verify RED hard block Alert is visible
    const blankAlert = page.locator('[data-testid="blank-contracts-warning"]');
    await expect(blankAlert).toBeVisible({ timeout: 20_000 });
    await expect(blankAlert).toContainText('Detectamos 1 contrato(s) sem nenhum número.');
    await expect(blankAlert).toContainText('Por favor, preencha a planilha e faça o upload novamente.');

    // Confirm that the advance button is NOT visible or enabled
    await expect(page.locator('button:has-text("Avançar para Passo 2")')).not.toBeVisible();
    await expect(page.locator('button:has-text("Próximo Passo")')).not.toBeVisible();

    // 2. Clear state by uploading a different file in place (the short contracts one)
    await step1Input.setInputFiles(getTestDataPath('contracts_with_short_numbers.xlsx'));

    // Verify red alert is cleared immediately upon file change
    await expect(blankAlert).not.toBeVisible();
  });

  test('should warn and soft block advancement when short contract numbers are detected', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAndGoToWizard(page);

    // Upload the contracts file with short contract numbers
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('contracts_with_short_numbers.xlsx'));

    // Trigger upload validation
    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // Verify ORANGE soft block Alert is visible
    const shortAlert = page.locator('[data-testid="short-contracts-warning"]');
    await expect(shortAlert).toBeVisible({ timeout: 20_000 });
    
    // Check that it lists the two short contract numbers we created: "12" and "999"
    await expect(shortAlert).toContainText('12');
    await expect(shortAlert).toContainText('999');

    // Confirm the advance button is not visible initially
    const advanceBtn = page.locator('#btn-advance-with-warnings');
    await expect(advanceBtn).not.toBeVisible();

    // Check the "Agree" checkbox
    await page.locator('#wiz-allow-shorts').check();

    // Confirm that the advance button is now visible
    await expect(advanceBtn).toBeVisible({ timeout: 5_000 });
    await advanceBtn.click();

    // Verify it advances to Step 2
    const step2Content = page.getByText('O sistema identificou os vendedores no arquivo');
    await expect(step2Content).toBeVisible({ timeout: 20_000 });
  });
});
