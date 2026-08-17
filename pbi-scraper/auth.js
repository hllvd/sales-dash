// auth.js
// Authenticates to avapro.ademicon.com.br using Puppeteer.
// Sends postMessage({ token: avaJwt }) into dashboardbi iframe.
// Captures the MWCToken (RSA encrypted JWT starting with MWCToken / ey...)
// used for PowerBI DAX queries.
// Integrates with tokenManager for in-memory token caching per matricula.

const puppeteer = require('puppeteer');
const axios     = require('axios');
const tokenManager = require('./tokenManager');

const AVA_URL       = 'https://avapro.ademicon.com.br/';
const DASHBOARD_URL = 'https://avapro.ademicon.com.br/dashboard';

class AuthError extends Error {
  constructor(authStatus, message, steps, powerbiLoaded = false, loginSuccess = false) {
    super(message);
    this.name        = 'AuthError';
    this.authStatus  = authStatus;
    this.authMessage = message;
    this.steps       = steps || [];
    this.powerbiLoaded = powerbiLoaded;
    this.loginSuccess  = loginSuccess;
  }
}

function addStep(steps, message) {
  const time  = new Date().toLocaleTimeString('pt-BR');
  const entry = `[${time}] ${message}`;
  steps.push(entry);
  console.log(`[AuthStep] ${entry}`);
}

/** True when the URL is the PowerBI dedicated query endpoint */
function isPbiQueryUrl(url) {
  const lower = url.toLowerCase();
  return (lower.includes('windows.net') || lower.includes('pbidedicated')) && lower.includes('query');
}

/**
 * Validates and normalises an MWCToken candidate string.
 * Specifically returns MWCToken formatted strings for DAX query execution.
 */
function parseMwcToken(candidate) {
  if (!candidate || typeof candidate !== 'string') return null;
  const s = candidate.trim();
  if (s.length < 100) return null;
  if (s.includes('<') || s.includes('function') || s.includes('var ')) return null;

  if (s.startsWith('MWCToken ')) return s;
  // Raw RSA-OAEP encrypted JWT
  if (s.startsWith('ey') && s.length > 300) return `MWCToken ${s}`;
  return null;
}

/**
 * Attaches request interception & proxying to a page/target.
 */
async function attachInterceptionToTarget(pageObj, tokenRef, steps, avaJwt, requestCountRef) {
  try {
    await pageObj.setRequestInterception(true);
  } catch (e) {
    return; // Already intercepted or closed
  }

  pageObj.on('request', async (req) => {
    if (requestCountRef) requestCountRef.count++;
    const url     = req.url();
    const method  = req.method();
    const headers = req.headers();
    const auth    = headers['authorization'] || headers['Authorization'] || (avaJwt ? `Bearer ${avaJwt}` : '');

    try {
      if (url.includes('windows.net') || url.includes('powerbi') || url.includes('pbidedicated')) {
        console.log(`[PBI Req] ${method} ${url.substring(0, 140)} | auth=${auth ? auth.substring(0, 30) + '...' : 'none'}`);
      }

      // Proxy apiv2 GET calls
      if (url.startsWith('https://apiv2.ademitech.com.br/') && method === 'GET') {
        const res = await axios.get(url, {
          headers: {
            'Authorization': auth,
            'Accept': headers['accept'] || 'application/json, text/plain, */*',
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)',
          },
          timeout: 15000,
          validateStatus: () => true,
        });
        const body = typeof res.data === 'object' ? JSON.stringify(res.data) : String(res.data);
        req.respond({
          status: res.status,
          contentType: res.headers['content-type'] || 'application/json',
          body,
          headers: {
            'Access-Control-Allow-Origin': '*',
            'Access-Control-Allow-Headers': 'Authorization, Content-Type',
          },
        });
        return;
      }

      // Capture MWCToken specifically (from /query endpoint or any request with MWCToken/ey...)
      const pbiAuth = headers['authorization'] || headers['Authorization'];
      if (pbiAuth && !tokenRef.value) {
        const formatted = parseMwcToken(pbiAuth);
        if (formatted) {
          tokenRef.value = formatted;
          addStep(steps, `[Intercept] Token PowerBI MWCToken (${formatted.substring(0, 25)}...) capturado de ${url.substring(0, 100)}`);
        }
      }

      req.continue();
    } catch (e) {
      try { req.continue(); } catch (_) {}
    }
  });
}

