import { test, expect, type Page } from '@playwright/test';
import path from 'path';

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

test.describe('[TEAR 2] Import Wizard — Outlier Amount Pre-Validation Warning', () => {
  test.describe.configure({ mode: 'serial' });

  test('should display yellow warning alert when ambiguous total amounts are detected in Excel', async ({ page }) => {
    test.setTimeout(60_000);
    await loginAndGoToWizard(page);

    // 1. Upload the contracts file with ambiguous total amount (e.g. 80.000.00)
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('contracts_with_outliers.xlsx'));

    // Trigger upload validation
    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // Verify YELLOW outlier warning Alert is visible
    const outlierAlert = page.locator('[data-testid="outlier-amounts-warning"]');
    await expect(outlierAlert).toBeVisible({ timeout: 20_000 });
    await expect(outlierAlert).toContainText('Valores com Formato Ambíguo no Campo Total');
    await expect(outlierAlert).toContainText('80.000.00');

    // Confirm that the warning details the likely formatted value (R$ 80.000,00)
    await expect(outlierAlert).toContainText('80.000,00');
  });
});
