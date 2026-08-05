import { Page, expect } from '@playwright/test';

export const SUPERADMIN_EMAIL = 'superadmin@salesapp.com';
export const SUPERADMIN_PASSWORD = 'string';

export const ADMIN_EMAIL = 'admin@salesapp.com';
export const ADMIN_PASSWORD = 'admin123';

/**
 * Performs UI login with given credentials, skipping if already logged in as that user,
 * or clearing session and logging in if a different user is requested.
 */
export async function loginAs(page: Page, email: string = SUPERADMIN_EMAIL, password: string = SUPERADMIN_PASSWORD) {
  await page.goto('/');
  
  const emailInput = page.locator('input[type="email"]');
  const isLoginFormVisible = await emailInput.isVisible({ timeout: 2000 }).catch(() => false);

  if (!isLoginFormVisible) {
    const storedToken = await page.evaluate(() => localStorage.getItem('token'));
    if (storedToken) {
      try {
        const payloadBase64 = storedToken.split('.')[1];
        const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
        const payload = JSON.parse(decodedJson);
        const currentEmail = payload.email || payload.unique_name || payload.sub;
        if (currentEmail && currentEmail.toLowerCase() === email.toLowerCase()) {
          // Already logged in as requested user!
          return;
        }
      } catch {
        // Fallthrough to re-login if token parsing fails
      }
    }

    // Swapping users or invalid session — clear and reload to get login form
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
    await page.goto('/');
    await expect(emailInput).toBeVisible({ timeout: 10000 });
  }

  await page.fill('input[type="email"]', email);
  await page.fill('input[type="password"]', password);
  await page.click('button.login-button');

  // Verify successful login navigation
  await expect(page.locator('button:has-text("Logout"), a[href="#/my-contracts"], .mantine-AppShell-main').first()).toBeVisible({ timeout: 15000 });
}



