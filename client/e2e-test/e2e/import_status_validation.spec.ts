/// <reference types="node" />
import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

const getTestDataPath = (filename: string) => path.resolve(process.cwd(), 'test-data', filename);

// Creates a minimal CSV that maps a WRONG column to Status (contains garbage values)
const INVALID_STATUS_CSV = `Obs Cota,Cota,Versao,Dt Venda,Dt Produção,Dt Cancelamento,Dt Contemplacao,Produção Analitica,Categoria,Consultor,Cód. PV,PV,Unidade Original,Unidade Atual,Crédito Venda,Tem Pagamento?,Situação Cobrança,Prazo Grupo,Plano Venda,id_bi,Matricula
,6111;300;X;Arthur;826650,1,2026-04-30,,,,,AP,,,,,,300000,,VALOR_INVALIDO_XYZ,,,,6111
,6111;301;X;Arthur;821590,1,2026-04-30,,,,,AS,,,,,,100000,,OUTRO_INVALIDO,,,,6111
`;

const VALID_STATUS_CSV = `Obs Cota,Cota,Versao,Dt Venda,Dt Produção,Dt Cancelamento,Dt Contemplacao,Produção Analitica,Categoria,Consultor,Cód. PV,PV,Unidade Original,Unidade Atual,Crédito Venda,Tem Pagamento?,Situação Cobrança,Prazo Grupo,Plano Venda,id_bi,Matricula
,6111;300;X;Arthur;826650,1,2026-04-30,,,,,AP,,,,,,300000,,NORMAL,,,,6111
,6111;301;X;Arthur;821590,1,2026-04-30,,,,,AS,,,,,,100000,,EXCLUIDO,,,,6111
`;

async function login(page: any) {
  await page.goto('/');
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15000 });
  await page.fill('input[type="email"]', 'superadmin@salesapp.com');
  await page.fill('input[type="password"]', 'string');
  await page.click('button.login-button');
  await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15000 });
}

async function openImportModal(page: any) {
  await page.click('a[href="#/contracts"]');
  await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });
  await page.waitForTimeout(1000);
  await page.click('button:has-text("Importar")');
  await expect(page.getByText('Importar Contratos em Lote')).toBeVisible();
}

async function uploadAndAdvanceToMapping(page: any, csvContent: string) {
  // Write temp CSV
  const tempPath = path.resolve(process.cwd(), 'temp', `status-validation-test-${Date.now()}.csv`);
  fs.mkdirSync(path.dirname(tempPath), { recursive: true });
  fs.writeFileSync(tempPath, csvContent, 'utf-8');

  await page.setInputFiles('input#file', tempPath);
  const nextBtn = page.locator('button:has-text("Próximo")');
  await expect(nextBtn).toBeEnabled({ timeout: 10000 });
  await nextBtn.click();

  // Wait for either the mapping screen or the mismatch screen to appear deterministically
  await Promise.race([
    page.waitForSelector('.mapping-section', { state: 'visible', timeout: 5000 }).catch(() => {}),
    page.waitForSelector('.verification-warning-section', { state: 'visible', timeout: 5000 }).catch(() => {})
  ]);

  // Handle mismatch if it appears
  const proceedBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
  if (await proceedBtn.isVisible()) {
    await proceedBtn.click();
  }

  await expect(page.locator('.mapping-section')).toBeVisible({ timeout: 15000 });

  // Clean up temp file
  try { fs.unlinkSync(tempPath); } catch { /* ignore */ }
}

test.describe('Status Column Validation on Mapping Step', () => {
  // Configure tests in this file to run serially to prevent database lock contention in SQLite
  test.describe.configure({ mode: 'serial' });

  test('should block confirm when Status column contains invalid values', async ({ page }) => {
    test.setTimeout(60000);
    await login(page);
    await openImportModal(page);

    // Setup network listener for status validation request
    const validationPromise = page.waitForResponse(response => 
      response.url().includes('/validate-status') && response.status() === 200,
      { timeout: 15000 }
    );

    await uploadAndAdvanceToMapping(page, INVALID_STATUS_CSV);
    await validationPromise;

    // The warning should appear automatically since "Situação Cobrança" is auto-mapped to Status
    await expect(page.locator('#status-validation-warning')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('#status-validation-warning')).toContainText('Valores de Status Inválidos');
    // The warning contains either the bad value or the "vazio" message — both mean the column is invalid
    await expect(page.locator('#status-validation-warning p').first()).not.toBeEmpty();

    // Confirm button must be disabled
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeDisabled({ timeout: 5000 });
  });

  test('should show success indicator and allow confirm when Status values are valid', async ({ page }) => {
    test.setTimeout(60000);
    await login(page);
    await openImportModal(page);

    // Setup network listener for status validation request
    const validationPromise = page.waitForResponse(response => 
      response.url().includes('/validate-status') && response.status() === 200,
      { timeout: 15000 }
    );

    await uploadAndAdvanceToMapping(page, VALID_STATUS_CSV);
    await validationPromise;

    // No warning should appear
    await expect(page.locator('#status-validation-warning')).not.toBeVisible();

    // Success indicator should appear
    await expect(page.getByText('Todos os valores de status são válidos.')).toBeVisible({ timeout: 10000 });

    // Confirm button must be enabled
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 10000 });
  });

  test('should clear warning when user remaps Status to a valid column', async ({ page }) => {
    test.setTimeout(60000);
    await login(page);
    await openImportModal(page);

    // Setup network listener for status validation request
    const validationPromise = page.waitForResponse(response => 
      response.url().includes('/validate-status') && response.status() === 200,
      { timeout: 15000 }
    );

    await uploadAndAdvanceToMapping(page, INVALID_STATUS_CSV);
    await validationPromise;

    // Warning should be visible
    await expect(page.locator('#status-validation-warning')).toBeVisible({ timeout: 10000 });

    // Now remap "Situação Cobrança" away from Status (select "-- Não mapear --")
    const statusRow = page.locator('.mapping-row', { hasText: 'Situação Cobrança' });
    await statusRow.locator('select').selectOption('');

    // Warning should disappear
    await expect(page.locator('#status-validation-warning')).not.toBeVisible({ timeout: 5000 });
  });

});
