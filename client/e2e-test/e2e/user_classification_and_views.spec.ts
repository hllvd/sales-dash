import { test, expect } from '@playwright/test';

test.describe('User Classification and Views Engine E2E Tests', () => {
  // Serial mode to keep mutable actions ordered
  test.describe.configure({ mode: 'serial' });

  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';

  const timestamp = Date.now();
  const testLevelName = `E2E User Level Test ${timestamp}`;
  const testGoal = 90000;
  const testPrize = `E2E Prize ${timestamp}`;

  const testDashboardName = `E2E Test Dashboard ${timestamp}`;

  test.beforeEach(async ({ page }) => {
    // Navigate first to have a valid domain context, then clear localStorage
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());
  });

  test('should manage user classification assignments and verify views execution', async ({ page }) => {
    test.setTimeout(100000);

    // 1. Log in as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', adminEmail);
    await page.fill('input[type="password"]', adminPassword);
    await page.click('button.login-button');

    // ────────────────────────────────────────────────────────────────────────
    // Part A: User Classification Levels & Member Assignment
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Navigating to Classifications Page...');
    const classificationsLink = page.locator('a[href="#/classifications"]');
    await expect(classificationsLink).toBeVisible({ timeout: 10000 });
    await classificationsLink.click();
    await expect(page.getByRole('heading', { name: 'Níveis de Classificação' })).toBeVisible({ timeout: 10000 });

    // Clean up any stale user levels in the initial phase
    console.log('>>> Running E2E User Classification Level pre-cleanup...');
    let keepCleaningLevels = true;
    while (keepCleaningLevels) {
      const staleCard = page.locator('.cls-level-card', { hasText: 'E2E User Level Test' }).first();
      if (await staleCard.isVisible()) {
        console.log('>>> Cleaning up stale E2E user level card');
        
        // Open members modal
        await staleCard.locator('button').nth(0).click();
        const membersModal = page.locator('.mantine-Modal-content');
        await expect(membersModal).toBeVisible({ timeout: 5000 });
        
        // Remove all active members
        let hasActiveMembers = true;
        while (hasActiveMembers) {
          const firstMemberCard = membersModal.locator('.cls-member-card').first();
          if (await firstMemberCard.isVisible()) {
            console.log('>>> Removing active member from stale level...');
            await firstMemberCard.locator('button').last().click();
            await page.waitForTimeout(600); // wait for state refresh
          } else {
            hasActiveMembers = false;
          }
        }
        
        // Close members modal
        await membersModal.locator('button.mantine-Modal-close').click();
        await expect(membersModal).not.toBeVisible();
        await page.waitForTimeout(500);

        // Delete the level
        await staleCard.locator('button').nth(2).click();
        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Excluir' }).click();
        // Wait for the modal/dialog to completely disappear to prevent click interception in next loops
        await expect(dialog).not.toBeVisible({ timeout: 8000 });
        await page.waitForTimeout(500);
      } else {
        keepCleaningLevels = false;
      }
    }

    // Create a new level
    console.log('>>> Creating a new user classification level...');
    await page.click('button:has-text("Novo Nível")');

    const modalTitle = page.locator('.mantine-Modal-title');
    await expect(modalTitle).toContainText('Novo Nível de Classificação');

    await page.fill('input[placeholder="Ex: Prata, Ouro, Estrela..."]', testLevelName);
    await page.fill('textarea[placeholder="Descrição opcional do nível..."]', 'E2E test description for user level assignment.');
    await page.fill('input[placeholder="Ex: Bônus de R$ 500, Viagem..."]', testPrize);
    await page.fill('input[placeholder="Ex: 50000"]', testGoal.toString());

    await page.click('button[type="submit"]:has-text("Criar Nível")');

    // Verify card is added
    const newLevelCard = page.locator('.cls-level-card', { hasText: testLevelName });
    await expect(newLevelCard).toBeVisible({ timeout: 10000 });

    // Manage members for this level
    console.log('>>> Assigning members to the level...');
    const membersBtn = newLevelCard.locator('button').nth(0);
    await membersBtn.click();

    const membersModal = page.locator('.mantine-Modal-content');
    await expect(membersModal).toBeVisible({ timeout: 5000 });
    await expect(membersModal.getByText('Nenhum membro ativo neste nível', { exact: true })).toBeVisible();

    // Select the first available user card
    const selectUserCard = membersModal.locator('.cls-select-user-card').first();
    await expect(selectUserCard).toBeVisible({ timeout: 10000 });

    const assignedUserName = await selectUserCard.locator('.cls-member-card__name').innerText();
    console.log(`>>> Selected user for assignment: ${assignedUserName}`);

    await selectUserCard.click();

    const assignBtn = membersModal.locator('button[type="submit"]');
    await expect(assignBtn).toContainText('Atribuir Nível a 1 usuário(s)');
    await assignBtn.click();

    // Wait for the server write & DOM update
    await page.waitForTimeout(800);

    // Verify that the assigned user is listed under Membros Ativos
    const activeMemberCard = membersModal.locator('.cls-member-card', { hasText: assignedUserName });
    await expect(activeMemberCard).toBeVisible({ timeout: 5000 });

    // Close the members assignment modal
    await membersModal.locator('button.mantine-Modal-close').click();
    await expect(membersModal).not.toBeVisible();

    // As requested: DO NOT perform cleanup of this classification or assigned member at the end.
    console.log('>>> Verified user classification level. Leaving level and assignments intact.');

    // ────────────────────────────────────────────────────────────────────────
    // Part B: Views Engine / Dashboard Verification
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Navigating to Dashboards Page...');
    const dashboardsLink = page.locator('a[href="#/views"]');
    await expect(dashboardsLink).toBeVisible({ timeout: 10000 });
    await dashboardsLink.click();
    await expect(page.getByRole('heading', { name: 'Dashboards (Views Engine)' })).toBeVisible({ timeout: 10000 });

    // Clean up any stale E2E dashboards in the initial phase
    console.log('>>> Running E2E Dashboards pre-cleanup...');
    let keepCleaningDashboards = true;
    while (keepCleaningDashboards) {
      const staleCard = page.locator('.mantine-Card-root', { hasText: 'E2E Test Dashboard' }).first();
      if (await staleCard.isVisible()) {
        console.log('>>> Cleaning up stale dashboard card');
        await staleCard.locator('button').nth(2).click();
        const dialog = page.getByRole('dialog');
        await dialog.getByRole('button', { name: 'Excluir' }).click();
        // Wait for the modal/dialog to completely disappear
        await expect(dialog).not.toBeVisible({ timeout: 8000 });
        await page.waitForTimeout(500);
      } else {
        keepCleaningDashboards = false;
      }
    }

    // Create a new Dashboard view
    console.log('>>> Creating a new Dashboard View...');
    await page.click('button:has-text("Novo Dashboard")');

    await page.fill('input[placeholder="Ex: Painel Geral de Vendas e Comissão"]', testDashboardName);
    await page.fill('textarea[placeholder="Uma breve descrição sobre a finalidade ou audiência deste painel"]', 'E2E Test Dashboard Description.');

    // Add a single row (1 column grid)
    console.log('>>> Adding layout row...');
    await page.click('button:has-text("+ Linha (1 Coluna)")');

    // Save dashboard
    await page.click('button:has-text("Salvar Dashboard")');
    await page.waitForTimeout(1000);

    // Verify it exists in the dashboards list
    const newDashboardCard = page.locator('.mantine-Card-root', { hasText: testDashboardName });
    await expect(newDashboardCard).toBeVisible({ timeout: 10000 });

    // Click "Abrir Dashboard" (the first button in the group inside the card representing play icon)
    console.log('>>> Opening the compiled dashboard...');
    await newDashboardCard.locator('button').first().click();

    // Verify execution page is loaded and dashboard title is displayed
    console.log('>>> Verifying execution page loads without errors...');
    const executionTitle = page.locator('.mantine-Title-root', { hasText: testDashboardName });
    await expect(executionTitle).toBeVisible({ timeout: 10000 });

    // Verify layout grid renders slots correctly
    await expect(page.locator('.mantine-Grid-root')).toBeVisible({ timeout: 5000 });

    console.log('>>> E2E Test Completed successfully!');
  });
});
