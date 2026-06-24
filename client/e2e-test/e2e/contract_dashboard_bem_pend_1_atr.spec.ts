import { test, expect, type Page } from '@playwright/test';
import path from 'path';

/**
 * E2E tests for the "CONT BEM PEND 1 ATR" status that was added to the
 * contractDashboard import. This status must be recognised as Late1 (one
 * installment in arrears) — NOT as Pending or Canceled.
 *
 * Test data: test-data/contracts_bem_pend_1_atr.csv
 *   Row 1  TEST-BEM-PEND1-001  →  CONT BEM PEND 1 ATR  →  should map to Late1
 *   Row 2  TEST-BEM-PEND1-OK   →  Ativo                 →  should map to Active
 */

const TEST_FILE = path.resolve(process.cwd(), 'test-data', 'contracts_bem_pend_1_atr.csv');

async function loginAndGoToContracts(page: Page): Promise<void> {
  await page.goto('/');
  await page.evaluate(() => {
    localStorage.clear();
    sessionStorage.clear();
  });
  await page.reload();
  await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15_000 });
  await page.fill('input[type="email"]', 'superadmin@salesapp.com');
  await page.fill('input[type="password"]', 'string');
  await page.click('button.login-button');
  await expect(page.locator('a[href="#/contracts"]')).toBeVisible({ timeout: 15_000 });
  await page.click('a[href="#/contracts"]');
  await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible();
}

test.describe('Dashboard Import — CONT BEM PEND 1 ATR Status', () => {
  test.describe.configure({ mode: 'serial' });

  test(
    'CONT BEM PEND 1 ATR should be accepted as a valid status (no validation error)',
    async ({ page }) => {
      test.setTimeout(90_000);

      await loginAndGoToContracts(page);

      // Open import modal
      await page.click('button:has-text("Importar")');
      await expect(page.getByText('Importar Contratos em Lote')).toBeVisible();

      // Upload CSV containing CONT BEM PEND 1 ATR
      await page.setInputFiles('input#file', TEST_FILE);
      const nextBtn = page.locator('button:has-text("Próximo")');
      await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
      await nextBtn.click();

      // Handle possible header-mismatch warning
      const proceedBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
      try {
        if (await proceedBtn.isVisible({ timeout: 3_000 })) {
          await proceedBtn.click();
        }
      } catch {
        // Not present — fine
      }

      // Wait for the status validation network call to settle
      const validationResponse = page.waitForResponse(
        r => r.url().includes('/validate-status') && r.status() === 200,
        { timeout: 15_000 }
      );

      // Mapping step must be visible
      await expect(page.locator('.mapping-section')).toBeVisible({ timeout: 15_000 });
      await validationResponse;

      // No validation warning must be shown — CONT BEM PEND 1 ATR is a known value
      await expect(page.locator('#status-validation-warning')).not.toBeVisible({ timeout: 5_000 });

      // Success indicator must be shown
      await expect(
        page.getByText('Todos os valores de status são válidos.')
      ).toBeVisible({ timeout: 10_000 });

      // Confirm button must be enabled
      const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
      await expect(confirmBtn).toBeEnabled({ timeout: 10_000 });
    }
  );

  test(
    'CONT BEM PEND 1 ATR should import successfully and appear in the contracts list',
    async ({ page }) => {
      test.setTimeout(90_000);

      await loginAndGoToContracts(page);

      // Open import modal
      await page.click('button:has-text("Importar")');
      await expect(page.getByText('Importar Contratos em Lote')).toBeVisible();

      // Upload and advance
      await page.setInputFiles('input#file', TEST_FILE);
      const nextBtn = page.locator('button:has-text("Próximo")');
      await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
      await nextBtn.click();

      // Handle mismatch warning
      const proceedBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
      try {
        if (await proceedBtn.isVisible({ timeout: 3_000 })) {
          await proceedBtn.click();
        }
      } catch {
        // Not present — fine
      }

      await expect(page.locator('.mapping-section')).toBeVisible({ timeout: 15_000 });

      // Wait for validation to finish before confirming
      await page
        .waitForResponse(
          r => r.url().includes('/validate-status') && r.status() === 200,
          { timeout: 15_000 }
        )
        .catch(() => {/* already resolved */});

      // Confirm import
      const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
      await expect(confirmBtn).toBeEnabled({ timeout: 10_000 });
      await confirmBtn.click();

      // Result step
      await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30_000 });

      // Both rows must be imported (no desistente-style skipping)
      await expect(page.getByText(/Importados:\s*2/)).toBeVisible({ timeout: 5_000 });

      // Close modal
      await page.click('button:has-text("Fechar")');

      // Clear any active filters
      const clearBtn = page.locator('button.clear-filters-btn');
      if (await clearBtn.isVisible()) {
        await clearBtn.click();
        await page.waitForTimeout(500);
      }

      // Verify TEST-BEM-PEND1-001 exists in contracts list
      await page.fill('input#filterContractNumber', 'TEST-BEM-PEND1-001');
      const rowBemPend = page.locator('table tbody tr', { hasText: 'TEST-BEM-PEND1-001' });
      await expect(rowBemPend).toBeVisible({ timeout: 10_000 });

      // The Status cell must show "Atrasado 1" (the Late1 label) — not Cancelado or Ativo
      const statusCell = rowBemPend.locator('td').nth(6); // Status is the 7th column (0-indexed: 6)
      await expect(statusCell).toContainText('Atrasado 1', { timeout: 5_000 });
    }
  );
});
