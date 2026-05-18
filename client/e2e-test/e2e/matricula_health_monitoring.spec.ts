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

      // Parse the date pattern DD/MM/YYYY HH:mm
      const match = cellText.match(/(\d{2})\/(\d{2})\/(\d{4})\s+(\d{2}):(\d{2})/);
      if (match) {
        const [_, day, month, year, hour, minute] = match;
        
        // Parse last update date in local timezone since it's displayed in local timezone
        const lastUpdateDate = new Date(
          parseInt(year),
          parseInt(month) - 1,
          parseInt(day),
          parseInt(hour),
          parseInt(minute)
        );
        
        const now = new Date();
        const diffMs = Math.abs(now.getTime() - lastUpdateDate.getTime());
        const diffMinutes = diffMs / 1000 / 60;
        
        console.log(`[Matricula ${matricula}] Parsed: ${lastUpdateDate.toLocaleString()}, Now: ${now.toLocaleString()}, Diff: ${diffMinutes.toFixed(2)} min`);
        
        // Assert that the last update was within 5 minutes
        expect(diffMinutes).toBeLessThan(5);
      } else {
        throw new Error(`Could not parse update date from cell text: "${cellText}"`);
      }

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
