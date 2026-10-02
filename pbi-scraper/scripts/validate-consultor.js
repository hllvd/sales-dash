// validate-consultor.js
// Bateria de testes de integridade entre arquivos Consultor scrapeado e PBI exports.
//
// Uso: node pbi-scraper/scripts/validate-consultor.js
//
// Arquivos esperados em pbi-scraper/outputs/:
//   - arquivo-consultor-scrapped.csv   → CSV gerado pelo scraper Consultor
//   - arquivo-consultor.xlsx           → Exportação direta do PBI (Consultor)
//   - arquivo-contract-dashboard.xlsx  → Template contractDashboard (import manual)
//
// Qualquer arquivo ausente → o teste correspondente é pulado com aviso.

'use strict';

const fs   = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// ─── Configuração de colunas ───────────────────────────────────────────────────

const OUTPUTS_DIR = path.resolve(__dirname, '../outputs');

const FILES = {
  scrapped:      path.join(OUTPUTS_DIR, 'arquivo-consultor-scrapped.csv'),
  consultorXlsx: path.join(OUTPUTS_DIR, 'arquivo-consultor.xlsx'),
  dashboardXlsx: path.join(OUTPUTS_DIR, 'arquivo-contract-dashboard.xlsx'),
};

// Mapeamento XLSX consultor (sem prefixo) → CSV scrapeado (com prefixo)
// Usado pelo Teste 1 para comparar campo a campo.
const COLUMN_MAP_XLSX_TO_CSV = {
  'Grupo':                    '2 Rel Carteira.Grupo',
  'Cota':                     'Sum(2 Rel Carteira.Cota)',
  'Versão':                   'Sum(2 Rel Carteira.Versão)',
  'Definição.Situação':       '2 Rel Carteira.Definição.Situação',
  'Consorciado':              '2 Rel Carteira.Consorciado',
  'Últ.Pagto Parcela':        '2 Rel Carteira.Últ.Pagto Parcela',
  'Quant.Parcelas em Atraso': 'Sum(2 Rel Carteira.Quant.Parcelas em Atraso)',
  'Quant.Parcelas Pagas':     'Sum(2 Rel Carteira.Quant.Parcelas Pagas)',
  'Data.Venda':               '2 Rel Carteira.Data.Venda',
  'Data.Cancelamento.Cota':   '2 Rel Carteira.Data.Cancelamento.Cota',
};

// ─── ContractStatusMappings (espelho fiel do appsettings.json) ───────────────

const STATUS_MAP = {
  Active:          ['Active','Normal','Ativa','Ativo','QUITADO','PAGO','CONT PAGO','Contemplado','CONTEMPLADO'],
  Late1:           ['Late1','NCONT 1 AT','CONT NÃO ENTREGUE 1 ATR','CONT 1 ATR','CONT BEM PEND 1 ATR'],
  Late2:           ['Late2','NCONT 2 AT','CONT NÃO ENTREGUE 2 ATR','CONT BEM PEND 2 ATR'],
  Late3:           ['Late3','NCONT 3 AT','SUJ. A CANCELAMENTO','SUJ. A  CANCELAMENTO','delinquent'],
  AwaitingPayment: ['AwaitingPayment','SUJ. CANC. 1ª PARC. SEM PGTO'],
  Defaulted:       ['Defaulted','EXCLUIDO','EXCLUÍDO','EXCLUIDA','EXCLUÍDA','CANCELADO','Cancelada','CACELADO','DISTRATADO','SUSPENSO','paid_off'],
  Desistente:      ['Desistente','DESISTENTE','Desistência','DESISTÊNCIA','Desistencia','DESISTENCIA'],
  Transferred:     ['Transferred','TRANSFERIDO'],
  NaoDefinido:     ['NaoDefinido','Não definido','Nao definido','COBRANCA ADMINISTRATIVA','COBRANÇA ADMINISTRATIVA'],
};

/** Build reverse lookup map once: rawValue → canonicalStatus */
const STATUS_REVERSE_MAP = (() => {
  const map = new Map();
  for (const [canonical, values] of Object.entries(STATUS_MAP)) {
    for (const v of values) {
      map.set(v.toLowerCase(), canonical);
    }
  }
  return map;
})();

// ─── Pure helpers ─────────────────────────────────────────────────────────────

