// extractor.js
const axios = require('axios');

const ENDPOINT = '7a8110990e16404daec259c355434bc6.pbidedicated.windows.net';
const PATH     = '/webapi/capacities/7A811099-0E16-404D-AEC2-59C355434BC6/workloads/QES/QueryExecutionService/automatic/public/query';
const URL      = `https://${ENDPOINT}${PATH}`;

function getCalendarFilters(dateString) {
  if (!dateString) {
    const currentYear = new Date().getFullYear();
    return [{
      "Condition": {
        "In": {
          "Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "c"}}, "Property": "Ano"}}],
          "Values": [[{"Literal": {"Value": `${currentYear}L` }}]]
        }
      }
    }];
  }

  const parts = dateString.split('-');
  const filters = [];

  // Year (YYYY)
  if (parts[0]) {
    filters.push({
      "Condition": {
        "In": {
          "Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "c"}}, "Property": "Ano"}}],
          "Values": [[{"Literal": {"Value": `${parts[0]}L` }}]]
        }
      }
    });
  }

  // Month (MM)
  if (parts[1]) {
    const month = parseInt(parts[1], 10);
    filters.push({
      "Condition": {
        "In": {
          "Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "c"}}, "Property": "Mês"}}],
          "Values": [[{"Literal": {"Value": `${month}L` }}]]
        }
      }
    });
  }

  // Day (DD)
  if (parts[2]) {
    const day = parseInt(parts[2], 10);
    filters.push({
      "Condition": {
        "In": {
          "Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "c"}}, "Property": "Dia"}}],
          "Values": [[{"Literal": {"Value": `${day}L` }}]]
        }
      }
    });
  }

  return filters;
}

