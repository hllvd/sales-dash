/// <reference types="node" />
import { test, expect, type Page } from '@playwright/test';
import path from 'path';

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

test.describe('CSV Delimiter Auto-Detection', () => {
  // Run tests one at a time — they all upload the same large xlsx and share
  // the superadmin account, so running in parallel causes race conditions.
  test.describe.configure({ mode: 'serial' });

  /**
   * Helper: log in as superadmin and navigate to the Import Wizard.
   */
  async function loginAndGoToWizard(page: Page) {
    await page.goto('/');
    await expect(page.locator('button.login-button')).toBeVisible();
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');
    // Wait for post-login navigation before clicking the wizard link
    await expect(page.locator('a[href="#/import-wizard"]')).toBeVisible({ timeout: 15_000 });
    await page.click('a[href="#/import-wizard"]');
    await expect(
      page.getByRole('heading', { name: 'Assistente de Importação Completa' })
    ).toBeVisible({ timeout: 10_000 });
  }

  /**
   * Helper: step 1 — upload the contracts xlsx and wait for "Próximo Passo" to be ready.
   */
  async function uploadContractsStep1(page: Page) {
    // Step 1's FileInput accepts .csv AND .xlsx — use that to distinguish it from Step 2's input
    const step1Input = page.locator('input[type="file"][accept=".csv,.xlsx"]');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('historical_contracts.xlsx'));
    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 15_000 });
    await nextBtn.click();
  }

  // ---------------------------------------------------------------------------
  // Scenario 1: Semicolon-delimited users file (Excel pt-BR save)
  // ---------------------------------------------------------------------------
  test('should accept a semicolon-delimited users CSV (Excel pt-BR behaviour)', async ({ page }) => {
    test.setTimeout(180_000);

    await loginAndGoToWizard(page);
    await uploadContractsStep1(page);

    // Step 2
    // Step 2's FileInput accepts only .csv — use that to target it specifically
    // (Step 1's input is still in the DOM but has accept=".csv,.xlsx")
    const step2Input = page.locator('input[type="file"][accept=".csv"]');
    await expect(step2Input).toBeAttached({ timeout: 10_000 });
    await step2Input.setInputFiles(getTestDataPath('users-semicolon.csv'));
    // Give Mantine's FileInput onChange a moment to update React state
    await page.waitForTimeout(1000);
    await page.locator('button:has-text("Importar Usuários e Avançar")').click();

    // 'Usuários Importados!' is the Alert shown only when step 3 body is active
    await expect(page.getByText('Usuários Importados!')).toBeVisible({ timeout: 60_000 });
  });

  // ---------------------------------------------------------------------------
  // Scenario 2: Comma-delimited users file (regression guard)
  // ---------------------------------------------------------------------------
  test('should still accept a comma-delimited users CSV (regression guard)', async ({ page }) => {
    test.setTimeout(180_000);

    await loginAndGoToWizard(page);
    await uploadContractsStep1(page);

    // Step 2
    // Step 2's FileInput accepts only .csv — use that to target it specifically
    const step2Input = page.locator('input[type="file"][accept=".csv"]');
    await expect(step2Input).toBeAttached({ timeout: 10_000 });
    await step2Input.setInputFiles(getTestDataPath('users-demo.csv'));
    await page.waitForTimeout(1000);
    await page.locator('button:has-text("Importar Usuários e Avançar")').click();

    await expect(page.getByText('Usuários Importados!')).toBeVisible({ timeout: 60_000 });
  });

  // ---------------------------------------------------------------------------
  // Scenario 3: Ambiguous file — import must be BLOCKED
  // ---------------------------------------------------------------------------
  test('should reject a CSV whose delimiter cannot be determined', async ({ page }) => {
    test.setTimeout(120_000);

    await loginAndGoToWizard(page);
    await uploadContractsStep1(page);

    // Step 2
    // Step 2's FileInput accepts only .csv — use that to target it specifically
    const step2Input = page.locator('input[type="file"][accept=".csv"]');
    await expect(step2Input).toBeAttached({ timeout: 10_000 });
    await step2Input.setInputFiles(getTestDataPath('users-ambiguous.csv'));
    await page.waitForTimeout(1000);
    await page.locator('button:has-text("Importar Usuários e Avançar")').click();

    // Expect a Mantine error toast — wizard must NOT advance.
    // Errors in this app use toast.error() which renders via @mantine/notifications
    // with a title of "Erro" inside the notification portal.
    await expect(
      page.locator('.mantine-Notification-root').filter({ hasText: 'Erro' })
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText('Usuários Importados!')).not.toBeVisible();
  });

  // ---------------------------------------------------------------------------
  // Scenario 4: Unsupported delimiter ("&&") — import must be BLOCKED
  // The detector only recognises ',' and ';'. A file with no commas or semicolons
  // causes both methods to return null → Resolve(null, null) → throws.
  // ---------------------------------------------------------------------------
  test('should reject a CSV using an unsupported delimiter (&&)', async ({ page }) => {
    test.setTimeout(120_000);

    await loginAndGoToWizard(page);
    await uploadContractsStep1(page);

    // Step 2
    const step2Input = page.locator('input[type="file"][accept=".csv"]');
    await expect(step2Input).toBeAttached({ timeout: 10_000 });
    await step2Input.setInputFiles(getTestDataPath('users-unsupported-delimiter.csv'));
    await page.waitForTimeout(1000);
    await page.locator('button:has-text("Importar Usuários e Avançar")').click();

    // Backend should throw because neither ',' nor ';' appear in the file
    await expect(
      page.locator('.mantine-Notification-root').filter({ hasText: 'Erro' })
    ).toBeVisible({ timeout: 20_000 });
    await expect(page.getByText('Usuários Importados!')).not.toBeVisible();
  });
});
