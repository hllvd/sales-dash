// auth.js
// Authenticates to avapro.ademicon.com.br using Puppeteer.
// Navigates to the dashboard, intercepts the first PowerBI "query" request
// and returns the Authorization token from that request and diagnostic steps.

const puppeteer = require('puppeteer');

const AVA_URL       = 'https://avapro.ademicon.com.br/';
const DASHBOARD_URL = 'https://avapro.ademicon.com.br/dashboard';
const QUERY_HOST    = 'pbidedicated.windows.net';

class AuthError extends Error {
  constructor(authStatus, message, steps, powerbiLoaded = false) {
    super(message);
    this.name = 'AuthError';
    this.authStatus = authStatus; // 'invalid-credentials' | 'timeout' | 'error'
    this.authMessage = message;
    this.steps = steps || [];
    this.powerbiLoaded = powerbiLoaded;
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

  try {
    const page = await browser.newPage();
    page.setDefaultNavigationTimeout(30000);
    page.setDefaultTimeout(30000);

    await page.setRequestInterception(true);

    // Intercept requests for token capture
    page.on('request', (req) => {
      const url = req.url();
      const headers = req.headers();
      const authHeader = headers['authorization'];

      if (url.includes(QUERY_HOST) && url.includes('/query') && authHeader) {
        if (!capturedToken) {
          capturedToken = authHeader;
          addStep(steps, 'Token de consulta PowerBI capturado com sucesso.');
        }
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
    addStep(steps, `Preenchendo credenciais (Matrícula: ${matricula})...`);
    await page.waitForSelector('input[type="text"]', { timeout: 15000 });
    await page.type('input[type="text"]', matricula, { delay: 50 });

    await page.waitForSelector('input[type="password"]', { timeout: 15000 });
    await page.type('input[type="password"]', password, { delay: 50 });
    addStep(steps, 'Credenciais preenchidas no formulário.');

    // Step 3: Click login button
    addStep(steps, 'Submetendo formulário de login...');
    try {
      await page.waitForSelector('button', { timeout: 15000 });
      const [button] = await page.$x("//button[contains(., 'Entrar') or contains(., 'entrar')]");
      if (button) {
        await button.click();
      } else {
        await page.click('button.inline-flex');
      }
    } catch (err) {
      addStep(steps, 'Clique padrão falhou, tentando seletor secundário...');
      await page.click('button.inline-flex');
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
      throw new AuthError('invalid-credentials', errMsg, steps, false);
    }

    // Step 4: Wait for navigation to dashboard
    addStep(steps, 'Aguardando navegação pós-login para o dashboard...');
    try {
      await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 30000 });
    } catch (navErr) {
      addStep(steps, 'Aviso: Navegação direta pós-login excedeu o tempo limítrofe, verificando URL atual...');
    }

    // Check again after navigation step
    const currentUrl = page.url();
    addStep(steps, `URL atual após login: ${currentUrl}`);

    if (currentUrl.includes('/login') || (!currentUrl.includes('/dashboard') && pageContent.includes('input[type="password"]'))) {
      const errMsg = interceptedAuthError || 'Usuário ou senha inválida';
      addStep(steps, `Permaneceu na página de login. Autenticação rejeitada: ${errMsg}`);
      throw new AuthError('invalid-credentials', errMsg, steps, false);
    }

    if (!currentUrl.includes('/dashboard')) {
      addStep(steps, `Navegando explicitamente para o dashboard (${DASHBOARD_URL})...`);
      await page.goto(DASHBOARD_URL, { waitUntil: 'networkidle2', timeout: 30000 });
    }
    addStep(steps, 'Dashboard do Avapro acessado.');

    // Step 5: Look for "Geral" link or main navigation links if present
    try {
      const links = await page.$$eval('a, button, nav item', els => 
        els.map(el => ({ text: el.innerText ? el.innerText.trim() : '', href: el.href || '' }))
           .filter(l => l.text.length > 0)
      );
      const geralLink = links.find(l => l.text.toLowerCase().includes('geral'));
      if (geralLink) {
        addStep(steps, `Link "Geral" encontrado: ${geralLink.text} (${geralLink.href || 'sem URL'})`);
      } else {
        const availableLinks = links.slice(0, 5).map(l => l.text).join(', ');
        addStep(steps, `Links principais carregados no dashboard: ${availableLinks || 'Nenhum link textual detectado'}`);
      }
    } catch (e) {
      addStep(steps, 'Verificação de links no dashboard concluída.');
    }

    // Step 6: Wait for PowerBI container
    addStep(steps, 'Verificando se o relatório PowerBI foi carregado na página...');
    try {
      await page.waitForSelector('iframe, .powerbi-container, .report-container', { timeout: 15000 });
      powerbiLoaded = true;
      addStep(steps, 'Relatório PowerBI detectado na estrutura da página.');
    } catch (e) {
      powerbiLoaded = false;
      addStep(steps, 'Aviso: Contêiner explícito do PowerBI não foi detectado dentro do tempo estipulado.');
    }

    // Step 7: Wait for query token
    if (!capturedToken) {
      addStep(steps, 'Aguardando requisição interna do PowerBI para capturar o token de acesso...');
      await new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
          reject(new AuthError('timeout', 'Tempo limite de 30s excedido aguardando token do PowerBI', steps, powerbiLoaded));
        }, 30000);
        
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
      steps
    };
  } catch (err) {
    if (err instanceof AuthError) {
      throw err;
    }
    const errMsg = err.message || 'Erro desconhecido durante autenticação';
    addStep(steps, `ERRO: ${errMsg}`);
    throw new AuthError('error', errMsg, steps, powerbiLoaded);
  } finally {
    await browser.close();
    console.log('[Auth] Browser closed.');
  }
}

module.exports = { getTokenFromLogin, AuthError };
