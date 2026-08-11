import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD, ADMIN_EMAIL, ADMIN_PASSWORD } from './helpers/auth';

test.describe('Teams Page State & Store Filters (TEAR 2)', () => {
  test.describe.configure({ mode: 'serial' });

  const timestamp = Date.now();
  const testStoreName = `Loja Filter E2E ${timestamp}`;
  const testStoreState = 'SC';
  const testTeamWithStoreName = `Equipe Com Loja E2E ${timestamp}`;
  const testTeamWithoutStoreName = `Equipe Sem Loja E2E ${timestamp}`;

  test('should filter teams by state and store as superadmin and hide filters for regular admin', async ({ page }) => {
    test.setTimeout(90000);

    // 1. Log in as Superadmin
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);

    // 2. Create a test Store with state SC
    await page.goto('/#/stores');
    await expect(page.getByRole('heading', { name: 'Lojas' })).toBeVisible({ timeout: 15000 });
    await page.click('button:has-text("Nova Loja")');
    const storeModal = page.getByRole('dialog');
    await expect(storeModal).toBeVisible();

    await page.fill('input[placeholder="Ex: BALNEARIO CAMBORIU"]', testStoreName);
    await storeModal.locator('.mantine-Select-input').click();
    await page.click('div[role="option"]:has-text("Santa Catarina (SC)")');
    await page.click('button:has-text("Criar Loja")');
    await expect(storeModal).not.toBeVisible({ timeout: 10000 });
    await expect(page.locator('tr', { hasText: testStoreName })).toBeVisible({ timeout: 15000 });

    // 3. Create a team linked to the new store
    await page.goto('/#/teams');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Equipes' })).toBeVisible({ timeout: 15000 });
    await page.click('button:has-text("Nova Equipe")');
    const promptModal = page.getByRole('dialog');
    await expect(promptModal).toBeVisible();
    await page.fill('input[placeholder="Ex: EQUIPE ALPHA"]', testTeamWithStoreName);
    await page.click('button:has-text("Criar")');
    await expect(promptModal).not.toBeVisible({ timeout: 10000 });

    // Link the created team to our test store
    const teamRow = page.locator('tr', { hasText: testTeamWithStoreName.toUpperCase() });
    await expect(teamRow).toBeVisible({ timeout: 15000 });
    await teamRow.locator('button[title="Editar equipe"], button:has(.tabler-icon-edit)').click();
    const editModal = page.getByRole('dialog');
    await expect(editModal).toBeVisible();

    // Select the store in team edit form
    const storeSelect = editModal.locator('.mantine-Select-input').first();
    await storeSelect.click();
    await page.click(`div[role="option"]:has-text("${testStoreName}")`);
    await editModal.locator('button:has-text("Salvar Alterações")').click();
    await expect(editModal).not.toBeVisible({ timeout: 10000 });

    // 4. Create a team without a store
    await page.click('button:has-text("Nova Equipe")');
    await expect(promptModal).toBeVisible();
    await page.fill('input[placeholder="Ex: EQUIPE ALPHA"]', testTeamWithoutStoreName);
    await page.click('button:has-text("Criar")');
    await expect(promptModal).not.toBeVisible({ timeout: 10000 });
    await expect(page.locator('tr', { hasText: testTeamWithoutStoreName.toUpperCase() })).toBeVisible({ timeout: 15000 });

    // 5. Verify Superadmin sees Estado and Loja filters
    const searchContainer = page.locator('.search-container');
    const stateFilter = searchContainer.locator('input[placeholder="Filtrar por estado"]');
    const storeFilter = searchContainer.locator('input[placeholder="Filtrar por loja"]');
    await expect(stateFilter).toBeVisible();
    await expect(storeFilter).toBeVisible();

    // 6. Test State Filter ("SC")
    await stateFilter.click();
    await page.click(`div[role="option"]:has-text("${testStoreState}")`);

    // Team with store should be visible; Team without store should be hidden
    await expect(page.locator('tr', { hasText: testTeamWithStoreName.toUpperCase() })).toBeVisible();
    await expect(page.locator('tr', { hasText: testTeamWithoutStoreName.toUpperCase() })).not.toBeVisible();

    // 7. Test Store Filter (specific store)
    await storeFilter.click();
    await page.click(`div[role="option"]:has-text("${testStoreName}")`);

    // With both filters active (AND), team with store is visible, team without store is hidden
    await expect(page.locator('tr', { hasText: testTeamWithStoreName.toUpperCase() })).toBeVisible();
    await expect(page.locator('tr', { hasText: testTeamWithoutStoreName.toUpperCase() })).not.toBeVisible();

    // 8. Clear state filter by clicking clear icon or resetting
    await searchContainer.locator('.mantine-MultiSelect-clearButton').first().click();
    await expect(page.locator('tr', { hasText: testTeamWithStoreName.toUpperCase() })).toBeVisible();
    await expect(page.locator('tr', { hasText: testTeamWithoutStoreName.toUpperCase() })).not.toBeVisible();

    // Clear all filters
    const clearBtns = await searchContainer.locator('.mantine-MultiSelect-clearButton').all();
    for (const btn of clearBtns) {
      await btn.click().catch(() => {});
    }

    // After clearing all filters, both teams should be visible
    await expect(page.locator('tr', { hasText: testTeamWithStoreName.toUpperCase() })).toBeVisible();
    await expect(page.locator('tr', { hasText: testTeamWithoutStoreName.toUpperCase() })).toBeVisible();

    // 9. Verify regular admin does NOT see Estado and Loja filters
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);
    await page.goto('/#/teams');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Equipes' })).toBeVisible({ timeout: 15000 });

    const adminSearchContainer = page.locator('.search-container');
    await expect(adminSearchContainer.locator('input[placeholder="Filtrar por estado"]')).not.toBeVisible();
    await expect(adminSearchContainer.locator('input[placeholder="Filtrar por loja"]')).not.toBeVisible();

    // 10. Cleanup test data as Superadmin
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);
    await page.goto('/#/teams');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Equipes' })).toBeVisible({ timeout: 15000 });

    // Delete teams
    for (const name of [testTeamWithStoreName, testTeamWithoutStoreName]) {
      const row = page.locator('tr', { hasText: name.toUpperCase() });
      if (await row.isVisible()) {
        await row.locator('button[title="Excluir equipe"], button:has(.tabler-icon-trash)').click();
        const deleteModal = page.getByRole('dialog', { name: 'Confirmar Exclusão' });
        await expect(deleteModal).toBeVisible();
        await deleteModal.locator('button:has-text("Excluir")').click();
        await expect(deleteModal).not.toBeVisible({ timeout: 10000 });
      }
    }

    // Delete store
    await page.goto('/#/stores');
    await expect(page.getByRole('heading', { name: 'Lojas' })).toBeVisible({ timeout: 15000 });
    const storeRow = page.locator('tr', { hasText: testStoreName });
    if (await storeRow.isVisible()) {
      await storeRow.locator('button[title="Delete"], button:has(.tabler-icon-trash)').click();
      const deleteModal = page.getByRole('dialog', { name: 'Confirmar Exclusão' });
      await expect(deleteModal).toBeVisible();
      await deleteModal.locator('button:has-text("Excluir")').click();
      await expect(deleteModal).not.toBeVisible({ timeout: 10000 });
    }
  });
});