function mapStatus(raw) {
  if (!raw || raw.trim() === '') return 'NaoDefinido';
  return STATUS_REVERSE_MAP.get(raw.trim().toLowerCase()) ?? 'NaoDefinido';
}

function parseHasPayment(v) {
  if (v == null) return null;
  const t = v.trim().toLowerCase();
  if (['sim','s','yes','true'].includes(t))  return true;
  if (['não','nao','n','no','false'].includes(t)) return false;
  return null;
}

function parseLastPaymentDate(v) {
  if (v == null || v === '') return null;
  const t = v.trim();
  if (t === '' || t.toLowerCase() === 'null' || t === '0') return false;
  return true;
}

function parseNullableInt(v) {
  if (v == null || v.trim() === '') return null;
  const n = parseInt(v, 10);
  if (!isNaN(n)) return n;
  const f = parseFloat(v);
  if (!isNaN(f)) return Math.round(f);
  return null;
}

function parseDate(v) {
  if (!v || v.trim() === '') return null;
  const d = new Date(v.trim());
  return isNaN(d.getTime()) ? null : d;
}

function isExcluido(s) {
  return ['excluido','excluído','excluida','excluída','cancelado','cancelada','defaulted']
    .includes(s.toLowerCase());
}

function isNormal(s) {
  return ['normal','active','ativo','ativa'].includes(s.toLowerCase());
}

/**
 * Derives canonical status for a Consultor CSV row.
 * Mirrors ConsultorStatusEvaluator.cs DeriveStatus() exactly.
 */
function deriveConsultorStatus(rawStatus, overdueInstallments, paidInstallments, hasPayment, saleStartDate, referenceDate) {
  if (!rawStatus || rawStatus.trim() === '') return 'NaoDefinido';

  const s = rawStatus.trim();

  if (isExcluido(s)) {
    if (paidInstallments != null && paidInstallments === 0) return 'Desistente';
    return 'Defaulted';
  }

  if (isNormal(s)) {
    if (hasPayment === false) {
      if (saleStartDate != null) {
        const diffDays = (referenceDate - saleStartDate) / (1000 * 60 * 60 * 24);
        if (diffDays <= 30) return 'AwaitingPayment';
      }
      if (overdueInstallments != null) {
        if (overdueInstallments >= 3) return 'Late3';
        if (overdueInstallments === 2) return 'Late2';
        if (overdueInstallments === 1) return 'Late1';
      }
      return 'AwaitingPayment';
    }

    if (overdueInstallments != null) {
      if (overdueInstallments >= 3) return 'Late3';
      if (overdueInstallments === 2) return 'Late2';
      if (overdueInstallments === 1) return 'Late1';
    }

    return 'Active';
  }

  return mapStatus(s);
}

// ─── File readers ──────────────────────────────────────────────────────────────

function readXlsxRows(xlsxPath) {
  const tmpPy = `/tmp/xlsx_rows_${Date.now()}.py`;

  const pyLines = [
    'import zipfile, re, sys, json, xml.etree.ElementTree as ET',
    '',
    'def strip_ns(tag):',
    "    return re.sub('{[^}]+}', '', tag)",
    '',
    'wb_path = sys.argv[1]',
    'shared_strings = []',
    'with zipfile.ZipFile(wb_path, "r") as z:',
    '    names = z.namelist()',
    '    if "xl/sharedStrings.xml" in names:',
    '        ss_xml = z.read("xl/sharedStrings.xml").decode("utf-8")',
    '        ss_root = ET.fromstring(ss_xml)',
    '        for si in ss_root:',
    '            parts = []',
    '            for elem in si.iter():',
    '                tag = strip_ns(elem.tag)',
    '                if tag == "t" and elem.text:',
    '                    parts.append(elem.text)',
    '            shared_strings.append("".join(parts))',
    '    sheet_xml = z.read("xl/worksheets/sheet1.xml").decode("utf-8")',
    '',
    'root = ET.fromstring(sheet_xml)',
    'rows_data = []',
    'for row_elem in root.iter():',
    '    if strip_ns(row_elem.tag) != "row":',
    '        continue',
    '    cells = []',
    '    for c in row_elem:',
    '        if strip_ns(c.tag) != "c":',
    '            continue',
    '        t_attr = c.get("t", "")',
    '        val = ""',
    '        for child in c:',
    '            ctag = strip_ns(child.tag)',
    '            if ctag == "v" and child.text is not None:',
    '                if t_attr == "s":',
    '                    idx = int(child.text)',
    '                    val = shared_strings[idx] if idx < len(shared_strings) else ""',
    '                else:',
    '                    val = child.text',
    '            elif ctag == "is":',
    '                for t_el in child.iter():',
    '                    if strip_ns(t_el.tag) == "t" and t_el.text:',
    '                        val += t_el.text',
    '        cells.append(val)',
    '    rows_data.append(cells)',
    '',
    'print(json.dumps(rows_data))',
  ];
  const pyCode = pyLines.join('\n');

  fs.writeFileSync(tmpPy, pyCode, 'utf8');
  try {
    const out = execSync(`python3 "${tmpPy}" "${xlsxPath}"`, { encoding: 'utf8', maxBuffer: 256 * 1024 * 1024 });
    const rawRows = JSON.parse(out.trim());
    if (rawRows.length === 0) return { headers: [], rows: [] };

    const headers = rawRows[0];
    const rows = rawRows.slice(1).map(cells => {
      const obj = {};
      headers.forEach((h, i) => { obj[h] = cells[i] ?? ''; });
      return obj;
    });
    return { headers, rows };
  } finally {
    try { fs.rmSync(tmpPy, { force: true }); } catch { /* ignore */ }
  }
}

