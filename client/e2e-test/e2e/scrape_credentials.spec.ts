import { test, expect } from '@playwright/test';
import { loginAs, ADMIN_EMAIL, ADMIN_PASSWORD } from './helpers/auth';

test.describe('Scrape Credentials Management (TEAR 2)', () => {
  const testStore = 'AHU - PR';
  const testMatricula = '123456';
  const testPassword = 'testpassword';

  test('should add a new scrape credential without testing auth, then remove it', async ({ page }) => {
    // This test has many sequential steps; increase timeout to be safe
    test.setTimeout(60000);
    console.log(`>>> [Tear 2] Logging in as Admin to test Scrape Credentials`);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);


    // Go to Scrapes dashboard
    await page.click('a[href="#/scrapes"]');
    await expect(page.getByRole('heading', { name: 'Extração PowerBI' })).toBeVisible();

    // Clean up any existing config for this store just in case
    // We use exact: true and .first() to prevent strict mode violations if multiple rows exist
    const accountRow = page.locator('tr').filter({ has: page.getByText(testStore, { exact: true }) }).first();

    while (await accountRow.isVisible()) {
      page.once('dialog', async dialog => await dialog.accept());
      const trashButton = accountRow.locator('button', { has: page.locator('.tabler-icon-trash') });
      await trashButton.click();
      await expect(page.getByText('Vínculo de conta removido')).toBeVisible({ timeout: 10000 });
      await page.waitForTimeout(500); // slight pause to allow table to update
    }

    // Click Nova Conta
    await page.getByRole('button', { name: 'Nova Conta' }).click();

    // Fill the configuration form
    await page.getByPlaceholder('Selecione a unidade').click();
    await page.getByPlaceholder('Selecione a unidade').fill(testStore);
    await page.getByRole('option', { name: testStore, exact: true }).click();

    await page.getByPlaceholder('Ex: 99999').fill(testMatricula);
    await page.getByPlaceholder('Digite sua senha').fill(testPassword);

    // Uncheck "Validar credenciais ao salvar" to prevent auth testing (runs too long, could fail)
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

    // Set up a permanent dialog handler just for this deletion phase
    page.on('dialog', async dialog => {
      console.log(`>>> DIALOG POPPED UP: ${dialog.message()}`);
      await dialog.accept();
    });

    // Capture browser console to see if React is throwing an error
    page.on('console', msg => console.log(`BROWSER CONSOLE: ${msg.text()}`));
    page.on('pageerror', err => console.log(`PAGE ERROR: ${err.message}`));

    // Clean up: Remove the created config
    const trashButton = accountRow.locator('button', { has: page.locator('.tabler-icon-trash') });
    console.log(`>>> Clicking Trash button...`);
    // Ensure the button is visible and enabled before clicking
    await expect(trashButton).toBeVisible({ timeout: 10000 });
    await trashButton.click();

    console.log(`>>> Waiting for result notification...`);

    // Wait for EITHER success or failure notification
    const successNotif = page.getByText('Vínculo de conta removido');
    const errorNotif = page.getByText('Falha ao remover');

    await expect(successNotif.or(errorNotif)).toBeVisible({ timeout: 10000 });

    // Check if it was an error
    if (await errorNotif.isVisible()) {
      throw new Error('>>> API FAIL: The backend returned an error when trying to delete the config.');
    }
    await expect(accountRow).not.toBeVisible();

    console.log(`>>> [Tear 2] Scrape credentials test completed successfully.`);
  });
});
