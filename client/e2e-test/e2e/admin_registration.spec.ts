import { test, expect } from '@playwright/test';

// Helper to generate a random string containing only alphabetic letters to comply with name validation rules
const randomLetters = (length: number) => {
  const letters = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ';
  return Array.from({ length }, () => letters[Math.floor(Math.random() * letters.length)]).join('');
};

test.describe('Admin Wizard Registration E2E Tests', () => {
  test.describe.configure({ mode: 'serial' });
  
  test.beforeEach(async ({ page }) => {
    // Navigate first to have a valid domain context, then clear localStorage
    await page.goto('/');
    await page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
  });

  test('should block registration when the link uses plain text date (invalid encoding)', async ({ page }) => {
    // Navigate with a date in the past
    await page.goto('#/user/registration/admin?d=2000-01-01:00:00');
    
    // Assert overlay is visible
    await expect(page.locator('.lock-overlay')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.lock-title')).toContainText('Link Expirado');
    
    // Check that form input is disabled or not accessible
    await expect(page.locator('input#managerEmail')).toBeDisabled();
  });

  test('should block registration when the link uses Base64 date (invalid encoding)', async ({ page }) => {
    // MjAwMC0wMS0wMTowMDowMA== is Base64 for 2000-01-01:00:00
    await page.goto('#/user/registration/admin?d=MjAwMC0wMS0wMTowMDowMA==');
    
    await expect(page.locator('.lock-overlay')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.lock-title')).toContainText('Link Expirado');
  });

  test('should block registration when the link is expired (decimal encoded date in past)', async ({ page }) => {
    // "50484848454849454849584848584848" is decimal ASCII for 2000-01-01:00:00
    await page.goto('#/user/registration/admin?d=50484848454849454849584848584848');
    
    await expect(page.locator('.lock-overlay')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.lock-title')).toContainText('Link Expirado');
    await expect(page.locator('input#managerEmail')).toBeDisabled();
  });

  test('should show WhatsApp support link when manager email already exists', async ({ page }) => {
    // 50485148454849454849584848584848 is decimal ASCII for 2030-01-01:00:00
    await page.goto('#/user/registration/admin?d=50485148454849454849584848584848');

    // Superadmin is guaranteed to exist
    const emailInput = page.locator('input#managerEmail');
    await emailInput.fill('superadmin@salesapp.com');
    await page.click('button:has-text("Verificar E-mail")');

    // Verify warnings
    await expect(page.locator('.user-exists-banner')).toBeVisible({ timeout: 15000 });
    await expect(page.locator('.user-exists-text')).toContainText('Usuário já cadastrado');
    
    // Verify whatsapp button link
    const whatsappLink = page.locator('.whatsapp-btn');
    await expect(whatsappLink).toBeVisible();
    await expect(whatsappLink).toHaveAttribute('href', /wa\.me/);
  });

  test('should successfully register a new manager and log them in', async ({ page }) => {
    test.setTimeout(60000);

    const RUN_ID = randomLetters(8);
    const managerEmail = `manager_${RUN_ID.toLowerCase()}@example.com`;
    const managerName = `Manager Name ${RUN_ID}`;
    const teamName = `Team ${RUN_ID}`;
    const password = `Password123!`;

    // 1. Navigate to Wizard with a future date (decimal encoded 2030-01-01:00:00)
    await page.goto('#/user/registration/admin?d=50485148454849454849584848584848');
    await expect(page.locator('.reg-title')).toContainText('Cadastro de Novo Gestor');

    // 2. Fill email and click Verify
    const emailInput = page.locator('input#managerEmail');
    await emailInput.fill(managerEmail);
    await page.click('button:has-text("Verificar E-mail")');

    // Wait for the next form fields to be revealed
    const nameInput = page.locator('input#name');
    await expect(nameInput).toBeVisible({ timeout: 15000 });

    // 3. Fill registration details
    await nameInput.fill(managerName);
    await page.fill('input#password', password);
    await page.fill('input#teamName', teamName);

    // Select classification level from select
    const select = page.locator('select#classification');
    await expect(select).toBeVisible();
    
    // Fill optional start date
    await page.fill('input#startDate', '2026-06-01');

    // Click Continue
    await page.click('button[type="submit"]');

    // 4. Step 2: Role selection page
    await expect(page.getByText('Quem está realizando este cadastro?')).toBeVisible({ timeout: 10000 });
    
    // Select manager role (first card)
    await page.click('.role-card:has-text("Eu sou o Gestor")');
    
    // Finalize
    await page.click('button:has-text("Finalizar Cadastro")');

    // 5. Step 3: Success page
    await expect(page.locator('.success-badge')).toBeVisible({ timeout: 20000 });
    await expect(page.locator('.instruction-text')).toContainText('assistir ao vídeo abaixo');
    await expect(page.locator('.video-wrapper iframe')).toBeVisible();

    // 6. Test direct login
    await page.fill('input#loginPassword', password);
    await page.click('button[type="submit"]');

    // 7. Verify successful login and redirect
    await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 20000 });

    // Verify token exists in local storage
    const token = await page.evaluate(() => localStorage.getItem('token'));
    expect(token).toBeTruthy();
  });
});
