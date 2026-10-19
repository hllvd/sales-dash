// column-mapper.js
// Utility to map columns between PBI export (.xlsx) and scrapped CSV.
// Usage: node scripts/column-mapper.js
// Outputs a full mapping table + highlights missing/extra columns.

'use strict';

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// ─── Pure helpers ─────────────────────────────────────────────────────────────

/**
 * Strips prefixes like '2 Rel Carteira.', 'Sum(2 Rel Carteira.', 'Sum(' etc.
 * for fuzzy comparison between PBI readable names and raw scrape names.
 *
 * @param {string} col
 * @returns {string}
 */
function normalize(col) {
  return col
    .replace(/^Sum\(/, '')
    .replace(/\)$/, '')
    .replace(/^[0-9]+ Rel Carteira\./, '')
    .replace(/^[0-9]+ Medidas\./, '')
    .toLowerCase()
    .trim();
}

/**
 * Reads the header row from an xlsx file using Python3 stdlib (no external Node deps).
 * Handles xlsx files with inline strings and x: namespace prefixes.
 *
 * @param {string} xlsxPath - Absolute path to .xlsx file
 * @returns {string[]} - Array of column header strings
 */
function readXlsxHeaders(xlsxPath) {
  const tmpPy = path.join('/tmp', `xlsx_parse_${Date.now()}.py`);
  const pyLines = [
    'import zipfile, re, sys',
    'wb_path = sys.argv[1]',
    'with zipfile.ZipFile(wb_path, "r") as z:',
    '    xml = z.read("xl/worksheets/sheet1.xml").decode("utf-8")',
    'xml = re.sub(r"<(/?)[a-zA-Z]+:", "<\\\\1", xml)',
    'row_m = re.search(r"<row[^>]*>(.*?)</row>", xml, re.DOTALL)',
    'if not row_m:',
    '    sys.exit("No row found")',
    'cells = re.findall(r"<c[^>]*>.*?</c>", row_m.group(1), re.DOTALL)',
    'for cell in cells:',
    '    is_m = re.search(r"<is>.*?<t[^>]*>([^<]*)</t>.*?</is>", cell, re.DOTALL)',
    '    if is_m:',
    '        print(is_m.group(1))',
    '    else:',
    '        v_m = re.search(r"<v>([^<]*)</v>", cell)',
    '        print(v_m.group(1) if v_m else "")',
  ];

  fs.writeFileSync(tmpPy, pyLines.join('\n'), 'utf8');
  try {
    const out = execSync(`python3 "${tmpPy}" "${xlsxPath}"`, { encoding: 'utf8' });
    return out.split('\n').map(l => l.trim()).filter(l => l.length > 0);
  } finally {
    try { fs.rmSync(tmpPy, { force: true }); } catch { /* ignore */ }
  }
}

/**
 * Reads the header row from a CSV file (first line, comma-separated).
 *
 * @param {string} csvPath - Absolute path to .csv file
 * @returns {string[]}
 */
function readCsvHeaders(csvPath) {
  const firstLine = fs.readFileSync(csvPath, 'utf8').split('\n')[0];
  return firstLine.replace(/\r$/, '').split(',');
}

/**
 * Builds a mapping between PBI (readable) headers and scrapped (raw) headers.
 * Uses normalize() for fuzzy matching.
 *
 * @param {string[]} pbiHeaders
 * @param {string[]} scrappedHeaders
 * @returns {Array<{pbiIndex: number, pbiColumn: string, scrappedColumn: string|null, status: string}>}
 */
function buildColumnMapping(pbiHeaders, scrappedHeaders) {
  const scrappedNorm = scrappedHeaders.map(h => ({ original: h, norm: normalize(h) }));

  return pbiHeaders.map((pbi, pbiIdx) => {
    const pbiNorm = normalize(pbi);
    const match = scrappedNorm.find(s => s.norm === pbiNorm);
    return {
      pbiIndex: pbiIdx + 1,
      pbiColumn: pbi,
      scrappedColumn: match ? match.original : null,
      status: match ? 'MAPPED' : 'MISSING_IN_SCRAPE'
    };
  });
}

/**
 * Finds scrapped columns that have no corresponding PBI column.
 *
 * @param {Array} mapping - Output of buildColumnMapping
 * @param {string[]} scrappedHeaders
 * @returns {Array<{scrappedColumn: string, status: string}>}
 */
