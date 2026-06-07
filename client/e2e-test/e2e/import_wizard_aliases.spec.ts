/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import * as XLSX from 'xlsx';

test.describe('Import Wizard Aliases Flow', () => {
  test.afterAll(async () => {
    // Cleanup generated files in temp
    const tempDir = path.resolve(process.cwd(), 'client/e2e-test/temp');
    if (fs.existsSync(tempDir)) {
      const files = fs.readdirSync(tempDir);
      for (const file of files) {
        if (file.endsWith('.xlsx') || file.endsWith('.csv')) {
          fs.unlinkSync(path.join(tempDir, file));
        }
      }
    }
  });

  test('should process Consultor and Nome PV aliases correctly', async ({ page }) => {
    test.setTimeout(45_000);
    const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);
    const getTempPath = (filename: string) => path.resolve(process.cwd(), 'client/e2e-test/temp', filename);

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
    await page.locator('#wizard-step1-input').setInputFiles(historicalFile);
    await page.click('button:has-text("Próximo Passo")');

    // 5. Verify Step 2 transition and Download users.csv template to verify extraction
    await expect(page.getByText('Preenchimento de Usuários')).toBeVisible({ timeout: 15000 });
    
    const [downloadUsers] = await Promise.all([
      page.waitForEvent('download'),
      page.click('button:has-text("Baixar users.xlsx para Preencher")')
    ]);
    
    // Read the downloaded file content to ensure extraction worked
    const usersPath = await downloadUsers.path();
    if (!usersPath) throw new Error("usersPath is null");
    
    const workbook = XLSX.readFile(usersPath);
    const sheetName = workbook.SheetNames[0];
    const worksheet = workbook.Sheets[sheetName];
    const usersData = XLSX.utils.sheet_to_json(worksheet) as any[];

    // Assert that 'Name' and 'Matricula' were extracted successfully
    const joao = usersData.find(u => u.Name === 'João Silva');
    expect(joao).toBeDefined();
    expect(joao.Matricula).toBe('MAT-001');

    const maria = usersData.find(u => u.Name === 'Maria Souza');
    expect(maria).toBeDefined();
    expect(maria.Matricula).toBe('MAT-002');

    const pedro = usersData.find(u => u.Name === 'Pedro Alves');
    expect(pedro).toBeDefined();

    // 6. Upload pre-filled users to proceed
    const usersFileFilled = getTestDataPath('users_demo_aliases_filled.xlsx');
    await page.locator('#wizard-step2-input').setInputFiles(usersFileFilled);
    await page.click('button:has-text("Importar Usuários e Avançar")');

    // 7. Verify Step 3 and Download enriched contracts.xlsx
    await expect(page.getByText('Opções de Importação')).toBeVisible({ timeout: 15000 });
    
    const [downloadContracts] = await Promise.all([
      page.waitForEvent('download'),
      page.click('button:has-text("Baixar contracts.xlsx")')
    ]);
    
    const finalContractsPath = getTempPath('downloaded_contracts_aliases.xlsx');
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
    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30000 });
    await page.click('button:has-text("Fechar")');
    
    // Verify the data is actually in the table (TEST-001, TEST-002, TEST-003 from our demo file)
    await expect(page.getByText('TEST-001')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('TEST-002')).toBeVisible();
    await expect(page.getByText('TEST-003')).toBeVisible();
  });
});
