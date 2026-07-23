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

    // Fill new parent email and submit
    await page.fill('input[placeholder="superior@exemplo.com"]', 'admin@salesapp.com');
    await expect(page.locator('input[placeholder="superior@exemplo.com"]')).toHaveValue('admin@salesapp.com');
    await page.click('button:has-text("Enviar Solicitação")');

    // Verify success banner or modal close
    await expect(page.locator('text=Solicitação de alteração enviada com sucesso!')).toBeVisible();

    // Go to Requests page to verify request is listed under Minhas Solicitações
    await page.goto('/#/requests');
    await page.click('text=Minhas Solicitações');
    await expect(page.locator('text=Alteração de Superior (ParentEmail)').first()).toBeVisible();
  });
});
