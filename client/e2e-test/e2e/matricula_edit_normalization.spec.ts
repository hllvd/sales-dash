import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Matricula Edit and Normalization (TEAR 2)', () => {
  const targetUserEmail = 'carlosmendes@example.com';
  const targetUserName = 'Carlos Mendes';
  const initialMatricula = '77' + Date.now().toString().slice(-6);
  const normalizedValue = '99999';
  const inputWithZeros = '000' + normalizedValue;

  test.beforeEach(async ({ page }) => {
    await loginAs(page);
    await page.goto('/#/matriculas', { timeout: 15000 });
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Matrículas' })).toBeVisible({ timeout: 15000 });
  });


  test('should normalize matricula number and persist changes when editing', async ({ page }) => {
    console.log('>>> Step 1: Create initial matricula');
    await page.click('button:has-text("Nova Matrícula")');

    // Search and select User
    await page.fill('input[placeholder="Digite para buscar um usuário"]', targetUserEmail);
    // Wait for the async search to return results
    await page.waitForTimeout(1000);
    const option = page.locator('[role="option"]').filter({ hasText: targetUserEmail });
    await expect(option).toBeVisible({ timeout: 15000 });
    await option.click();

    await page.fill('input[placeholder="Ex: 123456"]', initialMatricula);
    await page.click('button:has-text("Criar Matrícula")');

    // Wait for modal to disappear
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    console.log('>>> Step 2: Find and edit the matricula');
    // Search to isolate the row
    await page.fill('input[placeholder="Buscar por número de matrícula (ou separadas por vírgula) ou usuário..."]', initialMatricula);
    await page.waitForTimeout(1500); // Wait for debounce

    const row = page.locator('tr', { hasText: initialMatricula });
    await expect(row).toBeVisible({ timeout: 10000 });

    // Click Edit button (IconEdit)
    await row.locator('.tabler-icon-edit').click();
    await expect(page.getByRole('dialog').getByText('Editar Matrícula')).toBeVisible();

    console.log('>>> Step 3: Change number to one with leading zeros and change status');
    // Change number to '00099999'
    const numberInput = page.locator('input[placeholder="Ex: 123456"]');
    await numberInput.fill(inputWithZeros);

    // Change status to Pending (or Active if it was something else)
    const statusSelect = page.getByRole('dialog').locator('label:has-text("Status")').locator('..').locator('.mantine-Select-input');
    await statusSelect.click();
    await page.locator('[role="option"]').filter({ hasText: 'Pendente' }).click();

    await page.click('button:has-text("Salvar Alterações")');

    // Wait for modal to disappear
    await expect(page.getByRole('dialog')).not.toBeVisible({ timeout: 10000 });

    console.log('>>> Step 4: Verify normalization and persistence');
    // Clear search and search for the new normalized number
    await page.fill('input[placeholder="Buscar por número de matrícula (ou separadas por vírgula) ou usuário..."]', '');
    await page.fill('input[placeholder="Buscar por número de matrícula (ou separadas por vírgula) ou usuário..."]', normalizedValue);
    await page.waitForTimeout(1500);

    const updatedRow = page.locator('tr', { hasText: normalizedValue });
    await expect(updatedRow).toBeVisible();
    
    // Check that it shows '99999' and not '00099999'
    await expect(updatedRow.locator('strong')).toHaveText(normalizedValue);
    
    // Check status changed to 'Pendente'
    await expect(updatedRow).toContainText('Pendente');

    console.log('>>> Step 5: Cleanup');
    await updatedRow.locator('.tabler-icon-trash').click();
    await page.getByRole('dialog').getByRole('button', { name: 'Excluir' }).click();
    await expect(updatedRow).not.toBeVisible({ timeout: 10000 });
    
    console.log('>>> Success: Matricula was edited, normalized, and persisted correctly.');
  });
});
