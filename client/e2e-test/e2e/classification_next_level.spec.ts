import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';


test.describe('Classification Level — NextLevel Chain (TEAR 3)', () => {
  test.describe.configure({ mode: 'serial' });

  const adminEmail = 'superadmin@salesapp.com';
  const adminPassword = 'string';

  const timestamp = Date.now();
  const levelAName = `E2E Chain Level A ${timestamp}`;
  const levelBName = `E2E Chain Level B ${timestamp}`;
  const searchPattern = 'E2E Chain Level';

  test.beforeEach(async ({ page }) => {
    await loginAs(page, adminEmail, adminPassword);
  });

  test('should create a NextLevel chain, persist it, and clear it', async ({ page }) => {
    await page.goto('/#/classifications');
    await expect(page.getByRole('heading', { name: 'Níveis de Classificação' })).toBeVisible({ timeout: 10000 });


    // ── 2. Pre-cleanup (Clean-Up First pattern) ─────────────────────────────
    console.log('>>> Running E2E Chain Level pre-cleanup...');
    let keepCleaning = true;
    while (keepCleaning) {
      keepCleaning = false;
      const cardNames = await page.locator('.cls-level-card__name').allInnerTexts();
      for (const name of cardNames) {
        if (name.includes(searchPattern)) {
          console.log(`>>> Cleaning up stale E2E chain level: ${name}`);
          const staleCard = page.locator('.cls-level-card', { hasText: name }).first();

          // Open members modal and clear active members to prevent deletion conflict
          await staleCard.locator('button').nth(0).click();
          const membersModal = page.locator('.mantine-Modal-content');
          if (await membersModal.isVisible({ timeout: 3000 }).catch(() => false)) {
            let hasActiveMembers = true;
            while (hasActiveMembers) {
              const firstMemberCard = membersModal.locator('.cls-member-card:not(.inactive)').first();
              if (await firstMemberCard.isVisible({ timeout: 1500 }).catch(() => false)) {
                await firstMemberCard.locator('button').last().click();
                await page.waitForTimeout(400);
              } else {
                hasActiveMembers = false;
              }
            }
            await membersModal.locator('button.mantine-Modal-close').click();
            await expect(membersModal).not.toBeVisible();
            await page.waitForTimeout(300);
          }

          await staleCard.locator('button').nth(2).click();
          const dialog = page.getByRole('dialog');
          await dialog.getByRole('button', { name: 'Excluir' }).click();
          await expect(dialog).not.toBeVisible({ timeout: 10000 });
          keepCleaning = true;
          break;
        }
      }
    }

    // ── 3. Create Level A (base tier) ───────────────────────────────────────
    console.log(`>>> Creating Level A: ${levelAName}`);
    await page.click('button:has-text("Novo Nível")');
    await expect(page.locator('.mantine-Modal-title')).toContainText('Novo Nível de Classificação');
    await page.fill('input[placeholder="Ex: Prata, Ouro, Estrela..."]', levelAName);
    await page.click('button[type="submit"]:has-text("Criar Nível")');

    const cardA = page.locator('.cls-level-card', { hasText: levelAName });
    await expect(cardA).toBeVisible({ timeout: 10000 });

    // SQLite settle
    await page.waitForTimeout(400);

    // ── 4. Create Level B (next tier) ───────────────────────────────────────
    console.log(`>>> Creating Level B: ${levelBName}`);
    await page.click('button:has-text("Novo Nível")');
    await expect(page.locator('.mantine-Modal-title')).toContainText('Novo Nível de Classificação');
    await page.fill('input[placeholder="Ex: Prata, Ouro, Estrela..."]', levelBName);
    await page.click('button[type="submit"]:has-text("Criar Nível")');

    const cardB = page.locator('.cls-level-card', { hasText: levelBName });
    await expect(cardB).toBeVisible({ timeout: 10000 });

    // SQLite settle
    await page.waitForTimeout(400);

    // ── 5. Edit Level A → set NextLevel = Level B ───────────────────────────
    console.log('>>> Editing Level A to set NextLevel = Level B...');
    // Edit button is index 1 (0=members, 1=edit, 2=delete)
    await cardA.locator('button').nth(1).click();
    await expect(page.locator('.mantine-Modal-title')).toContainText(`Editar: ${levelAName}`);

    // Open the NextLevel select (identified by data-testid)
    const nextLevelSelect = page.locator('[data-testid="select-next-level"]');
    await expect(nextLevelSelect).toBeVisible();
    await nextLevelSelect.click();

    // Pick Level B from the dropdown options
    const option = page.locator('.mantine-Select-option', { hasText: levelBName }).first();
    await expect(option).toBeVisible({ timeout: 5000 });
    await option.click();

    // Verify selection shows Level B name
    await expect(nextLevelSelect).toHaveValue(levelBName);

    // Submit
    await page.click('button[type="submit"]:has-text("Salvar Alterações")');

    // ── 6. Assert chip appears on Level A card ──────────────────────────────
    console.log('>>> Asserting NextLevel chip on Level A card...');
    const nextLevelChip = cardA.locator('.cls-next-level-chip');
    await expect(nextLevelChip).toBeVisible({ timeout: 10000 });
    await expect(nextLevelChip).toContainText(levelBName);

    // ── 7. Verify persistence after page reload ─────────────────────────────
    console.log('>>> Verifying persistence after reload...');
    await page.reload();
    await expect(page.getByRole('heading', { name: 'Níveis de Classificação' })).toBeVisible({ timeout: 10000 });

    const cardAAfterReload = page.locator('.cls-level-card', { hasText: levelAName });
    const chipAfterReload = cardAAfterReload.locator('.cls-next-level-chip');
    await expect(chipAfterReload).toBeVisible({ timeout: 10000 });
    await expect(chipAfterReload).toContainText(levelBName);

    // ── 8. Edit Level A → clear NextLevel ───────────────────────────────────
    console.log('>>> Clearing NextLevel from Level A...');
    await cardAAfterReload.locator('button').nth(1).click();
    await expect(page.locator('.mantine-Modal-title')).toContainText(`Editar: ${levelAName}`);

    // The select should still show Level B
    const nextLevelSelectEdit = page.locator('[data-testid="select-next-level"]');
    await expect(nextLevelSelectEdit).toHaveValue(levelBName);

    // Hover the Select wrapper to reveal the clear button (Mantine hides it unless hovered)
    await page.locator('[data-testid="select-next-level"]').locator('..').hover();
    const clearBtn = page.locator('[data-testid="select-next-level"]').locator('..').locator('button');
    await clearBtn.click();
    await expect(nextLevelSelectEdit).not.toHaveValue(levelBName);

    // Submit
    await page.click('button[type="submit"]:has-text("Salvar Alterações")');

    // Assert chip disappears
    const cardAFinal = page.locator('.cls-level-card', { hasText: levelAName });
    await expect(cardAFinal.locator('.cls-next-level-chip')).not.toBeVisible({ timeout: 10000 });

    console.log('>>> NextLevel chain E2E test completed! Leaving levels intact for visual inspection.');
  });
});
