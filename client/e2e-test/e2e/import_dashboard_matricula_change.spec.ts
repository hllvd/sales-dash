/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

/**
 * [TEAR-1] Dashboard Matricula Change Detection
 *
 * Imports a 2-row contractDashboard CSV from the Contracts page bulk-import modal.
 *
 * Record 1 (MC-MATCHANGE-E2E-1):
 *   - Pre-seeded with matricula 6111 (existing, Carlos Mendes is linked to it)
 *   - CSV sends matricula 11177 (also existing in system)
 *   - Assert: contract.matricula = 11177, contract.userId unchanged (Carlos Mendes)
 *
 * Record 2 (MC-MATCHANGE-E2E-2):
 *   - Pre-seeded with matricula 6111
 *   - CSV sends a brand-new timestamped matricula (auto-created by import)
 *   - Assert: contract.matricula = newMat, contract.userId unchanged (Carlos Mendes)
 *   - Cleanup: delete both contracts + delete the new matricula from the system
 */

// ── Constants ─────────────────────────────────────────────────────────────────
const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };
const CARLOS = 'carlosmendes@example.com';
const CONTRACT_1 = 'MC-MATCHANGE-E2E-1';
const CONTRACT_2 = 'MC-MATCHANGE-E2E-2';
const MAT_EXISTING_A = '6111';   // seeded by tear-1 import_wizard — Carlos Mendes linked
const MAT_EXISTING_B = '11177';  // seeded by tear-1 — Contract 1 will move here

// ── Helpers ───────────────────────────────────────────────────────────────────
async function login(page: Page) {
  await page.goto('/');
  await page.evaluate(() => { localStorage.clear(); sessionStorage.clear(); });
  await page.goto('/');
  await expect(page.locator('button.login-button')).toBeVisible({ timeout: 15000 });
  await page.fill('input[type="email"]', ADMIN.email);
  await page.fill('input[type="password"]', ADMIN.password);
  await page.click('button.login-button');
  await expect(page.getByRole('heading', { name: 'Meus Contratos' })).toBeVisible({ timeout: 15000 });
}

async function getToken(page: Page): Promise<string> {
  return page.evaluate(() => localStorage.getItem('token') ?? '');
}

