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
 * Reuses PowerBI SemanticQuery response structure parser.
 */
function parseDSR(data) {
  const result = data?.results?.[0]?.result?.data;
  if (!result) return [];

  const descriptor = result.descriptor;
  const ds = result.dsr?.DS?.[0];
  if (!ds) return [];

  const selectItems = descriptor?.Select || [];
  const friendlyName = {};
  const selectIndexMap = {};

  selectItems.forEach((item, idx) => {
    friendlyName[item.Value] = item.NativeReferenceName || item.Name;
    selectIndexMap[item.Value] = idx;
  });

  const dicts = ds.ValueDicts || {};
  const ph = ds.PH || [];

  const allRows = [];

  for (const group of ph) {
    const dmKey = group.DM1 ? 'DM1' : group.DM0 ? 'DM0' : null;
    if (!dmKey) continue;

    const entries = group[dmKey];
    let schemaRow = null;
    let prev = [];

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
          const alias = s.N;
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
        const C = entry.C;
        const R = entry.R || 0;
        const resolved = [...prev];
        let ci = 0;

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
          const alias = s.N;
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

  console.log('====================================================');
  console.log('🚀 Iniciando Scrape Tipo Consultor');
  console.log(`👤 Matrícula: ${matricula}`);
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

    // Also attach to new target pages/popups if created
    browser.on('targetcreated', async (target) => {
      try {
        const targetPage = await target.page();
        if (targetPage && targetPage !== page) {
          targetPage.on('response', handleResponse);
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

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const jsonFileName = `consultor_${matricula}_${timestamp}.json`;
    const csvFileName = `consultor_${matricula}_${timestamp}.csv`;

    const jsonFilePath = path.join(outPath, jsonFileName);
    const csvFilePath = path.join(outPath, csvFileName);

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
      // Cria uma chave única baseada em contrato/cota ou conteúdo da linha
      const uniqueKey = row['Cota'] ||
                        row['Contrato'] ||
                        row['tbl_cotas.id_cota'] ||
                        row['id_cota'] ||
                        row['CNPJ'] ||
                        JSON.stringify(row);

      if (!seenRowKeys.has(uniqueKey)) {
        seenRowKeys.add(uniqueKey);
        uniqueRows.push(row);
      }
    });

    // 1. Salva JSON consolidado (com queries brutas e metadata)
    const jsonPayload = {
      matricula,
      scrapedAt: new Date().toISOString(),
      totalQueriesCaptured: capturedQueries.length,
      totalRawRows: allRowsCombined.length,
      totalUniqueRows: uniqueRows.length,
      queries: capturedQueries
    };

    fs.writeFileSync(jsonFilePath, JSON.stringify(jsonPayload, null, 2), 'utf8');
    console.log(`✅ Arquivo JSON salvo com sucesso: ${jsonFilePath}`);

    // 2. Salva CSV consolidado
    const csvText = toCsv(uniqueRows.length > 0 ? uniqueRows : allRowsCombined);
    fs.writeFileSync(csvFilePath, csvText, 'utf8');
    console.log(`✅ Arquivo CSV salvo com sucesso: ${csvFilePath}`);

    console.log('\n====================================================');
    console.log('🎉 Scrape Tipo Consultor Concluído!');
    console.log(`📊 Queries capturadas: ${capturedQueries.length}`);
    console.log(`📋 Total de registros brutos: ${allRowsCombined.length}`);
    console.log(`✨ Total de registros únicos: ${uniqueRows.length}`);
    console.log(`📁 JSON: ${jsonFileName}`);
    console.log(`📁 CSV:  ${csvFileName}`);
    console.log('====================================================\n');

    return {
      status: 'Succeeded',
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
