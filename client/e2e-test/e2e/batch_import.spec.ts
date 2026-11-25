/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { loginAs } from './helpers/auth';

const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };

async function login(page: Page) {
  await loginAs(page, ADMIN.email, ADMIN.password);
}

test.describe('Batch Import — Serial Import of Multiple Files', () => {
  test.describe.configure({ mode: 'serial' });

  const tempDir = path.join(__dirname, '../temp');

  test.beforeAll(() => {
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }
  });

  test('Superadmin uploads multiple contract files and imports them in series with progress and summary', async ({ page }) => {
    test.setTimeout(120_000);
    await login(page);

    // 1. Navigate to batch import page
    await page.goto('/#/batch-import');
    await expect(page.getByRole('heading', { name: 'Importação de Arquivos em Lote' })).toBeVisible({ timeout: 15_000 });

    const timestamp = Date.now();
    const rawNumber1 = `${timestamp % 100000000}`;
    const contractNum2 = `BATCH-CTR-2-${timestamp}`;

    // File 1: Contract Dashboard format
    const header1 = 'Contrato,Código PV,Nome PV,Matrícula,Consultor,Grupo,Cota,Data da Venda,Valor,Nome do Cliente,Tipo,Status';
    const row1 = `${rawNumber1},PV100,Loja Sul,6111,Arthur Terplak,G1,101,2025-01-01,100000.00,Cliente Batch 1,Normal,Ativo`;
    const file1Path = path.join(tempDir, `batch_file_1_${timestamp}.csv`);
    fs.writeFileSync(file1Path, `${header1}\n${row1}\n`, 'utf-8');

    // File 2: Consultor Dashboard format
    const header2 = 'Identificador.Cota,Cod.Ponto de Venda,Ponto de Venda,Cod.Comissionado,Comissionado,Fim.Vigência,Data.Venda,Data.Cancelamento.Cota,Grupo,Cota,Versão,Nº Contrato,Definição.Situação,Num.Primeira Assembleia,Data Primeira Assembleia,CPF / CNPJ Cons.,Consorciado,Contemplação,Data Contemplação,Bem,Descrição,Valor Saldo Devedor,% Saldo Devedor,% FC Pago,R$ Bem Venda,R$ Bem Atual,Quant.Parcelas Pagas,Quant.Parcelas A Pagar,Num Proxima Assembleia,Data Proximo Vencimento,Valor da Parcela,Valor FC Mês,Valor FR Mês,Valor TA Mês,Valor do Crédito Contemplado,Valor do Crédito Corrigido,Valor do Bem Entregue,Valor do Bem a Pagar,Prazo da Cota,Últ.Pagto Parcela,Quant.Parcelas em Atraso,Valor Parcelas em Atraso,e-mail,ddd,Telefone,Endereço,NO_Endereco,Bairro,Complemento,CEP,Cidade,Estado,Estado.Civil,Idade.Cliente,Dt.Nascimento,Filtro de Cotas';
    const row2 = `ID-BATCH-2,PV100,Loja Sul,6111,Consultor Teste,,2026-01-10,,5001,102,1,${contractNum2},Normal,1,2026-02-15,12345678901,Cliente Batch 2,Nao,,Automovel,Carro,0,0,0,150000.00,150000.00,2,58,2,2026-03-10,2500.00,0,0,0,0,0,0,0,60,2026-02-10,0,0,batch2@test.com,47,999999999,Rua Batch,200,Centro,,89000000,Joinville,SC,Solteiro,30,1996-01-01,1`;
    const file2Path = path.join(tempDir, `batch_file_2_${timestamp}.csv`);
    fs.writeFileSync(file2Path, `${header2}\n${row2}\n`, 'utf-8');

    // 2. Select both files
    const fileInput = page.locator('input[data-testid="batch-file-input"]');
    await fileInput.setInputFiles([file1Path, file2Path]);

    // 3. Verify queue displays both files
    await expect(page.getByText('Fila de Arquivos (2)')).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText(path.basename(file1Path))).toBeVisible();
    await expect(page.getByText(path.basename(file2Path))).toBeVisible();

    // 4. Verify default options are checked
    const skipMissing = page.locator('[data-testid="batch-opt-skip-missing"]');
    await expect(skipMissing).toBeChecked();

    const autoGroups = page.locator('[data-testid="batch-opt-auto-groups"]');
    await expect(autoGroups).toBeChecked();

    const autoPVs = page.locator('[data-testid="batch-opt-auto-pvs"]');
    await expect(autoPVs).toBeChecked();

    // 5. Start batch import
    const startBtn = page.locator('[data-testid="batch-start-btn"]');
    await expect(startBtn).toBeEnabled();
    await startBtn.click();

    // 6. Wait for completion and verify summary
    const summaryHeader = page.locator('[data-testid="batch-results-summary"]');
    await expect(summaryHeader).toBeVisible({ timeout: 60_000 });

    await expect(page.getByText('Resumo da Importação em Lote')).toBeVisible();
    await expect(page.locator('[data-testid="metric-total-files"]')).toHaveText('2');
    await expect(page.locator('[data-testid="metric-success-files"]')).toHaveText('2');

    // 7. Verify per-file results in accordion
    await expect(page.locator('[data-testid="batch-result-item-0"]')).toBeVisible();
    await expect(page.locator('[data-testid="batch-result-item-1"]')).toBeVisible();
  });
});
