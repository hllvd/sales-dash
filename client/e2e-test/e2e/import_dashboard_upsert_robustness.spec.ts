/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { loginAs } from './helpers/auth';

const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };

async function login(page: Page) {
  await loginAs(page, ADMIN.email, ADMIN.password);
}

test.describe('Contract Dashboard Import — Upsert Robustness & Duplicate Prevention', () => {
  test.describe.configure({ mode: 'serial' });

  const tempDir = path.join(__dirname, '../temp');

  test.beforeAll(() => {
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }
  });

  test('Dashboard import handles re-imports and leading-zero normalized duplicate contracts without SQLite UNIQUE error', async ({ page }) => {
    test.setTimeout(120_000);
    await login(page);

    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10_000 });

    const timestamp = Date.now();
    const rawNumber = `${timestamp % 100000000}`;
    const contractNumWithZeros = `000${rawNumber}`;
    const contractNumNormalized = rawNumber.replace(/^0+/, '');

    const header = 'Contrato,Código PV,Nome PV,Matrícula,Consultor,Grupo,Cota,Data da Venda,Valor,Nome do Cliente,Tipo,Status';
    
    // Row 1: initial import with leading zeros in contract number
    const row1 = `${contractNumWithZeros},PV100,Loja Sul,6111,Arthur Terplak,G1,G1;300;X;Cliente Upsert Teste;${contractNumWithZeros},2025-01-01,100000.00,Cliente Upsert Teste,Normal,Ativo`;
    
    const tempCsvPath1 = path.join(tempDir, `test_upsert_robustness_1_${timestamp}.csv`);
    fs.writeFileSync(tempCsvPath1, `${header}\n${row1}\n`, 'utf-8');

    // --- STEP A: Initial Import (Create contract) ---
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });
    await page.setInputFiles('input#file', tempCsvPath1);

    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    const proceedAnywayBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch { /* ignore */ }

    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });
    await page.waitForResponse(r => r.url().includes('/validate-status') && r.status() === 200, { timeout: 15_000 }).catch(() => {});

    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 15_000 });
    await confirmBtn.click();

    await expect(page.getByText(/Importados: 1/)).toBeVisible({ timeout: 30_000 });
    await page.click('button:has-text("Fechar")');

    // --- STEP B: Second Import with normalized contract number & updated amount (Upsert should update without unique crash) ---
    const row2 = `${contractNumNormalized},PV100,Loja Sul,6111,Arthur Terplak,G1,G1;300;X;Cliente Upsert Teste;${contractNumNormalized},2025-01-01,250000.00,Cliente Upsert Teste Updated,Normal,Ativo`;
    const tempCsvPath2 = path.join(tempDir, `test_upsert_robustness_2_${timestamp}.csv`);
    fs.writeFileSync(tempCsvPath2, `${header}\n${row2}\n`, 'utf-8');

    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });
    await page.setInputFiles('input#file', tempCsvPath2);

    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch { /* ignore */ }

    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });
    await page.waitForResponse(r => r.url().includes('/validate-status') && r.status() === 200, { timeout: 15_000 }).catch(() => {});

    await expect(confirmBtn).toBeEnabled({ timeout: 15_000 });
    await confirmBtn.click();

    // Must successfully complete the import with no 500 error / UNIQUE constraint crash
    await expect(page.getByText(/Importados: 1/)).toBeVisible({ timeout: 30_000 });
    await page.click('button:has-text("Fechar")');

    // Clean up temporary files
    [tempCsvPath1, tempCsvPath2].forEach(file => {
      if (fs.existsSync(file)) {
        fs.unlinkSync(file);
      }
    });
  });
});
