import { test, expect } from '@playwright/test';

test.describe('[TEAR 2] Import Wizard Data Integrity', () => {
  const targetUser = 'carlosmendes@example.com';
  const targetPassword = '123456';
  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';

  test.beforeEach(async ({ page }) => {
    // Login as Super Admin before each test (required for #/matriculas access)
    await page.goto('/');
    await page.fill('input[type="email"]', adminEmail);
    await page.fill('input[type="password"]', adminPassword);
    await page.click('button.login-button');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });
  });

  test('should verify user-parent hierarchy was imported correctly', async ({ page }) => {
    console.log('>>> Checking Julio Mota parent info');

    // 1. Navigate to Users page
    await page.click('a[href="#/users"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();

    // 2. Search for Julio Mota
    await page.fill('input[placeholder="Buscar por nome ou email..."]', 'Julio Mota');

    // Wait for the search bounce and the row to appear
    const userRow = page.locator('tr', { hasText: 'Julio Mota' });
    await expect(userRow).toBeVisible({ timeout: 10000 });

    // 3. Verify parent info is displayed correctly in the name cell
    // Based on UsersPage.tsx: <span className="user-parent">Pai: {user.parentUserName}</span>
    await expect(userRow.locator('.user-parent')).toContainText('Pai: Carlos Mendes');

    console.log('>>> Parent hierarchy verified for Julio Mota');
  });

  test('should verify multiple matriculas were imported correctly for Carlos Mendes (System View)', async ({ page }) => {
    console.log('>>> Checking Carlos Mendes matriculas from Admin view');

    // 1. Navigate to Matriculas page
    await page.click('a[href="#/matriculas"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Matrículas' })).toBeVisible();

    // 2. Search for Carlos Mendes
    await page.fill('input[placeholder="Buscar por número de matrícula (ou separadas por vírgula) ou usuário..."]', 'Carlos Mendes');
    
    // 3. Verify there are rows for both 6111 and 11177
    // Using .filter() to ensure we find the row that has both the name and the specific matricula
    const row6111 = page.locator('tr').filter({ hasText: 'Carlos Mendes' }).filter({ hasText: '6111' });
    const row11177 = page.locator('tr').filter({ hasText: 'Carlos Mendes' }).filter({ hasText: '11177' });
    
    await expect(row6111).toBeVisible({ timeout: 10000 });
    await expect(row11177).toBeVisible({ timeout: 10000 });
    
    console.log('>>> Multiple matriculas verified for Carlos Mendes from Admin view');
  });

  test('should verify Carlos Mendes sees both of his matriculas in his profile (User View)', async ({ page }) => {
    console.log('>>> Checking Carlos Mendes personal profile view');
    
    // 1. Logout Admin and Login as Carlos
    await page.evaluate(() => localStorage.clear());
    await page.goto('/');
    await page.fill('input[type="email"]', targetUser);
    await page.fill('input[type="password"]', targetPassword);
    await page.click('button.login-button');
    
    // 2. Navigate to "Meu Perfil"
    await page.click('a[href="#/my-profile"]');
    await expect(page.getByRole('heading', { name: 'Meu Perfil' })).toBeVisible();

    // 3. Verify both matriculas appear in the table
    const profileMatriculas = page.locator('.matriculas-table');
    await expect(profileMatriculas).toContainText('6111');
    await expect(profileMatriculas).toContainText('11177');
    
    console.log('>>> Carlos Mendes verified both matriculas in his personal profile.');
  });

  test('should verify Julio Mota matricula was imported correctly', async ({ page }) => {
    console.log('>>> Checking Julio Mota matricula');

    // 1. Navigate to Matriculas page
    await page.click('a[href="#/matriculas"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Matrículas' })).toBeVisible();

    // 2. Search for Julio Mota's matricula 9999
    await page.fill('input[placeholder="Buscar por número de matrícula (ou separadas por vírgula) ou usuário..."]', '9999');

    // 3. Verify it is linked to Julio Mota
    const matricula9999 = page.locator('tr', { hasText: '9999' });
    await expect(matricula9999).toContainText('Julio Mota');

    console.log('>>> Matricula 9999 verified for Julio Mota');
  });
});
