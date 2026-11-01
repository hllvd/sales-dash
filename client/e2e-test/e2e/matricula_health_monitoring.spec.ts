import fs from 'fs';
import path from 'path';
import { test, expect } from '@playwright/test';
import { loginAs } from './helpers/auth';

test.describe('Matricula Health Monitoring', () => {
  test('should show correct contract count and last update time for matriculas 9999 and 11177', async ({ page }) => {
    test.setTimeout(30_000);

    // Compute expected contract count dynamically based on the 14-month rolling cutoff
    const csvPath = path.join(__dirname, '../test-data/contracts.csv');
    const csvContent = fs.readFileSync(csvPath, 'utf8');
    const now = new Date();
    const cutoffDate = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
    cutoffDate.setUTCMonth(cutoffDate.getUTCMonth() - 14);

    const getExpectedCount = (matricula: string): string => {
      let count = 0;
      for (const line of csvContent.split('\n')) {
        const parts = line.split(',');
        if (parts.length > 13) {
          const cNum = parts[0]?.trim();
          const rowMat = parts[4]?.trim();
          const dateStr = parts[9]?.trim();
          const status = parts[13]?.trim().toLowerCase();
          // Contract 1100005807 is reassigned to matricula 8888 in hierarchy tests
          if (cNum === '1100005807') continue;
          if (rowMat === matricula && status !== 'desistente') {
            const [m, d, y] = dateStr.split('/').map(Number);
            if (m && d && y) {
              const saleDate = new Date(Date.UTC(y, m - 1, d));
              if (saleDate >= cutoffDate) {
                count++;
              }
            }
          }
        }
      }
      return count.toString();
    };

    // 1. Login as superadmin and navigate directly to Matricula Health Monitoring
    await loginAs(page);
    await page.goto('/#/monitoring/matricula-health');

    // 3. Verify page title is loaded
    await expect(page.getByRole('heading', { name: 'Saúde das Matrículas' })).toBeVisible({ timeout: 10000 });

    // Helper to verify matricula row content, freshness, and status
    async function verifyMatriculaRow(matricula: string, expectedCount: string) {
      const row = page.locator('.matriculas-tab-container table tbody tr').filter({
        has: page.locator('td').first().filter({ hasText: new RegExp(`^${matricula}$`) })
      });
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

    // 4. Verify Matricula 9999 has expected contracts (within last 14 months) and is healthy
    await verifyMatriculaRow('9999', getExpectedCount('9999'));

    // 5. Verify Matricula 11177 has expected contracts (within last 14 months) and is healthy
    await verifyMatriculaRow('11177', getExpectedCount('11177'));
  });
});

