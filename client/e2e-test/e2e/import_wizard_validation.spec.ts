import { test, expect, type Page } from '@playwright/test';
import path from 'path';
import { loginAs } from './helpers/auth';

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

test.describe('Import Wizard Validation', () => {

  /**
   * Helper: log in as superadmin and navigate to the Import Wizard.
   */
  async function loginAndGoToWizard(page: Page) {
    await loginAs(page);
    await page.goto('/#/import-wizard');
  }


  /**
   * Helper: step 1 — upload the contracts xlsx.
   */
  async function uploadContractsStep1(page: Page) {
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('historical_contracts.xlsx'));
    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 15_000 });
    await nextBtn.click();

    // Handle possible 'Modelo Divergente' warning
    const prosseguirBtn = page.locator('button:has-text("Prosseguir assim mesmo")');
    if (await prosseguirBtn.isVisible({ timeout: 5000 }).catch(() => false)) {
      await prosseguirBtn.click();
    }
  }

  test('should show validation error when mandatory fields are missing in Step 2', async ({ page }) => {
    test.setTimeout(60_000);

    await loginAndGoToWizard(page);
    await uploadContractsStep1(page);

    // Step 2: Upload file with missing fields
    const step2Content = page.getByText('O sistema identificou os vendedores no arquivo');
    await expect(step2Content).toBeVisible({ timeout: 30_000 });
    const step2Input = page.locator('#wizard-step2-input');
    await expect(step2Input).toBeAttached({ timeout: 30_000 });

    // We expect users_missing_fields.xlsx to have rows with missing Name/Email/Matricula
    await step2Input.setInputFiles(getTestDataPath('users_missing_fields.xlsx'));

    const importBtn = page.locator('button:has-text("Importar Usuários e Avançar")');
    await expect(importBtn).toBeEnabled({ timeout: 10_000 });
    await importBtn.click();

    // Verify Warning Alert (Orange) - filter by text to avoid ambiguity
    const alert = page.getByRole('alert').filter({ hasText: 'Nenhum usuário importado' });
    await expect(alert).toBeVisible({ timeout: 15_000 });
    await expect(alert).toContainText('Nenhum usuário importado');
    await expect(alert).toContainText('Verifique se as colunas Nome, Email e Matricula estão preenchidas');

  });
});
