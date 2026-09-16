// scrapeConsultor.js
// Specialized scraping flow for "Consultor" view:
// 1. Logs in to AVA PRO (avapro.ademicon.com.br)
// 2. Navigates/clicks on the "Consultor" menu option
// 3. Positions mouse in center of viewport and scrolls periodically
// 4. Intercepts and collects all QueryExecutionService responses (.../workloads/QES/.../query)
// 5. Stops when 20 seconds pass without any new query responses
// 6. Exports consolidated JSON and CSV to outputs/

const puppeteer = require('puppeteer');
const fs = require('fs');
const path = require('path');
const axios = require('axios');

const AVA_URL = 'https://avapro.ademicon.com.br/';
const DASHBOARD_URL = 'https://avapro.ademicon.com.br/dashboard';
const DASHBOARD_CONSULTOR_URL = 'https://avapro.ademicon.com.br/dashboard/consultor';

/**
 * Pure helper to convert DSR semantic query result into normalized rows.
 * Supports PowerBI 64-bit repeat bitmask (R), null bitmask (Ø), and ValueDicts.
 */
function parseDSR(data, context = null) {
  const result = data?.results?.[0]?.result?.data;
  if (!result) return [];

  const descriptor = result.descriptor;
  const ds = result.dsr?.DS?.[0];
  if (!ds) return [];

  const selectItems = descriptor?.Select || [];
  // Ignore single-column / title queries (like '0 Medidas.Atualização' or 'RELATÓRIOS DO CONSULTOR')
  if (selectItems.length < 5) return [];

  const friendlyName = {};
  selectItems.forEach((item) => {
    friendlyName[item.Value] = item.NativeReferenceName || item.Name;
  });

  const dicts = ds.ValueDicts || {};
  const ph = ds.PH || [];
  const allRows = [];
  let prev = (context?.prev && Array.isArray(context.prev)) ? [...context.prev] : [];
  let schemaRow = context?.schemaRow || null;

  for (const group of ph) {
    const dmKey = group.DM1 ? 'DM1' : (group.DM0 && group.DM0[0]?.S?.length >= 5 ? 'DM0' : null);
    if (!dmKey) continue;

    const entries = group[dmKey];

    for (const entry of entries) {
      if (entry.S) {
        schemaRow = entry.S;
        if (context && typeof context === 'object') {
          context.schemaRow = schemaRow;
        }
        if (!prev || prev.length !== schemaRow.length) {
          prev = new Array(schemaRow.length).fill(null);
        }
      }

      if (!schemaRow) continue;

      const hasDirectData = schemaRow.some(s => entry[s.N] !== undefined);
      if (hasDirectData) {
        const row = {};
        schemaRow.forEach((s, i) => {
          const alias = s.N;
          const dictKey = s.DN;
          let val = entry[alias] ?? prev[i] ?? null;
          if (dictKey && dicts[dictKey] !== undefined && val !== null && val !== undefined) {
            val = dicts[dictKey][val] ?? val;
          }
          row[friendlyName[alias] || alias] = val;
          if (entry[alias] !== undefined) {
            prev[i] = entry[alias];
          }
        });
        allRows.push(row);
        continue;
      }

      if (entry.C) {
        const C = entry.C;
        // 64-bit bitmasks for repeated values and explicit null values
        const R_big = entry.R !== undefined ? BigInt(entry.R) : 0n;
        const nullBitmask = (entry['Ø'] !== undefined) ? BigInt(entry['Ø']) : 0n;

        const resolved = [...prev];
        let ci = 0;

        for (let pos = 0; pos < schemaRow.length; pos++) {
          const bit = 1n << BigInt(pos);
          const isRepeated = (R_big & bit) !== 0n;
          const isExplicitNull = (nullBitmask & bit) !== 0n;

          if (isExplicitNull) {
            resolved[pos] = null;
          } else if (!isRepeated) {
            resolved[pos] = C[ci] !== undefined ? C[ci] : null;
            ci++;
          }
        }

        for (let i = 0; i < schemaRow.length; i++) prev[i] = resolved[i];

        const row = {};
        schemaRow.forEach((s, i) => {
          const alias = s.N;
          const dictKey = s.DN;
          let val = resolved[i];
          if (dictKey && dicts[dictKey] !== undefined && val !== null && val !== undefined) {
            val = dicts[dictKey][val] ?? val;
          }
          row[friendlyName[alias] || alias] = val;
        });
        allRows.push(row);
        continue;
      }

      if (entry.R !== undefined || entry['Ø'] !== undefined) {
        const R_big = entry.R !== undefined ? BigInt(entry.R) : 0n;
        const nullBitmask = (entry['Ø'] !== undefined) ? BigInt(entry['Ø']) : 0n;

        const resolved = [...prev];

        for (let pos = 0; pos < schemaRow.length; pos++) {
          const bit = 1n << BigInt(pos);
          const isRepeated = (R_big & bit) !== 0n;
          const isExplicitNull = (nullBitmask & bit) !== 0n;

          if (isExplicitNull) {
            resolved[pos] = null;
          } else if (!isRepeated) {
            resolved[pos] = entry[schemaRow[pos].N] ?? null;
          }
        }

        for (let i = 0; i < schemaRow.length; i++) prev[i] = resolved[i];

        const row = {};
        schemaRow.forEach((s, i) => {
          const alias = s.N;
          const dictKey = s.DN;
          let val = resolved[i];
          if (dictKey && dicts[dictKey] !== undefined && val !== null && val !== undefined) {
            val = dicts[dictKey][val] ?? val;
          }
          row[friendlyName[alias] || alias] = val;
        });
        allRows.push(row);
      }
    }
  }

  if (context && typeof context === 'object') {
    context.prev = prev;
  }

  // Format valid timestamps (between 2010 and 2035) to YYYY-MM-DD
  const MIN_VALID_TIMESTAMP = 1262304000000;
  const MAX_VALID_TIMESTAMP = 2051222400000;
  const toYyyyMmDd = (val) => {
    if (val === null || val === undefined) return null;
    const num = Number(val);
    if (!isNaN(num) && num >= MIN_VALID_TIMESTAMP && num <= MAX_VALID_TIMESTAMP) {
      const d = new Date(num);
      return d.toISOString().split('T')[0];
    }
    if (typeof val === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(val.trim())) {
      return val.trim();
    }
    return val;
  };

  allRows.forEach(row => {
    Object.keys(row).forEach(k => {
      const lower = k.toLowerCase();
      if (lower.includes('data') || lower.includes('dt_') || lower.includes('vigência') || lower.includes('pagto')) {
        row[k] = toYyyyMmDd(row[k]);
      }
    });
  });

  return allRows;
}

