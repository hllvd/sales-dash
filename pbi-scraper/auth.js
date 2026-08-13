// auth.js
// Authenticates to avapro.ademicon.com.br using Puppeteer.
// Navigates to the dashboard, intercepts the first PowerBI "query" request
// and returns the Authorization token from that request and diagnostic steps.

const puppeteer = require('puppeteer');

const AVA_URL       = 'https://avapro.ademicon.com.br/';
const DASHBOARD_URL = 'https://avapro.ademicon.com.br/dashboard';
const QUERY_HOST    = 'pbidedicated.windows.net';

class AuthError extends Error {
  constructor(authStatus, message, steps, powerbiLoaded = false, loginSuccess = false) {
    super(message);
    this.name = 'AuthError';
    this.authStatus = authStatus; // 'invalid-credentials' | 'timeout' | 'error'
    this.authMessage = message;
    this.steps = steps || [];
    this.powerbiLoaded = powerbiLoaded;
    this.loginSuccess = loginSuccess;
  }
}

/**
 * Helper to push timestamped entries into steps log
 */
function addStep(steps, message) {
  const time = new Date().toLocaleTimeString('pt-BR');
  const entry = `[${time}] ${message}`;
  steps.push(entry);
  console.log(`[AuthStep] ${entry}`);
}

/**
 * Launches a browser, logs in with the given credentials,
 * waits for the dashboard to fire a PowerBI "query" request,
 * and returns token + diagnostic steps.
 *
 * @param {string} matricula - The user's matricula (username)
 * @param {string} password  - The user's password
 * @returns {Promise<{token: string, authStatus: string, authMessage: string, powerbiLoaded: boolean, steps: string[]}>}
 */
