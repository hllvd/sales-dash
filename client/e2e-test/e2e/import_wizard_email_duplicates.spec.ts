import { test, expect, type Page } from '@playwright/test';
import path from 'path';
import { loginAs } from './helpers/auth';

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

test.describe('Import Wizard Duplicate Email Validation', () => {

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

  test('should show validation error when different users share the same email', async ({ page }) => {
    test.setTimeout(60_000);

    await loginAndGoToWizard(page);
    await uploadContractsStep1(page);

    // Step 2: Upload file with duplicate email for different user names
    const step2Content = page.getByText('O sistema identificou os vendedores no arquivo');
    await expect(step2Content).toBeVisible({ timeout: 30_000 });
    const step2Input = page.locator('#wizard-step2-input');
    await expect(step2Input).toBeAttached({ timeout: 30_000 });

    await step2Input.setInputFiles(getTestDataPath('duplicate_emails_different_names.csv'));

    const importBtn = page.locator('button:has-text("Importar Usuários e Avançar")');
    await expect(importBtn).toBeEnabled({ timeout: 10_000 });
    await importBtn.click();

    // Verify Error Alert (Red)
    const alert = page.locator('[data-testid="duplicate-email-error"]');
    await expect(alert).toBeVisible({ timeout: 15_000 });
    await expect(alert).toContainText('E-mails Duplicados com Nomes Diferentes');
    await expect(alert).toContainText("O e-mail 'duplicate@test.com' está associado a múltiplos usuários: Jane Smith, John Doe.");
  });

  test('should allow proceeding when same email shares the same user name', async ({ page }) => {
    test.setTimeout(60_000);

    await loginAndGoToWizard(page);
    await uploadContractsStep1(page);

    // Step 2: Upload file with duplicate email for same user name
    const step2Content = page.getByText('O sistema identificou os vendedores no arquivo');
    await expect(step2Content).toBeVisible({ timeout: 30_000 });
    const step2Input = page.locator('#wizard-step2-input');
    await expect(step2Input).toBeAttached({ timeout: 30_000 });

    await step2Input.setInputFiles(getTestDataPath('duplicate_emails_same_name.csv'));

    const importBtn = page.locator('button:has-text("Importar Usuários e Avançar")');
    await expect(importBtn).toBeEnabled({ timeout: 10_000 });
    await importBtn.click();

    // Verify it proceeds to Step 3 (Download Contracts step)
    const step3Content = page.getByText('Baixar contracts.xlsx');
    await expect(step3Content).toBeVisible({ timeout: 30_000 });
  });
});
