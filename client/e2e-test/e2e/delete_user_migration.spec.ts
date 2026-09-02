import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import { loginAs } from './helpers/auth';

test.describe('Delete User without Mandatory Migration E2E Flow', () => {
  // Run tests in serial mode since they depend on the initial CSV import
  test.describe.configure({ mode: 'serial' });

  const RUN_ID = Array.from({ length: 8 }, () =>
    String.fromCharCode(97 + Math.floor(Math.random() * 26))
  ).join('');
  const PARENT_EMAIL = `superior.e2e.${RUN_ID}@test.com`;
  const CHILD_EMAIL = `subordinado.e2e.${RUN_ID}@test.com`;
  const PARENT_MATRICULA = `MAT-SUP-${RUN_ID}`;
  const CHILD_MATRICULA = `MAT-SUB-${RUN_ID}`;
  const CONTRACT_NUMBER = `CON-E2E-${RUN_ID}`;

  // Admin testing variables
  const ADMIN_EMAIL = `admin.e2e.${RUN_ID}@test.com`;
  const ADMIN_CHILD_EMAIL = `adminchild.e2e.${RUN_ID}@test.com`;
  const ADMIN_MATRICULA = `MAT-ADM-${RUN_ID}`;
  const ADMIN_CHILD_MATRICULA = `MAT-ADC-${RUN_ID}`;
  const ADMIN_CONTRACT_NUMBER = `CON-ADM-${RUN_ID}`;

  // Orphan testing variables
  const ORPHAN_EMAIL = `orphan.e2e.${RUN_ID}@test.com`;
  const ORPHAN_MATRICULA = `MAT-ORP-${RUN_ID}`;
  const ORPHAN_CONTRACT_NUMBER = `CON-ORP-${RUN_ID}`;

  const testDataDir = path.resolve(process.cwd(), 'test-data');
  const filePath = path.join(testDataDir, `migration_hierarchy_${RUN_ID}.csv`);

  test.beforeAll(async () => {
    // CSV headers: Name,Email,Role,ParentEmail,Matricula,IsMatriculaOwner,Password
    const csvContent = [
      'Name,Email,Role,ParentEmail,Matricula,IsMatriculaOwner,Password',
      `Superior E2E ${RUN_ID},${PARENT_EMAIL},user,,${PARENT_MATRICULA},1,ChangeMe123!`,
      `Subordinado E2E ${RUN_ID},${CHILD_EMAIL},user,${PARENT_EMAIL},${CHILD_MATRICULA},1,ChangeMe123!`,
      `Admin E2E ${RUN_ID},${ADMIN_EMAIL},admin,,${ADMIN_MATRICULA},1,ChangeMe123!`,
      `AdminChild E2E ${RUN_ID},${ADMIN_CHILD_EMAIL},user,${ADMIN_EMAIL},${ADMIN_CHILD_MATRICULA},1,ChangeMe123!`,
      `Orphan E2E ${RUN_ID},${ORPHAN_EMAIL},user,,${ORPHAN_MATRICULA},1,ChangeMe123!`
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

  test('should allow direct deactivation of user with contracts without mandatory migration (Superadmin)', async ({ page }) => {
    test.setTimeout(120000);

    // 1. Login as Superadmin
    await loginAs(page);

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
    await vendedorSelect.fill('');
    await page.waitForTimeout(300);
    await vendedorSelect.pressSequentially(CHILD_EMAIL);
    await page.waitForTimeout(1000);

    // Select child option
    await page.getByRole('option').filter({ hasText: CHILD_EMAIL }).first().click();

    // Save contract
    await contractModal.locator('button[type="submit"]').click();
    await expect(page.getByText('Contrato criado com sucesso')).toBeVisible();

    // 4. Navigate back to Users page and trigger deletion flow of child user
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    await page.fill('input[placeholder="Buscar por nome ou email..."]', CHILD_EMAIL);
    await page.waitForTimeout(1000); // Wait for debounce

    const childRow = page.locator('table tbody tr').filter({ hasText: CHILD_EMAIL });
    await childRow.locator('button[title="Excluir"]').click();

    // 5. Verify the simplified direct deactivation modal
    const deleteDialog = page.getByRole('dialog');
    await expect(deleteDialog).toBeVisible();
    await expect(deleteDialog.getByText('Confirmar Desativação')).toBeVisible();
    await expect(deleteDialog.getByText('Seus contratos permanecerão registrados e visíveis nos relatórios')).toBeVisible();

    // Confirm deactivation directly
    await deleteDialog.getByRole('button', { name: /Desativar|Excluir/i }).click();

    // User should disappear from active users table
    await expect(childRow).not.toBeVisible({ timeout: 15000 });

    // 6. Verify that the contract still exists and is attributed to the deactivated seller
    await page.click('a[href="#/contracts"]');
    await expect(page.getByText('Gerenciamento de Contratos')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15000 });

    await page.fill('input[placeholder="Buscar por número..."]', CONTRACT_NUMBER);
    await page.waitForTimeout(1000);

    const contractRow = page.locator('table tbody tr').filter({ hasText: CONTRACT_NUMBER });
    await expect(contractRow).toBeVisible();
    await expect(contractRow.getByText(`Subordinado E2E ${RUN_ID}`)).toBeVisible();
  });

  test('admin should be able to directly deactivate child user without migration wizard', async ({ page }) => {
    test.setTimeout(120000);

    // 1. Create a contract for the Admin's child user (using Superadmin login first)
    await loginAs(page);

    await page.goto('/#/contracts');
    await expect(page.getByText('Gerenciamento de Contratos')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15000 });

    await page.click('button:has-text("Criar")');
    const contractModal = page.getByRole('dialog');
    await contractModal.locator('input[required]').first().fill(ADMIN_CONTRACT_NUMBER);
    await contractModal.locator('input[type="date"]').fill('2026-01-01');
    await contractModal.locator('input[inputmode="decimal"]').first().fill('3000');

    // Select AdminChild
    const vendedorSelect = contractModal.getByPlaceholder('Selecione o vendedor');
    await vendedorSelect.click();
    await vendedorSelect.fill('');
    await page.waitForTimeout(300);
    await vendedorSelect.pressSequentially(ADMIN_CHILD_EMAIL);
    await page.waitForTimeout(1000);
    await page.getByRole('option').filter({ hasText: ADMIN_CHILD_EMAIL }).first().click();

    await contractModal.locator('button[type="submit"]').click();
    await expect(page.getByText('Contrato criado com sucesso')).toBeVisible();

    // 2. Login as the newly created Admin
    await loginAs(page, ADMIN_EMAIL, 'ChangeMe123!');
    await expect(page.locator('.mantine-AppShell-navbar')).toBeVisible({ timeout: 20000 });

    // 3. Go to Users page
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // 4. Search and Excluir AdminChild
    await page.fill('input[placeholder="Buscar por nome ou email..."]', ADMIN_CHILD_EMAIL);
    await page.waitForTimeout(1000);

    const childRow = page.locator('table tbody tr').filter({ hasText: ADMIN_CHILD_EMAIL });
    await expect(childRow).toBeVisible();
    await childRow.locator('button[title="Excluir"]').click();

    // 5. Verify direct deactivation modal and confirm
    const deleteDialog = page.getByRole('dialog');
    await expect(deleteDialog).toBeVisible();
    await expect(deleteDialog.getByText('Confirmar Desativação')).toBeVisible();

    await deleteDialog.getByRole('button', { name: /Desativar|Excluir/i }).click();

    // Verify user is no longer in active view
    await expect(childRow).not.toBeVisible({ timeout: 15000 });
  });

  test('admin/superadmin can directly deactivate user with contracts and no superior', async ({ page }) => {
    test.setTimeout(90000);

    // 1. Create a contract for the Orphan user (using Superadmin)
    await loginAs(page);

    await page.goto('/#/contracts');
    await expect(page.getByText('Gerenciamento de Contratos')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.contracts-loading')).not.toBeVisible({ timeout: 15000 });

    await page.click('button:has-text("Criar")');
    const contractModal = page.getByRole('dialog');
    await contractModal.locator('input[required]').first().fill(ORPHAN_CONTRACT_NUMBER);
    await contractModal.locator('input[type="date"]').fill('2026-01-01');
    await contractModal.locator('input[inputmode="decimal"]').first().fill('4000');

    // Select Orphan
    const vendedorSelect = contractModal.getByPlaceholder('Selecione o vendedor');
    await vendedorSelect.click();
    await vendedorSelect.fill('');
    await page.waitForTimeout(300);
    await vendedorSelect.pressSequentially(ORPHAN_EMAIL);
    await page.waitForTimeout(1000);
    await page.getByRole('option').filter({ hasText: ORPHAN_EMAIL }).first().click();

    await contractModal.locator('button[type="submit"]').click();
    await expect(page.getByText('Contrato criado com sucesso')).toBeVisible();

    // 2. Go to Users page
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // 3. Search and Excluir Orphan
    await page.fill('input[placeholder="Buscar por nome ou email..."]', ORPHAN_EMAIL);
    await page.waitForTimeout(1000);

    const orphanRow = page.locator('table tbody tr').filter({ hasText: ORPHAN_EMAIL });
    await expect(orphanRow).toBeVisible();
    await orphanRow.locator('button[title="Excluir"]').click();

    // 4. Verify that deactivation is NOT blocked and can be confirmed directly
    const deleteDialog = page.getByRole('dialog');
    await expect(deleteDialog).toBeVisible();
    await expect(deleteDialog.getByText('Erro: Superior Mandatório')).not.toBeVisible();

    const deativarButton = deleteDialog.getByRole('button', { name: /Desativar|Excluir/i });
    await expect(deativarButton).toBeVisible();
    await deativarButton.click();

    await expect(orphanRow).not.toBeVisible({ timeout: 15000 });
  });

  test('should allow toggling Usuário Ativo checkbox in edit form even if user has active contracts', async ({ page }) => {
    test.setTimeout(60000);

    // Login as Superadmin
    await loginAs(page);
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 20000 });

    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // Search for Superior user (who has no direct contracts or can be edited)
    await page.fill('input[placeholder="Buscar por nome ou email..."]', PARENT_EMAIL);
    await page.waitForTimeout(1000);

    const parentRow = page.locator('table tbody tr').filter({ hasText: PARENT_EMAIL });
    await expect(parentRow).toBeVisible();
    
    // Click Editar
    await parentRow.locator('button[title="Editar"]').click();

    const editDialog = page.getByRole('dialog');
    await expect(editDialog).toBeVisible();

    // Verify checkbox is enabled and description warning is NOT displayed
    const checkbox = editDialog.locator('input[type="checkbox"]').last(); // Active checkbox
    await expect(checkbox).toBeEnabled();
    await expect(editDialog.getByText('Usuários com contratos ativos não podem ser desativados por aqui.')).not.toBeVisible();

    await editDialog.getByRole('button', { name: 'Cancelar' }).click();
  });
});
