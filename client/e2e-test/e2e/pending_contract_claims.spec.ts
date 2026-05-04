/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import path from 'path';

// ── Constants ─────────────────────────────────────────────────────────────────
const admin = { email: 'superadmin@salesapp.com', password: 'string' };

// Both users are linked to mat 10134 (non-owners) — seeded by tear-1 import_wizard
const user1 = { email: 'lucaspereira@example.com', password: 'ChangeMe123!', name: 'Lucas Pereira' };
const user2 = { email: 'carlafranciele@example.com', password: 'ChangeMe123!', name: 'Carla Franciele' };

// Owner of mat 10134 — seeded by tear-1 import_wizard (Owner_Matricula=1 in users-demo.csv)
const owner = { email: 'mariaeduarda@example.com', password: 'ChangeMe123!', name: 'Maria Eduarda' };

const MAT = '10134';
const CLM1 = '999901';
const CLM2 = '999902';
const CLM3 = '999903';

// ── Helpers ───────────────────────────────────────────────────────────────────
async function login(page: Page, email: string, password: string) {
  await page.goto('/');
  await page.evaluate(() => localStorage.clear());
  await page.goto('/');
  await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15000 });
  await page.fill('input[type="email"]', email);
  await page.fill('input[type="password"]', password);
  await page.click('button.login-button');
  await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15000 });
}

/**
 * Opens the assign modal, searches for contractNumber.
 * If alreadyClaimed=true → asserts error message and closes without registering.
 * Otherwise → selects matricula from dropdown and clicks "Registrar Interesse".
 */
async function claimContract(page: Page, contractNumber: string, mat: string, alreadyClaimed = false) {
  await expect(page.locator('.my-contracts-page')).toBeVisible({ timeout: 10000 });
  await page.click('button:has-text("Novo")');
  await expect(page.getByRole('dialog')).toBeVisible({ timeout: 10000 });

  await page.fill('input[placeholder="Digite o número do contrato"]', contractNumber);
  
  // Wait for the GET /contracts/number/{num} response
  const [searchResponse] = await Promise.all([
    page.waitForResponse(resp => resp.url().includes(`/contracts/number/${contractNumber}`) && resp.request().method() === 'GET', { timeout: 15000 }),
    page.click('button:has-text("Buscar Contrato")'),
  ]);

  if (searchResponse.status() === 403) {
    throw new Error(`Search failed with 403 Forbidden for user. Check permissions.`);
  }

  if (alreadyClaimed) {
    await expect(page.getByRole('dialog').getByText(/já foi solicitado por/)).toBeVisible({ timeout: 10000 });
    await expect(page.getByRole('dialog').locator('button:has-text("Registrar Interesse")')).toBeDisabled();
    // Click the X button to actually close the modal instead of "Voltar" which just goes back to the search screen
    await page.getByRole('button', { name: 'Fechar' }).click();
    await page.getByRole('dialog').waitFor({ state: 'hidden', timeout: 10000 });
    return;
  }

  // Blue "not yet imported" alert
  await expect(page.getByRole('dialog').locator('.mantine-Alert-root')).toBeVisible({ timeout: 10000 });

  // Select matricula from dropdown if visible
  const matSelect = page.getByRole('dialog').locator('.mantine-Select-input');
  if (await matSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
    await matSelect.click();
    await page.click(`[role="option"]:has-text("${mat}")`);
  }

  // Wait for the POST /contracts/claims response before checking modal state
  const [response] = await Promise.all([
    page.waitForResponse(resp => resp.url().includes('/contracts/claims') && resp.request().method() === 'POST', { timeout: 15000 }),
    page.getByRole('dialog').locator('button:has-text("Registrar Interesse")').click(),
  ]);

  if (!response.ok()) {
    const body = await response.json().catch(() => ({}));
    throw new Error(`POST /contracts/claims failed (${response.status()}): ${body.message ?? JSON.stringify(body)}`);
  }

  // Wait for modal to fully detach from the DOM
  await page.getByRole('dialog').waitFor({ state: 'hidden', timeout: 10000 });
}

