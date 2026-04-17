import { test, expect, type Page } from '@playwright/test';
import path from 'path';
import fs from 'fs';

const getTestDataPath = (filename: string) =>
  path.resolve(process.cwd(), 'test-data', filename);

test.describe('Email Mapping to contracts.csv', () => {

  test('should accurately map Emails from users.csv into the final contracts.csv', async ({ page }) => {
    test.setTimeout(45_000);

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

    // 4. Step 2: Upload users-demo.csv
    // Wait for the "Assistente" step to advance visually
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible();
    const step2Input = page.locator('#wizard-step2-input');
    await expect(step2Input).toBeAttached({ timeout: 10_000 });
    await step2Input.setInputFiles(getTestDataPath('users-demo.csv'));
    
    // Give state a moment to settle
    const importBtn = page.locator('button:has-text("Importar Usuários e Avançar")');
    await expect(importBtn).toBeEnabled({ timeout: 10000 });
    await importBtn.click();

    // 5. Wait for Step 3: Download Contracts Enriched
    await expect(page.getByText('Usuários Importados!')).toBeVisible({ timeout: 30_000 });
    const downloadBtn = page.getByRole('button', { name: /Baixar contracts\.csv Enriquecido/i });
    await expect(downloadBtn).toBeVisible({ timeout: 10_000 });

    // 6. Intercept download
    const downloadPromise = page.waitForEvent('download');
    await downloadBtn.click();
    const download = await downloadPromise;

    // 7. Process downloaded CSV memory stream
    const downloadPath = await download.path();
    const csvContent = fs.readFileSync(downloadPath, 'utf8');

    // Asserts:
    // We expect the backend email resolver mapping algorithm to have parsed users-demo.csv 
    // and matched it correctly against the XLSX data. 

    // E.g. Matricula 6111 with Comissionado Arthur Terplak -> arthurterplak@example.com
    expect(csvContent).toContain('arthurterplak@example.com');
    // Ensure another shared user on 6111 didn't overwrite Arthur
    expect(csvContent).toContain('gabrielfelipe@example.com');
    expect(csvContent).toContain('carlosmendes@example.com');
    
    // Validate CSV column headers structure is preserved
    expect(csvContent).toContain('Email');
  });
});