/**
 * Executes login via Puppeteer, obtains fresh tokens, and saves them to tokenManager.
 *
 * @param {string} matricula
 * @param {string} password
 * @param {string} store
 */
async function getTokenFromLogin(matricula, password, store = 'BALNEARIO CAMBORIU - SC') {
  const steps = [];
  addStep(steps, 'Iniciando navegador em modo automatizado...');

  const browser = await puppeteer.launch({
    headless: true,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-web-security',
      '--disable-features=IsolateOrigins,site-per-process',
      '--allow-running-insecure-content',
    ],
  });

  const tokenRef        = { value: null };
  const requestCountRef = { count: 0 };
  let powerbiLoaded     = false;
  let interceptedAuthError = null;
  let loginSuccess      = false;
  let avaJwt            = null;

  try {
    const page = await browser.newPage();
    await page.setUserAgent('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/127.0.0.0 Safari/537.36');
    await page.setViewport({ width: 1366, height: 768 });
    page.setDefaultNavigationTimeout(30000);
    page.setDefaultTimeout(30000);

    browser.on('targetcreated', async (target) => {
      try {
        const targetPage = await target.page();
        if (targetPage && targetPage !== page) {
          addStep(steps, `Novo target detectado: ${target.url().substring(0, 80) || '(blank)'}`);
          await attachInterceptionToTarget(targetPage, tokenRef, steps, avaJwt, requestCountRef);
        }
      } catch (e) { /* ignore */ }
    });

    page.on('response', async (res) => {
      try {
        const status = res.status();
        const url    = res.url();

        if (url.includes('bifrost') && url.includes('/login') && status === 200) {
          const body = await res.json().catch(() => null);
          if (body && body.token) {
            avaJwt = body.token;
            addStep(steps, `JWT Avapro capturado (${avaJwt.substring(0, 20)}...)`);
          }
        }

        if ((status === 401 || status === 403) && url.includes('bifrost')) {
          let message = 'Usuário ou senha inválida';
          try {
            const json = await res.json().catch(() => null);
            if (json && json.message) message = json.message.replace(/\s+/g, ' ').trim();
          } catch (e) {}
          interceptedAuthError = message;
          addStep(steps, `Erro HTTP ${status}: ${message}`);
        }
      } catch (err) { /* ignore */ }
    });

    // ── Step 1: Login ──────────────────────────────────────────────────────────
    addStep(steps, `Navegando para ${AVA_URL}...`);
    await page.goto(AVA_URL, { waitUntil: 'networkidle2', timeout: 30000 });
    addStep(steps, 'Página de login carregada.');

    addStep(steps, `Preenchendo credenciais — Matrícula: "${matricula}"...`);
    await page.waitForSelector('input[type="text"]', { timeout: 15000 });
    await page.$eval('input[type="text"]', el => el.value = '');
    await page.click('input[type="text"]');
    await page.type('input[type="text"]', matricula, { delay: 30 });

    await page.waitForSelector('input[type="password"]', { timeout: 15000 });
    await page.$eval('input[type="password"]', el => el.value = '');
    await page.click('input[type="password"]');
    await page.type('input[type="password"]', password, { delay: 30 });
    addStep(steps, 'Credenciais preenchidas.');

    addStep(steps, 'Submetendo formulário de login...');
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
        submitBtn.asElement().click(),
      ]);
    } else {
      await page.keyboard.press('Enter');
    }

    const currentUrl = page.url();
    addStep(steps, `URL após login: ${currentUrl}`);
    if (interceptedAuthError) throw new AuthError('wrong-password', interceptedAuthError, steps, false, false);
    if (currentUrl.includes('/login')) throw new AuthError('wrong-password', 'Permaneceu na página de login.', steps, false, false);
    loginSuccess = true;
    addStep(steps, 'Login bem-sucedido.');

    // ── Step 2: Wait for users/me ──────────────────────────────────────────────
    addStep(steps, 'Aguardando sessão (bifrost/users/me)...');
    try {
      await page.waitForResponse(
        res => res.url().includes('users/me') && res.status() === 200,
        { timeout: 15000 }
      );
      addStep(steps, 'Sessão validada.');
    } catch (e) {
      addStep(steps, 'Aviso: users/me não respondeu. Continuando...');
    }

    // ── Step 3: Enable API proxying ───────────────────────────────────────────
    addStep(steps, 'Ativando proxy apiv2 no Puppeteer...');
    await attachInterceptionToTarget(page, tokenRef, steps, avaJwt, requestCountRef);

    // ── Step 4: Navigate to /dashboard ─────────────────────────────────────────
    addStep(steps, `Navegando para ${DASHBOARD_URL}...`);
    await page.goto(DASHBOARD_URL, { waitUntil: 'networkidle2', timeout: 30000 }).catch(() => {});
    addStep(steps, `URL atual: ${page.url()}`);

    // ── Step 5: Wait for SPA to render iframe and postMessage JWT into it ──────
    addStep(steps, 'Aguardando iframe no dashboard...');
    try {
      await page.waitForSelector('iframe', { timeout: 15000 });
      powerbiLoaded = true;

      // Continuously dispatch postMessage({ token: avaJwt }) into iframe every 1s
      addStep(steps, 'Enviando postMessage com JWT para o iframe do dashboardbi...');
      const interval = setInterval(async () => {
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

      page.once('close', () => clearInterval(interval));

    } catch (e) {
      addStep(steps, 'Aviso: SPA não rendeu o iframe no tempo esperado.');
    }

    // ── Step 6: Wait up to 120s for MWCToken ─────────────────────────────────
    if (!tokenRef.value) {
      addStep(steps, 'Aguardando requisição PowerBI MWCToken /query (tempo limite: 120s)...');
      let elapsed = 0;
      await new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
          reject(new AuthError(
            'timeout',
            `Tempo limite de 120s excedido aguardando MWCToken. (${requestCountRef.count} requisições, ${page.frames().length} frames).`,
            steps, powerbiLoaded, loginSuccess
          ));
        }, 120000);

        const checkInterval = setInterval(() => {
          elapsed++;
          if (tokenRef.value) {
            clearTimeout(timeout);
            clearInterval(checkInterval);
            resolve();
          } else if (elapsed % 10 === 0) {
            addStep(steps, `[${elapsed}s] Aguardando MWCToken... Req: ${requestCountRef.count}, Frames: ${page.frames().length}`);
          }
        }, 1000);
      });
    } else {
      addStep(steps, 'Token MWCToken já capturado.');
    }

    addStep(steps, 'Autenticação e obtenção de MWCToken concluídas com sucesso.');

    // Save tokens in tokenManager cache
    tokenManager.setTokens(matricula, { avaJwt, pbiToken: tokenRef.value });

    return {
      token: tokenRef.value,
      pbiToken: tokenRef.value,
      avaJwt,
      authStatus:  'success',
      authMessage: 'Autenticação bem-sucedida',
      powerbiLoaded,
      loginSuccess,
      steps,
    };

  } catch (err) {
    if (err instanceof AuthError) throw err;
    const errMsg = err.message || 'Erro desconhecido';
    addStep(steps, `ERRO: ${errMsg}`);
    throw new AuthError('error', errMsg, steps, powerbiLoaded, loginSuccess);
  } finally {
    await browser.close();
    console.log('[Auth] Browser closed.');
  }
}

