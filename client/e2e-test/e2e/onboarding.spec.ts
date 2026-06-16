import { test, expect } from '@playwright/test';

test.describe('Onboarding Wizard E2E', () => {
  
  test.beforeEach(async ({ page }) => {
    // Clear localStorage to ensure fresh onboarding state
    await page.goto('/');
    await page.evaluate(() => localStorage.clear());
  });

  test('should show onboarding checklist for regular user and complete it', async ({ page }) => {
    // 1. Login as regular user (Julio Mota)
    await page.goto('/');
    await page.fill('input[type="email"]', 'juliomota@example.com');
    await page.fill('input[type="password"]', 'ChangeMe123!');
    await page.click('button.login-button');

    // 2. Verify we land on MyContractsPage and onboarding widget is visible
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });
    const widget = page.locator('.onboarding-widget');
    await expect(widget).toBeVisible();

    // 3. Check minimization/expansion
    await widget.locator('.onboarding-toggle-btn').click();
    await expect(widget).toHaveClass(/minimized/);

    await widget.locator('.onboarding-toggle-btn').click();
    await expect(widget).not.toHaveClass(/minimized/);

    // 4. Carlos might complete the step immediately because he has contracts in seed data.
    // So either we see the task title first, or we see the celebration directly.
    const celebration = widget.locator('.onboarding-celebration');
    const isCompletedImmediately = await celebration.isVisible();
    
    if (!isCompletedImmediately) {
      // Verify the step is listed
      await expect(widget.locator('.onboarding-step-title')).toContainText('Verificar seus contratos');
      // Wait for it to complete
      const stepCard = widget.locator('.onboarding-step-card');
      await expect(stepCard).toHaveClass(/completed/, { timeout: 10000 });
      await expect(celebration).toBeVisible();
    }

    await expect(celebration.locator('h4')).toContainText('Configuração Concluída!');

    // 5. Click "Começar a usar" to close
    await celebration.locator('.onboarding-celebration-btn').click();
    await expect(widget).not.toBeVisible();

    // 6. Verify the sidebar menu has "Guia de Configuração" NavLink with "Pronto" badge
    const sidebarLink = page.locator('.mantine-NavLink-root', { hasText: 'Guia de Configuração' }).first();
    await expect(sidebarLink).toBeVisible();
    await expect(sidebarLink.locator('.mantine-Badge-root')).toContainText('Pronto');
  });

  test('should show onboarding checklist for admin and handle step navigation', async ({ page }) => {
    // 1. Login as admin
    await page.goto('/');
    await page.fill('input[type="email"]', 'admin@salesapp.com');
    await page.fill('input[type="password"]', 'admin123');
    await page.click('button.login-button');

    // 2. Verify onboarding widget is visible
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 10000 });
    const widget = page.locator('.onboarding-widget');
    await expect(widget).toBeVisible();

    // 3. Verify admin specific steps are shown
    const stepTitles = widget.locator('.onboarding-step-title');
    await expect(stepTitles.nth(0)).toContainText('Importar contratos via Assistente');
    await expect(stepTitles.nth(1)).toContainText('Adicionar usuários às Equipes');
    await expect(stepTitles.nth(2)).toContainText('Acessar/Criar Dashboards');
    await expect(stepTitles.nth(3)).toContainText('Visualizar Relatórios');

    // 4. Click a step to navigate (e.g. Visualizar Relatórios)
    // Relatórios step is the 4th item (index 3)
    await widget.locator('.onboarding-step-card').nth(3).click();
    
    // Verify hash changed to #/reports
    await page.waitForURL('**/#/reports');
    
    // On #/reports (a non-landing page), the widget auto-minimizes.
    // Expand it again to inspect the steps
    await widget.locator('.onboarding-header').click();
    
    // The view_reports step should be marked as completed when visiting reports
    await expect(widget.locator('.onboarding-step-card').nth(3)).toHaveClass(/completed/, { timeout: 10000 });

    // 5. Close the checklist widget using close button
    await widget.locator('.onboarding-close-btn').click();
    await expect(widget).not.toBeVisible();

    // 6. Verify sidebar has "Guia de Configuração" NavLink with "A fazer" badge since not all steps are done
    const sidebarLink = page.locator('.mantine-NavLink-root', { hasText: 'Guia de Configuração' }).first();
    await expect(sidebarLink).toBeVisible();
    await expect(sidebarLink.locator('.mantine-Badge-root')).toContainText('A fazer');

    // 7. Click sidebar link to re-open the widget
    await sidebarLink.click();
    await expect(widget).toBeVisible();
  });
});
