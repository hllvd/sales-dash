/// <reference types="node" />
import { test, expect, type Page } from '@playwright/test';
import * as path from 'path';
import * as fs from 'fs';
import * as XLSX from 'xlsx';

// ── Types ────────────────────────────────────────────────────────────────────
interface ParsedCsv {
  headers: string[];
  rows: Record<string, string>[];
}

// ── Pure CSV Parsing Helpers (compliant with global rules) ───────────────────
function splitCsvLine(line: string): string[] {
  const result: string[] = [];
  let current = '';
  let inQuotes = false;
  
  for (let i = 0; i < line.length; i++) {
    const char = line[i];
    if (char === '"') {
      if (inQuotes && line[i + 1] === '"') {
        current += '"';
        i++; // skip next quote
      } else {
        inQuotes = !inQuotes;
      }
    } else if (char === ',' && !inQuotes) {
      result.push(current);
      current = '';
    } else {
      current += char;
    }
  }
  result.push(current);
  return result;
}

function parseCsv(content: string): ParsedCsv {
  const cleanContent = content.startsWith('\uFEFF') ? content.slice(1) : content;
  const lines = cleanContent.split(/\r?\n/).filter(line => line.trim().length > 0);
  if (lines.length === 0) {
    return { headers: [], rows: [] };
  }
  
  const headers = splitCsvLine(lines[0]);
  const rows = lines.slice(1).map(line => {
    const values = splitCsvLine(line);
    const rowObj: Record<string, string> = {};
    headers.forEach((header, idx) => {
      rowObj[header] = values[idx] ?? '';
    });
    return rowObj;
  });
  
  return { headers, rows };
}

// ── Login and Wizard Helpers ──────────────────────────────────────────────────
import { loginAs } from './helpers/auth';


async function loginAndGoToWizard(page: Page): Promise<void> {
  await loginAs(page);
  await page.goto('/#/import-wizard');
  await expect(page.getByRole('heading', { name: 'Assistente de Importação Completa' })).toBeVisible();
}

async function loginAndGoToContracts(page: Page): Promise<void> {
  await loginAs(page);
  await page.goto('/#/contracts');
  await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15_000 });
}


async function getToken(page: Page): Promise<string> {
  return page.evaluate(() => localStorage.getItem('token') ?? '');
}

