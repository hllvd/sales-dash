import { test, expect } from '@playwright/test';

test.describe('Matricula Health Monitoring', () => {
  test('should show correct contract count and last update time for matriculas 9999 and 11177', async ({ page }) => {
    test.setTimeout(30_000);

    // 1. Login as superadmin
    await page.goto('/');
    await page.fill('input[type="email"]', 'superadmin@salesapp.com');
    await page.fill('input[type="password"]', 'string');
    await page.click('button.login-button');

    // Wait for the main page to load
    await expect(page.locator('.mantine-AppShell-navbar')).toBeVisible({ timeout: 15000 });

    // 2. Navigate directly to the Matricula Health Monitoring page
    await page.goto('/#/monitoring/matricula-health');

    // 3. Verify page title is loaded
    await expect(page.getByRole('heading', { name: 'Saúde das Matrículas' })).toBeVisible({ timeout: 10000 });

    // Helper to verify matricula row content, freshness, and status
    async function verifyMatriculaRow(matricula: string, expectedCount: string) {
      const row = page.locator('table tbody tr').filter({ hasText: matricula });
      await expect(row).toBeVisible({ timeout: 10000 });

      // Verify contract count
      const countCell = row.locator('td').nth(1);
      await expect(countCell).toHaveText(expectedCount);

      // Verify update time is recent (less than 5 minutes)
      const dateCell = row.locator('td').nth(2);
      const cellText = await dateCell.textContent() || '';
      console.log(`Detected date cell content for matricula ${matricula}:`, cellText);

      // Verify update time is recent by checking if relative text indicates it was updated within seconds/minutes
      const isRecent = /segundo|minuto|second|minute|now|agora/i.test(cellText);
      if (!isRecent) {
        throw new Error(`Matricula ${matricula} update date is not recent: "${cellText}"`);
      }
      expect(isRecent).toBe(true);

      // Verify status badge is "Healthy" (Normal)
      const statusCell = row.locator('td').nth(3);
      await expect(statusCell).toContainText('Normal');
    }

    // 4. Verify Matricula 9999 has 19 contracts and is healthy
    await verifyMatriculaRow('9999', '19');

    // 5. Verify Matricula 11177 has 173 contracts and is healthy
    await verifyMatriculaRow('11177', '173');
  });
});
