import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

test.describe('Circular Hierarchy Prevention', () => {
  const RUN_ID = Date.now().toString().slice(-4);
  const EMAIL_A = `cycle.a.${RUN_ID}@test.com`;
  const EMAIL_B = `cycle.b.${RUN_ID}@test.com`;
  const EMAIL_C = `cycle.c.${RUN_ID}@test.com`;
  
  const testDataDir = path.resolve(process.cwd(), 'test-data');
  const filePath = path.join(testDataDir, `circular_test_${RUN_ID}.csv`);

  test.beforeAll(async () => {
    const csvContent = `Name,Email,ParentEmail,Matricula\nUser A,${EMAIL_A},${EMAIL_B},MAT-A-${RUN_ID}\nUser B,${EMAIL_B},${EMAIL_C},MAT-B-${RUN_ID}\nUser C,${EMAIL_C},${EMAIL_A},MAT-C-${RUN_ID}`;
    if (!fs.existsSync(testDataDir)) {
      fs.mkdirSync(testDataDir, { recursive: true });
    }
    fs.writeFileSync(filePath, csvContent);
  });

  test.afterAll(async () => {
    if (fs.existsSync(filePath)) {
      fs.unlinkSync(filePath);
    }
  });

  test('should detect and warn about circular references during user import', async ({ page }) => {
    test.setTimeout(60_000);

    // 1. Navigate directly to Users page (pre-authenticated)
    await page.goto('/#/users');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Usuários' })).toBeVisible();


    // 3. Open Import Modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Usuários em Lote')).toBeVisible();

    // 4. Upload the circular CSV
    await page.setInputFiles('input[type="file"]', filePath);
    
    // 5. Mappings step
    await page.click('button:has-text("Próximo")');
    await expect(page.getByText('Mapeamentos:')).toBeVisible({ timeout: 10000 });

    // Auto-mapping should work for Name, Email, ParentEmail, Matricula
    // Wait for the button to be enabled
    await expect(page.locator('button:has-text("Confirmar e Importar")')).toBeEnabled({ timeout: 10000 });
    await page.click('button:has-text("Confirmar e Importar")');

    // 6. Verify Warning in Results
    await expect(page.getByText('Avisos:')).toBeVisible({ timeout: 15000 });
    await expect(page.getByText('Encontramos referências circulares para estes usuários')).toBeVisible();
    await expect(page.getByText(EMAIL_A)).toBeVisible();
    await expect(page.getByText(EMAIL_B)).toBeVisible();
    await expect(page.getByText(EMAIL_C)).toBeVisible();

    // Close modal
    await page.click('button:has-text("Fechar")');

    // 7. Verify users were created and at least one parent is null (broken cycle)
    const emails = [EMAIL_A, EMAIL_B, EMAIL_C];
    let nullParentCount = 0;

    for (const email of emails) {
      // Search for the user
      await page.fill('input[placeholder="Buscar por nome ou email..."]', email);
      await page.waitForTimeout(1000); // Wait for debounced search
      
      const userRow = page.locator('table tbody tr').filter({ hasText: email });
      await expect(userRow).toBeVisible();

      // Check if parent info is displayed
      const parentInfo = userRow.locator('.user-parent');
      const isVisible = await parentInfo.isVisible();
      if (!isVisible) {
        nullParentCount++;
      }
    }

    // At least one user in the cycle MUST have a null parent to break the circular reference
    expect(nullParentCount).toBeGreaterThan(0);

    // 8. Cleanup: Delete (deactivate) the test users
    for (const email of emails) {
      await page.fill('input[placeholder="Buscar por nome ou email..."]', email);
      await page.waitForTimeout(500);
      
      const userRow = page.locator('table tbody tr').filter({ hasText: email });
      await userRow.locator('button[title="Excluir"]').click();
      
      // Confirm deletion
      await page.getByRole('dialog').getByRole('button', { name: 'Excluir' }).click();
      
      // Select "Inativos" filter to verify inactive status badge
      await page.locator('.search-bar .mantine-Select-input').click();
      await page.locator('.mantine-Select-option', { hasText: 'Inativos' }).first().click();
      await expect(userRow.getByText('Inativo')).toBeVisible();

      // Reset to "Ativos" filter for next search
      await page.locator('.search-bar .mantine-Select-input').click();
      await page.locator('.mantine-Select-option', { hasText: 'Ativos' }).first().click();
    }
  });
});
