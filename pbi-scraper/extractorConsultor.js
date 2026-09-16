// extractorConsultor.js
// Direct HTTP POST extraction for Consultor dashboard ("2 Rel Carteira")
// Uses PowerBI SemanticQueryDataShapeCommand with calendar/date filters.

const fs = require('fs');
const path = require('path');
const axios = require('axios');
const tokenManager = require('./tokenManager');
const { getOrFetchTokens } = require('./auth');
const { parseDSR, toCsv } = require('./scrapeConsultor');
const { captureTemplate } = require('./captureTemplate');

const ENDPOINT = '7a8110990e16404daec259c355434bc6.pbidedicated.windows.net';
const PATH     = '/webapi/capacities/7A811099-0E16-404D-AEC2-59C355434BC6/workloads/QES/QueryExecutionService/automatic/public/query';
const URL      = `https://${ENDPOINT}${PATH}`;

/**
 * Normalizes input date parameter into an array of cleaned 'YYYY-MM' strings.
 * Pure deterministic function.
 *
 * @param {string|string[]|null|undefined} dates
 * @returns {string[]} - Array of 'YYYY-MM' strings
 */
function normalizeTargetMonths(dates) {
  if (!dates) return [];
  let rawList = [];
  if (Array.isArray(dates)) {
    rawList = dates;
  } else if (typeof dates === 'string') {
    rawList = dates.split(',');
  }

  const cleaned = rawList
    .map(d => (d !== null && d !== undefined ? String(d).trim() : ''))
    .filter(d => /^\d{4}-\d{2}/.test(d))
    .map(d => d.substring(0, 7));

  return Array.from(new Set(cleaned));
}

/**
 * Builds PowerBI Date.Venda range filter conditions for target months.
 * Pure deterministic function.
 *
 * @param {string} relSource - Entity source alias (e.g. '2')
 * @param {string[]} targetMonths - e.g. ['2026-07']
 * @returns {object[]} - Array of PowerBI Where filter objects
 */
function buildDateRangeFilter(relSource, targetMonths) {
  if (!targetMonths || targetMonths.length === 0) return [];

  const monthConditions = targetMonths.map(ym => {
    const [yStr, mStr] = ym.split('-');
    const year = parseInt(yStr, 10);
    const month = parseInt(mStr, 10);

    const startStr = `${year}-${String(month).padStart(2, '0')}-01T00:00:00`;
    const nextYear = month === 12 ? year + 1 : year;
    const nextMonth = month === 12 ? 1 : month + 1;
    const nextStr = `${nextYear}-${String(nextMonth).padStart(2, '0')}-01T00:00:00`;

    return {
      "And": {
        "Left": {
          "Comparison": {
            "ComparisonKind": 2, // GreaterThanOrEqual (>=)
            "Left": { "Column": { "Expression": { "SourceRef": { "Source": relSource } }, "Property": "Data.Venda" } },
            "Right": { "Literal": { "Value": `datetime'${startStr}'` } }
          }
        },
        "Right": {
          "Comparison": {
            "ComparisonKind": 3, // LessThan (<)
            "Left": { "Column": { "Expression": { "SourceRef": { "Source": relSource } }, "Property": "Data.Venda" } },
            "Right": { "Literal": { "Value": `datetime'${nextStr}'` } }
          }
        }
      }
    };
  });

  if (monthConditions.length === 1) {
    return [{ "Condition": monthConditions[0] }];
  }

  let combined = monthConditions[0];
  for (let i = 1; i < monthConditions.length; i++) {
    combined = {
      "Or": {
        "Left": combined,
        "Right": monthConditions[i]
      }
    };
  }

  return [{ "Condition": combined }];
}

/**
 * Updates the matricula condition in the Where clause if present.
 */
function updateMatriculaFilter(sqQuery, matricula) {
  if (!matricula || !Array.isArray(sqQuery.Where)) return;
  const padded = String(matricula).padStart(6, '0');
  for (const w of sqQuery.Where) {
    const exprs = w.Condition?.In?.Expressions;
    if (Array.isArray(exprs) && exprs.some(e => e.Column?.Property === 'cd_comissionado')) {
      w.Condition.In.Values = [[{ "Literal": { "Value": `'${padded}'` } }]];
    }
  }
}

/**
 * Loads the Consultor template payload and injects target month filters.
 *
 * @param {string} templatePath
 * @param {string[]} targetMonths
 * @param {string} matricula
 * @returns {object} - Modified payload ready to send
 */
function prepareConsultorPayload(templatePath, targetMonths, matricula) {
  if (!fs.existsSync(templatePath)) {
    throw new Error(`Template de query não encontrado em: ${templatePath}`);
  }

  const rawJson = fs.readFileSync(templatePath, 'utf8');
  const payload = JSON.parse(rawJson);

  const commands = payload?.queries?.[0]?.Query?.Commands;
  if (Array.isArray(commands)) {
    for (const cmd of commands) {
      const sqQuery = cmd?.SemanticQueryDataShapeCommand?.Query;
      if (sqQuery) {
        // Update matricula in Where
        updateMatriculaFilter(sqQuery, matricula);

        // Find alias for '2 Rel Carteira'
        const relSource = sqQuery.From?.find(f => f.Entity === '2 Rel Carteira')?.Name || '2';

        // Inject date range filters if months are specified
        if (targetMonths && targetMonths.length > 0) {
          const dateFilters = buildDateRangeFilter(relSource, targetMonths);
          if (!Array.isArray(sqQuery.Where)) {
            sqQuery.Where = [];
          }
          sqQuery.Where.push(...dateFilters);
        }
      }
    }
  }

  return payload;
}

