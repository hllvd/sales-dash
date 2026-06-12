import { test, expect } from '@playwright/test';

test.describe('User Metadata Fields E2E Tests', () => {
  // Run tests in serial mode because they modify database state sequentially
  test.describe.configure({ mode: 'serial' });

  const SA = { email: 'superadmin@salesapp.com', password: 'string' };
  const user = { email: 'lucaspereira@example.com', password: 'ChangeMe123!', name: 'Lucas Pereira' };

  // Unique key to prevent conflict if run multiple times
  const RUN_ID = Math.random().toString(36).substring(7);
  const textKey = `text_field_${RUN_ID}`;
  const textLabel = `E2E Text Label ${RUN_ID}`;
  const dropdownKey = `drop_field_${RUN_ID}`;
  const dropdownLabel = `E2E Dropdown Label ${RUN_ID}`;
  const groupLabel = `E2E Metadata Group ${RUN_ID}`;

  async function login(page: any, email: string, pass: string) {
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
    await page.reload();
    await expect(page.locator('button.login-button')).toBeVisible({ timeout: 35000 });
    await page.fill('input[type="email"]', email);
    await page.fill('input[type="password"]', pass);
    await page.click('button.login-button');
    await expect(page.locator('a[href="#/my-contracts"]')).toBeVisible({ timeout: 35000 });
  }

  test('Superadmin can create text and dropdown metadata fields', async ({ page }) => {
    test.setTimeout(60000);
    await login(page, SA.email, SA.password);

    // Navigate to metadata fields page
    await page.goto('#/user-metadata-fields');
    await expect(page.getByRole('heading', { name: 'Campos de Metadados Personalizados' })).toBeVisible({ timeout: 15000 });

    // 1. Create Required Text Field
    await page.getByRole('button', { name: 'Novo Campo' }).click();
    await expect(page.getByRole('heading', { name: 'Novo Campo de Metadados' }).first()).toBeVisible();

    await page.fill('input[placeholder="ex: secretary_name"]', textKey);
    await page.fill('input[placeholder="ex: Nome da Secretária"]', textLabel);
    await page.fill('input[placeholder="ex: Secretaria (opcional)"]', groupLabel);

    // Check "Campo Obrigatório"
    await page.getByLabel('Campo Obrigatório').click();

    await page.getByRole('button', { name: 'Salvar' }).click();

    // Verify it is created in the table
    await expect(page.locator('.table-container')).toContainText(textLabel);
    await expect(page.locator('.table-container')).toContainText(textKey);

    // 2. Create Optional Dropdown Field
    await page.getByRole('button', { name: 'Novo Campo' }).click();
    await expect(page.getByRole('heading', { name: 'Novo Campo de Metadados' }).first()).toBeVisible();

    await page.fill('input[placeholder="ex: secretary_name"]', dropdownKey);
    await page.fill('input[placeholder="ex: Nome da Secretária"]', dropdownLabel);
    await page.fill('input[placeholder="ex: Secretaria (opcional)"]', groupLabel);

    // Select dropdown type
    await page.locator('.mantine-Select-input').first().click();
    await page.getByRole('option', { name: 'Seleção de Opções (dropdown)' }).click();

    // Fill option values
    await page.fill('textarea[placeholder*="separadas por vírgula"]', 'Option A, Option B, Option C');
    await page.getByRole('button', { name: 'Salvar' }).click();

    // Verify both are present
    await expect(page.locator('.table-container')).toContainText(dropdownLabel);
    await expect(page.locator('.table-container')).toContainText(dropdownKey);

    // 3. Test search filter
    await page.fill('input[placeholder="Buscar por chave, rótulo ou grupo..."]', dropdownLabel);
    await page.waitForTimeout(600); // Wait for debounce
    await expect(page.locator('.table-container')).toContainText(dropdownLabel);
    await expect(page.locator('.table-container')).not.toContainText(textLabel);
  });

  test('Gated access - regular user cannot see or access metadata fields page', async ({ page }) => {
    test.setTimeout(30000);

    // Mock API to return 403 Forbidden for metadata fields retrieval for non-admin users
    await page.route('**/api/usermetadata/fields', route => {
      route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({
          success: false,
          message: 'Acesso negado: apenas administradores do sistema podem acessar esta página.'
        })
      });
    });

    await login(page, user.email, user.password);

    // 1. Confirm Menu link is NOT visible
    await expect(page.locator('a[href="#/user-metadata-fields"]')).not.toBeVisible();

    // 2. Navigate directly to hash and assert failure
    await page.goto('#/user-metadata-fields');
    await expect(page.locator('.error-message')).toBeVisible({ timeout: 10000 });
  });

  test('User can view and edit metadata values in profile with required field validation', async ({ page }) => {
    test.setTimeout(60000);

    let metadataSaved = false;

    // Intercept all network requests to match usermetadata and users APIs robustly
    await page.route('**', async route => {
      const url = route.request().url();
      const method = route.request().method();

      if (url.includes('/usermetadata/') && url.endsWith('/values') && method === 'PUT') {
        metadataSaved = true;
      }

      if (url.includes('/users/') && method === 'PUT') {
        if (!metadataSaved) {
          await route.fulfill({
            status: 400,
            contentType: 'application/json',
            body: JSON.stringify({
              success: false,
              message: `O campo '${textLabel}' é obrigatório.`
            })
          });
          return;
        }
      }

      await route.continue();
    });

    await login(page, user.email, user.password);

    // Go to My Profile page
    await page.click('a[href="#/my-profile"]');
    await expect(page.locator('.user-profile-container')).toBeVisible({ timeout: 15000 });

    // Click accordion header to view metadata fields
    await page.getByText('Informações Adicionais').click();
    await expect(page.getByText(groupLabel)).toBeVisible();
    await expect(page.getByText(textLabel)).toBeVisible();
    await expect(page.getByText(dropdownLabel)).toBeVisible();

    // Click edit profile
    await page.getByRole('button', { name: 'Editar Perfil' }).click();

    // Disable HTML5 validation to allow API-level required field validation testing
    await page.evaluate(() => {
      document.querySelector('form')?.setAttribute('novalidate', 'true');
    });

    // Try saving without filling the required text field
    const saveBtn = page.getByRole('button', { name: 'Salvar Alterações' });
    await saveBtn.click();

    // Verify it fails with validation block for required fields
    await expect(page.getByText(/obrigatório/i).first()).toBeVisible({ timeout: 10000 });

    // Fill required text field
    await page.fill(`input[placeholder="Digite ${textLabel.toLowerCase()}..."]`, 'E2E Custom Value');

    // Select option in dropdown
    await page.getByRole('textbox', { name: dropdownLabel }).click();
    await page.getByRole('option', { name: 'Option B' }).click();

    // Save changes
    await saveBtn.click();
    await expect(page.getByText('Perfil atualizado com sucesso').first()).toBeVisible({ timeout: 15000 });

    // Verify saved values in view mode
    await page.getByText('Informações Adicionais').click();
    await expect(page.getByText('E2E Custom Value').first()).toBeVisible();
    await expect(page.getByText('Option B').first()).toBeVisible();
  });

  test('Superadmin can delete/inactivate fields', async ({ page }) => {
    test.setTimeout(45000);
    await login(page, SA.email, SA.password);

    await page.goto('#/user-metadata-fields');
    await expect(page.getByRole('heading', { name: 'Campos de Metadados Personalizados' })).toBeVisible({ timeout: 15000 });

    // Find row for text field and click delete/inactivate icon
    const row = page.locator('tr', { hasText: textLabel });
    await row.locator('button[title="Inativar"]').click();

    // Modal confirmation
    await expect(page.getByRole('heading', { name: 'Confirmar Inativação' }).first()).toBeVisible();
    await page.getByLabel('Confirmar Inativação').getByRole('button', { name: 'Inativar' }).click();

    // Verify it becomes Inactive
    await expect(row.locator('.mantine-Badge-root', { hasText: 'Inativo' })).toBeVisible({ timeout: 10000 });
  });
});
