import { test, expect } from '@playwright/test';

test.describe('Login Flow', () => {
  test('should login successfully with admin credentials', async ({ page }) => {
    // 1. Navigate to the login page
    await page.goto('/');

    // 2. Verify we are on the login page
    await expect(page.getByText('Bem-vindo de Volta')).toBeVisible();

    // 3. Fill in the login form
    await page.fill('input[type="email"]', 'admin@salesapp.com');
    await page.fill('input[type="password"]', 'admin123');

    // 4. Click the login button
    await page.click('button.login-button');

    // 5. Verify successful redirection/login
    // The App.tsx renders MyContractsPage by default after login
    // which has the title "Meus Contratos"
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });
    
    // 6. Verify token is stored in localStorage
    const token = await page.evaluate(() => localStorage.getItem('token'));
    expect(token).toBeTruthy();
  });

  test('should show error message with invalid credentials', async ({ page }) => {
    await page.goto('/');
    
    await page.fill('input[type="email"]', 'wrong@example.com');
    await page.fill('input[type="password"]', 'wrongpassword');
    await page.click('button.login-button');

    // Verify error message appears
    // The LoginPage.tsx shows error in div.error-message
    await expect(page.locator('.error-message')).toBeVisible();
    await expect(page.locator('.error-message')).toContainText('Credenciais inválidas');
  });
});