function buildPayload1(store, matricula, scrapeDate) {
  const paddedMatricula = matricula.padStart(6, '0');
  const calendarFilters = getCalendarFilters(scrapeDate);
  
  return {
    "version": "1.0.0",
    "queries": [{
      "Query": {
        "Commands": [{
          "SemanticQueryDataShapeCommand": {
            "Query": {
              "Version": 2,
              "From": [
                {"Name": "m", "Entity": "1_Medidas", "Type": 0},
                {"Name": "t", "Entity": "tbl_cotas", "Type": 0},
                {"Name": "r", "Entity": "Regiões", "Type": 0},
                {"Name": "c", "Entity": "Calendario", "Type": 0},
                {"Name": "p", "Entity": "Parâmetro_Senhas", "Type": 0},
                {"Name": "a", "Entity": "acessos", "Type": 0}
              ],
              "Select": [
                {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Produção Oficial"}, "Name": "Medidas.Produção Oficial", "NativeReferenceName": "Produção Oficial"},
                {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Retenção"}, "Name": "Medidas.Retenção", "NativeReferenceName": "Retenção"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "id_consultor"}, "Name": "tbl_cotas.id_consultor", "NativeReferenceName": "CNPJ"},
                {"Column": {"Expression": {"SourceRef": {"Source": "r"}}, "Property": "regiao"}, "Name": "Regiões.regiao", "NativeReferenceName": "Região"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "Consultor_Matricula"}, "Name": "tbl_cotas.Consultor_Matricula", "NativeReferenceName": "Consultor"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_unidade_bi_original"}, "Name": "tbl_cotas.nm_unidade_bi_original", "NativeReferenceName": "Unidade Original"}
              ],
              "Where": [
                {
                  "Condition": {"Comparison": {"ComparisonKind": 1, "Left": {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Produção Oficial"}}, "Right": {"Literal": {"Value": "0L"}}}},
                  "Target": [
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "id_consultor"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "Consultor_Matricula"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_unidade_bi_original"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "r"}}, "Property": "regiao"}}
                  ]
                },
                {
                  "Condition": {"Comparison": {"ComparisonKind": 1, "Left": {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Filtro de Cotas"}}, "Right": {"Literal": {"Value": "0L"}}}},
                  "Target": [
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "id_consultor"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "Consultor_Matricula"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_unidade_bi_original"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "r"}}, "Property": "regiao"}}
                  ]
                },
                {"Condition": {"In": {"Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "Grupo Ativo"}}], "Values": [[{"Literal": {"Value": "'Sim'"}}]]}}},
                ...calendarFilters,
                {"Condition": {"In": {"Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property":"nm_unidade_bi_original"}}], "Values": [[{"Literal": {"Value": `'${store}'` }}]]}}},
                {"Condition": {"And": {"Left": {"Comparison": {"ComparisonKind": 2, "Left": {"Column": {"Expression": {"SourceRef": {"Source": "p"}}, "Property": "Parâmetro_Senhas"}}, "Right": {"Literal": {"Value": "929009D"}}}}, "Right": {"Comparison": {"ComparisonKind": 4, "Left": {"Column": {"Expression": {"SourceRef": {"Source": "p"}}, "Property": "Parâmetro_Senhas"}}, "Right": {"Literal": {"Value": "929009D"}}}}}}},
                {"Condition": {"In": {"Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "a"}}, "Property": "matricula"}}], "Values": [[{"Literal": {"Value": `'${paddedMatricula}'` }}]]}}}
              ],
              "OrderBy": [{"Direction": 2, "Expression": {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Produção Oficial"}}}]
            },
            "Binding": {"Primary": {"Groupings": [{"Projections": [0, 1, 2, 3, 4, 5], "Subtotal": 1}]}, "DataReduction": {"DataVolume": 3, "Primary": {"Window": {"Count": 500}}}, "Version": 1},
            "ExecutionMetricsKind": 1
          }
        }],
        "QueryId": "",
        "ApplicationContext": {"DatasetId": "ecfe45f3-4e9f-4a6e-9d56-91020972365d", "Sources": [{"ReportId": "0fdd545d-8c7f-4a8a-9723-daf16cac10d6", "VisualId": "081f7f5c60a98980243b"}]}
      }
    }],
    "cancelQueries": [],
    "modelId": 5258155,
    "userPreferredLocale": "en-US",
    "allowLongRunningQueries": true
  };
}

function buildPayload2(store, matricula, scrapeDate) {
  const paddedMatricula = matricula.padStart(6, '0');
  const calendarFilters = getCalendarFilters(scrapeDate);

  return {
    "version": "1.0.0",
    "queries": [{
      "Query": {
        "Commands": [{
          "SemanticQueryDataShapeCommand": {
            "Query": {
              "Version": 2,
              "From": [
                {"Name": "2", "Entity": "2_Medidas_Tabela", "Type": 0},
                {"Name": "t", "Entity": "tbl_cotas", "Type": 0},
                {"Name": "m", "Entity": "1_Medidas", "Type": 0},
                {"Name": "c", "Entity": "Calendario", "Type": 0},
                {"Name": "p", "Entity": "Parâmetro_Senhas", "Type": 0},
                {"Name": "a", "Entity": "acessos", "Type": 0}
              ],
              "Select": [
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "versao"}, "Name": "Sum(tbl_cotas.versao)", "NativeReferenceName": "Versao"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_consultor"}, "Name": "tbl_cotas.nm_consultor", "NativeReferenceName": "Consultor"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "id_matricula"}, "Name": "tbl_cotas.id_matricula", "NativeReferenceName": "Matricula"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_pv"}, "Name": "tbl_cotas.nm_pv", "NativeReferenceName": "PV"},
                {"Aggregation": {"Expression": {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "vl_credito_venda"}}, "Function": 0}, "Name": "Sum(tbl_cotas.vl_credito_venda)", "NativeReferenceName": "Crédito Venda"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_producao"}, "Name": "tbl_cotas.dt_producao", "NativeReferenceName": "Dt Produção"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_venda"}, "Name": "tbl_cotas.dt_venda", "NativeReferenceName": "Dt Venda"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "categoria_consultor"}, "Name": "tbl_cotas.categoria_consultor", "NativeReferenceName": "Categoria"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "cd_ponto_venda"}, "Name": "tbl_cotas.cd_ponto_venda", "NativeReferenceName": "Cód. PV"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_cancelamento"}, "Name": "tbl_cotas.dt_cancelamento", "NativeReferenceName": "Dt Cancelamento"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_unidade_bi_atual"}, "Name": "tbl_cotas.nm_unidade_bi_atual", "NativeReferenceName": "Unidade Atual"},
                {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Obs Cota"}, "Name": "1_Medidas.Obs Restrições Cota", "NativeReferenceName": "Obs Cota"},
                {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Produção Analitica"}, "Name": "1_Medidas.Produção Analitica", "NativeReferenceName": "Produção Analitica"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "rn"}, "Name": "Sum(tbl_cotas.rn)", "NativeReferenceName": "id_bi"},
                {"Measure": {"Expression": {"SourceRef": {"Source": "2"}}, "Property": "Cota"}, "Name": "2_Medidas_Tabela.id_cota", "NativeReferenceName": "Cota"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "pz_cota"}, "Name": "Sum(tbl_cotas.pz_cota)", "NativeReferenceName": "Prazo Cota"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "pz_comercializacao"}, "Name": "Sum(tbl_cotas.pz_comercializacao)", "NativeReferenceName": "Prazo Grupo"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "tem_pagamento"}, "Name": "tbl_cotas.tem_pagamento", "NativeReferenceName": "Tem Pagamento?"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_contemplacao"}, "Name": "tbl_cotas.dt_contemplacao", "NativeReferenceName": "Dt Contemplacao"},
                {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property":"nm_unidade_bi_original"}, "Name": "tbl_cotas.nm_unidade_bi_original", "NativeReferenceName": "Unidade Original"},
                {"Column": {"Expression": {"SourceRef": {"Source":"t"}}, "Property": "qtd_pc_atraso"}, "Name": "Sum(tbl_cotas.qtd_pc_atraso)", "NativeReferenceName": "Qtd Parcelas Atraso"},
                {"Column": {"Expression": {"SourceRef": {"Source":"t"}}, "Property": "nm_plano_venda"}, "Name": "tbl_cotas.nm_plano_venda", "NativeReferenceName": "Plano Venda"},
                {"Column": {"Expression": {"SourceRef": {"Source":"t"}}, "Property": "nm_situacao_cobranca"}, "Name": "tbl_cotas.nm_situacao_cobranca", "NativeReferenceName": "Situação Cobrança"}
              ],
              "Where": [
                {
                  "Condition": {"Comparison": {"ComparisonKind": 0, "Left": {"Measure": {"Expression": {"SourceRef": {"Source": "m"}}, "Property": "Filtro de Cotas"}}, "Right": {"Literal": {"Value": "1L"}}}},
                  "Target": [
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "versao"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_venda"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_producao"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_cancelamento"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_contemplacao"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "id_matricula"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "categoria_consultor"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_consultor"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "cd_ponto_venda"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_pv"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_unidade_bi_original"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_unidade_bi_atual"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "tem_pagamento"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "pz_cota"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "pz_comercializacao"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "qtd_pc_atraso"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_situacao_cobranca"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_plano_venda"}},
                    {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "rn"}}
                  ]
                },
                ...calendarFilters,
                {"Condition": {"In": {"Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "nm_unidade_bi_original"}}], "Values": [[{"Literal": {"Value": `'${store}'` }}]]}}},
                {"Condition": {"And": {"Left": {"Comparison": {"ComparisonKind": 2, "Left": {"Column": {"Expression": {"SourceRef": {"Source": "p"}}, "Property": "Parâmetro_Senhas"}}, "Right": {"Literal": {"Value": "929009D"}}}}, "Right": {"Comparison": {"ComparisonKind": 4, "Left": {"Column": {"Expression": {"SourceRef": {"Source": "p"}}, "Property": "Parâmetro_Senhas"}}, "Right": {"Literal":{"Value": "929009D"}}}}}}},
                {"Condition": {"In": {"Expressions": [{"Column": {"Expression": {"SourceRef": {"Source": "a"}}, "Property": "matricula"}}], "Values": [[{"Literal": {"Value": `'${paddedMatricula}'` }}]]}}}
              ],
              "OrderBy": [{"Direction": 2, "Expression": {"Column": {"Expression": {"SourceRef": {"Source": "t"}}, "Property": "dt_producao"}}}]
            },
            "Binding": {"Primary": {"Groupings": [{"Projections": [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22], "Subtotal": 1}]}, "DataReduction": {"DataVolume": 3, "Primary": {"Window": {"Count": 500}}}, "Version": 1},
            "ExecutionMetricsKind": 1
          }
        }],
        "QueryId": "",
        "ApplicationContext": {"DatasetId": "ecfe45f3-4e9f-4a6e-9d56-91020972365d", "Sources": [{"ReportId": "0fdd545d-8c7f-4a8a-9723-daf16cac10d6", "VisualId": "cc96a767b12339a67c49"}]}
      }
    }],
    "cancelQueries": [],
    "modelId": 5258155,
    "userPreferredLocale": "en-US",
    "allowLongRunningQueries": true
  };
}


function parseDSR(data) {
  const result = data?.results?.[0]?.result?.data;
  if (!result) return [];

  const descriptor = result.descriptor;
  const ds         = result.dsr?.DS?.[0];
  if (!ds) return [];

  const selectItems  = descriptor?.Select || [];
  const friendlyName = {};
  selectItems.forEach(item => {
    friendlyName[item.Value] = item.NativeReferenceName || item.Name;
  });

  const dicts = ds.ValueDicts || {};
  const ph    = ds.PH || [];

  const allRows = [];

  for (const group of ph) {
    const dmKey  = group.DM1 ? 'DM1' : group.DM0 ? 'DM0' : null;
    if (!dmKey) continue;

    const entries = group[dmKey];
    let schemaRow = null;
    let prev      = [];

    for (const entry of entries) {
      if (entry.S) {
        schemaRow = entry.S;
        prev = new Array(schemaRow.length).fill(null);
        const hasDirectData = schemaRow.some(s => entry[s.N] !== undefined);
        if (hasDirectData) {
          const row = {};
          schemaRow.forEach(s => {
            const alias = s.N;
            const dictKey = s.DN;
            let val = entry[alias] ?? null;
            if (dictKey && dicts[dictKey] !== undefined && val !== null) {
              val = dicts[dictKey][val] ?? val;
            }
            row[friendlyName[alias] || alias] = val;
          });
          allRows.push(row);
          schemaRow.forEach((s, i) => { prev[i] = entry[s.N] ?? null; });
        }
        continue;
      }

      if (!entry.C && schemaRow) {
        const row = {};
        schemaRow.forEach((s, i) => {
          const alias   = s.N;
          const dictKey = s.DN;
          let val = entry[alias] ?? prev[i];
          if (dictKey && dicts[dictKey] !== undefined && val !== null) {
            val = dicts[dictKey][val] ?? val;
          }
          row[friendlyName[alias] || alias] = val;
          prev[i] = entry[alias] !== undefined ? entry[alias] : prev[i];
        });
        allRows.push(row);
        continue;
      }

      if (entry.C && schemaRow) {
        const C        = entry.C;
        const R        = entry.R || 0;
        const resolved = [...prev];
        let   ci       = 0;

        for (let pos = 0; pos < schemaRow.length; pos++) {
          const repeated = (R >> pos) & 1;
          if (!repeated) {
            resolved[pos] = C[ci] !== undefined ? C[ci] : null;
            ci++;
          }
        }

        for (let i = 0; i < schemaRow.length; i++) prev[i] = resolved[i];

        const row = {};
        schemaRow.forEach((s, i) => {
          const alias   = s.N;
          const dictKey = s.DN;
          let val = resolved[i];
          if (dictKey && dicts[dictKey] !== undefined && val !== null) {
            val = dicts[dictKey][val] ?? val;
          }
          row[friendlyName[alias] || alias] = val;
        });
        allRows.push(row);
      }
    }
  }

  return allRows;
}

function toCsv(rows) {
  if (!rows.length) return '';
  const headers = Object.keys(rows[0]);
  const esc = v => {
    const s = (v === null || v === undefined) ? '' : String(v);
    return s.includes(',') || s.includes('"') || s.includes('\n')
      ? `"${s.replace(/"/g, '""')}"` : s;
  };
  return [
    headers.join(','),
    ...rows.map(r => headers.map(h => esc(r[h])).join(','))
  ].join('\n');
}

async function postWithRetry(url, payload, headers, label) {
  const maxRetries = parseInt(process.env.SCRAPE_MAX_RETRIES || '2', 10);
  const timeoutMs = parseInt(process.env.SCRAPE_TIMEOUT_MS || '240000', 10);
  let lastError;

  for (let attempt = 0; attempt <= maxRetries; attempt++) {
    try {
      if (attempt > 0) {
        console.log(`[Extractor] ${label} - Retry attempt ${attempt}/${maxRetries}...`);
      }
      
      const response = await axios.post(url, payload, { 
        headers, 
        timeout: timeoutMs 
      });
      return response;
    } catch (err) {
      lastError = err;
      const isTimeout = err.code === 'ECONNABORTED' || err.message.includes('timeout');
      console.warn(`[Extractor] ${label} - Attempt ${attempt + 1} failed: ${err.message}${isTimeout ? ' (Timeout)' : ''}`);
      
      if (attempt === maxRetries) break;
      
      // Wait a bit before retrying (exponential backoff could be added here, but simple delay for now)
      await new Promise(r => setTimeout(r, 1000 * (attempt + 1)));
    }
  }

  throw new Error(`${label} failed after ${maxRetries + 1} attempts. Last error: ${lastError.message}`);
}

async function scrape(store, matricula, token, scrapeDate) {
  const headers = {
    'Authorization':  token,
    'Content-Type':   'application/json',
    'Accept':         'application/json, text/plain, */*',
    'Origin':         'https://dashboardbi.ademicon.com.br',
    'Referer':        'https://dashboardbi.ademicon.com.br/'
  };

  console.log(`[Extractor] Querying for Store: "${store}", Matricula: "${matricula}", Date: "${scrapeDate || 'Default'}"`);

  const payload1 = buildPayload1(store, matricula, scrapeDate);
  const payload2 = buildPayload2(store, matricula, scrapeDate);

  // DEBUG: Write payloads to temp files so they can be inspected manually
  try {
    const fs = require('fs');
    const path = require('path');
    const debugDir = path.join(__dirname, 'scratch', 'debug');
    if (!fs.existsSync(debugDir)) fs.mkdirSync(debugDir, { recursive: true });
    fs.writeFileSync(path.join(debugDir, 'payload1.json'), JSON.stringify(payload1, null, 2));
    fs.writeFileSync(path.join(debugDir, 'payload2.json'), JSON.stringify(payload2, null, 2));
    console.log(`[Extractor] Payloads saved to ${debugDir} for debugging.`);
    
    // Log the date filter being sent specifically
    const q1Filter = payload1.queries[0].Query.Where.find(w => w.Condition?.In?.Expressions?.[0]?.Column?.Property === 'Ano');
    if (q1Filter) {
      console.log(`[Extractor] Year Filter being sent: ${q1Filter.Condition.In.Values[0][0].Literal.Value}`);
    }
  } catch (e) {}

  console.log('[Extractor] Sending Query 1 and Query 2 via Promise.all with retries...');

  try {
    const [res1, res2] = await Promise.all([
      postWithRetry(URL, payload1, headers, 'Query 1'),
      postWithRetry(URL, payload2, headers, 'Query 2')
    ]);

    console.log(`[Extractor] Query 1 Response Status: ${res1.status}`);
    console.log(`[Extractor] Query 2 Response Status: ${res2.status}`);

    const rows1 = parseDSR(res1.data);
    const rows2 = parseDSR(res2.data);

    console.log(`[Extractor] Query 1 returned ${rows1.length} rows`);
    console.log(`[Extractor] Query 2 returned ${rows2.length} rows`);

    // Debug: Log raw response if no detail rows
    if (!res2.data || !res2.data.results) {
      console.log('[Extractor] Query 2 Raw Response Data (First 200 chars):', JSON.stringify(res2.data).substring(0, 200));
    }

    return {
      rows: [...rows1, ...rows2],
      csv: toCsv(rows2) // We prioritize the detailed rows for CSV export
    };
  } catch (err) {
    console.error('[Extractor] Extraction failed after retries:', err.message);
    throw err;
  }
}

module.exports = { scrape };
