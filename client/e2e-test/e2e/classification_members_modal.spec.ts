import { test, expect } from '@playwright/test';

test.describe('Classification Members Modal UX & Admin Scoping (TEAR 3)', () => {
  test.describe.configure({ mode: 'serial' });

  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';

  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());
  });

  test('should display classification members modal with left assign column, right active members with search, and click-to-add', async ({ page }) => {
    // 1. Log in as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', adminEmail);
    await page.fill('input[type="password"]', adminPassword);
    await page.click('button.login-button');

    // 2. Navigate to Classifications Page
    const classificationsLink = page.locator('a[href="#/classifications"]');
    await expect(classificationsLink).toBeVisible({ timeout: 10000 });
    await classificationsLink.click();
    await expect(page.getByRole('heading', { name: 'Níveis de Classificação' })).toBeVisible({ timeout: 10000 });

    // 3. Open members modal of the first classification card
    const firstCard = page.locator('.cls-level-card').first();
    await expect(firstCard).toBeVisible({ timeout: 10000 });
    const membersBtn = firstCard.locator('button').nth(0);
    await membersBtn.click();

    const modal = page.locator('.mantine-Modal-content');
    await expect(modal).toBeVisible({ timeout: 5000 });

    // 4. Verify Column Layout: Left is "Atribuir Novos Membros", Right is "Membros Ativos"
    const modalColumns = modal.locator('.cls-modal-col');
    await expect(modalColumns).toHaveCount(2);

    const leftColHeader = modalColumns.nth(0).getByText('Atribuir Novos Membros');
    await expect(leftColHeader).toBeVisible();

    const rightColHeader = modalColumns.nth(1).getByText('Membros Ativos');
    await expect(rightColHeader).toBeVisible();

    // 5. Verify search input in "Membros Ativos" (Right column)
    const rightSearch = modalColumns.nth(1).locator('input[placeholder="Buscar membro..."]');
    await expect(rightSearch).toBeVisible();

    // 6. Test search filter in "Membros Ativos"
    await rightSearch.fill('nonexistinguser123456');
    await expect(modalColumns.nth(1).getByText('Nenhum membro ativo neste nível')).toBeVisible();
    await rightSearch.fill('');

    // 7. Verify search input in "Atribuir Novos Membros" (Left column)
    const leftSearch = modalColumns.nth(0).locator('input[placeholder="Buscar usuário..."]');
    await expect(leftSearch).toBeVisible();

    // Close modal
    await modal.locator('button.mantine-Modal-close').click();
    await expect(modal).not.toBeVisible();
  });

  test('should allow admin user to see Níveis de Classificação in left menu and navigate to page', async ({ page }) => {
    // Log in as standard admin user
    await page.goto('/');
    await page.fill('input[type="email"]', 'admin@salesapp.com');
    await page.fill('input[type="password"]', 'admin123');
    await page.click('button.login-button');

    // Verify Níveis de Classificação link is visible in left menu for Admin user
    const classificationsLink = page.locator('a[href="#/classifications"]');
    await expect(classificationsLink).toBeVisible({ timeout: 10000 });
    await classificationsLink.click();
    await expect(page.getByRole('heading', { name: 'Níveis de Classificação' })).toBeVisible({ timeout: 10000 });
  });
});