function readCsvRows(csvPath) {
  const content = fs.readFileSync(csvPath, 'utf8');
  const lines = content.split(/\r?\n/).filter(l => l.trim() !== '');
  if (lines.length === 0) return { headers: [], rows: [] };

  const parseLine = (line) => {
    const result = [];
    let cur = '';
    let inQuote = false;
    for (let i = 0; i < line.length; i++) {
      const ch = line[i];
      if (ch === '"') {
        if (inQuote && line[i + 1] === '"') { cur += '"'; i++; }
        else { inQuote = !inQuote; }
      } else if (ch === ',' && !inQuote) {
        result.push(cur);
        cur = '';
      } else {
        cur += ch;
      }
    }
    result.push(cur);
    return result;
  };

  const headers = parseLine(lines[0]);
  const rows = lines.slice(1).map(line => {
    const cells = parseLine(line);
    const obj = {};
    headers.forEach((h, i) => { obj[h] = cells[i] ?? ''; });
    return obj;
  });
  return { headers, rows };
}

function normalizeContractNumber(v) {
  if (!v) return '';
  return String(v).trim().replace(/^0+/, '') || '0';
}

// ─── Reporting helpers ────────────────────────────────────────────────────────

const LINE = '='.repeat(100);
const DIV  = '-'.repeat(100);

function printHeader(title) {
  console.log('\n' + LINE);
  console.log('  ' + title);
  console.log(LINE);
}

function printSection(title) {
  console.log('\n' + DIV);
  console.log('  ' + title);
  console.log(DIV);
}

// Date columns in XLSX come as Excel serial integers; CSV has formatted dates.
const DATE_COLUMNS_XLSX = new Set([
  'Últ.Pagto Parcela',
  'Data.Venda',
  'Data.Cancelamento.Cota',
  'Data Primeira Assembleia',
  'Data Contemplação',
  'Fim.Vigência',
]);

/**
 * Converts an Excel serial date number to a 'YYYY-MM-DD' string.
 * Excel epoch is 1899-12-30 (not 1900-01-01 due to the leap year bug).
 * Returns the original value if it's not a valid serial number.
 * @param {string} v
 * @returns {string}
 */
