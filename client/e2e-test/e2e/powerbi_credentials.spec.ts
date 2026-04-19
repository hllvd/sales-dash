import { test, expect } from '@playwright/test';

test.describe('PowerBI Credentials', () => {
    const adminEmail = 'admin@salesapp.com';
    const adminPassword = 'admin123';
    const userEmail = 'carlosmendes@example.com';
    const userPassword = '123456';

    test('should allow admin to see and save PowerBI credentials', async ({ page }) => {
        // 1. Login as Admin
        await page.goto('/');
        await page.fill('input[type="email"]', adminEmail);
        await page.fill('input[type="password"]', adminPassword);
        await page.click('button.login-button');

        // 2. Go to Profile Page
        await page.click('a[href="#/my-profile"]');
        await expect(page.getByRole('heading', { name: 'Meu Perfil' })).toBeVisible();

        // 3. Verify PowerBI section is visible
        await expect(page.getByRole('heading', { name: 'Credenciais PowerBI' })).toBeVisible();

        // 4. Fill and save credentials
        const testUser = 'pbi_test_user_' + Date.now();
        await page.getByLabel('Usuário PowerBI').fill(testUser);
        await page.getByLabel('Senha PowerBI').fill('pbi_secret_123');
        await page.click('button:has-text("Salvar Credenciais PowerBI")');

        // 5. Verify success toast and badge
        await expect(page.getByText('Credenciais PowerBI salvas com sucesso')).toBeVisible();
        await expect(page.getByText('✓ Credenciais configuradas')).toBeVisible();

        // 6. Reload and verify persistence
        await page.reload();
        await expect(page.getByLabel('Usuário PowerBI')).toHaveValue(testUser);
        // Password field should be empty after reload for security
        await expect(page.getByLabel('Senha PowerBI')).toHaveValue('');
    });

    test('should NOT show PowerBI credentials section to regular users', async ({ page }) => {
        // 1. Login as Regular User
        await page.goto('/');
        await page.fill('input[type="email"]', userEmail);
        await page.fill('input[type="password"]', userPassword);
        await page.click('button.login-button');

        // 2. Go to Profile Page
        await page.click('a[href="#/my-profile"]');
        await expect(page.getByRole('heading', { name: 'Meu Perfil' })).toBeVisible();

        // 3. Verify PowerBI section is NOT visible
        await expect(page.getByRole('heading', { name: 'Credenciais PowerBI' })).not.toBeVisible();
    });
});
