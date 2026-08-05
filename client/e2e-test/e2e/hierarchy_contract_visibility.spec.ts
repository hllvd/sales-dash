import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('[TEAR 3] Hierarchy Contract Visibility', () => {
  const targetUser = 'carlosmendes@example.com';
  const targetPassword = '123456';

  test.beforeEach(async ({ page }) => {
    await loginAs(page, targetUser, targetPassword);
  });


  test('should see own contracts and descendant contracts', async ({ page }) => {
    console.log('>>> Checking Carlos Mendes visibility tree');

    // 1. Navigate to All Contracts page
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: /Contratos/i })).toBeVisible({ timeout: 10000 });

    // 2. Check Matricula 6111 (Own)
    console.log('>>> Filtering by Matricula 6111 (Own)');
    const filterInput = page.locator('input[placeholder="Filtrar por matrícula..."], #filterMatricula').first();
    await filterInput.click();
    await filterInput.fill('6111');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(1500);
    await expect(page.locator('tr', { hasText: '6111' }).first()).toBeVisible({ timeout: 10000 });

    // 3. Check Matricula 11177 (Own)
    console.log('>>> Filtering by Matricula 11177 (Own)');
    await page.locator('.clear-filters-btn').click().catch(() => {});
    await filterInput.click();
    await filterInput.fill('11177');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(1500);
    await expect(page.locator('tr', { hasText: '11177' }).first()).toBeVisible({ timeout: 10000 });

    // 4. Check Matricula 9999 (Julio Mota - Descendant)
    console.log('>>> Filtering by Matricula 9999 (Julio Mota - Descendant)');
    await page.locator('.clear-filters-btn').click().catch(() => {});
    await filterInput.click();
    await filterInput.fill('9999');
    await page.keyboard.press('Enter');
    await page.waitForTimeout(1500);


    // If the hierarchy is working, Carlos (Manager) should see Julio's contracts
    await expect(page.locator('tr', { hasText: '9999' }).first()).toBeVisible({ timeout: 15000 });
    await expect(page.locator('tr', { hasText: 'Julio Mota' }).first()).toBeVisible({ timeout: 10000 });

    console.log('>>> Hierarchy visibility verified: Carlos sees Julio\'s data correctly isolated.');
  });

});
