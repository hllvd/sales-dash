import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Classification Management & History E2E Tests', () => {
  // Use serial mode to keep mutable actions ordered
  test.describe.configure({ mode: 'serial' });

  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';

  const timestamp = Date.now();
  const testLevelName = `E2E Test Level ${timestamp}`;
  const testGoal = 75000;
  const testPrize = `E2E Prize ${timestamp}`;

  // Unique names for clean lookups
  const searchPattern = 'E2E Test Level';

  test.beforeEach(async ({ page }) => {
    // Navigate first to have a valid domain context, then clear localStorage
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());
  });

  test('should manage classification levels and member assignments including collapsed history', async ({ page }) => {
    // 1. Log in as superadmin
    await loginAs(page, adminEmail, adminPassword);


    // Wait for landing nav to load and navigate to Classifications Page
    const classificationsLink = page.locator('a[href="#/classifications"]');
    await expect(classificationsLink).toBeVisible({ timeout: 10000 });
    await classificationsLink.click();
    await expect(page.getByRole('heading', { name: 'Níveis de Classificação' })).toBeVisible({ timeout: 10000 });

    // ────────────────────────────────────────────────────────────────────────
    // PRE-CLEANUP ROUTINE (Clean-Up First Pattern)
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Running E2E Level pre-cleanup...');
    // Re-read the card list after each deletion (DOM re-renders after delete)
    let keepCleaning = true;
    while (keepCleaning) {
      keepCleaning = false;
      const cardNames = await page.locator('.cls-level-card__name').allInnerTexts();
      for (const name of cardNames) {
        if (name.includes(searchPattern)) {
          console.log(`>>> Cleaning up stale E2E level: ${name}`);
          const staleCard = page.locator('.cls-level-card', { hasText: name }).first();
          await staleCard.locator('button').nth(2).click();
          await page.getByRole('dialog').getByRole('button', { name: 'Excluir' }).click();
          await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });
          keepCleaning = true; // re-scan after DOM update
          break;
        }
      }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 1. CREATE LEVEL
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Creating a new classification level...');
    await page.click('button:has-text("Novo Nível")');

    // Verify Modal high contrast header
    const modalTitle = page.locator('.mantine-Modal-title');
    await expect(modalTitle).toContainText('Novo Nível de Classificação');

    // Fill form
    await page.fill('input[placeholder="Ex: Prata, Ouro, Estrela..."]', testLevelName);
    await page.fill('textarea[placeholder="Descrição opcional do nível..."]', 'Formulated description for E2E testing purposes');
    await page.fill('input[placeholder="Ex: Bônus de R$ 500, Viagem..."]', testPrize);
    
    // Fill Goal (salesGoal is a mantine number input)
    await page.fill('input[placeholder="Ex: 50000"]', testGoal.toString());

    // Fill Retention
    await page.fill('input[placeholder="Ex: 85"]', '80');

    // Submit
    await page.click('button[type="submit"]:has-text("Criar Nível")');

    // Verify card is added
    const newCard = page.locator('.cls-level-card', { hasText: testLevelName });
    await expect(newCard).toBeVisible({ timeout: 10000 });
    await expect(newCard.locator('.cls-level-card__stats')).toContainText(`Meta: R$ ${testGoal.toLocaleString('pt-BR')}`);
    await expect(newCard.locator('.cls-level-card__stats')).toContainText(testPrize);
    await expect(newCard.locator('.cls-level-card__stats')).toContainText('Retenção: 80%');

    // ────────────────────────────────────────────────────────────────────────
    // 2. ASCENDING ORDERING VERIFICATION
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Verifying ascending order of levels by goal...');
    const goalSmaller = testGoal - 10000;
    const levelSmallerName = `E2E Test Level Smaller ${timestamp}`;
    
    await page.click('button:has-text("Novo Nível")');
    await page.fill('input[placeholder="Ex: Prata, Ouro, Estrela..."]', levelSmallerName);
    await page.fill('input[placeholder="Ex: 50000"]', goalSmaller.toString());
    await page.click('button[type="submit"]:has-text("Criar Nível")');

    // Settle SQLite write delay
    await page.waitForTimeout(500);

    // Assert ordering: find the DOM order index of each E2E card by name.
    // This is 100% robust against grid columns, responsive wrapping, and coordinates.
    const names = await page.locator('.cls-level-card .cls-level-card__name').allInnerTexts();
    const smallerIndex = names.findIndex(name => name.includes(levelSmallerName));
    const largerIndex = names.findIndex(name => name.includes(testLevelName));
    
    console.log(`>>> Cards ordering indices in DOM: smaller=${smallerIndex}, larger=${largerIndex}`);
    expect(smallerIndex).toBeGreaterThan(-1);
    expect(largerIndex).toBeGreaterThan(-1);
    expect(smallerIndex).toBeLessThan(largerIndex);

    // ────────────────────────────────────────────────────────────────────────
    // 3. EDIT LEVEL
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Editing classification level...');
    const editBtn = newCard.locator('button').nth(1);
    await editBtn.click();

    // Verify modal header is visible and correct
    await expect(modalTitle).toContainText(`Editar: ${testLevelName}`);

    // Verify Retention is pre-populated from the created level
    await expect(page.locator('input[placeholder="Ex: 85"]')).toHaveValue('80%');

    // Update goal
    const newGoal = testGoal + 25000;
    await page.fill('input[placeholder="Ex: 50000"]', newGoal.toString());
    await page.click('button[type="submit"]:has-text("Salvar Alterações")');

    // Verify updated goal
    await expect(newCard.locator('.cls-level-card__stats')).toContainText(`Meta: R$ ${newGoal.toLocaleString('pt-BR')}`);

    // ────────────────────────────────────────────────────────────────────────
    // 4. MEMBERS ASSIGNMENT, REMOVAL, AND COLLAPSED INACTIVE SECTION
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Managing level members...');
    const membersBtn = newCard.locator('button').nth(0);
    await membersBtn.click();

    // Verify modal opened
    const membersModal = page.locator('.mantine-Modal-content');
    await expect(membersModal).toBeVisible({ timeout: 5000 });

    // Assert active members list is initially empty
    await expect(membersModal.getByText('Nenhum membro ativo neste nível', { exact: true })).toBeVisible();

    // Search and assign user (We will select the first available user in the selection panel)
    const selectUserCard = membersModal.locator('.cls-select-user-card').first();
    await expect(selectUserCard).toBeVisible({ timeout: 10000 });
    
    const assignedUserName = await selectUserCard.locator('.cls-member-card__name').innerText();
    console.log(`>>> Assigning user to level: ${assignedUserName}`);
    
    // Click the user card to assign immediately
    await selectUserCard.click();

    // Settle SQLite write delay
    await page.waitForTimeout(500);

    // Assert user is now listed under "Membros Ativos"
    const activeMemberCard = membersModal.locator('.cls-member-card', { hasText: assignedUserName });
    await expect(activeMemberCard).toBeVisible({ timeout: 5000 });
    
    // ────────────────────────────────────────────────────────────────────────
    // 5. COLLAPSIBLE "MEMBROS INATIVOS" & DYNAMIC HEIGHT TEST
    // ────────────────────────────────────────────────────────────────────────
    console.log('>>> Removing member and verifying Collapsed Inactive section...');
    // Click "Remover do nível" (X icon - 2nd button: index 1)
    await activeMemberCard.locator('button').nth(1).click();

    // Settle SQLite write delay
    await page.waitForTimeout(500);

    // Assert active list is empty again
    await expect(membersModal.getByText('Nenhum membro ativo neste nível', { exact: true })).toBeVisible();

    // Inactive header should now show (1) inactive member — click the group row to toggle
    const inactiveHeader = membersModal.getByText('Membros Inativos (1)', { exact: true }).first();
    await expect(inactiveHeader).toBeVisible();

    // Assert that the inactive members section is collapsed initially
    const inactiveCard = membersModal.locator('.cls-member-card.inactive', { hasText: assignedUserName });
    await expect(inactiveCard).not.toBeVisible();

    // Assert dynamic active ScrollArea expanded height viewport (collapsed state height 340)
    // We verify that the ScrollArea for active members has the larger capacity
    const activeScrollArea = membersModal.locator('.mantine-ScrollArea-viewport').first();
    await expect(activeScrollArea).toHaveCSS('max-height', 'none'); // dynamic Mantine container

    // Expand the inactive members section
    console.log('>>> Toggling Inactive Members container...');
    await inactiveHeader.click();

    // Assert that the inactive member card is now visible with dashed borders
    await expect(inactiveCard).toBeVisible({ timeout: 5000 });
    await expect(inactiveCard).toHaveClass(/inactive/);
    await expect(inactiveCard.locator('.cls-member-card__dates')).toContainText('Período:');

    // Close Modal
    await membersModal.locator('button.mantine-Modal-close').click();
    await expect(membersModal).not.toBeVisible();

    console.log('>>> E2E Test completed successfully! Leaving level intact for visual debugging.');
  });
});
