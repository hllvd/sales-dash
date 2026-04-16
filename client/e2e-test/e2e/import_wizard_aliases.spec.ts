/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

test.describe('Import Wizard Aliases Flow', () => {
  test('should process Consultor and Nome PV aliases correctly', async ({ page }) => {
    test.setTimeout(45_000);
    const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);

    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // Wait for login to complete
    await expect(page.getByRole('heading', { name: 'Contratos' })).toBeVisible({ timeout: 15000 });

    // 3. Go to Import Wizard
    await page.goto('/#/import-wizard');
    await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible();

    // 4. Step 1: Upload demo file with aliases
    const historicalFile = getTestDataPath('wizard_demo_aliases.csv');
    // Ensure the system settles before uploading to avoid flaky tests
    await page.waitForTimeout(2000);
    await page.setInputFiles('input[type="file"]', historicalFile);
    await page.click('button:has-text("Próximo Passo")');

    // 5. Verify Step 2 transition and Download users.csv template to verify extraction
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible({ timeout: 15000 });
    
    const [downloadUsers] = await Promise.all([
      page.waitForEvent('download'),
      page.click('button:has-text("Baixar users.csv para Preencher")')
    ]);
    
    // Read the downloaded file content to ensure extraction worked
    const usersPath = await downloadUsers.path();
    if (!usersPath) throw new Error("usersPath is null");
    const usersContent = fs.readFileSync(usersPath, 'utf8');

    // Assert that 'Consultor' names and other data were extracted successfully
    expect(usersContent).toContain('João Silva');
    expect(usersContent).toContain('MAT-001');
    expect(usersContent).toContain('Maria Souza');
    expect(usersContent).toContain('MAT-002');
    expect(usersContent).toContain('Pedro Alves');

    // 6. Upload pre-filled users to proceed
    const usersFileFilled = getTestDataPath('users_demo_aliases_filled.csv');
    await page.setInputFiles('input[type="file"]', usersFileFilled);
    await page.click('button:has-text("Importar Usuários e Avançar")');

    // 7. Verify Step 3 and Download enriched contracts.csv
    await expect(page.getByText('Download de Contratos')).toBeVisible({ timeout: 15000 });
    
    const [downloadContracts] = await Promise.all([
      page.waitForEvent('download'),
      page.click('button:has-text("Baixar contracts.csv Enriquecido")')
    ]);
    
    const finalContractsPath = getTestDataPath('downloaded_contracts_aliases.csv');
    await downloadContracts.saveAs(finalContractsPath);

    // 8. Navigate to Contracts Page and perform standard import to prove mapping
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Contratos' })).toBeVisible({ timeout: 10000 });
    await page.click('button:has-text("Importar")');

    // Inside Import Modal
    await page.setInputFiles('input#file', finalContractsPath);
    await page.selectOption('select#templateSelection', { label: 'Contracts' });
    await page.click('button:has-text("Próximo")');

    // Wait for Mapping screen
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 10000 });
    // Aggressive wait for auto-mapping to settle
    await page.waitForTimeout(3000);

    // We MUST verify that 'Nome PV' was auto-mapped to 'PvName'
    // Let's assert the UI mapping state if possible. The <select> elements should have correct value
    // In our CSV, the column is 'Nome PV'. We expect the auto-mapper assigned it to 'PvName'.
    const nomePvRow = page.locator('.mapping-row', { hasText: 'Nome PV' });
    await expect(nomePvRow).toBeVisible();
    await expect(nomePvRow.locator('select')).toHaveValue('PvName');
    
    // Execute Import
    await page.click('button:has-text("Confirmar e Importar")');
    await page.click('button:has-text("Fechar")');
    
    // Verify the data is actually in the table (TEST-001, TEST-002, TEST-003 from our demo file)
    await expect(page.getByText('TEST-001')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('TEST-002')).toBeVisible();
    await expect(page.getByText('TEST-003')).toBeVisible();
  });
});
