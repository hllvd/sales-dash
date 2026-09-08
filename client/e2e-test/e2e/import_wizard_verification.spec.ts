import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Import Wizard Record Verification', () => {
  // Use parallel mode for these independent verification tests
  test.describe.configure({ mode: 'parallel' });

  test.beforeEach(async ({ page }) => {
    await loginAs(page);
    // Pre-set filterStartDate in localStorage before navigation so initial load uses 2020-01-01 without extra re-queries
    await page.addInitScript(() => {
      localStorage.setItem('contracts_filterStartDate', '2020-01-01');
    });
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15000 });
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(1000);
    }
  });

  const getFormField = (page, label: string) => page.locator('div').filter({ has: page.locator('label', { hasText: label, exact: true }) }).last();

  test('verify contract 90001305 - Leonardo Bandieri', async ({ page }) => {
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(1000);
    }

    const searchInput = page.locator('input#filterContractNumber');
    
    // Wait for the debounced search API call containing contractNumber=90001305
    await Promise.all([
      page.waitForResponse(
        resp => resp.url().includes('contractNumber=90001305') && resp.status() === 200,
        { timeout: 15000 }
      ),
      searchInput.fill('90001305')
    ]);

    const row = page.locator('table tbody tr').filter({ hasText: '90001305' }).first();
    await expect(row).toBeVisible({ timeout: 20000 });
    const editBtn = row.locator('button[title="Editar"]');
    await expect(editBtn).toBeVisible({ timeout: 10000 });
    await editBtn.click();
    await expect(page.getByRole('heading', { name: 'Editar Contrato' }).first()).toBeVisible({ timeout: 15000 });

    await expect(getFormField(page, 'Número do Contrato').locator('input')).toHaveValue('90001305', { timeout: 10000 });
    await expect(getFormField(page, 'Vendedor').getByRole('textbox')).toHaveValue(/Leonardo Bandieri/, { timeout: 10000 });
    await expect(getFormField(page, 'Grupo (Opcional)').getByRole('textbox')).toHaveValue('1680', { timeout: 10000 });
    await expect(getFormField(page, 'Ponto de Venda').getByRole('textbox')).toHaveValue('KNAAN INVESTIMENTOS LTDA', { timeout: 10000 });
    await expect(getFormField(page, 'Valor Total').locator('input')).toHaveValue(/50.*000/, { timeout: 10000 });
    await expect(getFormField(page, 'Status').getByRole('textbox')).toHaveValue('Ativo', { timeout: 10000 });
    await expect(getFormField(page, 'Data de Início').locator('input')).toHaveValue('2025-09-19', { timeout: 10000 });
    await expect(getFormField(page, 'Nome do Cliente').locator('input')).toHaveValue('Lucas Maggio de Oliveira', { timeout: 10000 });
    await expect(getFormField(page, 'Número da Matrícula').getByRole('textbox')).toHaveValue('11177', { timeout: 10000 });
  });

  test('verify contract 868498 - Paulo Carvalho', async ({ page }) => {
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(1000);
    }

    const searchInput = page.locator('input#filterContractNumber');

    // Wait for the debounced search API call containing contractNumber=868498
    await Promise.all([
      page.waitForResponse(
        resp => resp.url().includes('contractNumber=868498') && resp.status() === 200,
        { timeout: 15000 }
      ),
      searchInput.fill('868498')
    ]);

    const row = page.locator('table tbody tr').filter({ hasText: '868498' }).first();
    await expect(row).toBeVisible({ timeout: 20000 });
    const editBtn = row.locator('button[title="Editar"]');
    await expect(editBtn).toBeVisible({ timeout: 10000 });
    await editBtn.click();
    await expect(page.getByRole('heading', { name: 'Editar Contrato' }).first()).toBeVisible({ timeout: 15000 });

    await expect(getFormField(page, 'Número do Contrato').locator('input')).toHaveValue('868498', { timeout: 10000 });
    await expect(getFormField(page, 'Vendedor').getByRole('textbox')).toHaveValue(/Paulo Carvalho/, { timeout: 10000 });
    await expect(getFormField(page, 'Grupo (Opcional)').getByRole('textbox')).toHaveValue('12135', { timeout: 10000 });
    await expect(getFormField(page, 'Ponto de Venda').getByRole('textbox')).toHaveValue('TSALACH CONSULTORIA LTDA', { timeout: 10000 });
    await expect(getFormField(page, 'Valor Total').locator('input')).toHaveValue(/100.*000/, { timeout: 10000 });
    await expect(getFormField(page, 'Status').getByRole('textbox')).toHaveValue(/Cancelado|Inadimplente|Defaulted|Ativo/, { timeout: 10000 });
    await expect(getFormField(page, 'Data de Início').locator('input')).toHaveValue('2025-06-05', { timeout: 10000 });
    await expect(getFormField(page, 'Nome do Cliente').locator('input')).toHaveValue('Ellen Mansur do Nascimento', { timeout: 10000 });
    await expect(getFormField(page, 'Número da Matrícula').getByRole('textbox')).toHaveValue('6111', { timeout: 10000 });
  });
});
