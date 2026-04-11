import { test, expect } from '@playwright/test';

test.describe('[TEAR 3] Hierarchy Contract Visibility', () => {
  const targetUser = 'carlosmendes@example.com';
  const targetPassword = '123456';

  test.beforeEach(async ({ page }) => {
    // Login as Carlos Mendes (who should be Admin by now due to Tear 2)
    await page.goto('/');
    await page.fill('input[type="email"]', targetUser);
    await page.fill('input[type="password"]', targetPassword);
    await page.click('button.login-button');

    // Verify successful login
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });
  });

  test('should see own contracts and descendant contracts', async ({ page }) => {
    console.log('>>> Checking Carlos Mendes visibility tree');

    // 1. Navigate to All Contracts page
    await page.click('[data-testid="nav-contracts"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible();

    // The filters have a 3s debounce, so we need to be patient or wait for loading
    const waitForLoading = async () => {
      await page.waitForSelector('.contracts-loading', { state: 'visible', timeout: 5000 }).catch(() => { });
      await page.waitForSelector('.contracts-loading', { state: 'hidden', timeout: 15000 });
    };

    // 2. Check Matricula 6111 (Own)
    console.log('>>> Filtering by Matricula 6111 (Own)');
    await page.fill('#filterMatricula', '6111');
    await waitForLoading();

    // Verify we see results for 6111
    // We check if at least one row exists with 6111
    await expect(page.locator('tr >> text=6111').first()).toBeVisible();

    // 3. Check Matricula 11177 (Own)
    console.log('>>> Filtering by Matricula 11177 (Own)');
    await page.fill('#filterMatricula', ''); // Clear first
    await page.fill('#filterMatricula', '11177');
    await waitForLoading();
    await expect(page.locator('tr >> text=11177').first()).toBeVisible();

    // 4. Check Matricula 9999 (Julio Mota - Descendant)
    // This is the CRITICAL check for the hierarchy bug fix
    console.log('>>> Filtering by Matricula 9999 (Julio Mota - Descendant)');
    await page.fill('#filterMatricula', ''); // Clear first
    await page.fill('#filterMatricula', '9999');
    await waitForLoading();

    // If the hierarchy is working, Carlos (Manager) should see Julio's contracts
    await expect(page.locator('tr >> text=9999').first()).toBeVisible({ timeout: 10000 });
    await expect(page.locator('tr >> text=Julio Mota').first()).toBeVisible();

    console.log('>>> Hierarchy visibility verified: Carlos sees Julio\'s data.');
  });
});
