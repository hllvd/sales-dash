import { test, expect } from '@playwright/test';

test.describe('User Profile E2E Tests (TEAR 5)', () => {
  const user = {
    email: 'lucaspereira@example.com',
    password: 'ChangeMe123!',
    name: 'Lucas Pereira',
    updatedName: 'Lucas Pereira E2E Test'
  };

  test.beforeEach(async ({ page }) => {
    // 1. Go to Login page
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());
    await page.goto('/');

    // 2. Fill login form
    await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15000 });
    await page.fill('input[type="email"]', user.email);
    await page.fill('input[type="password"]', user.password);
    await page.click('button.login-button');

    // 3. Confirm login success by looking for 'Meus Contratos'
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15000 });
  });

  test('should navigate to profile, assert user details, edit profile, and restore state', async ({ page }) => {
    console.log('>>> Step 1: Navigate to "Meu Usuário" page');
    await page.click('a[href="#/my-profile"]', { timeout: 10000 });
    
    // Confirm profile container load
    await expect(page.locator('.user-profile-container')).toBeVisible({ timeout: 15000 });

    console.log('>>> Step 2: Assert initial user profile data');
    // Verify name title
    await expect(page.getByRole('heading', { name: user.name })).toBeVisible({ timeout: 10000 });
    // Verify email text
    await expect(page.locator('.profile-email-row')).toContainText(user.email);

    console.log('>>> Step 3: Edit user profile');
    const editBtn = page.getByRole('button', { name: 'Editar Perfil' });
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    // Fill in the updated name
    const nameInput = page.getByLabel('Nome Completo');
    await expect(nameInput).toBeVisible();
    await nameInput.fill(user.updatedName);

    // Save changes
    await page.getByRole('button', { name: 'Salvar Alterações' }).click();

    // Verify name updated successfully in header card
    await expect(page.getByRole('heading', { name: user.updatedName })).toBeVisible({ timeout: 10000 });
    console.log('>>> Step 3 OK: Profile updated successfully');

    console.log('>>> Step 4: Revert profile edits to keep DB pristine');
    await page.getByRole('button', { name: 'Editar Perfil' }).click();
    await nameInput.fill(user.name);
    await page.getByRole('button', { name: 'Salvar Alterações' }).click();

    // Confirm reverted name title
    await expect(page.getByRole('heading', { name: user.name })).toBeVisible({ timeout: 10000 });
    console.log('>>> Step 4 OK: Profile reverted successfully');
  });
});