async function getContractByNumber(page: Page, token: string, contractNumber: string) {
  const resp = await page.request.get(`/api/contracts/number/${contractNumber}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!resp.ok()) return null;
  return (await resp.json()).data ?? null;
}

async function deleteContractByNumber(page: Page, token: string, contractNumber: string) {
  const contract = await getContractByNumber(page, token, contractNumber);
  if (!contract) return;
  await page.request.delete(`/api/contracts/${contract.id}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  console.log(`>>> Deleted contract ${contractNumber} (id=${contract.id})`);
}

// ── Test Suite ────────────────────────────────────────────────────────────────
test.describe('[TEAR 2] Import Error CSV Download', () => {
  test.describe.configure({ mode: 'serial' });

  const contractsWithErrorsPath = path.resolve(__dirname, '../test-data/contracts_with_errors.xlsx');
  const usersWithErrorsPath = path.resolve(__dirname, '../test-data/users_with_errors.csv');

  test.beforeAll(async () => {
    // Ensure temp dir exists
    const tempDir = path.resolve(__dirname, '../temp');
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }

    // 1. Generate users_with_errors.csv
    const usersHeader = 'Name,Email,Role,ParentEmail,Matricula,Owner_Matricula,Password';
    const userRowValid = 'Valid Wizard User,valid_wizard_user@example.com,user,,6111,0,123456';
    const userRowInvalid = 'Invalid Wizard User,invalid_wizard_user@example.com,user,,,0,123456';
    fs.writeFileSync(usersWithErrorsPath, [usersHeader, userRowValid, userRowInvalid].join('\n'), 'utf-8');
    console.log(`>>> Generated ${usersWithErrorsPath}`);

    // 2. Generate contracts_with_errors.xlsx programmatically by modifying contracts_with_duplicates.xlsx
    const sourcePath = path.resolve(__dirname, '../test-data/contracts_with_duplicates.xlsx');
    if (!fs.existsSync(sourcePath)) {
      throw new Error(`Source contracts file not found at ${sourcePath}`);
    }

    const workbook = XLSX.readFile(sourcePath);
    const sheetName = workbook.SheetNames[0];
    const worksheet = workbook.Sheets[sheetName];
    const data: any[] = XLSX.utils.sheet_to_json(worksheet);

    // Slice to 3 rows and modify them
    const modifiedData = data.slice(0, 3).map((row, idx) => {
      const contractNumber = `ERR-TEST-00${idx + 1}`;
      return {
        ...row,
        Contrato: contractNumber,
        Valor: idx < 2 ? 'INVALID_AMOUNT,00' : '50000',
        Cota: `2047;A;B;Nome Teste;${contractNumber}`,
        'Matrícula': '6111',
        REP: '6111',
        Email: 'superadmin@salesapp.com'
      };
    });

    const newSheet = XLSX.utils.json_to_sheet(modifiedData);
    const newWorkbook = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(newWorkbook, newSheet, sheetName);
    XLSX.writeFile(newWorkbook, contractsWithErrorsPath);
    console.log(`>>> Generated ${contractsWithErrorsPath}`);
  });

  test.afterAll(async () => {
    // Clean up generated files
    const testDataFiles = [contractsWithErrorsPath, usersWithErrorsPath];
    for (const file of testDataFiles) {
      if (fs.existsSync(file)) {
        fs.unlinkSync(file);
        console.log(`>>> Cleaned up test data file: ${file}`);
      }
    }

    // Clean up downloaded files in temp
    const tempDir = path.resolve(__dirname, '../temp');
    if (fs.existsSync(tempDir)) {
      const files = fs.readdirSync(tempDir);
      for (const file of files) {
        if (file.includes('wizard_users_errors') || file.includes('bulk_import_errors')) {
          try {
            fs.unlinkSync(path.join(tempDir, file));
            console.log(`>>> Cleaned up temp downloaded file: ${file}`);
          } catch (e) {
            // ignore
          }
        }
      }
    }
  });

  // ── Block 1: Import Wizard Step 2 (User Import Errors) ──────────────────────
  test('should download CSV with ERR column when user import in Step 2 fails', async ({ page }) => {
    test.setTimeout(90_000);
    await loginAndGoToWizard(page);

    // Step 1: Upload historical_contracts.xlsx
    const step1Input = page.locator('#wizard-step1-input');
    await expect(step1Input).toBeAttached({ timeout: 10_000 });
    const historicalFile = path.resolve(__dirname, '../test-data/historical_contracts.xlsx');
    await step1Input.setInputFiles(historicalFile);

    const nextBtn = page.locator('button:has-text("Próximo Passo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // Handle optional 'Modelo Divergente' warning
    const mismatchProceed = page.locator('button:has-text("Prosseguir assim mesmo")');
    const step2Input = page.locator('#wizard-step2-input');
    await Promise.race([
      step2Input.waitFor({ state: 'attached', timeout: 30_000 }).catch(() => {}),
      mismatchProceed.waitFor({ state: 'visible', timeout: 30_000 }).catch(() => {})
    ]);

    if (await mismatchProceed.isVisible()) {
      await mismatchProceed.click();
    }

    await expect(step2Input).toBeAttached({ timeout: 15_000 });

    // Step 2: Upload users_with_errors.csv
    await step2Input.setInputFiles(usersWithErrorsPath);

    // Setup download event listener BEFORE clicking import
    const downloadPromise = page.waitForEvent('download');
    
    // Click Import
    const importBtn = page.locator('button:has-text("Importar Usuários e Avançar")');
    await expect(importBtn).toBeEnabled({ timeout: 5_000 });
    await importBtn.click();

    // Await download and save it
    const download = await downloadPromise;
    const savePath = path.resolve(__dirname, '../temp/wizard_users_errors_downloaded.csv');
    await download.saveAs(savePath);

    // Verify file exists
    expect(fs.existsSync(savePath)).toBe(true);

    // Read and parse CSV
    const content = fs.readFileSync(savePath, 'utf-8');
    const parsed = parseCsv(content);

    // Assert headers
    expect(parsed.headers).toContain('Name');
    expect(parsed.headers).toContain('Email');
    expect(parsed.headers).toContain('Matricula');
    expect(parsed.headers[parsed.headers.length - 1]).toBe('ERR');

    // Assert failed row is in the file and has non-empty ERR message
    const failedRow = parsed.rows.find(r => r.Email === 'invalid_wizard_user@example.com');
    expect(failedRow).toBeDefined();
    expect(failedRow?.ERR).toContain('required');

    // Assert that the successfully imported row is NOT in the error file
    const succeededRow = parsed.rows.find(r => r.Email === 'valid_wizard_user@example.com');
    expect(succeededRow).toBeUndefined();

    console.log('>>> Step 2 User Import Error CSV verified successfully.');
  });

  // ── Block 2: Bulk Import Modal (Contract Import Errors) ──────────────────────
  test('should download CSV with ERR column when contract import via bulk modal fails', async ({ page }) => {
    test.setTimeout(90_000);
    await loginAndGoToContracts(page);

    // Open Bulk Import Modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });

    // Upload contracts_with_errors.xlsx
    await page.setInputFiles('input#file', contractsWithErrorsPath);

    // Select Contracts template
    await page.selectOption('select#templateSelection', { label: 'Contracts' });

    // Setup status validation listener before mapping step starts
    const validationPromise = page.waitForResponse(response => 
      response.url().includes('/validate-status') && response.status() === 200,
      { timeout: 15_000 }
    ).catch(() => {});

    // Click "Próximo"
    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 5_000 });
    await nextBtn.click();

    // Handle optional 'Modelo Divergente' mismatch warning
    const proceedBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedBtn.isVisible({ timeout: 3_000 })) {
        await proceedBtn.click();
      }
    } catch {
      // Ignore if not visible
    }

    // Reach mapping step
    await expect(page.locator('.mapping-section')).toBeVisible({ timeout: 15_000 });

    // Wait for status validation to settle and UI to update
    await validationPromise;
    await page.waitForTimeout(2000);

    // Setup download event listener BEFORE clicking Import
    const downloadPromise = page.waitForEvent('download');

    // Click "Confirmar e Importar"
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 15_000 });
    await confirmBtn.click();

    // Await download and save it
    const download = await downloadPromise;
    const savePath = path.resolve(__dirname, '../temp/bulk_import_errors_downloaded.csv');
    await download.saveAs(savePath);

    // Verify file exists
    expect(fs.existsSync(savePath)).toBe(true);

    // Read and parse CSV
    const content = fs.readFileSync(savePath, 'utf-8');
    const parsed = parseCsv(content);

    // Assert headers
    expect(parsed.headers).toContain('Contrato');
    expect(parsed.headers).toContain('Valor');
    expect(parsed.headers[parsed.headers.length - 1]).toBe('ERR');

    // Assert failed rows are in the file with invalid amount message
    const failedRow1 = parsed.rows.find(r => r.Contrato === 'ERR-TEST-001');
    expect(failedRow1).toBeDefined();
    expect(failedRow1?.ERR).toContain('Invalid total amount');

    const failedRow2 = parsed.rows.find(r => r.Contrato === 'ERR-TEST-002');
    expect(failedRow2).toBeDefined();
    expect(failedRow2?.ERR).toContain('Invalid total amount');

    // Assert that the successfully imported row is NOT in the error file
    const succeededRow = parsed.rows.find(r => r.Contrato === 'ERR-TEST-003');
    expect(succeededRow).toBeUndefined();

    // Assert result screen shows errors
    await expect(page.getByText(/Erros: 2/)).toBeVisible({ timeout: 10_000 });

    // Close Modal
    await page.click('button:has-text("Fechar")');

    // Cleanup successfully imported contract ERR-TEST-003 via API
    const token = await getToken(page);
    await deleteContractByNumber(page, token, 'ERR-TEST-003');

    console.log('>>> Bulk Import Modal Contract Import Error CSV verified successfully.');
  });
});
