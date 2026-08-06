/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { loginAs } from './helpers/auth';

const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };

async function login(page: Page) {
  await loginAs(page, ADMIN.email, ADMIN.password);
}

test.describe('Contract Dashboard Import — Cota Field Extraction & Upsert', () => {
  test.describe.configure({ mode: 'serial' });

  const tempDir = path.join(__dirname, '../temp');

  test.beforeAll(() => {
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }
  });

  test('Cota field from compound Cota column is populated for new and existing contracts', async ({ page }) => {
    test.setTimeout(90_000);
    await login(page);

    // 1. Navigate to Contracts Page
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10_000 });

    const contractNum = `CNT-COTA-E2E-${Date.now()}`;
    const header = 'Contrato,Código PV,Nome PV,Matrícula,Consultor,Grupo,Cota,Data da Venda,Valor,Nome do Cliente,Tipo,Status';
    // Compound Cota format: Group;Quota;X;CustomerName;ContractNumber
    const row = `${contractNum},PV100,Loja Sul,6111,Arthur Terplak,G1,G1;300;X;Cliente Cota Teste;${contractNum},2025-01-01,150000.00,Cliente Cota Teste,Normal,Ativo`;
    const tempCsvPath = path.join(tempDir, `test_cota_field_${Date.now()}.csv`);
    fs.writeFileSync(tempCsvPath, `${header}\n${row}\n`, 'utf-8');

    // --- STEP A: Initial Import (New Contract Path) ---
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });
    await page.setInputFiles('input#file', tempCsvPath);

    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    const proceedAnywayBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch { /* ignore if not present */ }

    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });
    await page.waitForResponse(r => r.url().includes('/validate-status') && r.status() === 200, { timeout: 15_000 }).catch(() => {});

    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 15_000 });
    await confirmBtn.click();

    await expect(page.getByText(/Importados: 1/)).toBeVisible({ timeout: 30_000 });
    await page.click('button:has-text("Fechar")');

    // --- STEP B: Enable 'Cota' column visibility and verify Quota value ---
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }
    await page.fill('input#filterContractNumber', contractNum);
    
    // Open column selection modal to enable Cota column
    const colBtn = page.getByRole('button', { name: 'Colunas' });
    await expect(colBtn).toBeVisible({ timeout: 10_000 });
    await colBtn.click();

    const modal = page.locator('.mantine-Modal-content');
    const quotaCheckbox = modal.getByRole('checkbox', { name: 'Cota' });
    await expect(quotaCheckbox).toBeVisible();
    if (!(await quotaCheckbox.isChecked())) {
      await quotaCheckbox.check();
    }
    await modal.getByRole('button', { name: 'Concluir' }).click();

    // Verify contract row has '300' in the Cota column
    const contractRow = page.locator('table tbody tr', { hasText: contractNum });
    await expect(contractRow).toBeVisible({ timeout: 10_000 });
    await expect(contractRow).toContainText('300');

    // --- STEP C: Re-import Same CSV (Existing Contract Upsert Path) ---
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });
    await page.setInputFiles('input#file', tempCsvPath);

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

    await expect(page.getByText(/Importados: 1/)).toBeVisible({ timeout: 30_000 });
    await page.click('button:has-text("Fechar")');

    // --- STEP D: Verify Cota value is still populated after update ---
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }
    await page.fill('input#filterContractNumber', contractNum);
    const updatedContractRow = page.locator('table tbody tr', { hasText: contractNum });
    await expect(updatedContractRow).toBeVisible({ timeout: 10_000 });
    await expect(updatedContractRow).toContainText('300');

    // Cleanup temp file
    if (fs.existsSync(tempCsvPath)) {
      fs.unlinkSync(tempCsvPath);
    }
  });
});
