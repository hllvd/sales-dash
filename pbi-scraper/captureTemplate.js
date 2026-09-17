// captureTemplate.js
// Captures the exact SemanticQueryDataShapeCommand payload for the Consultor dashboard ("2 Rel Carteira")
// Run with: node captureTemplate.js [matricula] [password]

require('dotenv').config();
const fs = require('fs');
const path = require('path');
const puppeteer = require('puppeteer');

const AVA_URL = 'https://avapro.ademicon.com.br/';
const DASHBOARD_CONSULTOR_URL = 'https://avapro.ademicon.com.br/dashboard/consultor';

async function captureTemplate(matricula, password, stepLogger = null) {
  const addStep = (msg) => {
    const line = `[${new Date().toLocaleTimeString('pt-BR')}] ${msg}`;
    console.log(line);
    if (typeof stepLogger === 'function') stepLogger(line);
    else if (Array.isArray(stepLogger)) stepLogger.push(line);
  };

  const tplDir = path.resolve(__dirname, 'templates');
  if (!fs.existsSync(tplDir)) fs.mkdirSync(tplDir, { recursive: true });
  const tplFile = path.join(tplDir, 'consultorQueryTemplate.json');

  const isHeadless = process.env.HEADLESS === 'false' || process.env.HEADLESS === '0' ? false : true;
  const tokenFile = path.join(tplDir, 'consultorToken.json');

  addStep(`[Capture] Iniciando Puppeteer (Headless: ${isHeadless})...`);
  const browser = await puppeteer.launch({
    headless: isHeadless ? 'new' : false,
    executablePath: process.env.PUPPETEER_EXECUTABLE_PATH || undefined,
    defaultViewport: { width: 1440, height: 900 },
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-gpu',
      '--disable-software-rasterizer',
      '--disable-web-security',
      '--disable-features=IsolateOrigins,site-per-process',
      '--allow-running-insecure-content',
    ]
  });

  let capturedTemplate = fs.existsSync(tplFile);
  let capturedToken = null;

  try {
    const page = await browser.newPage();
    await page.setUserAgent('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36');

    let avaJwt = null;

    const onRequest = (req) => {
      try {
        const url = req.url();
        const method = req.method();
        const isQesQuery = (url.includes('windows.net') || url.includes('pbidedicated')) &&
                           url.includes('workloads/QES/QueryExecutionService') &&
                           url.includes('/query');

        if (isQesQuery) {
          const headers = req.headers();
          const auth = headers['authorization'] || headers['Authorization'];
          if (auth && (auth.startsWith('MWCToken') || auth.startsWith('ey'))) {
            capturedToken = auth.startsWith('MWCToken') ? auth : `MWCToken ${auth}`;
            fs.writeFileSync(tokenFile, JSON.stringify({ token: capturedToken, capturedAt: Date.now() }, null, 2), 'utf8');
            console.log(`[Capture] 🎯 MWCToken específico de Consultor capturado!`);
          }
        }

        if (isQesQuery && method === 'POST' && !capturedTemplate) {
          const postData = req.postData();
          if (postData) {
            try {
              const parsed = JSON.parse(postData);
              const sel = parsed?.queries?.[0]?.Query?.Commands?.[0]?.SemanticQueryDataShapeCommand?.Query?.Select;
              if (Array.isArray(sel) && sel.length >= 50) {
                fs.writeFileSync(tplFile, postData, 'utf8');
                console.log(`\n🎉 [Capture] SUCESSO! Template da tabela (55 colunas) capturado e salvo em:\n  ${tplFile}\n`);
                capturedTemplate = true;
              }
            } catch (_) {}
          }
        }
      } catch (_) {}
    };

    const onResponse = async (res) => {
      try {
        const url = res.url();
        if (url.includes('bifrost') && url.includes('/login') && res.status() === 200) {
          const body = await res.json().catch(() => null);
          if (body?.token) {
            avaJwt = body.token;
            console.log(`[Capture] AVA JWT capturado: ${avaJwt.substring(0, 20)}...`);
          }
        }
      } catch (_) {}
    };

    page.on('request', onRequest);
    page.on('response', onResponse);

    browser.on('targetcreated', async (target) => {
      try {
        const tPage = await target.page();
        if (tPage && tPage !== page) {
          tPage.on('request', onRequest);
          tPage.on('response', onResponse);
        }
      } catch (_) {}
    });

    addStep(`[Capture] Acessando tela de login: ${AVA_URL}...`);
    await page.goto(AVA_URL, { waitUntil: 'networkidle2', timeout: 35000 });

    addStep('[Capture] Preenchendo credenciais...');
    await page.waitForSelector('input[type="text"]', { timeout: 15000 });
    await page.type('input[type="text"]', matricula, { delay: 25 });
    await page.waitForSelector('input[type="password"]', { timeout: 15000 });
    await page.type('input[type="password"]', password, { delay: 25 });

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

    addStep('[Capture] Login efetuado com sucesso. Navegando para /dashboard/consultor...');
    await page.goto(DASHBOARD_CONSULTOR_URL, { waitUntil: 'networkidle2', timeout: 35000 }).catch(() => {});

    // Token postMessage loop into iframe
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

    // Aguarda até capturar token e template ou timeout de 35s
    addStep('[Capture] Aguardando MWCToken e template de Consultor (limite 35s)...');
    const startWait = Date.now();
    while ((!capturedToken || !capturedTemplate) && (Date.now() - startWait) < 35000) {
      await new Promise(r => setTimeout(r, 500));
    }

    clearInterval(tokenInterval);

    if (!capturedToken) {
      throw new Error('Timeout: Não foi possível interceptar o MWCToken de Consultor em 35s.');
    }

    addStep('[Capture] MWCToken e template interceptados com sucesso.');

    return {
      templatePath: tplFile,
      token: capturedToken
    };
  } finally {
    await browser.close();
    addStep('[Capture] Navegador Puppeteer fechado.');
  }
}

async function main() {
  const args = process.argv.slice(2);
  const matricula = args[0] || process.env.AVAPRO_MATRICULA;
  const password = args[1] || process.env.AVAPRO_PASSWORD;

  if (!matricula || !password) {
    console.error('❌ Uso: node captureTemplate.js <matricula> <senha>');
    process.exit(1);
  }

  try {
    await captureTemplate(matricula, password);
    console.log('✅ Template capturado com sucesso!');
  } catch (err) {
    console.error('❌ Erro:', err.message);
    process.exit(1);
  }
}

if (require.main === module) {
  main();
}

module.exports = { captureTemplate };
