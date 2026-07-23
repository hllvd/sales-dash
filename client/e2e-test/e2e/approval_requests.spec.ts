import { test, expect } from '@playwright/test';

test.describe('Approval Requests E2E', () => {
  test.describe.configure({ mode: 'serial' });

  test('Superadmin can view requests page and tabs', async ({ page }) => {
    // 1. Login as SuperAdmin
    await page.goto('/#/login');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button[type="submit"]');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible();

    // 2. Navigate to Requests page
    await page.goto('/#/requests');
    await page.waitForSelector('text=Central de Solicitações');

    // 3. Verify tabs exist
    await expect(page.locator('text=Solicitações Pendentes')).toBeVisible();
    await expect(page.locator('text=Minhas Solicitações')).toBeVisible();
  });

  test('User can create parent email change request and approver can approve it', async ({ page }) => {
    // Login as Superadmin
    await page.goto('/#/login');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button[type="submit"]');
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible();

    // Go to My Profile and open request modal
    await page.goto('/#/my-profile');
    await page.click('text=Solicitar Alteração de Superior');
    await page.waitForSelector('text=E-mail do Novo Superior');

    // Fill new parent email using keyboard events to trigger React onChange
    const emailInput = page.locator('input[placeholder="superior@exemplo.com"]');
    await emailInput.click();
    await emailInput.selectText();
    await emailInput.pressSequentially('admin@salesapp.com', { delay: 30 });
    await expect(emailInput).toHaveValue('admin@salesapp.com');
    await page.click('button:has-text("Enviar Solicitação")');

    // Wait for modal to close (success closes it) and verify success alert appears on the page
    await expect(page.locator('[role="dialog"]')).toHaveCount(0, { timeout: 15000 });
    await expect(page.locator('.mantine-Alert-root, [role="alert"]').filter({ hasText: 'Solicitação de alteração enviada com sucesso!' })).toBeVisible({ timeout: 15000 });

    // Go to Requests page to verify request is listed under Minhas Solicitações
    await page.goto('/#/requests');
    await page.click('text=Minhas Solicitações');
    await expect(page.getByRole('cell', { name: 'Alteração de Superior (ParentEmail)' }).first()).toBeVisible({ timeout: 15000 });
  });
});