function findExtrasInScrape(mapping, scrappedHeaders) {
  const mappedScrapped = new Set(mapping.map(m => m.scrappedColumn).filter(Boolean));
  return scrappedHeaders
    .filter(h => !mappedScrapped.has(h))
    .map(h => ({ scrappedColumn: h, status: 'EXTRA_IN_SCRAPE' }));
}

// ─── Main ─────────────────────────────────────────────────────────────────────

function main() {
  const outputsDir = path.resolve(__dirname, '../outputs');
  const xlsxPath = path.join(outputsDir, 'arquivo-original.xlsx');
  const csvPath = path.join(outputsDir, 'arquivo-scrapped.csv');

  if (!fs.existsSync(xlsxPath)) {
    console.error(`❌ Arquivo não encontrado: ${xlsxPath}`);
    process.exit(1);
  }
  if (!fs.existsSync(csvPath)) {
    console.error(`❌ Arquivo não encontrado: ${csvPath}`);
    process.exit(1);
  }

  console.log('📂 Lendo colunas do arquivo PowerBI (XLSX)...');
  const pbiHeaders = readXlsxHeaders(xlsxPath);
  console.log(`   → ${pbiHeaders.length} colunas encontradas.\n`);

  console.log('📂 Lendo colunas do arquivo scrapped (CSV)...');
  const scrappedHeaders = readCsvHeaders(csvPath);
  console.log(`   → ${scrappedHeaders.length} colunas encontradas.\n`);

  const mapping = buildColumnMapping(pbiHeaders, scrappedHeaders);
  const extras = findExtrasInScrape(mapping, scrappedHeaders);

  // ── Print report ────────────────────────────────────────────────────────────

  const W = 44;
  const pad = (s, n) => String(s ?? '').padEnd(n);
  const line = '='.repeat(100);
  const divider = '-'.repeat(100);

  console.log(line);
  console.log(`${pad('#', 4)}${pad('PBI Column (readable)', W)}${pad('Scrapped Column (raw)', W)}Status`);
  console.log(line);

  for (const m of mapping) {
    const icon = m.status === 'MAPPED' ? '✅' : '❌';
    const label = m.status === 'MAPPED' ? 'MAPEADO' : 'FALTANDO NO SCRAPE';
    console.log(`${pad(m.pbiIndex, 4)}${pad(m.pbiColumn, W)}${pad(m.scrappedColumn ?? '—', W)}${icon} ${label}`);
  }

  if (extras.length > 0) {
    console.log('\n' + divider);
    console.log('Colunas presentes no SCRAPE mas ausentes no PBI export:');
    console.log(divider);
    for (const e of extras) {
      console.log(`${pad('', 4)}${pad('', W)}${pad(e.scrappedColumn, W)}⚠️  EXTRA NO SCRAPE`);
    }
  }

  const missing = mapping.filter(m => m.status === 'MISSING_IN_SCRAPE');
  const mapped  = mapping.filter(m => m.status === 'MAPPED');

  console.log('\n' + line);
  console.log('📊 Resumo:');
  console.log(`   PBI columns   : ${pbiHeaders.length}`);
  console.log(`   Scrape columns: ${scrappedHeaders.length}`);
  console.log(`   Mapeadas      : ${mapped.length}`);
  console.log(`   Faltando      : ${missing.length}`);
  console.log(`   Extras scrape : ${extras.length}`);

  if (missing.length > 0) {
    console.log('\n❌ Colunas do PBI FALTANDO no scrape:');
    for (const m of missing) {
      console.log(`   [${m.pbiIndex}] "${m.pbiColumn}"  →  normalizado: "${normalize(m.pbiColumn)}"`);
    }
  }

  // ── Save JSON mapping ───────────────────────────────────────────────────────

  const outputJson = path.join(outputsDir, 'column-mapping.json');
  fs.writeFileSync(outputJson, JSON.stringify({
    generatedAt: new Date().toISOString(),
    pbiFile: 'arquivo-original.xlsx',
    scrapeFile: 'arquivo-scrapped.csv',
    pbiTotal: pbiHeaders.length,
    scrapeTotal: scrappedHeaders.length,
    mappedCount: mapped.length,
    missingCount: missing.length,
    extrasCount: extras.length,
    mapping,
    extras
  }, null, 2), 'utf8');

  console.log(`\n💾 Mapping salvo em: ${outputJson}`);
}

main();
