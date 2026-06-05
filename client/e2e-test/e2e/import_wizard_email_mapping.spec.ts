import { test, expect, type Page } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import * as XLSX from 'xlsx';

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

test.describe('Email Mapping to contracts.xlsx', () => {

  test('should accurately map Emails from users.csv into the final contracts.xlsx', async ({ page }) => {
    test.setTimeout(90_000);

    // 1. Login as superadmin
    await page.goto('/');
    await expect(page.locator('button.login-button')).toBeVisible();
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Go to Import Wizard
    await expect(page.locator('a[href="#/import-wizard"]')).toBeVisible({ timeout: 15_000 });
    await page.click('a[href="#/import-wizard"]');
    await expect(
      page.getByRole('heading', { name: 'Assistente de Importação Completa' })
    ).toBeVisible({ timeout: 10_000 });

    // 3. Step 1: Upload historical contracts
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    await step1Input.setInputFiles(getTestDataPath('historical_contracts.xlsx'));

    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 15_000 });
    await nextBtn.click();

    // Handle possible 'Modelo Divergente' warning
    const mismatchProceed = page.locator('button:has-text("Prosseguir assim mesmo")');
    const step2Content = page.getByText('O sistema identificou os vendedores no arquivo');
    await Promise.race([
      step2Content.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => {}),
      mismatchProceed.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => {})
    ]);

    if (await mismatchProceed.isVisible()) {
      await mismatchProceed.click();
    }

    // 4. Step 2: Upload users-demo.csv
    // Wait for the "Assistente" step to advance visually
    await expect(step2Content).toBeVisible({ timeout: 30_000 });
    const step2Input = page.locator('#wizard-step2-input');
    await expect(step2Input).toBeAttached({ timeout: 30_000 });
    await step2Input.setInputFiles(getTestDataPath('users-demo.csv'));

    // Give state a moment to settle
    const importBtn = page.locator('button:has-text("Importar Usuários e Avançar")');
    await expect(importBtn).toBeEnabled({ timeout: 10000 });
    await importBtn.click();

    // 5. Wait for Step 3: Download Contracts Enriched
    await expect(page.getByText('Usuários Importados!')).toBeVisible({ timeout: 30_000 });
    const downloadBtn = page.getByRole('button', { name: /Baixar contracts\.xlsx/i });
    await expect(downloadBtn).toBeVisible({ timeout: 10_000 });

    // 6. Intercept download
    const downloadPromise = page.waitForEvent('download');
    await downloadBtn.click();
    const download = await downloadPromise;

    // 7. Process downloaded XLSX file
    const downloadPath = await download.path();
    if (!downloadPath) throw new Error("downloadPath is null");

    const workbook = XLSX.readFile(downloadPath);
    const sheetName = workbook.SheetNames[0];
    const worksheet = workbook.Sheets[sheetName];
    const data = XLSX.utils.sheet_to_json(worksheet) as any[];

    // Asserts:
    // We expect the backend email resolver mapping algorithm to have parsed users-demo.csv 
    // and matched it correctly against the XLSX data. 

    // Check if any row has the expected emails
    const emails = data.map(row => row.Email);
    expect(emails).toContain('arthurterplak@example.com');
    expect(emails).toContain('gabrielfelipe@example.com');
    expect(emails).toContain('carlosmendes@example.com');

    // Validate Header structure (sheet_to_json maps keys from headers)
    const firstRow = data[0];
    expect(firstRow).toHaveProperty('Email');
  });
});
