/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { loginAs } from './helpers/auth';

const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };

async function login(page: Page) {
  await loginAs(page, ADMIN.email, ADMIN.password);
}

test.describe('Contract Retention — Unpaid Contracts as Awaiting Payment Toggle', () => {
  test.describe.configure({ mode: 'serial' });

  const tempDir = path.join(__dirname, '../temp');
  const timestamp = Date.now();
  const testMatricula = `89${timestamp.toString().slice(-4)}`;
  const contractPaid1 = `CNT-RET-P1-${timestamp}`;
  const contractPaid2 = `CNT-RET-P2-${timestamp}`;
  const contractUnpaid1 = `CNT-RET-U1-${timestamp}`;
  const contractUnpaid2 = `CNT-RET-U2-${timestamp}`;
  const tempCsvPath = path.join(tempDir, `test_retention_unpaid_${timestamp}.csv`);

  test.beforeAll(() => {
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }

    const header = 'Obs Cota,Cota,Versao,Dt Venda,Dt Produção,Dt Cancelamento,Dt Contemplacao,Produção Analitica,Categoria,Consultor,Cód. PV,PV,Unidade Original,Unidade Atual,Crédito Venda,Tem Pagamento?,Situação Cobrança,Prazo Grupo,Plano Venda,id_bi,Matricula';

    const formatRow = (contractNum: string, quota: number, custName: string, amount: number, hasPayment: 'Sim' | 'Não') => [
      '', // Obs Cota
      `${testMatricula};${quota};0;${custName};${contractNum}`, // Cota
      '0', // Versao
      '2026-09-10', // Dt Venda
      '2026-09-10', // Dt Produção
      '', // Dt Cancelamento
      '', // Dt Contemplacao
      `${amount}`, // Produção Analitica
      'AP', // Categoria
      'Consultor Retencao', // Consultor
      '', // Cód. PV
      '', // PV
      '', // Unidade Original
      '', // Unidade Atual
      `${amount}`, // Crédito Venda
      hasPayment, // Tem Pagamento?
      'NORMAL', // Situação Cobrança
      '', // Prazo Grupo
      '', // Plano Venda
      '', // id_bi
      testMatricula // Matricula
    ].join(',');

    const row1 = formatRow(contractPaid1, 101, 'Cliente Paid Um', 100000, 'Sim');
    const row2 = formatRow(contractPaid2, 102, 'Cliente Paid Dois', 200000, 'Sim');
    const row3 = formatRow(contractUnpaid1, 103, 'Cliente Unpaid Um', 150000, 'Não');
    const row4 = formatRow(contractUnpaid2, 104, 'Cliente Unpaid Dois', 50000, 'Não');

    fs.writeFileSync(tempCsvPath, [header, row1, row2, row3, row4].join('\n') + '\n', 'utf-8');
  });

  test.afterAll(async () => {
    if (fs.existsSync(tempCsvPath)) {
      fs.unlinkSync(tempCsvPath);
    }
  });

  test('should toggle unpaid contracts as awaiting payment and recalculate retention metrics', async ({ page }) => {
    test.setTimeout(90_000);
    await login(page);

    // 1. Navigate to Contracts Page
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10_000 });

    // 2. Import contracts via contractDashboard template
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });
    await page.setInputFiles('input#file', tempCsvPath);

    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    const proceedAnywayBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 3000 })) {
        await proceedAnywayBtn.click();
      }
    } catch { /* ignore if not present */ }

    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });

    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 25_000 });
    await confirmBtn.click();

    await expect(page.getByText(/Importados: 4/)).toBeVisible({ timeout: 30_000 });
    await page.click('button:has-text("Fechar")');

    // 3. Filter by our unique Matrícula
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }

    const matriculaInput = page.locator('input[placeholder="Filtrar por matrícula..."]').first();
    await expect(matriculaInput).toBeVisible({ timeout: 10_000 });
    await matriculaInput.click();
    await matriculaInput.fill(testMatricula);
    await page.keyboard.press('Enter');

    // Wait for the table to load filtered contracts
    const firstRow = page.locator('table tbody tr', { hasText: contractPaid1 });
    await expect(firstRow).toBeVisible({ timeout: 15_000 });

    // 4. Assert initial state (switch is OFF):
    // Total Geral = 100k + 200k + 150k + 50k = R$ 500.000,00
    // Total Ativo = R$ 500.000,00
    const aggSummary = page.locator('.aggregation-summary');
    await expect(aggSummary).toBeVisible({ timeout: 10_000 });
    await expect(aggSummary.locator('.aggregation-item', { hasText: 'Total Geral:' })).toContainText(/500\.000,00/);
    await expect(aggSummary.locator('.aggregation-item', { hasText: 'Total Ativo:' })).toContainText(/500\.000,00/);

    // Verify all 4 contracts show status "Ativo"
    for (const cNum of [contractPaid1, contractPaid2, contractUnpaid1, contractUnpaid2]) {
      const row = page.locator('table tbody tr', { hasText: cNum });
      await expect(row).toBeVisible();
      await expect(row).toContainText('Ativo');
    }

    // 5. Open Configurações Modal and enable the switch
    const configBtn = page.getByRole('button', { name: /Colunas|Configurações/ });
    await expect(configBtn).toBeVisible({ timeout: 10_000 });
    await configBtn.click();

    const modal = page.locator('.mantine-Modal-content');
    await expect(modal).toBeVisible({ timeout: 10_000 });
    await expect(modal.getByText('Retenção')).toBeVisible();

    const retentionSwitch = modal.getByRole('switch');
    await expect(retentionSwitch).not.toBeChecked();

    // Toggle switch ON by clicking the label text
    const prefUpdatePromise = page.waitForResponse(
      r => r.url().includes('/api/users/me/preferences') && r.request().method() === 'PUT' && r.status() === 200
    );
    await modal.getByText("Mostrar contratos como não pago, como 'Aguardando pagamento'").click();
    await prefUpdatePromise;

    await expect(retentionSwitch).toBeChecked();
    await expect(page.getByText('Preferência de retenção atualizada')).toBeVisible({ timeout: 5000 });
    await modal.getByRole('button', { name: 'Concluir' }).click();
    await expect(modal).not.toBeVisible();

    // 6. Assert updated state (switch is ON):
    // Unpaid contracts are remapped to AwaitingPayment, excluded from Total Geral and Total Ativo
    // Total Geral = 100k + 200k = R$ 300.000,00
    // Total Ativo = R$ 300.000,00
    await expect(aggSummary.locator('.aggregation-item', { hasText: 'Total Geral:' })).toContainText(/300\.000,00/);
    await expect(aggSummary.locator('.aggregation-item', { hasText: 'Total Ativo:' })).toContainText(/300\.000,00/);

    // Verify Paid contracts show "Ativo"
    for (const cNum of [contractPaid1, contractPaid2]) {
      const row = page.locator('table tbody tr', { hasText: cNum });
      await expect(row).toBeVisible();
      await expect(row).toContainText('Ativo');
    }

    // Verify Unpaid contracts show "Aguardando Pagamento"
    for (const cNum of [contractUnpaid1, contractUnpaid2]) {
      const row = page.locator('table tbody tr', { hasText: cNum });
      await expect(row).toBeVisible();
      await expect(row).toContainText('Aguardando Pagamento');
    }

    // 7. Hover over an unpaid contract badge and verify tooltip
    const unpaidRow = page.locator('table tbody tr', { hasText: contractUnpaid1 });
    const badge = unpaidRow.locator('.mantine-Badge-root', { hasText: 'Aguardando Pagamento' });
    await badge.hover();
    await expect(page.getByText('Esse contrato tem status NORMAL(ativo) mas ainda não foi confirmado o pagamento')).toBeVisible({ timeout: 5000 });

    // 8. Revert / Cleanup preference back to OFF
    await configBtn.click();
    await expect(modal).toBeVisible();

    const resetPromise = page.waitForResponse(
      r => r.url().includes('/api/users/me/preferences') && r.request().method() === 'PUT' && r.status() === 200
    );
    await modal.getByText("Mostrar contratos como não pago, como 'Aguardando pagamento'").click();
    await resetPromise;

    await expect(retentionSwitch).not.toBeChecked();
    await modal.getByRole('button', { name: 'Concluir' }).click();
    await expect(modal).not.toBeVisible();

    // Verify metrics reverted back to R$ 500.000,00
    await expect(aggSummary.locator('.aggregation-item', { hasText: 'Total Geral:' })).toContainText(/500\.000,00/);
    await expect(aggSummary.locator('.aggregation-item', { hasText: 'Total Ativo:' })).toContainText(/500\.000,00/);
  });
});