async function getContractByNumber(page: Page, token: string, contractNumber: string) {
  const resp = await page.request.get(`/api/contracts/number/${contractNumber}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!resp.ok()) return null;
  return (await resp.json()).data ?? null;
}

async function deleteContractByNumber(page: Page, token: string, contractNumber: string) {
  const contract = await getContractByNumber(page, token, contractNumber);
  if (!contract) return;
  await page.request.delete(`/api/contracts/${contract.id}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  console.log(`>>> Deleted contract ${contractNumber} (id=${contract.id})`);
}

async function getMatriculaByNumber(page: Page, token: string, number: string) {
  const resp = await page.request.get(`/api/matriculas?number=${number}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!resp.ok()) return null;
  const data = (await resp.json()).data;
  const list = Array.isArray(data) ? data : data?.items ?? [];
  return list.find((m: any) => m.matriculaNumber === number) ?? null;
}

async function seedContract(
  page: Page,
  token: string,
  contractNumber: string,
  userId: string,
) {
  // Delete if already exists (idempotent setup)
  await deleteContractByNumber(page, token, contractNumber);

  const resp = await page.request.post('/api/contracts', {
    headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
    data: {
      contractNumber,
      totalAmount: 100000,
      status: 'Active',
      matriculaNumber: '6111',
      userId,
      contractStartDate: '2024-01-01T00:00:00Z',
    },
  });

  if (!resp.ok()) {
    const body = await resp.text();
    throw new Error(`Failed to seed contract ${contractNumber}: ${resp.status()} — ${body}`);
  }
  console.log(`>>> Seeded contract ${contractNumber}`);
}

// ── Tests ─────────────────────────────────────────────────────────────────────
test.describe.serial('[TEAR-1] Dashboard Import — Matricula Change Detection', () => {
  test.setTimeout(120_000);

  let newMatNumber: string;
  let csvPath: string;
  let carlosUserId: string;

  test.beforeAll(async ({ browser }) => {
    newMatNumber = `MAT-E2E-${Date.now()}`;
    const tmpDir = path.resolve(process.cwd(), 'temp');
    fs.mkdirSync(tmpDir, { recursive: true });

    // Build CSV: same PBI export format used by pending_claim_contracts.csv
    // Cota format: "MatA;Quota;X;Customer;ContractNumber"
    // Record 1: 6111 → 11177 (existing matricula)
    // Record 2: 6111 → newMatNumber (auto-created)
    const header = 'Obs Cota,Cota,Versao,Dt Venda,Dt Produção,Dt Cancelamento,Dt Contemplacao,Produção Analitica,Categoria,Consultor,Cód. PV,PV,Unidade Original,Unidade Atual,Crédito Venda,Tem Pagamento?,Situação Cobrança,Prazo Grupo,Plano Venda,id_bi,Matricula';
    const row1 = `,${MAT_EXISTING_A};100;X;TestRecord1;${CONTRACT_1},1,2024-01-01,100000,,,,AP,,,,,,100000,,NORMAL,,,,${MAT_EXISTING_B}`;
    const row2 = `,${MAT_EXISTING_A};200;X;TestRecord2;${CONTRACT_2},1,2024-01-01,200000,,,,AP,,,,,,200000,,NORMAL,,,,${newMatNumber}`;
    csvPath = path.join(tmpDir, 'dashboard_matricula_change.csv');
    fs.writeFileSync(csvPath, [header, row1, row2].join('\n'), 'utf-8');
    console.log(`>>> CSV written to ${csvPath}`);
    console.log(`>>> New matricula number: ${newMatNumber}`);

    // Seed pre-conditions via API
    const context = await browser.newContext();
    const page = await context.newPage();
    await login(page);
    const token = await getToken(page);

    // Get Carlos Mendes userId
    const usersResp = await page.request.get(`/api/users?email=${CARLOS}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    const usersData = (await usersResp.json()).data;
    const carlosUser = (Array.isArray(usersData) ? usersData : usersData?.items ?? [])
      .find((u: any) => u.email === CARLOS);
    if (!carlosUser) throw new Error(`Carlos Mendes not found — has tear-1 import_wizard run?`);
    carlosUserId = carlosUser.id;
    console.log(`>>> Carlos Mendes userId: ${carlosUserId}`);

    // Get matricula 6111 id
    const mat6111 = await getMatriculaByNumber(page, token, MAT_EXISTING_A);
    if (!mat6111) throw new Error(`Matricula ${MAT_EXISTING_A} not found — has tear-1 import_wizard run?`);

    await seedContract(page, token, CONTRACT_1, carlosUserId);
    await seedContract(page, token, CONTRACT_2, carlosUserId);

    await context.close();
  });

  // ── Step 1: Import CSV via UI and assert warning ──────────────────────────
  test('Step 1 – Import CSV and assert matricula-change warning in result', async ({ page }) => {
    await login(page);

    // Navigate to Contracts page
    await page.click('a[href="#/contracts"]');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10000 });
    await page.waitForTimeout(2000);

    // Open bulk import modal
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10000 });

    // Upload the CSV
    await page.setInputFiles('input#file', csvPath);
    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10000 });
    await nextBtn.click();

    // Handle optional mismatch-warning step
    const proceedBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedBtn.isVisible({ timeout: 3000 })) {
        await proceedBtn.click();
      }
    } catch { /* no mismatch warning — continue */ }

    // Mapping step
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15000 });
    await page.waitForTimeout(10000); // allow auto-mapping to settle

    // Confirm import
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 15000 });
    await confirmBtn.click();

    // Wait for result step
    await expect(page.getByText(/Importados:/)).toBeVisible({ timeout: 30000 });
    const resultText = await page.getByText(/Importados:/).textContent();
    console.log(`>>> Import result: ${resultText}`);

    // Assert: warnings-info section is visible (matricula-change warning)
    const warningsSection = page.locator('.warnings-info');
    await expect(warningsSection).toBeVisible({ timeout: 10000 });

    // Assert: warning text contains the key phrase and both contract numbers
    await expect(warningsSection).toContainText('change of matriculas');
    await expect(warningsSection).toContainText(CONTRACT_1);
    await expect(warningsSection).toContainText(CONTRACT_2);

    // Assert: new matricula mentioned in the warning
    await expect(warningsSection).toContainText(newMatNumber);

    console.log(`>>> Step 1 OK: warning visible with both contract numbers`);

    // Close the modal
    await page.click('button:has-text("Fechar")');
  });

  // ── Step 2: Assert Record 1 — existing matricula updated, user preserved ──
  test('Step 2 – Record 1: matricula updated to 11177, userId unchanged', async ({ page }) => {
    await login(page);
    const token = await getToken(page);

    const contract = await getContractByNumber(page, token, CONTRACT_1);
    expect(contract, `Contract ${CONTRACT_1} not found after import`).not.toBeNull();

    // Matricula must have changed to 11177
    const newMat = contract.matriculaNumber;
    expect(newMat, 'Record 1: matricula should be 11177').toBe(MAT_EXISTING_B);

    // UserId must NOT have changed
    expect(contract.userId, 'Record 1: userId must remain Carlos Mendes').toBe(carlosUserId);

    console.log(`>>> Step 2 OK: contract ${CONTRACT_1} — matricula=${newMat}, userId=${contract.userId}`);
  });

  // ── Step 3: Assert Record 2 — new matricula created, user preserved ───────
  test('Step 3 – Record 2: new matricula created, userId unchanged', async ({ page }) => {
    await login(page);
    const token = await getToken(page);

    const contract = await getContractByNumber(page, token, CONTRACT_2);
    expect(contract, `Contract ${CONTRACT_2} not found after import`).not.toBeNull();

    // Matricula must have changed to newMatNumber
    const mat = contract.matriculaNumber;
    expect(mat, `Record 2: matricula should be ${newMatNumber}`).toBe(newMatNumber);

    // UserId must NOT have changed
    expect(contract.userId, 'Record 2: userId must remain Carlos Mendes').toBe(carlosUserId);

    // New matricula must exist in the system
    const newMat = await getMatriculaByNumber(page, token, newMatNumber);
    expect(newMat, `Matricula ${newMatNumber} should have been auto-created by import`).not.toBeNull();

    console.log(`>>> Step 3 OK: contract ${CONTRACT_2} — matricula=${mat}, userId=${contract.userId}`);
  });

  // ── Cleanup ───────────────────────────────────────────────────────────────
  test('Cleanup – Delete test contracts and new matricula', async ({ page }) => {
    await login(page);
    const token = await getToken(page);

    // 1. Delete both test contracts
    await deleteContractByNumber(page, token, CONTRACT_1);
    await deleteContractByNumber(page, token, CONTRACT_2);

    // 2. Delete the auto-created matricula (system matriculas 6111 and 11177 are left intact)
    const newMat = await getMatriculaByNumber(page, token, newMatNumber);
    if (newMat) {
      const delResp = await page.request.delete(`/api/matriculas/${newMat.id}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      console.log(`>>> Deleted matricula ${newMatNumber} (id=${newMat.id}): ${delResp.ok()}`);
    } else {
      console.log(`>>> Matricula ${newMatNumber} already gone — skipping`);
    }

    // 3. Clean up temp CSV
    try { fs.unlinkSync(csvPath); } catch { /* ignore */ }

    console.log(`>>> Cleanup complete`);
  });
});