/**
 * Returns valid tokens for a given matricula.
 * If forceRefresh is false and cached tokens exist, returns cached tokens immediately.
 * Otherwise, launches Puppeteer to fetch fresh tokens.
 *
 * @param {string} matricula
 * @param {string} password
 * @param {string} store
 * @param {boolean} forceRefresh
 */
async function getOrFetchTokens(matricula, password, store = 'BALNEARIO CAMBORIU - SC', forceRefresh = false) {
  if (!forceRefresh) {
    const cached = tokenManager.getTokens(matricula);
    if (cached && cached.pbiToken) {
      console.log(`[Auth] Reutilizando token PowerBI em cache para matrícula ${matricula}`);
      return {
        token: cached.pbiToken,
        pbiToken: cached.pbiToken,
        avaJwt: cached.avaJwt,
        authStatus: 'success',
        authMessage: 'Token reutilizado do cache em memória',
        powerbiLoaded: true,
        loginSuccess: true,
        steps: [`[Auth] Token reutilizado do cache em memória para matrícula ${matricula}.`],
      };
    }
  }

  if (forceRefresh) {
    console.log(`[Auth] Forçando nova autenticação (cache ignorado/inválido) para matrícula ${matricula}...`);
    tokenManager.invalidateTokens(matricula);
  }

  return await getTokenFromLogin(matricula, password, store);
}

module.exports = { getTokenFromLogin, getOrFetchTokens, AuthError };
