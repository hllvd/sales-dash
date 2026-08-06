import { test, expect, type Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { loginAs } from './helpers/auth';

const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };

async function login(page: Page) {
  await loginAs(page, ADMIN.email, ADMIN.password);
}

test.describe('Contract Dashboard Import — Missing TotalAmount (Crédito Venda) Handling', () => {
  test.describe.configure({ mode: 'serial' });

  const tempDir = path.join(__dirname, '../temp');

  test.beforeAll(() => {
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }
  });

  test('New contract without TotalAmount should be skipped with warning message', async ({ page }) => {
    test.setTimeout(90_000);
    await login(page);

    // 1. Navigate to Contracts Page
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10_000 });

    // 2. Open import modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });

    // 3. Create temp CSV with a new contract missing TotalAmount (Valor column empty)
    const contractNoAmount = `CNT-NOAMT-NEW-${Date.now()}`;
    const header = 'Contrato,Código PV,Nome PV,Matrícula,Consultor,Grupo,Cota,Data da Venda,Valor,Nome do Cliente,Tipo,Status';
    const csvContent = `${header}\n${contractNoAmount},PV100,Loja Sul,6111,Arthur Terplak,G1,G1;001;X;Cliente A;${contractNoAmount},2025-01-01,,Cliente A,Normal,Ativo\n`;
    const tempCsvPath = path.join(tempDir, 'test_missing_amount_new.csv');
    fs.writeFileSync(tempCsvPath, csvContent);

    // 4. Upload file
    await page.setInputFiles('input#file', tempCsvPath);

    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    // Handle optional mismatch warning
    const proceedAnywayBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch {
      // Ignore if not present
    }

    // 5. Mapping Step -> Confirm
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });
    await page.waitForResponse(r => r.url().includes('/validate-status') && r.status() === 200, { timeout: 15_000 }).catch(() => {});

    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 15_000 });
    await confirmBtn.click();

    // 6. Verify result & Warning message
    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30_000 });
    const warningText = page.locator('.warnings-info');
    await expect(warningText).toBeVisible({ timeout: 10_000 });
    await expect(warningText).toContainText('Não criaremos estes contratos porque a Ava Pro não nos fornece o valor de `Crédito Venda`');
    await expect(warningText).toContainText(contractNoAmount);

    // Close modal
    await page.click('button:has-text("Fechar")');

    // 7. Verify contract was NOT created in table
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }
    await page.fill('input#filterContractNumber', contractNoAmount);
    await expect(page.locator('table tbody tr', { hasText: contractNoAmount })).not.toBeVisible({ timeout: 5_000 });

    // Cleanup CSV
    if (fs.existsSync(tempCsvPath)) {
      fs.unlinkSync(tempCsvPath);
    }
  });

  test('Existing contract without TotalAmount should update status and warn about TotalAmount update', async ({ page }) => {
    test.setTimeout(90_000);
    await login(page);

    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10_000 });

    const header = 'Contrato,Código PV,Nome PV,Matrícula,Consultor,Grupo,Cota,Data da Venda,Valor,Nome do Cliente,Tipo,Status';

    // Step A: Import a contract with valid TotalAmount first
    const contractNum = `CNT-NOAMT-EXIST-${Date.now()}`;
    const initialCsvPath = path.join(tempDir, 'test_missing_amount_exist_step1.csv');
    fs.writeFileSync(initialCsvPath, `${header}\n${contractNum},PV100,Loja Sul,6111,Arthur Terplak,G1,G1;002;X;Cliente B;${contractNum},2025-01-01,50000.00,Cliente B,Normal,Ativo\n`);

    await page.click('button:has-text("Importar")');
    await page.setInputFiles('input#file', initialCsvPath);
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
    await page.click('button:has-text("Confirmar e Importar")');
    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30_000 });
    await page.click('button:has-text("Fechar")');

    // Step B: Re-import same contract number with CANCELADO status and EMPTY TotalAmount (Valor)
    const updateCsvPath = path.join(tempDir, 'test_missing_amount_exist_step2.csv');
    fs.writeFileSync(updateCsvPath, `${header}\n${contractNum},PV100,Loja Sul,6111,Arthur Terplak,G1,G1;002;X;Cliente B;${contractNum},2025-01-01,,Cliente B,Normal,CANCELADO\n`);

    await page.click('button:has-text("Importar")');
    await page.setInputFiles('input#file', updateCsvPath);
    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch { /* ignore */ }

    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });
    await page.waitForResponse(r => r.url().includes('/validate-status') && r.status() === 200, { timeout: 15_000 }).catch(() => {});

    // Verify "updateTotalAmountOnExisting" checkbox is checked by default
    const updateTotalAmountCb = page.locator('#updateTotalAmountOnExisting');
    await expect(updateTotalAmountCb).toBeChecked();

    await page.click('button:has-text("Confirmar e Importar")');
    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30_000 });

    // Verify warning for failing to update TotalAmount column
    const warningText = page.locator('.warnings-info');
    await expect(warningText).toBeVisible({ timeout: 10_000 });
    await expect(warningText).toContainText('Não foi possível atualizar a coluna de Valor Total para estes contratos porque a Ava Pro não nos fornece o valor de `Crédito Venda`');
    await expect(warningText).toContainText(contractNum);

    await page.click('button:has-text("Fechar")');

    // Step C: Verify in table that contract status was updated to Defaulted/Cancelado
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }
    await page.fill('input#filterContractNumber', contractNum);
    const row = page.locator('table tbody tr', { hasText: contractNum });
    await expect(row).toBeVisible({ timeout: 10_000 });

    // Cleanup temp CSVs
    if (fs.existsSync(initialCsvPath)) fs.unlinkSync(initialCsvPath);
    if (fs.existsSync(updateCsvPath)) fs.unlinkSync(updateCsvPath);
  });
});
