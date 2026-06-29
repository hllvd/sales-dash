import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

test.describe('Delete User with Contract Migration E2E Flow', () => {
  const RUN_ID = Date.now().toString().slice(-4);
  const PARENT_EMAIL = `superior.e2e.${RUN_ID}@test.com`;
  const CHILD_EMAIL = `subordinado.e2e.${RUN_ID}@test.com`;
  const PARENT_MATRICULA = `MAT-SUP-${RUN_ID}`;
  const CHILD_MATRICULA = `MAT-SUB-${RUN_ID}`;
  const CONTRACT_NUMBER = `CON-E2E-${RUN_ID}`;

  const testDataDir = path.resolve(process.cwd(), 'test-data');
  const filePath = path.join(testDataDir, `migration_hierarchy_${RUN_ID}.csv`);

  test.beforeAll(async () => {
    // CSV headers: Name,Email,ParentEmail,Matricula,IsMatriculaOwner
    const csvContent = [
      'Name,Email,ParentEmail,Matricula,IsMatriculaOwner',
      `Superior E2E ${RUN_ID},${PARENT_EMAIL},,${PARENT_MATRICULA},1`,
      `Subordinado E2E ${RUN_ID},${CHILD_EMAIL},${PARENT_EMAIL},${CHILD_MATRICULA},1`
    ].join('\n');

    if (!fs.existsSync(testDataDir)) {
      fs.mkdirSync(testDataDir, { recursive: true });
    }
    fs.writeFileSync(filePath, csvContent);
  });

  test.afterAll(async () => {
    if (fs.existsSync(filePath)) {
      fs.unlinkSync(filePath);
    }
  });

  test('should migrate contracts to parent user during deletion flow', async ({ page }) => {
    test.setTimeout(80000);

    // 1. Login as Superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // Wait for landing page to load
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 20000 });

    // 2. Import the hierarchy CSV
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Usuários em Lote')).toBeVisible();

    await page.setInputFiles('input[type="file"]', filePath);
    await page.click('button:has-text("Próximo")');
    await expect(page.getByText('Mapeamentos:')).toBeVisible({ timeout: 10000 });

    await expect(page.locator('button:has-text("Confirmar e Importar")')).toBeEnabled({ timeout: 10000 });
    await page.click('button:has-text("Confirmar e Importar")');

    // Close import result modal
    await expect(page.getByText('Importados:')).toBeVisible({ timeout: 15000 });
    await page.click('button:has-text("Fechar")');

    // Verify users were successfully imported by searching on Users page
    await page.fill('input[placeholder="Buscar por nome ou email..."]', CHILD_EMAIL);
    await page.waitForTimeout(1000); // Wait for debounce
    const userRow = page.locator('table tbody tr').filter({ hasText: CHILD_EMAIL });
    await expect(userRow).toBeVisible({ timeout: 10000 });

    // Clear search input so it doesn't affect subsequent searches
    await page.fill('input[placeholder="Buscar por nome ou email..."]', '');
    await page.waitForTimeout(500);

    // 3. Create a contract assigned to the child user
    await page.click('a[href="#/contracts"]');
    await expect(page.getByText('Gerenciamento de Contratos')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15000 });

    await page.click('button:has-text("Criar")');
    const contractModal = page.getByRole('dialog');
    await contractModal.locator('input[required]').first().fill(CONTRACT_NUMBER);
    await contractModal.locator('input[type="date"]').fill('2026-01-01');
    await contractModal.locator('input[inputmode="decimal"]').first().fill('5000');

    // Open Vendedor Select dropdown
    const vendedorSelect = contractModal.getByPlaceholder('Selecione o vendedor');
    await vendedorSelect.click();
    
    // Clear any pre-selected value (e.g. "Sem vendedor atribuído") to avoid search contamination
    await vendedorSelect.fill('');
    await page.waitForTimeout(300);
    
    await vendedorSelect.pressSequentially(CHILD_EMAIL); // Type sequentially to trigger search list popover
    await page.waitForTimeout(1000); // Wait for search results to filter

    // Select options containing child user and child matricula
    await page.getByRole('option').filter({ hasText: CHILD_EMAIL }).first().click();

    // Save contract
    await contractModal.locator('button[type="submit"]').click();
    await expect(page.getByText('Contrato criado com sucesso')).toBeVisible();

    // 4. Navigate back to Users page and trigger deletion flow of child user
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // Search child user
    await page.fill('input[placeholder="Buscar por nome ou email..."]', CHILD_EMAIL);
    await page.waitForTimeout(1000); // Wait for debounce

    const childRow = page.locator('table tbody tr').filter({ hasText: CHILD_EMAIL });
    await childRow.locator('button[title="Excluir"]').click();

    // 5. Verify the Portuguese alert and checkbox in Delete modal (Step 1)
    const deleteDialog = page.getByRole('dialog');
    await expect(deleteDialog).toBeVisible();
    await expect(deleteDialog.getByText('Contratos Detectados')).toBeVisible();
    await expect(deleteDialog.getByText('Gostaria de atribuir estes contratos ao superior')).toBeVisible();
    await expect(deleteDialog.getByText(`Superior E2E ${RUN_ID}`)).toBeVisible();

    // Verify checkbox is checked by default and click Continue
    const checkbox = deleteDialog.locator('input[type="checkbox"]');
    await expect(checkbox).toBeChecked();
    await deleteDialog.getByRole('button', { name: 'Continuar' }).click();

    // 6. Verify Matriculas preview step (Step 2)
    await expect(deleteDialog.getByText('Prévia da Migração de Contratos')).toBeVisible();
    await expect(deleteDialog.getByText(PARENT_MATRICULA)).toBeVisible();
    
    // Total should show 1 contract
    await expect(deleteDialog.getByText('Total de contratos a migrar: 1')).toBeVisible();

    // Click Executar
    await deleteDialog.getByRole('button', { name: 'Executar' }).click();

    // 7. Verify success toast message and check that user is inactive
    await expect(page.getByText('1 contratos migrados e usuário')).toBeVisible({ timeout: 15000 });
    await expect(childRow.getByText('Inativo')).toBeVisible({ timeout: 15000 });

    // 8. Now delete the Parent User (who has no parent/superior direct)
    await page.fill('input[placeholder="Buscar por nome ou email..."]', PARENT_EMAIL);
    await page.waitForTimeout(1000); // Wait for debounce

    const parentRow = page.locator('table tbody tr').filter({ hasText: PARENT_EMAIL });
    await parentRow.locator('button[title="Excluir"]').click();

    // Verify explicit parent-less warning alert shows up
    await expect(deleteDialog).toBeVisible();
    await expect(deleteDialog.getByText('Sem Superior Cadastrado')).toBeVisible();
    await expect(deleteDialog.getByText('não possui um superior cadastrado. Caso queira migrar seus contratos antes de excluí-lo')).toBeVisible();

    // Click Excluir direct deletion button
    await deleteDialog.getByRole('button', { name: 'Excluir' }).click();

    // Verify parent user becomes Inactive
    await expect(parentRow.getByText('Inativo')).toBeVisible({ timeout: 15000 });
  });

  test('admin should see restriction message when trying to delete a user', async ({ page }) => {
    test.setTimeout(45000);

    // 1. Login as Admin Carlos Mendes
    await page.goto('/');
    await page.fill('input[type="email"]', 'carlosmendes@example.com');
    await page.fill('input[type="password"]', '123456');
    await page.click('button.login-button');

    // Wait for landing page to load
    await expect(page.locator('.mantine-AppShell-navbar')).toBeVisible({ timeout: 15000 });

    // 2. Go to Users page
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // 3. Find Julio Mota
    await page.fill('input[placeholder="Buscar por nome ou email..."]', 'juliomota@example.com');
    await page.waitForTimeout(1000); // Wait for debounce

    const childRow = page.locator('table tbody tr').filter({ hasText: 'juliomota@example.com' });
    await expect(childRow).toBeVisible({ timeout: 10000 });

    // 4. Click Excluir
    await childRow.locator('button[title="Excluir"]').click();

    // 5. Verify the Portuguese restriction warning
    const deleteDialog = page.getByRole('dialog');
    await expect(deleteDialog).toBeVisible();
    await expect(deleteDialog.getByText('Ação Não Permitida')).toBeVisible();
    await expect(deleteDialog.getByText('Você não pode fazer isso. Para excluir e migrar os contratos deste usuário para o usuário superior')).toBeVisible();

    // 6. Verify only Fechar button is available (no Continuar/Excluir/Executar)
    const fecharButton = deleteDialog.getByRole('button', { name: 'Fechar' });
    await expect(fecharButton).toBeVisible();
    await expect(deleteDialog.getByRole('button', { name: 'Continuar' })).not.toBeVisible();
    await expect(deleteDialog.getByRole('button', { name: 'Excluir' })).not.toBeVisible();
    await expect(deleteDialog.getByRole('button', { name: 'Executar' })).not.toBeVisible();

    // 7. Click Fechar and verify dialog is closed
    await fecharButton.click();
    await expect(deleteDialog).not.toBeVisible();
  });
});