/**
 * Direct HTTP POST scrape function for Consultor.
 *
 * @param {object} options
 * @param {string} options.matricula
 * @param {string} options.password
 * @param {string|string[]|null} [options.scrapeDates]
 * @param {string} [options.outputDir='./outputs']
 */
async function scrapeConsultorDirect(options) {
  const {
    matricula,
    password,
    scrapeDates = null,
    outputDir = './outputs'
  } = options || {};

  if (!matricula || !password) {
    throw new Error('Matrícula e senha são obrigatórias para o scrape direto do Consultor.');
  }

  const targetMonths = normalizeTargetMonths(scrapeDates);
  const outPath = path.resolve(outputDir);
  if (!fs.existsSync(outPath)) {
    fs.mkdirSync(outPath, { recursive: true });
  }

  const startDate = new Date();
  const startTimeMs = Date.now();

  console.log('====================================================');
  console.log('🚀 Iniciando Scrape Consultor (Modo Direto HTTP POST)');
  console.log(`👤 Matrícula: ${matricula}`);
  console.log(`📅 Meses solicitados: ${targetMonths.length > 0 ? targetMonths.join(', ') : 'Todos os meses'}`);
  console.log(`🕒 Início: ${startDate.toLocaleString('pt-BR')}`);
  console.log('====================================================');

  const tplDir = path.resolve(__dirname, 'templates');
  const tplFile = path.join(tplDir, 'consultorQueryTemplate.json');
  const tokenFile = path.join(tplDir, 'consultorToken.json');

  // 1. Obtém o token específico de Consultor e o template
  console.log('[Direct] Obtendo MWCToken específico de Consultor...');
  let consultorToken = null;

  // Verifica cache em memória
  const mem = tokenManager.getTokens(`${matricula}_consultor`);
  if (mem && mem.pbiToken) {
    console.log('[Direct] Reutilizando MWCToken de Consultor em memória.');
    consultorToken = mem.pbiToken;
  }

  // Verifica cache em disco (consultorToken.json) válido por 40 minutos
  if (!consultorToken && fs.existsSync(tokenFile)) {
    try {
      const disk = JSON.parse(fs.readFileSync(tokenFile, 'utf8'));
      const ageMs = Date.now() - (disk.capturedAt || 0);
      if (disk.token && ageMs < 40 * 60 * 1000) {
        console.log(`[Direct] Reutilizando MWCToken de Consultor salvo em disco (${Math.round(ageMs / 60000)}m atrás).`);
        consultorToken = disk.token;
        tokenManager.setTokens(`${matricula}_consultor`, { pbiToken: consultorToken });
      }
    } catch (_) {}
  }

  // Se não tem token ou não tem template, captura via /dashboard/consultor
  if (!consultorToken || !fs.existsSync(tplFile)) {
    console.log('[Direct] MWCToken de Consultor ausente ou expirado. Capturando novo token via /dashboard/consultor...');
    const resCapture = await captureTemplate(matricula, password);
    consultorToken = resCapture.token;
  }

  if (!consultorToken) {
    throw new Error('Falha ao obter MWCToken de Consultor para execução da consulta.');
  }

  // 2. Prepara o payload com os filtros de data e matrícula
  const payload = prepareConsultorPayload(tplFile, targetMonths, matricula);

  const headers = {
    'Authorization': consultorToken,
    'Content-Type': 'application/json',
    'Accept': 'application/json, text/plain, */*',
    'Origin': 'https://dashboardbi.ademicon.com.br',
    'Referer': 'https://dashboardbi.ademicon.com.br/'
  };

  // 3. Loop de Paginação via RestartTokens (equivalente ao scrolldown, mas via HTTP em segundos)
  const allRows = [];
  let pageCount = 0;
  let isComplete = false;
  let restartTokens = null;
  let lastRawData = null;
  const paginationContext = { prev: [] };

  while (!isComplete) {
    pageCount++;
    console.log(`[Direct] Disparando requisição HTTP (Página ${pageCount})...`);

    // Injeta RestartTokens no Window da página 2 em diante
    const windowObj = payload?.queries?.[0]?.Query?.Commands?.[0]?.SemanticQueryDataShapeCommand?.Binding?.DataReduction?.Primary?.Window;
    if (windowObj) {
      if (restartTokens) {
        windowObj.RestartTokens = restartTokens;
      } else {
        delete windowObj.RestartTokens;
      }
    }

    const res = await axios.post(URL, payload, {
      headers,
      timeout: 120000
    });
    lastRawData = res.data;

    if (!res.data) {
      throw new Error('Resposta vazia da API do PowerBI.');
    }

    const dsrError = res.data?.results?.[0]?.result?.data?.dsr?.DataShapes?.[0]?.['odata.error'];
    if (dsrError) {
      const msg = dsrError.message?.value || JSON.stringify(dsrError);
      throw new Error(`Erro retornado pelo PowerBI: ${msg}`);
    }

    const pageRows = parseDSR(res.data, paginationContext);
    allRows.push(...pageRows);
    console.log(`[Direct] Página ${pageCount}: ${pageRows.length} registros recebidos (Total acumulado: ${allRows.length}).`);

    const ds = res.data?.results?.[0]?.result?.data?.dsr?.DS?.[0];
    const isFinished = ds?.IC === true;
    const nextRt = ds?.RT;

    if (isFinished || !nextRt || nextRt.length === 0 || pageRows.length === 0) {
      isComplete = true;
      console.log(`[Direct] Paginação concluída em ${pageCount} página(s).`);
    } else {
      restartTokens = nextRt;
      // Intervalo de 200ms entre requisições
      await new Promise(r => setTimeout(r, 200));
    }
  }

  // Deduplicação com base em Identificador.Cota ou chave única de contrato
  const seenKeys = new Set();
  const rows = [];
  for (const row of allRows) {
    const key = row['2 Rel Carteira.Identificador.Cota'] ?? 
                (row['2 Rel Carteira.Grupo'] && row['Sum(2 Rel Carteira.Cota)'] && row['Sum(2 Rel Carteira.Versão)']
                  ? `${row['2 Rel Carteira.Grupo']}_${row['Sum(2 Rel Carteira.Cota)']}_${row['Sum(2 Rel Carteira.Versão)']}`
                  : JSON.stringify(row));
    if (!seenKeys.has(key)) {
      seenKeys.add(key);
      rows.push(row);
    }
  }
  console.log(`[Direct] Total de registros únicos consolidados: ${rows.length}.`);

  // 4. Salva JSON e CSV
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const datesTag = targetMonths.length > 0 ? `_${targetMonths.join('_')}` : '';
  const jsonFileName = `consultor_direct_${matricula}${datesTag}_${timestamp}.json`;
  const csvFileName = `consultor_direct_${matricula}${datesTag}_${timestamp}.csv`;

  const jsonFilePath = path.join(outPath, jsonFileName);
  const csvFilePath = path.join(outPath, csvFileName);

  const endDate = new Date();
  const elapsedMs = Date.now() - startTimeMs;
  const elapsedSecTotal = Math.floor(elapsedMs / 1000);
  const elapsedMinutes = Math.floor(elapsedSecTotal / 60);
  const elapsedSeconds = elapsedSecTotal % 60;
  const durationFormatted = elapsedMinutes > 0
    ? `${elapsedMinutes}m ${elapsedSeconds}s`
    : `${elapsedSecTotal}s`;

  const jsonPayload = {
    matricula,
    targetMonths: targetMonths.length > 0 ? targetMonths : null,
    startedAt: startDate.toISOString(),
    finishedAt: endDate.toISOString(),
    durationSeconds: elapsedSecTotal,
    durationFormatted,
    scrapedAt: endDate.toISOString(),
    totalRows: rows.length,
    rows,
    rawData: lastRawData
  };

  fs.writeFileSync(jsonFilePath, JSON.stringify(jsonPayload, null, 2), 'utf8');
  console.log(`✅ Arquivo JSON salvo: ${jsonFilePath}`);

  const csvText = toCsv(rows);
  fs.writeFileSync(csvFilePath, csvText, 'utf8');
  console.log(`✅ Arquivo CSV salvo: ${csvFilePath}`);

  console.log('\n====================================================');
  console.log('🎉 Scrape Consultor Direto Concluído com Sucesso!');
  console.log(`🕒 Início: ${startDate.toLocaleString('pt-BR')}`);
  console.log(`🏁 Fim:    ${endDate.toLocaleString('pt-BR')}`);
  console.log(`⏱️  Tempo de Execução: ${durationFormatted} (${elapsedSecTotal}s)`);
  console.log(`📅 Meses filtrados: ${targetMonths.length > 0 ? targetMonths.join(', ') : 'Todos'}`);
  console.log(`✨ Total de registros extraídos: ${rows.length}`);
  console.log(`📁 JSON: ${jsonFileName}`);
  console.log(`📁 CSV:  ${csvFileName}`);
  console.log('====================================================\n');

  return {
    status: 'Succeeded',
    matricula,
    targetMonths: targetMonths.length > 0 ? targetMonths : null,
    startedAt: startDate.toISOString(),
    finishedAt: endDate.toISOString(),
    durationFormatted,
    durationSeconds: elapsedSecTotal,
    totalRows: rows.length,
    jsonFile: jsonFilePath,
    csvFile: csvFilePath
  };
}

module.exports = {
  scrapeConsultorDirect,
  buildDateRangeFilter,
  normalizeTargetMonths,
  prepareConsultorPayload
};
