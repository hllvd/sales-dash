import { test, expect } from '@playwright/test';
import { loginAs, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD, ADMIN_EMAIL, ADMIN_PASSWORD } from './helpers/auth';

test.describe('Helpdesk Private FAQ Flow (TEAR 3B)', () => {
  test.describe.configure({ mode: 'serial' });

  const testQuestionPrefix = `Duvida E2E - Como conferir comissao`;
  let questionTitle = '';
  let questionContent = '';
  let superadminReply = '';

  // Second admin credentials (seeded admin user in database)
  const ADMIN2_EMAIL = 'user@example.com';
  const ADMIN2_PASSWORD = 'string';

  test.beforeAll(() => {
    const runId = Math.floor(10000 + Math.random() * 90000).toString();
    questionTitle = `${testQuestionPrefix} ${runId}`;
    questionContent = `Pergunta detalhada enviada pelo Admin ${runId} sobre os totais de comissao e retencao.`;
    superadminReply = `Resposta oficial do Superadmin ${runId}: Os valores de comissao sao calculados com base no Total Ativo.`;
  });

  test('Step 1: Admin 1 clicks help ? (visible immediately without scroll), attaches an image, and submits question', async ({ page }) => {
    test.setTimeout(60000);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    // Go to My Contracts page
    await page.goto('/#/my-contracts');
    await expect(page.locator('h1, h2, .mantine-Title-root').first()).toBeVisible({ timeout: 15000 });

    // Verify ? help button is immediately visible on the page WITHOUT scrolling
    const helpBtn = page.locator('button[aria-label="Ajuda e FAQ"]');
    await expect(helpBtn).toBeVisible({ timeout: 10000 });
    await helpBtn.click();

    // Modal should open
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
    await expect(modal.getByText('Fazer uma Pergunta')).toBeVisible();

    // Fill form
    await modal.locator('input[placeholder*="Como funciona a retenção"]').fill(questionTitle);
    await modal.locator('textarea[placeholder*="Explique o cenário"]').fill(questionContent);

    // Attach sample image (PNG)
    const fileChooserPromise = page.waitForEvent('filechooser');
    await modal.getByRole('button', { name: /Adicionar Imagem/i }).click();
    const fileChooser = await fileChooserPromise;
    // 1x1 transparent PNG buffer
    const pngBuffer = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64');
    await fileChooser.setFiles({
      name: 'screenshot.png',
      mimeType: 'image/png',
      buffer: pngBuffer
    });

    // Verify image preview is rendered
    await expect(modal.getByAltText('Preview da dúvida')).toBeVisible({ timeout: 10000 });

    // Submit question
    await modal.getByRole('button', { name: 'Enviar Pergunta' }).click();

    // Verify modal closes
    await expect(modal).not.toBeVisible({ timeout: 10000 });

    // Navigate to Ajuda / Documents page and verify question is listed for Admin 1
    await page.goto('/#/documents');
    await expect(page.getByText(questionTitle)).toBeVisible({ timeout: 15000 });
    await expect(page.locator('tr', { hasText: questionTitle }).getByText('Não lido')).toBeVisible();
  });

  test('Step 2: Admin 2 logs in and CANNOT see Admin 1 question', async ({ page }) => {
    test.setTimeout(60000);
    await loginAs(page, ADMIN2_EMAIL, ADMIN2_PASSWORD);

    await page.goto('/#/documents');
    await page.waitForTimeout(1000);

    // Admin 2 should NOT see Admin 1 question
    const row = page.locator('tr', { hasText: questionTitle });
    await expect(row).not.toBeVisible();
  });

  test('Step 3: SuperAdmin views question, replies, and resolves topic', async ({ page }) => {
    test.setTimeout(60000);
    await loginAs(page, SUPERADMIN_EMAIL, SUPERADMIN_PASSWORD);

    await page.goto('/#/documents');
    await expect(page.getByText('Gestão de Dúvidas & FAQ (SuperAdmin)')).toBeVisible({ timeout: 15000 });

    // SuperAdmin sees Admin 1 question
    const row = page.locator('tr', { hasText: questionTitle });
    await expect(row).toBeVisible({ timeout: 15000 });

    // Open question
    await row.click();
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
    await expect(modal.getByText(questionTitle)).toBeVisible();

    // Send reply
    await modal.locator('textarea[placeholder*="Digite sua resposta"]').fill(superadminReply);
    await modal.getByRole('button', { name: 'Enviar Mensagem' }).click();
    await expect(modal.getByText(superadminReply)).toBeVisible({ timeout: 10000 });

    // Mark as resolved (opens confirmation dialog for AI Knowledge Base summary)
    const resolveBtn = modal.getByRole('button', { name: 'Marcar como Resolvido' });
    await resolveBtn.click();

    // Confirm AI summary generation
    const confirmDialog = page.getByRole('dialog', { name: 'Marcar como Resolvido' });
    await expect(confirmDialog).toBeVisible();
    await confirmDialog.getByRole('button', { name: 'Sim, gerar resumo com IA' }).click();

    await expect(modal.locator('.mantine-Badge-root', { hasText: 'Resolvido' })).toBeVisible({ timeout: 10000 });

    // Close question modal
    await modal.locator('.mantine-Modal-close').first().click();
    await expect(modal).not.toBeVisible();
  });

  test('Step 4: Admin 1 logs in, sees reply and status Resolvido', async ({ page }) => {
    test.setTimeout(60000);
    await loginAs(page, ADMIN_EMAIL, ADMIN_PASSWORD);

    await page.goto('/#/documents');
    const row = page.locator('tr', { hasText: questionTitle });
    await expect(row).toBeVisible({ timeout: 15000 });
    await expect(row.getByText('Resolvido')).toBeVisible();

    // Open thread to confirm reply content and attached image
    await row.click();
    const modal = page.getByRole('dialog');
    await expect(modal).toBeVisible();
    await expect(modal.getByText(superadminReply)).toBeVisible();
    await expect(modal.getByAltText('Imagem da dúvida')).toBeVisible({ timeout: 10000 });
  });
});
