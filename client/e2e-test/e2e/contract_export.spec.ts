import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import * as XLSX from 'xlsx';
import { loginAs } from './helpers/auth';

test.describe('[TEAR 3] Contract Export Verification', () => {
  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';

  test.beforeEach(async ({ page }) => {
    await loginAs(page, adminEmail, adminPassword);
  });


  test('should filter by Rodrigo Rosin and verify export sum', async ({ page }) => {
    console.log('>>> Navigating to Contracts page');
    await page.click('a[href="#/contracts"]');

    // 1. Filter by user Rodrigo Rosin
    console.log('>>> Filtering by Rodrigo Rosin');

    // Increase timeout for users to load and ensure we're looking at the right element
    // await page.waitForSelector('#filterUser option:not([value=""])', { timeout: 15000 });

    // Select Rodrigo Rosin using Mantine's MultiSelect component
    const userFilterInput = page.locator('input[placeholder="Selecionar usuários..."], input[placeholder="Nenhum usuário disponível"]').first();
    await userFilterInput.click();
    await userFilterInput.fill('Rodrigo Rosin');
    
    // Wait for the option to appear and click it
    const rodrigoOption = page.getByRole('option', { name: 'Rodrigo Rosin', exact: false });
    await rodrigoOption.waitFor({ state: 'visible', timeout: 5000 });
    await rodrigoOption.click();

    // IMPORTANT: Wait for the 3-second debounce in ContractsPage.tsx to fire 
    // and for the table to reload with filtered results.
    console.log('>>> Waiting for filter debounce and reload...');
    await page.waitForTimeout(4000);

    // 2. Trigger Export
    console.log('>>> Triggering Export');
    const downloadPromise = page.waitForEvent('download');
    await page.click('button:has-text("Exportar XLSX")');
    const download = await downloadPromise;

    // 3. Save and parse the file
    const downloadPath = path.join(__dirname, '../../test-results/exported_contracts.xlsx');
    await download.saveAs(downloadPath);
    console.log(`>>> Downloaded file to ${downloadPath}`);

    // Verify file exists
    expect(fs.existsSync(downloadPath)).toBeTruthy();

    // 4. Parse XLSX and verify sum
    const workbook = XLSX.readFile(downloadPath);
    const sheetName = workbook.SheetNames[0];
    const worksheet = workbook.Sheets[sheetName];

    // Convert to JSON for easier processing
    // header: 1 returns an array of arrays
    const data: any[][] = XLSX.utils.sheet_to_json(worksheet, { header: 1 });

    // Headers are in row 0
    // "Valor Total" is at index 6
    const totalAmountIdx = 6;
    expect(data[0][totalAmountIdx]).toBe('Valor Total');

    let sum = 0;
    let recordsCount = 0;

    // Start from index 1 (skip header)
    for (let i = 1; i < data.length; i++) {
      const row = data[i];
      if (row.length === 0) continue;

      const val = row[totalAmountIdx];
      if (typeof val === 'number') {
        sum += val;
        recordsCount++;
      } else if (typeof val === 'string') {
        // Handle string representation if necessary
        const cleaned = val.replace(/[^\d.,]/g, '').replace(',', '.');
        sum += parseFloat(cleaned);
        recordsCount++;
      }
    }

    console.log(`>>> Found ${recordsCount} records for Rodrigo Rosin`);
    console.log(`>>> Calculated sum: ${sum}`);

    // Assertions
    expect(recordsCount).toBe(9);
    // 1,950,000.00
    expect(sum).toBe(1950000);

    // Cleanup
    if (fs.existsSync(downloadPath)) {
      fs.unlinkSync(downloadPath);
    }
  });
});