function excelSerialToDateStr(v) {
  const n = parseInt(v, 10);
  if (isNaN(n) || n <= 0 || n > 3000000) return v; // not a plausible serial
  const epoch = new Date(Date.UTC(1899, 11, 30)); // 1899-12-30
  const ms = epoch.getTime() + n * 86400000;
  const d = new Date(ms);
  const y = d.getUTCFullYear();
  const m = String(d.getUTCMonth() + 1).padStart(2, '0');
  const day = String(d.getUTCDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/**
 * Normalizes field values for fair comparison:
 * - Pure numbers: rounded to 3 decimal places (e.g. "3.0" == "3")
 * - Strings / text: trimmed and compared case-insensitively (e.g. "FABIO" == "Fabio")
 * - Dates (already converted to YYYY-MM-DD): compared case-insensitively without numeric truncation
 */
function normalizeFieldVal(v) {
  if (v == null) return '';
  const trimmed = String(v).trim();
  if (trimmed === '') return '';

  // Only normalize strictly numeric values (avoiding date strings like "2026-02-28")
  if (/^-?\d+(\.\d+)?$/.test(trimmed)) {
    const n = parseFloat(trimmed);
    if (!isNaN(n)) {
      return String(Math.round(n * 1000) / 1000);
    }
  }

  return trimmed.toLowerCase();
}

function compareFields(xlsxRow, csvRow) {
  const discrepancies = [];

  for (const [xlsxCol, csvCol] of Object.entries(COLUMN_MAP_XLSX_TO_CSV)) {
    let xlsxVal = (xlsxRow[xlsxCol] ?? '').trim();
    const csvVal  = (csvRow[csvCol]   ?? '').trim();

    // Convert Excel serial dates to ISO format for fair comparison
    if (DATE_COLUMNS_XLSX.has(xlsxCol) && xlsxVal !== '') {
      xlsxVal = excelSerialToDateStr(xlsxVal);
    }

    if (normalizeFieldVal(xlsxVal) !== normalizeFieldVal(csvVal)) {
      discrepancies.push(`  ${xlsxCol}: xlsx="${xlsxVal}" vs csv="${csvVal}"`);
    }
  }

  return discrepancies;
}


function runTest1(xlsxData, csvData) {
  printHeader('TESTE 1 — Integridade dos Dados: XLSX Consultor vs CSV Scrapeado');

  const xlsxMap = new Map();
  for (const row of xlsxData.rows) {
    const key = normalizeContractNumber(row['Nº Contrato']);
    if (key) xlsxMap.set(key, row);
  }

  const csvMap = new Map();
  for (const row of csvData.rows) {
    const key = normalizeContractNumber(row['Sum(2 Rel Carteira.n_contrato)']);
    if (key) csvMap.set(key, row);
  }

  printSection('Cobertura de Registros');
  console.log(`  XLSX: ${xlsxMap.size} contratos únicos`);
  console.log(`  CSV : ${csvMap.size} contratos únicos`);

  const onlyInXlsx = [...xlsxMap.keys()].filter(k => !csvMap.has(k));
  const onlyInCsv  = [...csvMap.keys()].filter(k => !xlsxMap.has(k));

  if (onlyInXlsx.length > 0) {
    console.log(`\n  ⚠️  ${onlyInXlsx.length} contratos presentes no XLSX mas AUSENTES no CSV:`);
    for (const k of onlyInXlsx.slice(0, 20)) console.log(`     Contrato: ${k}`);
    if (onlyInXlsx.length > 20) console.log(`     ... e mais ${onlyInXlsx.length - 20}`);
  }

  if (onlyInCsv.length > 0) {
    console.log(`\n  ⚠️  ${onlyInCsv.length} contratos presentes no CSV mas AUSENTES no XLSX:`);
    for (const k of onlyInCsv.slice(0, 20)) console.log(`     Contrato: ${k}`);
    if (onlyInCsv.length > 20) console.log(`     ... e mais ${onlyInCsv.length - 20}`);
  }

  const both = [...xlsxMap.keys()].filter(k => csvMap.has(k));
  printSection(`Comparação Campo a Campo (${both.length} contratos em ambos)`);

  let contractsWithDiff = 0;
  const allDiscrepancies = [];

  for (const k of both) {
    const diffs = compareFields(xlsxMap.get(k), csvMap.get(k));
    if (diffs.length > 0) {
      contractsWithDiff++;
      allDiscrepancies.push({ contrato: k, diffs });
    }
  }

  if (allDiscrepancies.length === 0) {
    console.log('\n  ✅ Nenhuma discrepância de dados encontrada!');
  } else {
    for (const { contrato, diffs } of allDiscrepancies) {
      console.log(`\n  ❌ Contrato ${contrato}:`);
      for (const d of diffs) console.log(d);
    }
  }

  printSection('Resumo — Teste 1');
  console.log(`  Contratos em ambos os arquivos : ${both.length}`);
  console.log(`  Contratos com discrepâncias    : ${contractsWithDiff}`);
  console.log(`  Contratos sem discrepâncias    : ${both.length - contractsWithDiff}`);
  console.log(`  Apenas no XLSX                 : ${onlyInXlsx.length} (apenas reportado)`);
  console.log(`  Apenas no CSV                  : ${onlyInCsv.length} (apenas reportado)`);
}

// ─── Teste 2 — Integridade do Estado ─────────────────────────────────────────

function deriveDashboardStatus(row) {
  // contractDashboard: pure status lookup from ContractStatusMappings — no HasPayment override
  const situacao = (row['Situação Cobrança'] ?? row['Situacao Cobranca'] ?? '').trim();
  return mapStatus(situacao);
}

function deriveScrappedStatus(row, referenceDate) {
  const rawStatus           = (row['2 Rel Carteira.Definição.Situação'] ?? '').trim() || null;
  const overdueInstallments = parseNullableInt(row['Sum(2 Rel Carteira.Quant.Parcelas em Atraso)']);
  const paidInstallments    = parseNullableInt(row['Sum(2 Rel Carteira.Quant.Parcelas Pagas)']);
  const saleStartDate       = parseDate(row['2 Rel Carteira.Data.Venda']);

  const ultPagto     = row['2 Rel Carteira.Últ.Pagto Parcela'] ?? null;
  const temPagamento = row['Tem Pagamento?'] ?? row['Tem Pagamento'] ?? null;

  let hasPayment = parseHasPayment(temPagamento);
  if (hasPayment === null) hasPayment = parseLastPaymentDate(ultPagto);

  return deriveConsultorStatus(rawStatus, overdueInstallments, paidInstallments, hasPayment, saleStartDate, referenceDate);
}

function runTest2(dashboardData, csvData) {
  printHeader('TESTE 2 — Integridade do Estado: contractDashboard vs CSV Scrapeado');

  const referenceDate = new Date();

  const dashMap = new Map();
  for (const row of dashboardData.rows) {
    const key = normalizeContractNumber(
      row['Nº Contrato'] ?? row['n_contrato'] ?? row['ContractNumber'] ?? ''
    );
    if (key) dashMap.set(key, row);
  }

  const csvMap = new Map();
  for (const row of csvData.rows) {
    const key = normalizeContractNumber(row['Sum(2 Rel Carteira.n_contrato)']);
    if (key) csvMap.set(key, row);
  }

  printSection('Cobertura de Registros');
  console.log(`  contractDashboard: ${dashMap.size} contratos únicos`);
  console.log(`  CSV scrapeado    : ${csvMap.size} contratos únicos`);

  const onlyInDash = [...dashMap.keys()].filter(k => !csvMap.has(k));
  const onlyInCsv  = [...csvMap.keys()].filter(k => !dashMap.has(k));

  if (onlyInDash.length > 0) {
    console.log(`\n  ⚠️  ${onlyInDash.length} contratos no dashboard AUSENTES no CSV:`);
    for (const k of onlyInDash.slice(0, 20)) console.log(`     Contrato: ${k}`);
    if (onlyInDash.length > 20) console.log(`     ... e mais ${onlyInDash.length - 20}`);
  }

  if (onlyInCsv.length > 0) {
    console.log(`\n  ⚠️  ${onlyInCsv.length} contratos no CSV AUSENTES no dashboard:`);
    for (const k of onlyInCsv.slice(0, 20)) console.log(`     Contrato: ${k}`);
    if (onlyInCsv.length > 20) console.log(`     ... e mais ${onlyInCsv.length - 20}`);
  }

  const both = [...dashMap.keys()].filter(k => csvMap.has(k));
  printSection(`Comparação de Status (${both.length} contratos em ambos)`);

  const situacaoColName = dashboardData.headers.includes('Situação Cobrança')
    ? 'Situação Cobrança'
    : (dashboardData.headers.includes('Situacao Cobranca') ? 'Situacao Cobranca' : '?');

  const divergencias = [];
  for (const k of both) {
    const statusA = deriveDashboardStatus(dashMap.get(k));
    const statusB = deriveScrappedStatus(csvMap.get(k), referenceDate);

    if (statusA.trim().toLowerCase() !== statusB.trim().toLowerCase()) {
      const rawDash = (dashMap.get(k)[situacaoColName] ?? '').trim();
      const rawCsv  = (csvMap.get(k)['2 Rel Carteira.Definição.Situação'] ?? '').trim();
      divergencias.push({ contrato: k, statusA, statusB, rawDash, rawCsv });
    }
  }

  if (divergencias.length === 0) {
    console.log('\n  ✅ Nenhuma divergência de status encontrada!');
  } else {
    console.log(`\n  ❌ ${divergencias.length} divergências encontradas:\n`);
    const pad = (s, n) => String(s ?? '').padEnd(n);
    console.log(`  ${pad('Contrato', 14)}${pad('Status Dashboard', 18)}${pad('Status Scrape', 18)}${pad('Raw Dashboard', 30)}${pad('Raw CSV', 20)}`);
    console.log('  ' + '-'.repeat(96));
    for (const d of divergencias) {
      console.log(`  ${pad(d.contrato, 14)}${pad(d.statusA, 18)}${pad(d.statusB, 18)}${pad(d.rawDash, 30)}${pad(d.rawCsv, 20)}`);
    }
  }

  printSection('Distribuição de Status — contractDashboard');
  const distDash = {};
  for (const k of both) {
    const s = deriveDashboardStatus(dashMap.get(k));
    distDash[s] = (distDash[s] ?? 0) + 1;
  }
  for (const [s, n] of Object.entries(distDash).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${String(s).padEnd(20)} : ${n}`);
  }

  printSection('Distribuição de Status — CSV Scrapeado');
  const distCsv = {};
  for (const k of both) {
    const s = deriveScrappedStatus(csvMap.get(k), referenceDate);
    distCsv[s] = (distCsv[s] ?? 0) + 1;
  }
  for (const [s, n] of Object.entries(distCsv).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${String(s).padEnd(20)} : ${n}`);
  }

  printSection('Resumo — Teste 2');
  console.log(`  Contratos em ambos os arquivos : ${both.length}`);
  console.log(`  Status idênticos               : ${both.length - divergencias.length}`);
  console.log(`  Status divergentes             : ${divergencias.length}`);
  console.log(`  Apenas no dashboard            : ${onlyInDash.length} (apenas reportado)`);
  console.log(`  Apenas no CSV                  : ${onlyInCsv.length} (apenas reportado)`);
}

// ─── Main ─────────────────────────────────────────────────────────────────────

function main() {
  console.log(LINE);
  console.log('  BATERIA DE TESTES — Integridade de Dados e Estado — Consultor Scrape');
  console.log('  Data de referência: ' + new Date().toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' }) + ' (Brasília)');
  console.log(LINE);

  if (!fs.existsSync(FILES.scrapped)) {
    console.warn('\n⚠️  CSV scrapeado não encontrado: ' + FILES.scrapped);
    console.warn('   → Ambos os testes serão pulados.\n');
    return;
  }

  console.log('\n📂 Lendo CSV scrapeado: ' + path.basename(FILES.scrapped) + ' ...');
  const csvData = readCsvRows(FILES.scrapped);
  console.log('   → ' + csvData.rows.length + ' linhas, ' + csvData.headers.length + ' colunas.');

  if (!fs.existsSync(FILES.consultorXlsx)) {
    console.warn('\n⚠️  XLSX Consultor não encontrado: ' + FILES.consultorXlsx);
    console.warn('   → Teste 1 pulado.\n');
  } else {
    console.log('\n📂 Lendo XLSX Consultor: ' + path.basename(FILES.consultorXlsx) + ' ...');
    const xlsxData = readXlsxRows(FILES.consultorXlsx);
    console.log('   → ' + xlsxData.rows.length + ' linhas, ' + xlsxData.headers.length + ' colunas.');
    runTest1(xlsxData, csvData);
  }

  if (!fs.existsSync(FILES.dashboardXlsx)) {
    console.warn('\n⚠️  XLSX contractDashboard não encontrado: ' + FILES.dashboardXlsx);
    console.warn('   → Teste 2 pulado.\n');
  } else {
    console.log('\n📂 Lendo XLSX contractDashboard: ' + path.basename(FILES.dashboardXlsx) + ' ...');
    const dashData = readXlsxRows(FILES.dashboardXlsx);
    console.log('   → ' + dashData.rows.length + ' linhas, ' + dashData.headers.length + ' colunas.');
    runTest2(dashData, csvData);
  }

  console.log('\n' + LINE);
  console.log('  FIM DA BATERIA DE TESTES');
  console.log(LINE + '\n');
}

main();
