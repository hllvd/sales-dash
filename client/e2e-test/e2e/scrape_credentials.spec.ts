import { test, expect } from '@playwright/test';
import { loginAs, ADMIN_EMAIL, ADMIN_PASSWORD } from './helpers/auth';

test.describe('Scrape Credentials Management (TEAR 2)', () => {
  const testStore = 'AHU - PR';
  const testMatricula = '123456';
  const testPassword = 'testpassword';

  test('should add a new scrape credential without testing auth, then remove it', async ({ page }) => {
    test.setTimeout(60000);
    console.log(`>>> [Tear 2] Logging in as Admin to test Scrape Credentials`);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    // Auto-accept deletion confirmation popups
    page.on('dialog', dialog => {
      console.log(`>>> DIALOG POPPED UP: ${dialog.message()}`);
      dialog.accept().catch(() => {});
    });

    // Go directly to Scrapes dashboard
    await page.goto('/#/scrapes');
    await expect(page.getByRole('heading', { name: 'Extração PowerBI' })).toBeVisible({ timeout: 15000 });

    // Clean up any existing config for this store just in case
    const accountRow = page.locator('tr').filter({ has: page.getByText(testStore, { exact: true }) }).first();

    const getTrashButton = (row: typeof accountRow) =>
      row.getByTestId('delete-scrape-config-btn').first();

    let cleanupAttempts = 0;
    while (await accountRow.isVisible() && cleanupAttempts < 3) {
      cleanupAttempts++;
      const trashBtn = getTrashButton(accountRow);
      if (await trashBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await trashBtn.click();
        await page.waitForTimeout(1000);
      } else {
        break;
      }
    }

    // Click Nova Conta
    await page.getByRole('button', { name: 'Nova Conta' }).click();

    // Select Relatório Geral to enable Unidade (Store) field
    await page.getByText('Relatório Geral (Loja/PV)').click();

    // Fill the configuration form
    const storeInput = page.getByPlaceholder('Tentar selecionar automaticamente');
    await storeInput.click();
    await storeInput.fill(testStore);
    await page.getByRole('option', { name: testStore, exact: true }).click();

    await page.getByPlaceholder('Ex: 99999').fill(testMatricula);
    await page.getByPlaceholder('Digite sua senha').fill(testPassword);

    // Uncheck "Validar credenciais ao salvar" to prevent auth testing
    await page.getByLabel('Validar credenciais ao salvar').uncheck();

    // Save
    await page.getByRole('button', { name: 'Salvar Configuração' }).click();

    // Verify success notification and modal closure
    await expect(page.getByText('Configuração salva com sucesso')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // We must wait for the configuration to appear in the table
    await expect(accountRow).toBeVisible({ timeout: 20_000 });
    await expect(accountRow).toContainText(testMatricula);
    await expect(accountRow).toContainText('Não Testada');

    // Clean up: Remove the created config
    const trashButton = getTrashButton(accountRow);
    console.log(`>>> Clicking Trash button...`);
    await expect(trashButton).toBeVisible({ timeout: 10000 });
    await trashButton.click();

    // Verify row is removed from table
    await expect(accountRow).not.toBeVisible({ timeout: 15000 });

    console.log(`>>> [Tear 2] Scrape credentials test completed successfully.`);
  });

  test('should allow saving credentials without store (optional store, only matricula and password mandatory)', async ({ page }) => {
    test.setTimeout(60000);
    console.log(`>>> [Tear 2] Testing saving credentials without store`);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    page.on('dialog', dialog => {
      dialog.accept().catch(() => {});
    });

    await page.goto('/#/scrapes');
    await expect(page.getByRole('heading', { name: 'Extração PowerBI' })).toBeVisible({ timeout: 15000 });

    // Click Nova Conta
    await page.getByRole('button', { name: 'Nova Conta' }).click();

    // Fill only matricula and password (store left empty)
    const optionalMatricula = '654321';
    await page.getByPlaceholder('Ex: 99999').fill(optionalMatricula);
    await page.getByPlaceholder('Digite sua senha').fill('anysecretpass');

    // Uncheck "Validar credenciais ao salvar"
    await page.getByLabel('Validar credenciais ao salvar').uncheck();

    // Save
    await page.getByRole('button', { name: 'Salvar Configuração' }).click();

    // Verify success notification and modal closure
    await expect(page.getByText('Configuração salva com sucesso')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // Find row with this matricula
    const autoRow = page.locator('tr').filter({ has: page.getByText(optionalMatricula, { exact: true }) }).first();
    await expect(autoRow).toBeVisible({ timeout: 20000 });
    await expect(autoRow).toContainText('Tentar selecionar automaticamente');
    await expect(autoRow).toContainText('Não Testada');

    // Clean up: delete config
    const trashBtn = autoRow.getByTestId('delete-scrape-config-btn').first();
    await expect(trashBtn).toBeVisible({ timeout: 10000 });
    await trashBtn.click();
    await expect(autoRow).not.toBeVisible({ timeout: 15000 });
  });

  test('should auto-fill store and/or startDate when validating credentials on save', async ({ page }) => {
    test.setTimeout(60000);
    console.log(`>>> [Tear 2] Testing auto-fill of store/startDate on save with validation`);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    page.on('dialog', dialog => {
      dialog.accept().catch(() => {});
    });

    await page.goto('/#/scrapes');
    await expect(page.getByRole('heading', { name: 'Extração PowerBI' })).toBeVisible({ timeout: 15000 });

    // Click Nova Conta
    await page.getByRole('button', { name: 'Nova Conta' }).click();

    // Select Relatório Geral to enable store auto-detection
    await page.getByText('Relatório Geral (Loja/PV)').click();

    // Fill only matricula and password, keep "Validar credenciais ao salvar" checked
    const autoDetectMatricula = '789012';
    await page.getByPlaceholder('Ex: 99999').fill(autoDetectMatricula);
    await page.getByPlaceholder('Digite sua senha').fill('validsecretpass');

    // Ensure "Validar credenciais ao salvar" is checked
    const validateCheck = page.getByLabel('Validar credenciais ao salvar');
    if (!(await validateCheck.isChecked())) {
      await validateCheck.check();
    }

    // Save
    await page.getByRole('button', { name: 'Salvar Configuração' }).click();

    // Verify success notification and modal closure
    await expect(page.getByText('Configuração salva com sucesso')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // In E2E mode, validation auto-populates Store to 'AHU - PR' and DefaultStartMonth to current month
    const validatedRow = page.locator('tr').filter({ has: page.getByText(autoDetectMatricula, { exact: true }) }).first();
    await expect(validatedRow).toBeVisible({ timeout: 20000 });
    await expect(validatedRow).toContainText('AHU - PR');
    await expect(validatedRow).toContainText('Válida');

    // Clean up: delete config
    const trashBtn = validatedRow.getByTestId('delete-scrape-config-btn').first();
    await expect(trashBtn).toBeVisible({ timeout: 10000 });
    await trashBtn.click();
    await expect(validatedRow).not.toBeVisible({ timeout: 15000 });
  });

  test('should display Opções Avançadas accordion with correct default checkboxes and allow updating them', async ({ page }) => {
    test.setTimeout(60000);
    console.log(`>>> [Tear 2] Testing Opções Avançadas in Scrape modal`);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    page.on('dialog', dialog => {
      dialog.accept().catch(() => {});
    });

    await page.goto('/#/scrapes');
    await expect(page.getByRole('heading', { name: 'Extração PowerBI' })).toBeVisible({ timeout: 15000 });

    // Click Nova Conta
    await page.getByRole('button', { name: 'Nova Conta' }).click();

    // Fill form
    const advancedMatricula = '554433';
    await page.getByPlaceholder('Ex: 99999').fill(advancedMatricula);
    await page.getByPlaceholder('Digite sua senha').fill('advsecretpass');
    await page.getByLabel('Validar credenciais ao salvar').uncheck();

    // Open Opções Avançadas accordion
    await page.getByText('Opções Avançadas').click();
    await expect(page.getByText('Opções de Importação:')).toBeVisible();

    // Verify defaults
    const optSkipMissing = page.getByLabel('Pular linhas sem número de contrato (útil para arquivos com subtotais ou lixo)');
    const optAutoGroups = page.getByLabel('Permitir criação automática de grupos');
    const optAutoPVs = page.getByLabel('Permitir criação automática de PV');
    const optUpdateMatricula = page.getByLabel('Atualizar matrícula em contratos existentes');
    const optUpdateTotal = page.getByLabel('Atualizar valor total em contratos existentes');
    const optUpdateDate = page.getByLabel('Atualizar data do contrato');

    await expect(optSkipMissing).toBeChecked();
    await expect(optAutoGroups).toBeChecked();
    await expect(optAutoPVs).toBeChecked();
    await expect(optUpdateMatricula).not.toBeChecked();
    await expect(optUpdateTotal).toBeChecked();
    await expect(optUpdateDate).toBeChecked();

    // Toggle option to test persistence (check UpdateMatricula)
    await optUpdateMatricula.check();
    await expect(optUpdateMatricula).toBeChecked();

    // Save
    await page.getByRole('button', { name: 'Salvar Configuração' }).click();
    await expect(page.getByText('Configuração salva com sucesso')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // Find row with this matricula and click edit
    const advRow = page.locator('tr').filter({ has: page.getByText(advancedMatricula, { exact: true }) }).first();
    await expect(advRow).toBeVisible({ timeout: 20000 });

    const editBtn = advRow.getByTestId('edit-scrape-config-btn').first();
    await editBtn.click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 10000 });

    // Open accordion and verify updated option persisted
    await page.getByText('Opções Avançadas').click();
    await expect(page.getByLabel('Atualizar matrícula em contratos existentes')).toBeChecked();

    // Close modal
    await page.getByRole('button', { name: 'Cancelar' }).click();

    // Clean up
    const trashBtn = advRow.getByTestId('delete-scrape-config-btn').first();
    await expect(trashBtn).toBeVisible({ timeout: 10000 });
    await trashBtn.click();
    await expect(advRow).not.toBeVisible({ timeout: 15000 });
  });

  test('should select and save relative date options (1, 3, 12, 15 months, custom, all)', async ({ page }) => {
    test.setTimeout(60000);
    console.log(`>>> [Tear 2] Testing Relative Dates selection in Scrape modal`);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    page.on('dialog', dialog => dialog.accept().catch(() => {}));

    await page.goto('/#/scrapes');
    await expect(page.getByRole('heading', { name: 'Extração PowerBI' })).toBeVisible({ timeout: 15000 });

    // Open Nova Conta modal
    await page.getByRole('button', { name: 'Nova Conta' }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 10000 });

    const periodSelect = page.getByRole('textbox', { name: 'Período de Extração Padrão' });
    await expect(periodSelect).toBeVisible();

    // Default for new account is "Todas as datas (Sem filtro)"
    await expect(page.getByText('Sem filtro de data: o robô buscará todos os contratos disponíveis no portal.')).toBeVisible();

    // 1. Select Último 1 mês
    await periodSelect.click();
    await page.getByRole('option', { name: 'Último 1 mês (Mês atual)' }).click();
    await expect(page.getByText(/Extrairá dados a partir de 01\/\d{2}\/\d{4} até o mês atual\./)).toBeVisible();

    // 2. Select Últimos 3 meses
    await periodSelect.click();
    await page.getByRole('option', { name: 'Últimos 3 meses' }).click();
    await expect(page.getByText(/Extrairá dados a partir de 01\/\d{2}\/\d{4} até o mês atual\./)).toBeVisible();

    // 3. Select Últimos 12 meses
    await periodSelect.click();
    await page.getByRole('option', { name: 'Últimos 12 meses (1 ano)' }).click();
    await expect(page.getByText(/Extrairá dados a partir de 01\/\d{2}\/\d{4} até o mês atual\./)).toBeVisible();

    // 4. Select Últimos 15 meses
    await periodSelect.click();
    await page.getByRole('option', { name: 'Últimos 15 meses (Máximo)' }).click();
    await expect(page.getByText(/Extrairá dados a partir de 01\/\d{2}\/\d{4} até o mês atual\./)).toBeVisible();

    // 5. Select Mês Específico (Personalizado)
    await periodSelect.click();
    await page.getByRole('option', { name: 'Mês Específico (Personalizado)' }).click();
    const customMonthInput = page.getByLabel('Mês Inicial Personalizado');
    await expect(customMonthInput).toBeVisible();
    await customMonthInput.fill('2026-04');
    await expect(page.getByText('Extrairá dados a partir de 01/04/2026 até o mês atual.')).toBeVisible();

    // Fill credentials and save with 15 months selected
    await periodSelect.click();
    await page.getByRole('option', { name: 'Últimos 15 meses (Máximo)' }).click();

    const relMatricula = '987654';
    await page.getByPlaceholder('Ex: 99999').fill(relMatricula);
    await page.getByPlaceholder('Digite sua senha').fill('reldatepass');
    await page.getByLabel('Validar credenciais ao salvar').uncheck();

    await page.getByRole('button', { name: 'Salvar Configuração' }).click();
    await expect(page.getByText('Configuração salva com sucesso')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    // Verify row appeared in table
    const row = page.locator('tr').filter({ has: page.getByText(relMatricula, { exact: true }) }).first();
    await expect(row).toBeVisible({ timeout: 15000 });

    // Edit and verify 15 months was preserved
    const editBtn = row.getByTestId('edit-scrape-config-btn').first();
    await editBtn.click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 10000 });
    await expect(periodSelect).toHaveValue('Últimos 15 meses (Máximo)');

    // Close modal
    await page.getByRole('button', { name: 'Cancelar' }).click();

    // Cleanup
    const trashBtn = row.getByTestId('delete-scrape-config-btn').first();
    await trashBtn.click();
    await expect(row).not.toBeVisible({ timeout: 15000 });
  });

  test('should block scrape trigger when account has wrong-password (circuit breaker anti-lockout)', async ({ page }) => {
    test.setTimeout(60000);
    console.log(`>>> [Tear 2] Testing Circuit Breaker on wrong-password credential status`);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    page.on('dialog', dialog => dialog.accept().catch(() => {}));

    await page.goto('/#/scrapes');
    await expect(page.getByRole('heading', { name: 'Extração PowerBI' })).toBeVisible({ timeout: 15000 });

    const cbMatricula = '112233';
    const existingRows = page.locator('tr').filter({ has: page.getByText(cbMatricula, { exact: true }) });
    while (await existingRows.count() > 0 && await existingRows.first().isVisible().catch(() => false)) {
      const trash = existingRows.first().getByTestId('delete-scrape-config-btn');
      if (await trash.isVisible().catch(() => false)) {
        await trash.click();
        await page.waitForTimeout(1000);
      } else {
        break;
      }
    }

    // 1. Create a config with non-tested credentials
    await page.getByRole('button', { name: 'Nova Conta' }).click();
    await expect(page.getByRole('dialog')).toBeVisible({ timeout: 10000 });

    await page.getByPlaceholder('Ex: 99999').fill(cbMatricula);
    await page.getByPlaceholder('Digite sua senha').fill('badpass');
    await page.getByLabel('Validar credenciais ao salvar').uncheck();

    await page.getByRole('button', { name: 'Salvar Configuração' }).click();
    await expect(page.getByText('Configuração salva com sucesso')).toBeVisible({ timeout: 15000 });
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    const row = page.locator('tr').filter({ has: page.getByText(cbMatricula, { exact: true }) }).first();
    await expect(row).toBeVisible({ timeout: 15000 });

    // 2. Intercept GET configs to return wrong-password status (simulating background job or test failure)
    await page.route(`**/api/scrape/configs/me`, async route => {
      const response = await route.fetch();
      const json = await response.json();
      const updated = json.map((c: any) => c.matricula === cbMatricula ? { ...c, credentialStatus: 'wrong-password' } : c);
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(updated)
      });
    });

    // Click Sincronizar to reload configs with wrong-password status
    await page.getByRole('button', { name: 'Sincronizar' }).click();
    await expect(row.getByText('Senha Incorreta')).toBeVisible({ timeout: 10000 });

    // 3. Try clicking trigger button (Extrair) on the row
    const playBtn = row.getByRole('button', { name: 'Extrair' }).first();
    await playBtn.click();

    // Verify circuit breaker notification is shown and execution is prevented
    await expect(page.getByText('Extração Bloqueada')).toBeVisible({ timeout: 10000 });
    await expect(page.getByText('Esta conta está com falha de autenticação ("wrong-password")')).toBeVisible({ timeout: 10000 });

    // Cleanup
    await page.unrouteAll();
    while (await row.count() > 0 && await row.first().isVisible()) {
      const trashBtn = row.first().getByTestId('delete-scrape-config-btn');
      if (await trashBtn.isVisible()) {
        await trashBtn.click();
        await page.waitForTimeout(1000);
      } else {
        break;
      }
    }
    await expect(row).toHaveCount(0, { timeout: 15000 });
  });
});
