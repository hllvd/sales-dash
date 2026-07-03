import { test, expect } from '@playwright/test';

test.describe('Contracts UI Enhancements', () => {
  test.beforeEach(async ({ page }) => {
    // Clear localStorage to ensure clean state
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());
  });

  test('should have end date filter default to today and validate against start date', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as admin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Navigate to /#/contracts
    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });

    // 3. Verify End Date input exists and defaults to today's date
    const endDateInput = page.locator('input#filterEndDate');
    await expect(endDateInput).toBeVisible();
    
    const today = new Date().toISOString().split('T')[0];
    const defaultValue = await endDateInput.inputValue();
    expect(defaultValue).toBe(today);

    // Verify it is stored in localStorage
    const savedEndDate = await page.evaluate(() => localStorage.getItem('contracts_filterEndDate'));
    expect(savedEndDate).toBe(today);

    // 4. Change Start Date to be after End Date to trigger local validation
    // Let's set start date to a future date
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    const tomorrowStr = tomorrow.toISOString().split('T')[0];

    // Set filterStartDate
    await page.fill('input#filterStartDate', tomorrowStr);

    // Assert that the validation error is visible below the End Date input
    const errorMsg = page.locator('.filter-error-msg');
    await expect(errorMsg).toBeVisible();
    await expect(errorMsg).toHaveText('Data fim deve ser maior ou igual à data início');

    // Make sure it doesn't call API with this invalid date or blocks the API
    // We can verify this by checking if the clear filters button is still working or no requests with tomorrow's date were successfully used to update the contracts.
    
    // 5. Correct the dates (Set Start Date to a past date)
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 2);
    const yesterdayStr = yesterday.toISOString().split('T')[0];
    await page.fill('input#filterStartDate', yesterdayStr);

    // Validation error should disappear
    await expect(errorMsg).not.toBeVisible();
  });

  test('should toggle column visibility via Column Modal', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as admin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Navigate to /#/contracts
    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });

    // 3. Verify 'Colunas' button is present and click it
    const colBtn = page.getByRole('button', { name: 'Colunas' });
    await expect(colBtn).toBeVisible();
    await colBtn.click();

    // 4. Check if Selection Modal opens
    const modalTitle = page.locator('.mantine-Modal-title');
    await expect(modalTitle).toBeVisible();
    await expect(modalTitle).toHaveText('Selecionar Colunas');

    // Verify Checkboxes are shown
    const modal = page.locator('.mantine-Modal-content');
    const userCheckbox = modal.getByRole('checkbox', { name: 'Usuário' });
    await expect(userCheckbox).toBeVisible();
    await expect(userCheckbox).toBeChecked();

    const quotaCheckbox = modal.getByRole('checkbox', { name: 'Cota' });
    await expect(quotaCheckbox).toBeVisible();
    await expect(quotaCheckbox).not.toBeChecked();

    // Verify 'Cota' is not visible in table headers initially
    const tableHeader = page.locator('table thead tr th');
    await expect(tableHeader.filter({ hasText: 'Cota' })).not.toBeVisible();

    // 5. Uncheck 'Usuário', check 'Cota' and click 'Concluir'
    await userCheckbox.uncheck();
    await quotaCheckbox.check();
    await modal.getByRole('button', { name: 'Concluir' }).click();

    // Verify modal is closed
    await expect(modalTitle).not.toBeVisible();

    // Verify 'Usuário' header is no longer in the table, but 'Cota' is
    await expect(tableHeader.filter({ hasText: 'Usuário' })).not.toBeVisible();
    await expect(tableHeader.filter({ hasText: 'Cota' })).toBeVisible();

    // Verify localStorage has the state saved
    const visibleColumns = await page.evaluate(() => localStorage.getItem('contracts_visibleColumns'));
    expect(visibleColumns).toContain('"user":false');
    expect(visibleColumns).toContain('"quota":true');

    // 6. Click 'Colunas' again and click 'Restaurar Padrão'
    await colBtn.click();
    await expect(modalTitle).toBeVisible();
    await modal.getByRole('button', { name: 'Restaurar Padrão' }).click();
    
    // Check that checkbox states are restored
    await expect(quotaCheckbox).not.toBeChecked();
    await expect(userCheckbox).toBeChecked();
    
    await modal.getByRole('button', { name: 'Concluir' }).click();

    // Verify 'Usuário' is visible again, and 'Cota' is hidden
    await expect(tableHeader.filter({ hasText: 'Usuário' })).toBeVisible();
    await expect(tableHeader.filter({ hasText: 'Cota' })).not.toBeVisible();
  });

  test('should show correct empty state in My Contracts when filters are applied', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as standard user (carlosmendes@example.com is a matricula owner/user)
    await page.goto('/');
    await page.fill('input[type="email"]', 'carlosmendes@example.com');
    await page.fill('input[type="password"]', '123456');
    await page.click('button.login-button');

    // 2. Navigate to Meus Contratos
    await page.getByRole('link', { name: 'Meus Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });

    // 3. Set a filter that returns no results (e.g. matricula filter to '9999999999999')
    const matriculaFilterInput = page.locator('input#matriculaFilter');
    await expect(matriculaFilterInput).toBeVisible();
    await page.fill('input#matriculaFilter', '9999999999999');

    // Wait for debounce and reload
    await page.waitForTimeout(1000);

    // 4. Verify empty state message is shown
    const emptyStateText = page.locator('.my-contracts-empty p');
    await expect(emptyStateText).toBeVisible();
    await expect(emptyStateText).toContainText('Nenhum contrato correspondente aos filtros aplicados foi encontrado');

    const clearFiltersBtn = page.locator('.my-contracts-empty').getByRole('button', { name: 'Limpar Filtros' });
    await expect(clearFiltersBtn).toBeVisible();

    // 5. Click Limpar Filtros and check that matricula input gets cleared
    await clearFiltersBtn.click();
    await expect(matriculaFilterInput).toHaveValue('');
  });
});
