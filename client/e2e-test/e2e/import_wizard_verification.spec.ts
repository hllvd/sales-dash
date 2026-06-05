import { test, expect } from '@playwright/test';

test.describe('Import Wizard Record Verification', () => {
  // Use parallel mode for these independent verification tests
  test.describe.configure({ mode: 'parallel' });

  test.beforeEach(async ({ page }) => {
    // Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');
    
    // Go to Contracts page
    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15000 });
  });

  const getFormField = (page, label: string) => page.locator('div').filter({ has: page.locator('label', { hasText: label, exact: true }) }).last();

  test('verify contract 90001305 - Leonardo Bandieri', async ({ page }) => {
    await page.fill('input#filterContractNumber', '90001305');
    // Wait for filter debounce and table to settle
    await page.waitForTimeout(1000);
    const row = page.locator('table tbody tr').filter({ hasText: '90001305' });
    await expect(row).toBeVisible({ timeout: 20000 });
    const editBtn = row.locator('button[title="Editar"]');
    await expect(editBtn).toBeVisible({ timeout: 10000 });
    await editBtn.click();
    await expect(page.getByRole('heading', { name: 'Editar Contrato' }).first()).toBeVisible({ timeout: 15000 });

    await expect(getFormField(page, 'Número do Contrato').locator('input')).toHaveValue('90001305');
    await expect(getFormField(page, 'Vendedor').getByRole('textbox')).toHaveValue(/Leonardo Bandieri.*11177/);
    await expect(getFormField(page, 'Grupo (Opcional)').getByRole('textbox')).toHaveValue('1680');
    await expect(getFormField(page, 'Ponto de Venda').getByRole('textbox')).toHaveValue('KNAAN INVESTIMENTOS LTDA');
    await expect(getFormField(page, 'Valor Total').locator('input')).toHaveValue(/50.*000/);
    await expect(getFormField(page, 'Status').getByRole('textbox')).toHaveValue('Ativo');
    await expect(getFormField(page, 'Data de Início').locator('input')).toHaveValue('2025-09-19');
    await expect(getFormField(page, 'Nome do Cliente').locator('input')).toHaveValue('Lucas Maggio de Oliveira');
    await expect(getFormField(page, 'Número da Matrícula (Opcional)').locator('input')).toHaveValue('11177');
  });

  test('verify contract 868498 - Paulo Carvalho', async ({ page }) => {
    await page.fill('input#filterContractNumber', '868498');
    // Wait for filter debounce and table to settle
    await page.waitForTimeout(1000);
    const row = page.locator('table tbody tr').filter({ hasText: '868498' });
    await expect(row).toBeVisible({ timeout: 20000 });
    const editBtn = row.locator('button[title="Editar"]');
    await expect(editBtn).toBeVisible({ timeout: 10000 });
    await editBtn.click();
    await expect(page.getByRole('heading', { name: 'Editar Contrato' }).first()).toBeVisible({ timeout: 15000 });

    await expect(getFormField(page, 'Número do Contrato').locator('input')).toHaveValue('868498');
    await expect(getFormField(page, 'Vendedor').getByRole('textbox')).toHaveValue(/Paulo Carvalho.*6111/);
    await expect(getFormField(page, 'Grupo (Opcional)').getByRole('textbox')).toHaveValue('12135');
    await expect(getFormField(page, 'Ponto de Venda').getByRole('textbox')).toHaveValue('TSALACH CONSULTORIA LTDA');
    await expect(getFormField(page, 'Valor Total').locator('input')).toHaveValue(/100.*000/);
    await expect(getFormField(page, 'Status').getByRole('textbox')).toHaveValue('Cancelado');
    await expect(getFormField(page, 'Data de Início').locator('input')).toHaveValue('2025-06-05');
    await expect(getFormField(page, 'Nome do Cliente').locator('input')).toHaveValue('Ellen Mansur do Nascimento');
    await expect(getFormField(page, 'Número da Matrícula (Opcional)').locator('input')).toHaveValue('6111');
  });
});
