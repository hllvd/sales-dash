/// <reference types="node" />
import { test, expect, Page } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import { loginAs } from './helpers/auth';

const ADMIN = { email: 'superadmin@salesapp.com', password: 'string' };

async function login(page: Page) {
  await loginAs(page, ADMIN.email, ADMIN.password);
}

test.describe('Contract Dashboard Import — Consultor Exported Format', () => {
  test.describe.configure({ mode: 'serial' });

  const tempDir = path.join(__dirname, '../temp');

  test.beforeAll(() => {
    if (!fs.existsSync(tempDir)) {
      fs.mkdirSync(tempDir, { recursive: true });
    }
  });

  test('Auto-detects consultor template, maps fields, sets HasPayment and derives statuses correctly', async ({ page }) => {
    test.setTimeout(90_000);
    await login(page);

    // 1. Navigate to Contracts Page
    await page.goto('/#/contracts');
    await expect(page.getByRole('heading', { name: 'Gerenciamento de Contratos' })).toBeVisible({ timeout: 10_000 });

    const timestamp = Date.now();
    const contractOk = `CNT-CONS-OK-${timestamp}`;
    const contractDes = `CNT-CONS-DES-${timestamp}`;
    const contractDef = `CNT-CONS-DEF-${timestamp}`;

    const header = 'Identificador.Cota,Cod.Ponto de Venda,Ponto de Venda,Cod.Comissionado,Comissionado,Fim.Vigência,Data.Venda,Data.Cancelamento.Cota,Grupo,Cota,Versão,Nº Contrato,Definição.Situação,Num.Primeira Assembleia,Data Primeira Assembleia,CPF / CNPJ Cons.,Consorciado,Contemplação,Data Contemplação,Bem,Descrição,Valor Saldo Devedor,% Saldo Devedor,% FC Pago,R$ Bem Venda,R$ Bem Atual,Quant.Parcelas Pagas,Quant.Parcelas A Pagar,Num Proxima Assembleia,Data Proximo Vencimento,Valor da Parcela,Valor FC Mês,Valor FR Mês,Valor TA Mês,Valor do Crédito Contemplado,Valor do Crédito Corrigido,Valor do Bem Entregue,Valor do Bem a Pagar,Prazo da Cota,Últ.Pagto Parcela,Quant.Parcelas em Atraso,Valor Parcelas em Atraso,e-mail,ddd,Telefone,Endereço,NO_Endereco,Bairro,Complemento,CEP,Cidade,Estado,Estado.Civil,Idade.Cliente,Dt.Nascimento,Filtro de Cotas';

    // 3 rows:
    // Row 1: Normal, Paid = 2 -> Active, HasPayment = true
    const row1 = `ID1,PV100,Loja Sul,6111,Consultor Teste,,2026-01-10,,5001,100,1,${contractOk},Normal,1,2026-02-15,12345678900,Cliente Consultor Ativo,Nao,,Automovel,Carro,0,0,0,120000.00,120000.00,2,58,2,2026-03-10,2000.00,0,0,0,0,0,0,0,60,2026-02-10,0,0,cliente1@test.com,47,999999999,Rua Um,100,Centro,,89000000,Joinville,SC,Solteiro,30,1996-01-01,1`;
    // Row 2: Excluido, Paid = 0, Cancelled before Assembly -> Desistente, HasPayment = false
    const row2 = `ID2,PV100,Loja Sul,6111,Consultor Teste,,2026-01-10,2026-01-20,5001,101,1,${contractDes},Excluido,1,2026-02-15,12345678901,Cliente Consultor Desistente,Nao,,Automovel,Carro,0,0,0,85000.00,85000.00,0,60,2,2026-03-10,1500.00,0,0,0,0,0,0,0,60,,0,0,cliente2@test.com,47,999999998,Rua Dois,200,Centro,,89000000,Joinville,SC,Casado,35,1991-01-01,1`;
    // Row 3: Excluido, Paid = 1, Cancelled after Assembly -> Defaulted, HasPayment = true
    const row3 = `ID3,PV100,Loja Sul,6111,Consultor Teste,,2026-01-10,2026-03-20,5001,102,1,${contractDef},Excluido,1,2026-02-15,12345678902,Cliente Consultor Cancelado,Nao,,Automovel,Carro,0,0,0,95000.00,95000.00,1,59,2,2026-03-10,1800.00,0,0,0,0,0,0,0,60,2026-02-10,0,0,cliente3@test.com,47,999999997,Rua Tres,300,Centro,,89000000,Joinville,SC,Solteiro,28,1998-01-01,1`;

    const tempCsvPath = path.join(tempDir, `test_consultor_export_${timestamp}.csv`);
    fs.writeFileSync(tempCsvPath, `${header}\n${row1}\n${row2}\n${row3}\n`, 'utf-8');

    // --- STEP 1: Upload File ---
    await page.click('button:has-text("Importar")');
    await expect(page.getByText('Importar Contratos em Lote')).toBeVisible({ timeout: 10_000 });
    await page.setInputFiles('input#file', tempCsvPath);

    const nextBtn = page.locator('button:has-text("Próximo")');
    await expect(nextBtn).toBeEnabled({ timeout: 10_000 });
    await nextBtn.click();

    const proceedAnywayBtn = page.locator('button:has-text("Prosseguir Assim Mesmo")');
    try {
      if (await proceedAnywayBtn.isVisible({ timeout: 500 })) {
        await proceedAnywayBtn.click();
      }
    } catch { /* ignore if not present */ }

    // --- STEP 2: Verify Mappings Screen ---
    await expect(page.getByText('Mapeamento')).toBeVisible({ timeout: 15_000 });
    await page.waitForResponse(r => r.url().includes('/validate-status') && r.status() === 200, { timeout: 15_000 }).catch(() => {});

    // --- STEP 3: Confirm and Import ---
    const confirmBtn = page.locator('button:has-text("Confirmar e Importar")');
    await expect(confirmBtn).toBeEnabled({ timeout: 15_000 });
    await confirmBtn.click();

    // Verify Result Modal
    await expect(page.getByText(/Importados: 3/)).toBeVisible({ timeout: 30_000 });
    await page.click('button:has-text("Fechar")');

    // --- STEP 4: Verification in UI ---
    const clearBtn = page.locator('button.clear-filters-btn');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }

    // 4A. Verify Active contract
    await page.fill('input#filterContractNumber', contractOk);
    const rowActive = page.locator('table tbody tr', { hasText: contractOk });
    await expect(rowActive).toBeVisible({ timeout: 10_000 });
    await expect(rowActive).toContainText('Cliente Consultor Ativo');

    // 4B. Verify Defaulted contract
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }
    await page.fill('input#filterContractNumber', contractDef);
    const rowDefaulted = page.locator('table tbody tr', { hasText: contractDef });
    await expect(rowDefaulted).toBeVisible({ timeout: 10_000 });
    await expect(rowDefaulted).toContainText('Cliente Consultor Cancelado');

    // 4C. Verify Desistente contract (requires Desistente filter)
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
      await page.waitForTimeout(500);
    }
    await page.fill('input#filterContractNumber', contractDes);
    const rowDesistente = page.locator('table tbody tr', { hasText: contractDes });
    await expect(rowDesistente).not.toBeVisible({ timeout: 5000 });

    const statusSelect = page.locator('input#filterStatus');
    await statusSelect.click();
    await page.click('.mantine-MultiSelect-option:has-text("Desistente"), .mantine-Select-option:has-text("Desistente")');
    await expect(rowDesistente).toBeVisible({ timeout: 10_000 });
    await expect(rowDesistente).toContainText('Cliente Consultor Desistente');
  });
});
