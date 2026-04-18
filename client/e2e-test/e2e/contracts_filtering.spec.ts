import { test, expect } from '@playwright/test';

test.describe('Contracts Filtering', () => {
  test('should filter contracts by user email and sale start date', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as admin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Go specifically to Contracts page (not My Contracts)
    // Using a very strict locator to avoid selecting "Meus Contratos"
    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });

    // Wait for the initial users and contracts load
    //await page.waitForResponse(response => response.url().includes('/users?page=1') && response.status() === 200);

    // 3. Filter by User Email
    // Click on the mantine select input for Email
    await page.locator('input[placeholder="Buscar por email..."]').click();
    await page.locator('input[placeholder="Buscar por email..."]').fill('superadmin@salesapp.com');
    // Click the autocomplete option
    await page.getByRole('option', { name: 'superadmin@salesapp.com' }).click();

    // The frontend debounces by 3 seconds, so we wait for the /api/contracts request with strict query param mapping
    const emailRequestPromise = page.waitForRequest(request =>
      request.url().includes('/api/contracts') &&
      !request.url().includes('/user/') && // specifically not for user contracts endpoint
      request.url().includes('userEmail=superadmin%40salesapp.com') &&
      request.method() === 'GET',
      { timeout: 10000 }
    );
    await emailRequestPromise;

    // 4. Filter by Start Date
    const startDatePromise = page.waitForRequest(request =>
      request.url().includes('/api/contracts') &&
      !request.url().includes('/user/') &&
      request.url().includes('startDate=2024-01-01') &&
      request.method() === 'GET',
      { timeout: 10000 }
    );
    await page.fill('input#filterStartDate', '2024-01-01');
    await startDatePromise;

    // Verify clear filters button appears
    await expect(page.locator('.clear-filters-btn')).toBeVisible();
  });

  test('should filter by carlosmendes@example.com and return 14 results', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as admin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Go specifically to Contracts page (not My Contracts)
    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });

    // 3. Filter by User Email
    await page.locator('input[placeholder="Buscar por email..."]').click();
    await page.locator('input[placeholder="Buscar por email..."]').fill('carlosmendes@example.com');
    await page.getByRole('option', { name: 'carlosmendes@example.com' }).click();

    // The frontend debounces by 3 seconds, so we wait for the /api/contracts request
    const emailRequestPromise = page.waitForRequest(request =>
      request.url().includes('/api/contracts') &&
      !request.url().includes('/user/') && 
      request.url().includes('userEmail=carlosmendes%40example.com') &&
      request.method() === 'GET',
      { timeout: 10000 }
    );
    await emailRequestPromise;

    // Check if table has exactly 14 rows
    await expect(page.locator('table tbody tr')).toHaveCount(14, { timeout: 15000 });
  });

  test('should filter by start date and return 12 results for 2025-10-15', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as admin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // 2. Go specifically to Contracts page
    await page.getByRole('link', { name: 'Contratos', exact: true }).click();
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });

    // 3. Filter by Start Date
    const startDatePromise = page.waitForRequest(request =>
      request.url().includes('/api/contracts') &&
      !request.url().includes('/user/') && 
      request.url().includes('startDate=2025-10-15') && 
      request.method() === 'GET',
      { timeout: 10000 }
    );
    await page.fill('input#filterStartDate', '2025-10-15');
    await startDatePromise;

    // Check if table has exactly 12 rows (6 from Oct 15, 2 from Oct 16, 3 from Oct 17, 1 from Oct 21)
    await expect(page.locator('table tbody tr')).toHaveCount(12, { timeout: 15000 });
  });

  test('admin Carlos Mendes should see his child Julio Mota contracts', async ({ page }) => {
    test.setTimeout(45_000);

    // 1. Login as Admin Carlos Mendes (updated in users-demo.csv)
    await page.goto('/');
    await page.fill('input[type="email"]', 'carlosmendes@example.com');
    await page.fill('input[type="password"]', '123456');
    await page.click('button.login-button');

    // Wait for the menu/app to load properly
    await expect(page.locator('.mantine-AppShell-navbar')).toBeVisible({ timeout: 15000 });

    // 2. Go to Contracts page (available to admin role)
    // Using test-id to avoid confusion with "Meus Contratos"
    await page.getByTestId('nav-contracts').click();
    
    // Ensure we are on the right page
    await expect(page).toHaveURL(/.*#\/contracts/);
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 15000 });

    // 3. Filter by child's email (juliomota@example.com)
    await page.locator('input[placeholder="Buscar por email..."]').click();
    await page.locator('input[placeholder="Buscar por email..."]').fill('juliomota@example.com');
    
    // Select Julio Mota from autocomplete
    await page.getByRole('option', { name: 'juliomota@example.com' }).first().click();

    // Wait for the debounced search request
    const searchRequestPromise = page.waitForRequest(request =>
      request.url().includes('/api/contracts') &&
      request.url().includes('userEmail=juliomota%40example.com') &&
      request.method() === 'GET',
      { timeout: 15000 }
    );
    await searchRequestPromise;

    // 4. Assert that he can see Julio's 22 contracts
    // Even if he is not the owner, the hierarchy allows it.
    await expect(page.locator('table tbody tr')).toHaveCount(22, { timeout: 15000 });
    
    // Julio has contracts with both 9999 and 11177 matriculas, so we verify
    // that 9999 appears somewhere in the table (not necessarily in the first row).
    await expect(page.locator('table tbody tr').filter({ hasText: '9999' }).first()).toBeVisible({ timeout: 5000 });
  });
});
