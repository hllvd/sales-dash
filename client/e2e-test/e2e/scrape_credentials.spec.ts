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
});
