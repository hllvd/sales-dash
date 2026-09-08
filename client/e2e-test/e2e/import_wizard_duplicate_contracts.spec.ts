/// <reference types="node" />
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


// ── Test Suite ────────────────────────────────────────────────────────────────

test.describe('[TEAR 2] Import Wizard — Duplicate Contract Number Detection', () => {
  test.describe.configure({ mode: 'serial' });

  // ────────────────────────────────────────────────────────────────────────────
  // Test 1: uploading a file with duplicates shows the warning alert and
  //         blocks advancing to Step 2 without the checkbox.
  // ────────────────────────────────────────────────────────────────────────────
  test('should detect duplicate contract numbers and show warning alert', async ({ page }) => {
    test.setTimeout(60_000);

    await loginAndGoToWizard(page);

    // Upload the fixture file that has deliberate duplicate contract numbers
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('contracts_with_duplicates.xlsx'));

    // Click "Próximo Passo" to trigger upload + server-side duplicate detection
    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // ── Assert: duplicate warning alert is visible ───────────────────────────
    const dupeAlert = page.getByRole('alert').filter({ hasText: 'Contratos Duplicados Encontrados' });
    await expect(dupeAlert).toBeVisible({ timeout: 20_000 });

    // The alert must list at least the two duplicate contract numbers
    await expect(dupeAlert).toContainText('99001');
    await expect(dupeAlert).toContainText('99003');

    // The alert must NOT list 99002 (it is unique)
    await expect(dupeAlert).not.toContainText('99002');

    // ── Assert: "Avançar para Passo 2" button is NOT yet visible ────────────
    // (it only appears after the checkbox is checked)
    const advanceBtn = page.locator('button:has-text("Avançar para Passo 2")');
    await expect(advanceBtn).not.toBeVisible();

    // ── Assert: the wizard has NOT advanced to Step 2 ────────────────────────
    // "Preenchimento de Usuários" heading should not be the active step content
    await expect(page.locator('#wizard-step2-input')).not.toBeVisible();

    console.log('>>> Duplicate warning alert verified — step is correctly blocked.');
  });

  // ────────────────────────────────────────────────────────────────────────────
  // Test 2: checking the "Permitir duplicatas" checkbox reveals the advance
  //         button and allows the user to proceed to Step 2.
  // ────────────────────────────────────────────────────────────────────────────
  test('should allow proceeding to Step 2 after checking the allow-duplicates checkbox', async ({ page }) => {
    test.setTimeout(60_000);

    await loginAndGoToWizard(page);

    // Upload the same fixture file
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('contracts_with_duplicates.xlsx'));

    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // Wait for the duplicate alert
    const dupeAlert = page.getByRole('alert').filter({ hasText: 'Contratos Duplicados Encontrados' });
    await expect(dupeAlert).toBeVisible({ timeout: 20_000 });

    // Check the "Permitir duplicatas" checkbox
    const allowCheckbox = page.locator('#wiz-allow-duplicates');
    await expect(allowCheckbox).toBeVisible();
    await expect(allowCheckbox).not.toBeChecked();
    await allowCheckbox.check();
    await expect(allowCheckbox).toBeChecked();

    // "Avançar para Passo 2" button should now appear
    const advanceBtn = page.locator('button:has-text("Avançar para Passo 2")');
    await expect(advanceBtn).toBeVisible({ timeout: 3_000 });

    // Click it — should transition to Step 2
    await advanceBtn.click();

    // ── Assert: Step 2 content is now visible ────────────────────────────────
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible({ timeout: 10_000 });
    await expect(page.locator('#wizard-step2-input')).toBeAttached({ timeout: 10_000 });

    console.log('>>> Advance to Step 2 after allowing duplicates: verified.');
  });

  // ────────────────────────────────────────────────────────────────────────────
  // Test 3: uploading a file WITHOUT duplicate contract numbers should advance
  //         directly to Step 2 with no warning alert shown.
  // ────────────────────────────────────────────────────────────────────────────
  test('should advance directly to Step 2 when no duplicates are present', async ({ page }) => {
    test.setTimeout(90_000);

    await loginAndGoToWizard(page);

    // Use the standard historical contracts file (which has no duplicates on
    // the contract numbers visible to the duplicate-detection logic)
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('historical_contracts.xlsx'));

    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // Handle possible 'Modelo Divergente' warning (unrelated to duplicates)
    const mismatchProceed = page.locator('button:has-text("Prosseguir assim mesmo")');
    const step2Content = page.getByText('O sistema identificou os vendedores no arquivo');
    const step2Input = page.locator('#wizard-step2-input');
    await Promise.race([
      step2Input.waitFor({ state: 'attached', timeout: 30000 }).catch(() => {}),
      mismatchProceed.waitFor({ state: 'visible', timeout: 30000 }).catch(() => {})
    ]);

    if (await mismatchProceed.isVisible()) {
      await mismatchProceed.click();
    }

    // ── Assert: no duplicate alert appears ───────────────────────────────────
    const dupeAlert = page.getByRole('alert').filter({ hasText: 'Contratos Duplicados Encontrados' });
    await expect(dupeAlert).not.toBeVisible({ timeout: 3_000 });

    // ── Assert: wizard advanced directly to Step 2 ───────────────────────────
    await expect(step2Content).toBeVisible({ timeout: 30_000 });
    await expect(step2Input).toBeAttached({ timeout: 30_000 });

    console.log('>>> No-duplicate file: wizard advanced to Step 2 without warning.');
  });

  // ────────────────────────────────────────────────────────────────────────────
  // Test 4: navigating away from the wizard and back resets the duplicate
  //         warning state — the page starts clean with no alert visible.
  // ────────────────────────────────────────────────────────────────────────────
  test('should reset duplicate warning state when navigating away and returning', async ({ page }) => {
    test.setTimeout(90_000);

    await loginAndGoToWizard(page);

    // Step A: Upload the duplicate fixture → trigger warning
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('contracts_with_duplicates.xlsx'));

    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    const dupeAlert = page.getByRole('alert').filter({ hasText: 'Contratos Duplicados Encontrados' });
    await expect(dupeAlert).toBeVisible({ timeout: 20_000 });
    await expect(page.locator('#wiz-allow-duplicates')).toBeVisible();

    // Step B: Navigate away to another page (unmounts the wizard component)
    await page.click('a[href="#/contracts"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });

    // Step C: Navigate back to the Import Wizard
    await page.getByText('Importação', { exact: true }).click();
    await page.click('a[href="#/import-wizard"]');
    await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible({ timeout: 10_000 });

    // ── Assert: no duplicate alert on fresh page load ────────────────────────
    await expect(dupeAlert).not.toBeVisible({ timeout: 3_000 });

    // ── Assert: the normal "Próximo Passo" button is present (Step 1 ready) ──
    await expect(page.locator('#wizard-step1-input')).toBeAttached({ timeout: 5_000 });
    await expect(page.locator('button:has-text("Próximo Passo")')).toBeVisible();

    // ── Assert: the allow-duplicates checkbox is NOT visible ─────────────────
    await expect(page.locator('#wiz-allow-duplicates')).not.toBeVisible();

    console.log('>>> State reset verified: no duplicate alert after navigating away and back.');
  });
});