async function getTokenFromLogin(matricula, password) {
  const steps = [];
  addStep(steps, 'Iniciando navegador em modo automatizado...');

  const browser = await puppeteer.launch({
    headless: true,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
    ],
  });

  let capturedToken = null;
  let powerbiLoaded = false;
  let interceptedAuthError = null;
  let loginSuccess = false;

  try {
    const page = await browser.newPage();
    page.setDefaultNavigationTimeout(30000);
    page.setDefaultTimeout(30000);

    await page.setRequestInterception(true);

    // Known PowerBI/Azure/Ademicon API URL patterns that carry bearer tokens
    const PBI_HOSTS = [
      'pbidedicated.windows.net',
      'analysis.windows.net',
      'api.powerbi.com',
      'powerbi.com',
      'msit.pbidedicated.windows.net',
      'dashboardbi.ademicon.com.br',
      'ademicon.com.br',
    ];

    // Intercept requests for token capture
    page.on('request', (req) => {
      const url = req.url();
      const headers = req.headers();
      const authHeader = headers['authorization'] || headers['Authorization'] || headers['x-powerbi-token'] || headers['x-access-token'];
      const postData = req.postData() || '';

      const isPbiUrl = PBI_HOSTS.some(h => url.includes(h)) || url.includes('/api/') || url.includes('query');

      if (isPbiUrl) {
        console.log(`[PBI Request] ${req.method()} ${url.substring(0, 120)} | auth=${authHeader ? authHeader.substring(0, 40) + '...' : 'none'}`);
      }

      // Capture ANY Bearer or JWT authorization token on target domains or API routes
      if (!capturedToken && authHeader && (authHeader.startsWith('Bearer ') || authHeader.length > 20)) {
        capturedToken = authHeader;
        addStep(steps, `Token de autorização capturado via requisição [${req.method()}]: ${url.substring(0, 80)}...`);
      }

      // Also capture token embedded in POST body if present
      if (!capturedToken && postData && (postData.includes('"AccessToken"') || postData.includes('"token"'))) {
        try {
          const body = JSON.parse(postData);
          const t = body.AccessToken || body.accessToken || body.token;
          if (t && typeof t === 'string' && t.length > 20) {
            capturedToken = t.startsWith('Bearer ') ? t : `Bearer ${t}`;
            addStep(steps, `Token capturado via corpo da requisição POST (${url.substring(0, 60)}...).`);
          }
        } catch (e) {}
      }

      req.continue();
    });

    // Intercept response to catch 403 or authentication API errors
    page.on('response', async (res) => {
      try {
        const status = res.status();
        const url = res.url();
        if (status === 401 || status === 403 || url.includes('/login') || url.includes('/api/auth')) {
          if (status === 403 || status === 401) {
            const bodyText = await res.text().catch(() => '');
            let message = 'Usuário ou senha inválida';
            try {
              const json = JSON.parse(bodyText);
              if (json.message) {
                message = json.message.replace(/\s+/g, ' ').trim();
              }
            } catch (e) {
              if (bodyText && bodyText.includes('tentativas')) {
                message = bodyText.replace(/\s+/g, ' ').trim();
              }
            }
            interceptedAuthError = message;
            addStep(steps, `Erro de autenticação HTTP ${status}: ${message}`);
          }
        }
      } catch (err) {
        // Ignore response reading exceptions
      }
    });

    // Step 1: Navigate to login page
    addStep(steps, `Navegando para a página de login (${AVA_URL})...`);
    await page.goto(AVA_URL, { waitUntil: 'networkidle2', timeout: 30000 });
    addStep(steps, 'Página de login carregada com sucesso.');

    // Step 2: Fill in credentials
    addStep(steps, `Preenchendo credenciais — Matrícula: "${matricula}", Senha recebida no teste: "${password}" (tamanho: ${password ? password.length : 0})...`);
    
    await page.waitForSelector('input[type="text"]', { timeout: 15000 });
    await page.$eval('input[type="text"]', el => el.value = '');
    await page.click('input[type="text"]');
    await page.type('input[type="text"]', matricula, { delay: 30 });

    await page.waitForSelector('input[type="password"]', { timeout: 15000 });
    await page.$eval('input[type="password"]', el => el.value = '');
    await page.click('input[type="password"]');
    await page.type('input[type="password"]', password, { delay: 30 });
    addStep(steps, 'Credenciais preenchidas nos campos de texto.');

    // Step 3: Click login button
    addStep(steps, 'Submetendo formulário de login...');
    let buttonClicked = false;
    try {
      const buttonHandle = await page.evaluateHandle(() => {
        const buttons = Array.from(document.querySelectorAll('button'));
        return buttons.find(b => {
          const txt = (b.innerText || b.textContent || '').toLowerCase().trim();
          return txt.includes('entrar') || b.type === 'submit';
        }) || buttons[0] || null;
      });

      if (buttonHandle && buttonHandle.asElement()) {
        addStep(steps, 'Botão "Entrar" localizado no formulário DOM. Executando clique...');
        await buttonHandle.asElement().click();
        buttonClicked = true;
      }
    } catch (err) {
      addStep(steps, `Localização avançada de botão retornou erro: ${err.message}`);
    }

    if (!buttonClicked) {
      try {
        addStep(steps, 'Tentando clique alternativo no seletor button.inline-flex...');
        await page.click('button.inline-flex');
        buttonClicked = true;
      } catch (err) {
        addStep(steps, 'Pressionando tecla Enter no campo de senha...');
        await page.focus('input[type="password"]');
        await page.keyboard.press('Enter');
      }
    }

    // Wait short time to allow potential error messages or 403 API response to trigger
    await page.waitForTimeout ? page.waitForTimeout(2000) : new Promise(r => setTimeout(r, 2000));

    // Check for credential error on page DOM or intercepted response
    const pageContent = await page.content();
    if (interceptedAuthError || pageContent.includes('Usuário ou senha inválida') || pageContent.includes('tentativas')) {
      let errMsg = interceptedAuthError || 'Usuário ou senha inválida';
      
      // Try to extract exact attempts message from page DOM if present
      const attemptsMatch = pageContent.match(/Você ainda possui mais \d+ tentativas[^\n<"]*/i);
      if (attemptsMatch) {
        const attemptsText = attemptsMatch[0].trim();
        if (!errMsg.includes(attemptsText)) {
          errMsg = `${errMsg} — ${attemptsText}`;
        }
      }
      addStep(steps, `FALHA DE AUTENTICAÇÃO DETECTADA: ${errMsg}`);
      throw new AuthError('invalid-credentials', errMsg, steps, false, false);
    }

    // Step 4: Wait for navigation to dashboard (SPA will update URL via history API)
    addStep(steps, 'Aguardando navegação pós-login para o dashboard...');
    try {
      await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 30000 });
    } catch (navErr) {
      addStep(steps, 'Aviso: Navegação direta pós-login excedeu o tempo limítrofe, verificando URL atual...');
    }

    // Re-read URL and live page content AFTER navigation (SPA may have updated both)
    const currentUrl = page.url();
    const livePageContent = await page.content();
    addStep(steps, `URL atual após login: ${currentUrl}`);

    // Only treat an explicit /login URL as failure.
    // SPAs typically redirect to root "/" or another route on success — NOT back to /login.
    if (currentUrl.includes('/login')) {
      const errMsg = interceptedAuthError || 'Usuário ou senha inválida';
      addStep(steps, `Permaneceu na página de login. Autenticação rejeitada: ${errMsg}`);
      throw new AuthError('invalid-credentials', errMsg, steps, false, false);
    }

    loginSuccess = true;
    addStep(steps, `Login bem-sucedido. Rota atual: ${currentUrl}`);

    // Step 5: Navigate directly to Dashboard URL (https://avapro.ademicon.com.br/dashboard)
    if (!currentUrl.includes('/dashboard')) {
      addStep(steps, `Navegando diretamente para o dashboard (${DASHBOARD_URL})...`);
      try {
        await page.goto(DASHBOARD_URL, { waitUntil: 'networkidle2', timeout: 30000 });
      } catch (e) {
        addStep(steps, `Aviso ao navegar para dashboard: ${e.message}`);
      }
    }
    addStep(steps, 'Dashboard do Avapro acessado. Aguardando renderização do relatório PowerBI...');
    await new Promise(r => setTimeout(r, 3000));

    // Step 6: Verify PowerBI report container / iframe presence
    try {
      await page.waitForSelector('iframe, .powerbi-container, .report-container, [class*="powerbi"], [class*="report"]', { timeout: 15000 });
      powerbiLoaded = true;
      addStep(steps, 'Relatório PowerBI detectado na estrutura da página.');
      
      const iframeSrcs = await page.$$eval('iframe', frames => frames.map(f => f.src).filter(Boolean));
      if (iframeSrcs.length > 0) {
        iframeSrcs.forEach((src, i) => addStep(steps, `iframe[${i}] src: ${src.substring(0, 120)}`));
      } else {
        addStep(steps, 'Nenhum iframe com atributo src detectado.');
      }
    } catch (e) {
      powerbiLoaded = false;
      addStep(steps, 'Aviso: Contêiner explícito do PowerBI não foi detectado dentro do tempo estipulado.');
    }

    // Step 8: Wait for query token (60s timeout)
    if (!capturedToken) {
      addStep(steps, 'Aguardando requisição interna do PowerBI para capturar o token de acesso (tempo limite: 60s)...');
      await new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
          reject(new AuthError('timeout', 'Tempo limite de 60s excedido aguardando token do PowerBI.', steps, powerbiLoaded, loginSuccess));
        }, 60000);
        
        const interval = setInterval(() => {
          if (capturedToken) {
            clearTimeout(timeout);
            clearInterval(interval);
            resolve();
          }
        }, 500);
      });
    }

    addStep(steps, 'Autenticação e obtenção de token concluídas com sucesso.');
    return {
      token: capturedToken,
      authStatus: 'success',
      authMessage: 'Autenticação bem-sucedida',
      powerbiLoaded,
      loginSuccess,
      steps
    };
  } catch (err) {
    if (err instanceof AuthError) {
      throw err;
    }
    const errMsg = err.message || 'Erro desconhecido durante autenticação';
    addStep(steps, `ERRO: ${errMsg}`);
    throw new AuthError('error', errMsg, steps, powerbiLoaded, loginSuccess);
  } finally {
    await browser.close();
    console.log('[Auth] Browser closed.');
  }
}

module.exports = { getTokenFromLogin, AuthError };