/**
 * Converts array of row objects to CSV text.
 */
function toCsv(rows) {
  if (!rows || !rows.length) return '';
  const headers = Array.from(
    new Set(rows.flatMap(r => Object.keys(r)))
  );

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

/**
 * Main function to execute the Consultor scraping flow.
 *
 * @param {object} options
 * @param {string} options.matricula - AvaPro username / matrícula
 * @param {string} options.password - Plaintext password
 * @param {boolean} [options.headless=false] - Run browser headless or with visible UI
 * @param {number} [options.timeoutNoQueryMs=30000] - Time without queries before stopping scroll
 * @param {number} [options.scrollIntervalMs=2000] - Interval between scroll wheel actions
 * @param {string} [options.outputDir='./outputs'] - Directory where results will be written
 */
async function runConsultorScrape(options) {
  const {
    matricula,
    password,
    headless = false,
    timeoutNoQueryMs = 30000,
    scrollIntervalMs = 2000,
    outputDir = './outputs'
  } = options || {};

  if (!matricula || !password) {
    throw new Error('Matrícula e senha são obrigatórias para o scrape do Consultor.');
  }

  const outPath = path.resolve(outputDir);
  if (!fs.existsSync(outPath)) {
    fs.mkdirSync(outPath, { recursive: true });
  }

  const startDate = new Date();
  const startTimeMs = Date.now();

  console.log('====================================================');
  console.log('🚀 Iniciando Scrape Tipo Consultor');
  console.log(`👤 Matrícula: ${matricula}`);
  console.log(`🕒 Início: ${startDate.toLocaleString('pt-BR')}`);
  console.log(`🖥️  Headless: ${headless}`);
  console.log(`⏱️  Inatividade para término: ${timeoutNoQueryMs / 1000}s`);
  console.log(`⏳ Intervalo de scroll: ${scrollIntervalMs / 1000}s`);
  console.log('====================================================');

  const browser = await puppeteer.launch({
    headless: headless ? 'new' : false,
    defaultViewport: null,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-web-security',
      '--disable-features=IsolateOrigins,site-per-process',
      '--start-maximized'
    ]
  });

  const capturedQueries = [];
  let lastQueryTimestamp = Date.now();
  let totalCapturedCount = 0;
  let hasReceivedFirstQuery = false;

  try {
    const page = await browser.newPage();
    const viewportWidth = 1440;
    const viewportHeight = 900;
    await page.setViewport({ width: viewportWidth, height: viewportHeight });
    await page.setUserAgent('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36');
    page.setDefaultNavigationTimeout(45000);
    page.setDefaultTimeout(45000);

    let avaJwt = null;

    // Monitor responses across main page and any iframe
    const handleResponse = async (res) => {
      try {
        const url = res.url();
        const status = res.status();

        // Capture AvaPro login JWT if present
        if (url.includes('bifrost') && url.includes('/login') && status === 200) {
          const body = await res.json().catch(() => null);
          if (body?.token) {
            avaJwt = body.token;
            console.log(`[Auth] JWT AvaPro capturado: ${avaJwt.substring(0, 20)}...`);
          }
        }

        // Intercept target PowerBI QES query endpoint
        const isQesQuery = (url.includes('windows.net') || url.includes('pbidedicated')) &&
                           url.includes('workloads/QES/QueryExecutionService') &&
                           url.includes('/query');

        if (isQesQuery && status === 200) {
          totalCapturedCount++;
          lastQueryTimestamp = Date.now();
          hasReceivedFirstQuery = true;

          const json = await res.json().catch(() => null);
          if (json) {
            const rowsFromQuery = parseDSR(json);
            capturedQueries.push({
              index: totalCapturedCount,
              timestamp: new Date().toISOString(),
              url,
              rowCount: rowsFromQuery.length,
              rows: rowsFromQuery,
              rawData: json
            });

            console.log(`[Query #${totalCapturedCount}] Resposta capturada da rota /query (${rowsFromQuery.length} registros nesta query).`);
          }
        }
      } catch (err) {
        // Response body might already be drained or closed, safely ignore
      }
    };

    page.on('response', handleResponse);

    // Monitor requests to capture the exact QES semantic query template
    const handleRequest = (req) => {
      try {
        const url = req.url();
        const method = req.method();
        const isQesQuery = (url.includes('windows.net') || url.includes('pbidedicated')) &&
                           url.includes('workloads/QES/QueryExecutionService') &&
                           url.includes('/query');

        if (isQesQuery && method === 'POST') {
          const postData = req.postData();
          if (postData) {
            try {
              const parsed = JSON.parse(postData);
              const sel = parsed?.queries?.[0]?.Query?.Commands?.[0]?.SemanticQueryDataShapeCommand?.Query?.Select;
              if (Array.isArray(sel) && sel.length >= 50) {
                const tplDir = path.resolve(__dirname, 'templates');
                if (!fs.existsSync(tplDir)) fs.mkdirSync(tplDir, { recursive: true });
                const tplFile = path.join(tplDir, 'consultorQueryTemplate.json');
                fs.writeFileSync(tplFile, postData, 'utf8');
                console.log(`[Template] 🎯 Query template da tabela (55 colunas) capturado e salvo em: ${tplFile}`);
              }
            } catch (_) {}
          }
        }
      } catch (_) {}
    };

    page.on('request', handleRequest);

    // Also attach to new target pages/popups if created
    browser.on('targetcreated', async (target) => {
      try {
        const targetPage = await target.page();
        if (targetPage && targetPage !== page) {
          targetPage.on('response', handleResponse);
          targetPage.on('request', handleRequest);
        }
      } catch (_) {}
    });

    // ── 1. Login ──────────────────────────────────────────────────────────
    console.log(`[1/5] Navegando para a página de login: ${AVA_URL}...`);
    await page.goto(AVA_URL, { waitUntil: 'networkidle2', timeout: 35000 });

    console.log('[1/5] Preenchendo credenciais...');
    await page.waitForSelector('input[type="text"]', { timeout: 15000 });
    await page.$eval('input[type="text"]', el => el.value = '');
    await page.click('input[type="text"]');
    await page.type('input[type="text"]', matricula, { delay: 25 });

    await page.waitForSelector('input[type="password"]', { timeout: 15000 });
    await page.$eval('input[type="password"]', el => el.value = '');
    await page.click('input[type="password"]');
    await page.type('input[type="password"]', password, { delay: 25 });

    console.log('[1/5] Submetendo login...');
    const submitBtn = await page.evaluateHandle(() => {
      const btns = Array.from(document.querySelectorAll('button, input[type="submit"]'));
      return btns.find(b => {
        const txt = (b.textContent || b.innerText || b.value || '').toLowerCase().trim();
        return (txt.includes('entrar') || b.type === 'submit') && !txt.includes('esqueceu');
      }) || btns[0] || null;
    });

    if (submitBtn && submitBtn.asElement()) {
      await Promise.all([
        page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 30000 }).catch(() => {}),
        submitBtn.asElement().click()
      ]);
    } else {
      await page.keyboard.press('Enter');
      await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 30000 }).catch(() => {});
    }

    if (page.url().includes('/login')) {
      throw new Error('Falha no login: permaneceu na tela de login. Verifique matrícula e senha.');
    }
    console.log('✅ Login realizado com sucesso!');

    // ── 2. Navegar diretamente para /dashboard/consultor ─────────────────
    console.log(`[2/5] Navegando diretamente para a visualização de Consultor: ${DASHBOARD_CONSULTOR_URL}...`);
    await page.goto(DASHBOARD_CONSULTOR_URL, { waitUntil: 'networkidle2', timeout: 35000 }).catch(() => {});

    // Injetar token no iframe periodicamente se necessário
    const tokenInterval = setInterval(async () => {
      if (!avaJwt) return;
      try {
        await page.evaluate((jwt) => {
          const iframes = Array.from(document.querySelectorAll('iframe'));
          for (const iframe of iframes) {
            if (iframe.contentWindow) {
              iframe.contentWindow.postMessage({ token: jwt }, '*');
            }
          }
        }, avaJwt);
      } catch (_) {}
    }, 1000);

    // ── 3. Reforço: Clicar no menu "Consultor" se ainda não estiver ativo ─
    console.log('[3/5] Verificando e clicando na opção "Consultor" no menu lateral...');
    await new Promise(r => setTimeout(r, 2000));

    let clickedConsultor = false;
    const findAndClickConsultor = async () => {
      return await page.evaluate(() => {
        const elements = Array.from(document.querySelectorAll('a, button, div, span, li, p'));
        for (const el of elements) {
          const text = (el.innerText || el.textContent || '').trim();
          if (text === 'Consultor') {
            const rect = el.getBoundingClientRect();
            if (rect.width > 0 && rect.height > 0) {
              el.click();
              return true;
            }
          }
        }
        return false;
      });
    };

    // Tenta clicar como garantia
    for (let i = 0; i < 5; i++) {
      clickedConsultor = await findAndClickConsultor();
      if (clickedConsultor) {
        console.log('✅ Menu "Consultor" clicado com sucesso!');
        break;
      }
      await new Promise(r => setTimeout(r, 1000));
    }

    if (!clickedConsultor) {
      console.log('ℹ️ URL já em /dashboard/consultor. Buscando frames internos se aplicável...');
      for (const frame of page.frames()) {
        try {
          const clickedInFrame = await frame.evaluate(() => {
            const elements = Array.from(document.querySelectorAll('a, button, div, span, li, p'));
            for (const el of elements) {
              const text = (el.innerText || el.textContent || '').trim();
              if (text === 'Consultor') {
                el.click();
                return true;
              }
            }
            return false;
          });
          if (clickedInFrame) {
            clickedConsultor = true;
            console.log('✅ Menu "Consultor" clicado dentro de frame!');
            break;
          }
        } catch (_) {}
      }
    }

    // ── 4. Aguardar 10 segundos ou até que o iframe de Consultor carregue completamente ─
    console.log('[4/5] Aguardando o iframe de Consultor ser completamente inicializado...');
    try {
      await page.waitForSelector('iframe', { timeout: 15000 });
      console.log('✅ Elemento iframe detectado na página de Consultor.');
    } catch (e) {
      console.warn('⚠️ Iframe não detectado de imediato. Continuando espera...');
    }

    console.log('[4/5] Aguardando 10 segundos para renderização completa dos visuais antes do scroll...');
    for (let sec = 10; sec > 0; sec--) {
      process.stdout.write(`\r⏳ Aguardando inicialização completa do Consultor: ${sec}s restantes...   `);
      await new Promise(r => setTimeout(r, 1000));
    }
    console.log('\n✅ Tempo de espera concluído! Página de Consultor pronta.');

    const centerX = Math.floor(viewportWidth / 2);
    const centerY = Math.floor(viewportHeight / 2);

    console.log(`[5/5] Posicionando mouse no centro exato da tela: (${centerX}, ${centerY})`);
    await page.mouse.move(centerX, centerY);

    // Clicar suavemente no centro para garantir foco no visual/container correto
    await page.mouse.click(centerX, centerY).catch(() => {});
    await new Promise(r => setTimeout(r, 500));

    console.log(`[5/5] Iniciando loop de scrolldown (intervalo: ${scrollIntervalMs / 1000}s, limite de inatividade: ${timeoutNoQueryMs / 1000}s)...`);

    let scrollCount = 0;
    lastQueryTimestamp = Date.now(); // reset timer de inatividade para a fase de scroll

    while (true) {
      scrollCount++;
      const timeSinceLastQueryMs = Date.now() - lastQueryTimestamp;
      const secondsSinceLastQuery = Math.floor(timeSinceLastQueryMs / 1000);

      // Log periódico de status
      process.stdout.write(`\r🔄 Scroll #${scrollCount} | Queries capturadas: ${capturedQueries.length} | Inatividade: ${secondsSinceLastQuery}s / ${timeoutNoQueryMs / 1000}s   `);

      // Verifica condição de parada: se passou timeoutNoQueryMs sem nenhuma query nova
      if (timeSinceLastQueryMs >= timeoutNoQueryMs) {
        console.log(`\n\n🛑 Condição atingida: ${timeoutNoQueryMs / 1000} segundos sem novas requisições em /query. Encerrando rolagem.`);
        break;
      }

      // Executa clique no meio + disparo de PageDown + scroll wheel
      try {
        await page.mouse.move(centerX, centerY);
        await page.mouse.click(centerX, centerY).catch(() => {});

        // 1. Dispara tecla PageDown diretamente na página focada
        await page.keyboard.press('PageDown');

        // 2. Dispara mouse wheel com delta aumentado
        await page.mouse.wheel({ deltaY: 800 });

        // 3. Dispara evento de PageDown e scroll dentro dos iframes do PowerBI
        for (const frame of page.frames()) {
          try {
            await frame.evaluate(() => {
              // Dispara evento de teclado PageDown no documento do iframe
              const event = new KeyboardEvent('keydown', {
                key: 'PageDown',
                code: 'PageDown',
                keyCode: 34,
                which: 34,
                bubbles: true,
                cancelable: true
              });
              document.activeElement?.dispatchEvent(event) || document.body.dispatchEvent(event);

              // Scroll programático dos containers visuais do PowerBI
              const scrollables = Array.from(document.querySelectorAll('div, [role="grid"], [role="presentation"], .visualContainer, .innerContainer'));
              for (const el of scrollables) {
                if (el.scrollHeight > el.clientHeight) {
                  el.scrollTop += 800;
                }
              }
            }).catch(() => {});
          } catch (_) {}
        }
      } catch (err) {
        console.warn(`\nAviso no scroll #${scrollCount}: ${err.message}`);
      }

      await new Promise(r => setTimeout(r, scrollIntervalMs));
    }

    clearInterval(tokenInterval);

    // ── 5. Processamento e Consolidação dos Resultados ───────────────────
    console.log('\n[5/5] Consolidando resultados e gerando saídas...');

    // Consolidação de todas as linhas de todas as queries
    const allRowsCombined = [];
    capturedQueries.forEach(q => {
      if (Array.isArray(q.rows)) {
        allRowsCombined.push(...q.rows);
      }
    });

    // Deduplicação opcional com base em colunas chaves (se presentes)
    const uniqueRows = [];
    const seenRowKeys = new Set();

    allRowsCombined.forEach(row => {
      // Cria uma chave única baseada em cota, contrato ou identificador de cota
      const uniqueKey = (row['2 Rel Carteira.Identificador.Cota'] !== undefined && row['2 Rel Carteira.Identificador.Cota'] !== null)
        ? `cota_${row['2 Rel Carteira.Identificador.Cota']}`
        : (row['2 Rel Carteira.Grupo'] && row['Sum(2 Rel Carteira.Cota)'] && row['Sum(2 Rel Carteira.Versão)'])
          ? `${row['2 Rel Carteira.Grupo']}_${row['Sum(2 Rel Carteira.Cota)']}_${row['Sum(2 Rel Carteira.Versão)']}`
          : (row['Cota'] || row['Contrato'] || row['tbl_cotas.id_cota'] || row['id_cota'] || row['CNPJ'] || JSON.stringify(row));

      if (!seenRowKeys.has(uniqueKey)) {
        seenRowKeys.add(uniqueKey);
        uniqueRows.push(row);
      }
    });

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const jsonFileName = `consultor_${matricula}_${timestamp}.json`;
    const csvFileName = `consultor_${matricula}_${timestamp}.csv`;

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

    // 1. Salva JSON consolidado (com queries brutas e metadata)
    const jsonPayload = {
      matricula,
      startedAt: startDate.toISOString(),
      finishedAt: endDate.toISOString(),
      durationSeconds: elapsedSecTotal,
      durationFormatted,
      scrapedAt: endDate.toISOString(),
      totalQueriesCaptured: capturedQueries.length,
      totalRawRows: allRowsCombined.length,
      totalUniqueRows: uniqueRows.length,
      rows: uniqueRows,
      queries: capturedQueries
    };

    fs.writeFileSync(jsonFilePath, JSON.stringify(jsonPayload, null, 2), 'utf8');
    console.log(`✅ Arquivo JSON salvo com sucesso: ${jsonFilePath}`);

    // 2. Salva CSV consolidado
    const csvText = toCsv(uniqueRows);
    fs.writeFileSync(csvFilePath, csvText, 'utf8');
    console.log(`✅ Arquivo CSV salvo com sucesso: ${csvFilePath}`);

    console.log('\n====================================================');
    console.log('🎉 Scrape Tipo Consultor Concluído!');
    console.log(`🕒 Início: ${startDate.toLocaleString('pt-BR')}`);
    console.log(`🏁 Fim:    ${endDate.toLocaleString('pt-BR')}`);
    console.log(`⏱️  Tempo de Execução: ${durationFormatted} (${elapsedSecTotal}s)`);
    console.log(`📊 Queries capturadas: ${capturedQueries.length}`);
    console.log(`📋 Total de registros brutos: ${allRowsCombined.length}`);
    console.log(`✨ Total de registros únicos: ${uniqueRows.length}`);
    console.log(`📁 JSON: ${jsonFileName}`);
    console.log(`📁 CSV:  ${csvFileName}`);
    console.log('====================================================\n');

    return {
      status: 'Succeeded',
      matricula,
      startedAt: startDate.toISOString(),
      finishedAt: endDate.toISOString(),
      durationFormatted,
      durationSeconds: elapsedSecTotal,
      totalQueries: capturedQueries.length,
      totalRows: uniqueRows.length,
      jsonFile: jsonFilePath,
      csvFile: csvFilePath
    };

  } finally {
    await browser.close();
    console.log('[Consultor] Navegador encerrado.');
  }
}

module.exports = { runConsultorScrape, parseDSR, toCsv };
