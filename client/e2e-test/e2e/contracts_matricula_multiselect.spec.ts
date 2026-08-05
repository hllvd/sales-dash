import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Contracts Matrícula MultiSelect & Label', () => {
  test('should display Equipe label and allow MultiSelect filtering for Matrícula', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as admin and navigate to Contracts
    await loginAs(page);
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });


    // 3. Verify "Equipe" label is displayed instead of "Time"
    await expect(page.locator('label[for="filterTeam"]')).toHaveText('Equipe');

    // 4. Verify Matrícula label is present
    await expect(page.locator('label[for="filterMatricula"]')).toHaveText('Matrícula');

    // 5. Test MultiSelect interaction for Matrícula filter input
    const matriculaInput = page.locator('input[placeholder="Filtrar por matrícula..."]').first();
    await expect(matriculaInput).toBeVisible();

    // Prepare request promise before typing/pressing Enter
    const matriculaReqPromise = page.waitForRequest(request =>
      request.url().includes('/api/contracts') &&
      !request.url().includes('/user/') &&
      request.url().includes('matricula=9999') &&
      request.method() === 'GET',
      { timeout: 15000 }
    );

    // Type a matrícula number and press Enter to select/add
    await matriculaInput.click();
    await matriculaInput.fill('9999');
    await page.keyboard.press('Enter');

    await matriculaReqPromise;

    // Verify clear filters button is visible
    await expect(page.locator('.clear-filters-btn')).toBeVisible();

    // 6. Click Clear Filters and verify input resets
    await page.locator('.clear-filters-btn').click();
    await expect(page.locator('.clear-filters-btn')).not.toBeVisible();
  });
});
