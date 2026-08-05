import { test, expect } from '@playwright/test';

test.describe('Responsive Menu', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });
  });


  test('should show expanded sidebar and hide header on desktop', async ({ page }) => {
    // Desktop viewport (stored in playwright.config.ts but we can force it)
    await page.setViewportSize({ width: 1280, height: 720 });

    // Sidebar should be visible
    const sidebar = page.locator('.mantine-AppShell-navbar');
    await expect(sidebar).toBeVisible();

    // Header should BE HIDDEN on desktop per the new plan
    const header = page.locator('.mantine-AppShell-header');
    await expect(header).not.toBeVisible();
    
    // Check if title is in sidebar
    await expect(sidebar.getByText('Painel de Vendas')).toBeVisible();
  });

  test('should hide sidebar and show header on mobile', async ({ page }) => {
    // Mobile viewport (smaller than 'md' breakpoint which is 992px)
    await page.setViewportSize({ width: 375, height: 667 });

    // Sidebar should be hidden initially
    const sidebar = page.locator('.mantine-AppShell-navbar');
    await expect(sidebar).not.toBeVisible();

    // Header should be visible
    const header = page.locator('.mantine-AppShell-header');
    await expect(header).toBeVisible();

    // Burger button should be visible in header
    const burger = header.locator('button.mantine-Burger-root');
    await expect(burger).toBeVisible();

    // Title should be visible in header on mobile
    await expect(header.getByText('Painel de Vendas')).toBeVisible();

    // Click burger and verify sidebar appears
    await burger.click();
    await expect(sidebar).toBeVisible();

    // Click a menu item and verify sidebar closes
    // Let's go to "Usuários"
    await sidebar.getByRole('link', { name: 'Usuários' }).click();
    
    // Should navigate and sidebar should be hidden again
    await expect(page.getByRole('heading', { name: 'Usuários' })).toBeVisible();
    await expect(sidebar).not.toBeVisible();
  });
});