// ── Tests ─────────────────────────────────────────────────────────────────────
test.describe.serial('Pending Contract Claims — Full Lifecycle (tear-5)', () => {
  test.setTimeout(150_000);

  test.beforeAll(async ({ browser }) => {
    // Basic cleanup before starting to ensure contracts don't exist
    const context = await browser.newContext();
    const page = await context.newPage();
    await login(page, admin.email, admin.password);
    const token = await page.evaluate(() => localStorage.getItem('token'));
    
    const testContractNumbers = [CLM1, CLM2, CLM3];
    for (const num of testContractNumbers) {
      try {
        // 1. Delete any existing contracts with these numbers
        const contractResp = await page.request.get(`/api/contracts/number/${num}`, {
          headers: { 'Authorization': `Bearer ${token}` }
        });
        if (contractResp.ok()) {
          const result = await contractResp.json();
          await page.request.delete(`/api/contracts/${result.data.id}`, {
            headers: { 'Authorization': `Bearer ${token}` }
          });
          console.log(`>>> Pre-test Cleanup: Deleted contract ${num}`);
        }
        
        // 2. Delete ALL claims for this number (resolved or not) using the new endpoint
        const delClaimResp = await page.request.delete(`/api/contracts/claims/number/${num}`, {
          headers: { 'Authorization': `Bearer ${token}` }
        });
        if (!delClaimResp.ok()) {
          console.error(`>>> Pre-test Cleanup Warning: Failed to clear claims for ${num}. Status: ${delClaimResp.status()}`);
        } else {
          console.log(`>>> Pre-test Cleanup: Cleared all claims for ${num}`);
        }
      } catch (e) {
        console.error(`>>> Pre-test Cleanup Error: Failed for ${num}`, e);
      }
    }
    await context.close();
  });

  test.afterEach(async ({ page }, testInfo) => {
    if (testInfo.status !== testInfo.expectedStatus) {
      // Get step number from title to name the screenshot
      const stepMatch = testInfo.title.match(/Step (\d+)/);
      const stepName = stepMatch ? `step${stepMatch[1]}` : 'error';
      const screenshotPath = `test-results/screenshots/failure-${stepName}.png`;
      await page.screenshot({ path: screenshotPath, fullPage: true });
      console.log(`>>> Test Failed at "${testInfo.title}". Screenshot saved to: ${screenshotPath}`);
    }
  });

  // Step 1: sanity-check the mat/user data seeded by tear-1
  test('Step 1 – Admin verifies user1, user2 and owner are all linked to mat 10134', async ({ page }) => {
    await login(page, admin.email, admin.password);
    await page.click('a[href="#/matriculas"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Matrículas' })).toBeVisible();

    await page.fill('input[placeholder="Buscar por número de matrícula ou usuário..."]', MAT);
    await page.waitForTimeout(2000);

    const ownerRow = page.locator('tr').filter({ hasText: MAT }).filter({ hasText: owner.name });
    await expect(ownerRow).toBeVisible({ timeout: 10000 });
    await expect(ownerRow).toContainText('Proprietário');

    await expect(page.locator('tr').filter({ hasText: MAT }).filter({ hasText: user1.name })).toBeVisible({ timeout: 10000 });
    await expect(page.locator('tr').filter({ hasText: MAT }).filter({ hasText: user2.name })).toBeVisible({ timeout: 10000 });

    console.log('>>> Step 1 OK: all three users linked to mat', MAT);
  });

  // Step 2: User1 claims CLM1 and CLM2
  test('Step 2 – User1 claims contract1 and contract2', async ({ page }) => {
    await login(page, user1.email, user1.password);

    await claimContract(page, CLM1, MAT);
    await claimContract(page, CLM2, MAT);

    // Both contracts appear in the "Contratos Solicitados" pending table
    const pendingSection = page.locator('text=Contratos Solicitados').locator('../..');
    await expect(pendingSection).toBeVisible({ timeout: 10000 });
    await expect(page.locator('table').filter({ hasText: CLM1 })).toBeVisible({ timeout: 10000 });
    await expect(page.locator('table').filter({ hasText: CLM2 })).toBeVisible({ timeout: 10000 });

    console.log('>>> Step 2 OK: user1 claimed', CLM1, CLM2);
  });

  // Step 3: User2 gets duplicate error on CLM2, then claims CLM3
  test('Step 3 – User2 gets error on contract2 (already claimed), claims contract3', async ({ page }) => {
    await login(page, user2.email, user2.password);

    // CLM2 is already claimed by user1 — expect conflict message
    await claimContract(page, CLM2, MAT, true /* alreadyClaimed */);

    // CLM3 is free — should succeed
    await claimContract(page, CLM3, MAT);

    // Pending table shows CLM3 only
    await expect(page.locator('table').filter({ hasText: CLM3 })).toBeVisible({ timeout: 10000 });
    // CLM1 must NOT appear in user2's table
    await expect(page.locator('table').filter({ hasText: CLM1 })).not.toBeVisible();

    console.log('>>> Step 3 OK: user2 got conflict on', CLM2, 'and claimed', CLM3);
  });

  // Step 4: MatriculaOwner sees all 3 contracts in the owner banner
  test('Step 4 – MatriculaOwner sees all 3 contracts in owner banner', async ({ page }) => {
    await login(page, owner.email, owner.password);
    await page.waitForTimeout(3000); // allow pending claims API to resolve

    // "Atenção Proprietário" alert must be visible
    const alert = page.locator('.mantine-Alert-root').filter({ hasText: 'Proprietário' });
    await expect(alert).toBeVisible({ timeout: 15000 });

    // All 3 contract numbers must appear in the owner table
    const ownerTable = page.locator('table').filter({ hasText: user1.name });
    await expect(ownerTable.getByText(CLM1)).toBeVisible({ timeout: 10000 });
    await expect(ownerTable.getByText(CLM2)).toBeVisible({ timeout: 10000 });

    const ownerTable2 = page.locator('table').filter({ hasText: user2.name });
    await expect(ownerTable2.getByText(CLM3)).toBeVisible({ timeout: 10000 });

    console.log('>>> Step 4 OK: owner sees all 3 pending contracts');
  });

  // Step 5: Admin imports the 3 contracts via bulk import
  test('Step 5 – Admin imports pending_claim_contracts.csv', async ({ page }) => {
    const csvPath = path.resolve(process.cwd(), 'test-data', 'pending_claim_contracts.csv');

    await login(page, admin.email, admin.password);
    await page.click('a[href="#/contracts"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(2000);

    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible();

    await page.setInputFiles('input#file', csvPath);
    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10000 });
    await nextBtn.click();

    // Skip mismatch warning if it appears
    const proceedBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedBtn.isVisible({ timeout: 3000 })) await proceedBtn.click();
    } catch { /* no warning */ }

    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15000 });
    await page.waitForTimeout(10000); // wait for auto-mapping

    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 15000 });
    await confirmBtn.click();

    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30000 });
    const result = await page.getByText(/Importados:/).textContent();
    console.log('>>> Import result:', result);

    await page.click('button:has-text("Fechar")');
    console.log('>>> Step 5 OK: contracts imported');
  });

  // Step 6: User1 sees CLM1 + CLM2 auto-assigned; pending table is gone
  test('Step 6 – User1 sees contract1 and contract2 assigned; pending table empty', async ({ page }) => {
    await login(page, user1.email, user1.password);
    await page.waitForTimeout(3000);

    await expect(page.locator('table tbody tr').filter({ hasText: CLM1 })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('table tbody tr').filter({ hasText: CLM2 })).toBeVisible({ timeout: 15000 });

    // Pending table section should no longer exist
    await expect(page.locator('text=Contratos Solicitados')).not.toBeVisible();

    console.log('>>> Step 6 OK: user1 has', CLM1, CLM2, 'assigned; no pending');
  });

  // Step 7: User2 sees CLM3 assigned; pending table is gone
  test('Step 7 – User2 sees contract3 assigned; pending table empty', async ({ page }) => {
    await login(page, user2.email, user2.password);
    await page.waitForTimeout(3000);

    await expect(page.locator('table tbody tr').filter({ hasText: CLM3 })).toBeVisible({ timeout: 15000 });
    await expect(page.locator('text=Contratos Solicitados')).not.toBeVisible();

    console.log('>>> Step 7 OK: user2 has', CLM3, 'assigned; no pending');
  });

  // Step 8: MatriculaOwner banner is gone
  test('Step 8 – MatriculaOwner sees no owner banner', async ({ page }) => {
    await login(page, owner.email, owner.password);
    await page.waitForTimeout(3000);

    await expect(page.locator('.mantine-Alert-root').filter({ hasText: 'Proprietário' })).not.toBeVisible();

    console.log('>>> Step 8 OK: owner banner gone');
  });

  // ── Cleanup ──────────────────────────────────────────────────────────
  test('Cleanup created contracts and claims', async ({ page }) => {
    // Login as admin to have delete permissions
    await login(page, admin.email, admin.password);

    // Get the token for API calls
    const token = await page.evaluate(() => localStorage.getItem('token'));

    const testContractNumbers = [CLM1, CLM2, CLM3];

    for (const num of testContractNumbers) {
      console.log(`>>> Cleaning up contract/claims for: ${num}`);

      // 1. Find and delete the contract
      try {
        const contractResp = await page.request.get(`/api/contracts/number/${num}`, {
          headers: { 'Authorization': `Bearer ${token}` }
        });

        if (contractResp.ok()) {
          const result = await contractResp.json();
          const contractId = result.data.id;
          const delResp = await page.request.delete(`/api/contracts/${contractId}`, {
            headers: { 'Authorization': `Bearer ${token}` }
          });
          console.log(`    - Contract ${num} (ID: ${contractId}) deleted: ${delResp.ok()}`);
        } else {
          console.log(`    - Contract ${num} not found, skipping delete.`);
        }
      } catch (e) {
        console.error(`    - Error deleting contract ${num}:`, e);
      }

      // 2. Delete ALL claims for this number (resolved or not)
      try {
        const delClaimResp = await page.request.delete(`/api/contracts/claims/number/${num}`, {
          headers: { 'Authorization': `Bearer ${token}` }
        });
        console.log(`    - All claims for ${num} cleared: ${delClaimResp.ok()}`);
      } catch (e) {
        console.error(`    - Error cleaning up claims for ${num}:`, e);
      }
    }
  });
  });
