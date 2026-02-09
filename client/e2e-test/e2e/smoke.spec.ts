import { test, expect } from '@playwright/test';

test('login page loads', async ({ page }) => {
  await page.goto('/');
  // Expect the title to contain "SalesApp" or similar if defined, 
  // otherwise just check for presence of login form elements
  const loginButton = page.getByRole('button', { name: /Login|Entrar/i });
  await expect(loginButton).toBeVisible();
});
