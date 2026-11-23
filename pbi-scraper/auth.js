// auth.js
// Authenticates to avapro.ademicon.com.br using Puppeteer.
// Sends postMessage({ token: avaJwt }) into dashboardbi iframe.
// Captures the MWCToken (RSA encrypted JWT starting with MWCToken / ey...)
// used for PowerBI DAX queries.
// Integrates with tokenManager for in-memory token caching per matricula.

const puppeteer = require('puppeteer');
const axios     = require('axios');
const tokenManager = require('./tokenManager');
const { JobCancelledError, isAbortError, throwIfAborted } = require('./cancellationHelper');

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

/**
 * Pure helper to detect specific authentication errors (such as expired password or wrong credentials)
 * from visible text in the login page or API responses.
 *
 * @param {string|null|undefined} text
 * @returns {{ isFatalAuth: boolean, reason: string } | null}
 */
function detectLoginErrorText(text) {
  if (!text || typeof text !== 'string') return null;
  const lower = text.replace(/\s+/g, ' ').toLowerCase().trim();
  if (!lower) return null;

  // Ignore static login form instructions/prompts that contain the word "senha" or "matrícula"
  if (
    lower.includes('insira a sua matrícula') ||
    lower.includes('insira sua matrícula') ||
    lower.includes('insira a sua matricula') ||
    lower.includes('insira sua matricula') ||
    lower === 'esqueceu sua senha?' ||
    lower === 'esqueceu a senha?' ||
    lower === 'senha' ||
    lower === 'matrícula' ||
    lower === 'matricula'
  ) {
    return null;
  }

  if (
    lower.includes('senha expirada') || 
    lower.includes('redefina sua senha') ||
    lower.includes('redefinir a sua senha') ||
    lower.includes('sua senha expirou') ||
    lower.includes('senha vencida') ||
    lower.includes('trocar senha') ||
    lower.includes('troque sua senha') ||
    lower.includes('alterar senha') ||
    lower.includes('redefinir senha') ||
    lower.includes('redefinição de senha') ||
    lower.includes('senha temporária') ||
    lower.includes('limite de tentativas excedido') ||
    (lower.includes('senha') && lower.includes('expirou'))
  ) {
    return {
      isFatalAuth: true,
      reason: 'Senha expirada. Por favor, redefina sua senha no AVA PRO.'
    };
  }

  if (
    lower.includes('usuário ou senha') ||
    lower.includes('usuario ou senha') ||
    lower.includes('matrícula ou senha') ||
    lower.includes('matricula ou senha') ||
    lower.includes('credenciais inválidas') ||
    lower.includes('credencial inválida') ||
    lower.includes('credenciais incorretas') ||
    lower.includes('senha incorreta') ||
    lower.includes('senha errada') ||
    lower.includes('senha inválida') ||
    lower.includes('senha invalida') ||
    lower.includes('usuário inválido') ||
    lower.includes('usuario invalido') ||
    lower.includes('matrícula inválida') ||
    lower.includes('matricula invalida') ||
    lower.includes('dados inválidos') ||
    lower.includes('dados incorretos') ||
    lower.includes('não autorizado') ||
    lower.includes('nao autorizado') ||
    lower.includes('acesso negado') ||
    lower.includes('tentativas antes do bloqueio') ||
    (lower.includes('tentativa') && (lower.includes('bloqueio') || lower.includes('esqueceu') || lower.includes('restante')))
  ) {
    return {
      isFatalAuth: true,
      reason: 'Usuário ou senha inválida no AVA PRO.'
    };
  }

  return null;
}

/**
 * Pure helper that scans an array of candidate strings from the DOM or API
 * and returns the first fatal authentication error detected.
 *
 * @param {string[]} candidates
 * @returns {{ isFatalAuth: boolean, reason: string, rawText: string } | null}
 */
function findLoginErrorInCandidates(candidates) {
  if (!Array.isArray(candidates)) return null;
  for (const raw of candidates) {
    if (!raw || typeof raw !== 'string') continue;
    const trimmed = raw.replace(/\s+/g, ' ').trim();
    if (!trimmed) continue;
    const detected = detectLoginErrorText(trimmed);
    if (detected && detected.isFatalAuth) {
      return {
        ...detected,
        rawText: trimmed
      };
    }
  }
  return null;
}

/**
 * Collects candidate error strings from alert elements and body lines on the current page.
 *
 * @param {import('puppeteer').Page} page
 * @returns {Promise<string[]>}
 */
async function collectPageErrorCandidates(page) {
  return await page.evaluate(() => {
    const results = [];
    const selectors = [
      '.alert',
      '[role="alert"]',
      '.error',
      '.text-danger',
      '.invalid-feedback',
      '[class*="toast"]',
      '[class*="snack"]',
      '[class*="notification"]',
      'div[class*="error"]',
      'span[class*="error"]',
      'p[class*="error"]',
      'div[class*="red"]',
      'p[class*="red"]',
      'span[class*="red"]'
    ];
    for (const el of document.querySelectorAll(selectors.join(', '))) {
      if (el && el.textContent) {
        results.push(el.textContent.trim());
      }
    }
    if (document.body && document.body.innerText) {
      for (const line of document.body.innerText.split('\n')) {
        const t = line.trim();
        if (t) results.push(t);
      }
    }
    return results;
  });
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
        if (method === 'POST' && url.includes('QES/Query')) {
          try {
            const postData = req.postData();
            if (postData) {
              const fs = require('fs');
              const file = 'scratch/debug/all_qes_queries.json';
              let list = [];
              try { if (fs.existsSync(file)) list = JSON.parse(fs.readFileSync(file, 'utf8')); } catch (_) {}
              list.push(JSON.parse(postData));
              fs.writeFileSync(file, JSON.stringify(list, null, 2));
              console.log(`[Auth] Saved QES query #${list.length} to ${file}`);
            }
          } catch (_) {}
        }
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
async function getTokenFromLogin(matricula, password, store = null, signal = null, options = {}) {
  throwIfAborted(signal);
  const loginOnly = Boolean(options && options.loginOnly);

  const steps = [];
  addStep(steps, 'Iniciando navegador em modo automatizado...');

  const browser = await puppeteer.launch({
    headless: true,
    executablePath: process.env.PUPPETEER_EXECUTABLE_PATH || undefined,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
      '--disable-gpu',
      '--disable-software-rasterizer',
      '--disable-web-security',
      '--disable-features=IsolateOrigins,site-per-process',
      '--allow-running-insecure-content',
    ],
  });

  const onAbort = () => {
    browser.close().catch(() => {});
  };
  if (signal) {
    signal.addEventListener('abort', onAbort, { once: true });
  }

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

    let apiDetectedStore = null;
    page.on('response', async (res) => {
      try {
        const status = res.status();
        const url    = res.url();

        if (url.includes('bifrost') && url.includes('/login') && status === 200) {
          const body = await res.json().catch(() => null);
          if (body && body.token) {
            avaJwt = body.token;
            addStep(steps, `JWT Avapro capturado (${avaJwt.substring(0, 20)}...)`);
          } else if (body && (body.message || body.error)) {
            const msg = String(body.message || body.error).replace(/\s+/g, ' ').trim();
            interceptedAuthError = msg;
            addStep(steps, `Erro no login: ${msg}`);
          }
        }

        if (url.includes('modelsAndExploration') && status === 200) {
          const body = await res.json().catch(() => null);
          if (body) {
            try {
              require('fs').writeFileSync('scratch/debug/models.json', JSON.stringify(body, null, 2));
              console.log('[Auth] Saved PowerBI modelsAndExploration schema to scratch/debug/models.json');
            } catch (_) {}
          }
        }

        if (url.includes('users/me') && status === 200) {
          const body = await res.json().catch(() => null);
          if (body && typeof body === 'object') {
            const possible = body.unidade || body.loja || body.store || body.unidade_bi || body.unidadeNome || body.nm_unidade || body.unidadeOriginal;
            if (possible && typeof possible === 'string') {
              apiDetectedStore = possible.trim();
              addStep(steps, `Loja capturada via API users/me: "${apiDetectedStore}"`);
            }
          }
        }

        if (url.includes('bifrost') && url.includes('/login') && status !== 200) {
          let message = 'Usuário ou senha inválida';
          try {
            const json = await res.json().catch(() => null);
            if (json && json.message) message = json.message.replace(/\s+/g, ' ').trim();
            else if (json && json.error) message = json.error.replace(/\s+/g, ' ').trim();
          } catch (e) {}
          interceptedAuthError = message;
          addStep(steps, `Erro HTTP ${status} no login: ${message}`);
        } else if ((status === 401 || status === 403) && url.includes('bifrost')) {
          let message = 'Acesso não autorizado no AVA PRO';
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
    await page.goto(AVA_URL, { waitUntil: 'domcontentloaded', timeout: 25000 });
    addStep(steps, 'Página de login carregada.');

    addStep(steps, `Preenchendo credenciais — Matrícula: "${matricula}"...`);
    await page.waitForSelector('input[type="text"]', { visible: true, timeout: 15000 });
    await page.$eval('input[type="text"]', el => el.value = '');
    await page.click('input[type="text"]');
    await page.type('input[type="text"]', matricula, { delay: 10 });

    await page.waitForSelector('input[type="password"]', { visible: true, timeout: 15000 });
    await page.$eval('input[type="password"]', el => el.value = '');
    await page.click('input[type="password"]');
    await page.type('input[type="password"]', password, { delay: 10 });
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
      await submitBtn.asElement().click();
    } else {
      await page.keyboard.press('Enter');
    }

    // Aguarda rapidamente a resposta HTTP do login (avaJwt ou erro) ou alerta no DOM sem bloquear 8s em waitForNavigation
    let detectedDomError = null;
    const checkDeadline = Date.now() + 8000;
    while (!avaJwt && !interceptedAuthError && !detectedDomError && Date.now() < checkDeadline) {
      throwIfAborted(signal);
      const lowerUrl = page.url().toLowerCase();
      if (
        lowerUrl.includes('redefinir') || 
        lowerUrl.includes('trocar-senha') || 
        lowerUrl.includes('alterar-senha') || 
        lowerUrl.includes('primeiro-acesso')
      ) {
        const expMsg = 'Senha expirada. Por favor, redefina sua senha no AVA PRO.';
        addStep(steps, `[Falha de Autenticação Detectada na URL] ${expMsg}`);
        throw new AuthError('wrong-password', expMsg, steps, false, false);
      }

      try {
        const candidates = await collectPageErrorCandidates(page);
        const matched = findLoginErrorInCandidates(candidates);
        if (matched && matched.isFatalAuth) {
          detectedDomError = matched.reason;
          break;
        }
      } catch (_) {
        // A página pode estar navegando após login válido
      }
      await new Promise(r => setTimeout(r, 200));
    }

    const currentUrl = page.url();
    addStep(steps, `URL após login: ${currentUrl}`);

    if (interceptedAuthError) {
      const detectedIntercepted = detectLoginErrorText(interceptedAuthError);
      const finalMsg = detectedIntercepted ? detectedIntercepted.reason : interceptedAuthError;
      addStep(steps, `[Falha de Autenticação Detectada] ${finalMsg}`);
      throw new AuthError('wrong-password', finalMsg, steps, false, false);
    }

    if (detectedDomError) {
      addStep(steps, `[Falha de Autenticação Detectada na Página] ${detectedDomError}`);
      throw new AuthError('wrong-password', detectedDomError, steps, false, false);
    }

    // Se não obteve avaJwt, falha imediatamente como credencial inválida
    if (!avaJwt) {
      const fallbackReason = 'Usuário ou senha inválida no AVA PRO.';
      addStep(steps, `[Falha de Autenticação] ${fallbackReason}`);
      throw new AuthError('wrong-password', fallbackReason, steps, false, false);
    }

    loginSuccess = true;
    addStep(steps, 'Login bem-sucedido.');

    if (loginOnly) {
      if (!apiDetectedStore && !store) {
        const storeDeadline = Date.now() + 2500;
        while (!apiDetectedStore && Date.now() < storeDeadline) {
          throwIfAborted(signal);
          await new Promise(r => setTimeout(r, 150));
        }
      }
      const resolvedStore = apiDetectedStore || store || null;
      return {
        token: null,
        pbiToken: null,
        avaJwt,
        detectedStore: resolvedStore,
        authStatus: 'success',
        authMessage: 'Autenticação bem-sucedida no AVA PRO.',
        powerbiLoaded: false,
        loginSuccess: true,
        steps,
      };
    }

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

    // Helper to extract store from DOM
    const tryExtractStoreFromDom = async () => {
      try {
        return await page.evaluate(() => {
          const isValidStore = (str) => {
            if (!str || typeof str !== 'string') return false;
            const s = str.trim().toLowerCase();
            if (s.length < 3) return false;
            if (
              s.includes('selecionar') ||
              s.includes('selecione') ||
              s.includes('filtrar') ||
              s.includes('todas') ||
              s.includes('todos') ||
              s === 'unidade' ||
              s === 'loja' ||
              s.includes('por unidade') ||
              s.includes('por loja')
            ) {
              return false;
            }
            return true;
          };

          // 1. Target data-testid="select_loja"
          const selectLojaVal = document.querySelector('[data-testid="select_loja"] [data-slot="value"]')
                             || document.querySelector('[data-testid="select_loja"]');
          if (selectLojaVal && selectLojaVal.textContent) {
            const t = selectLojaVal.textContent.replace(/^Loja\s*/i, '').trim();
            if (isValidStore(t)) return t;
          }

          // 2. Target any data-slot="value"
          const dataSlotVals = Array.from(document.querySelectorAll('[data-slot="value"]'));
          for (const el of dataSlotVals) {
            const t = (el.textContent || '').replace(/^Loja\s*/i, '').trim();
            if (isValidStore(t) && (t.includes('-') || t.toUpperCase() === t)) {
              return t;
            }
          }

          // 3. Search header elements with text starting with "Loja"
          const elements = Array.from(document.querySelectorAll('span, div, header, p'));
          for (const el of elements) {
            const text = (el.textContent || '').trim();
            if (text.startsWith('Loja') && text.includes('-')) {
              const t = text.replace(/^Loja\s*/i, '').trim();
              if (isValidStore(t)) return t;
            }
          }

          return null;
        });
      } catch (e) {
        return null;
      }
    };

    // Attempt DOM store extraction right after home load with retry polling (up to 6s)
    let detectedStore = apiDetectedStore;
    if (!detectedStore) {
      addStep(steps, 'Aguardando renderização do cabeçalho da loja no DOM...');
      await page.waitForSelector('[data-testid="select_loja"], [data-slot="value"], header', { timeout: 8000 }).catch(() => {});
      for (let i = 0; i < 6; i++) {
        detectedStore = await tryExtractStoreFromDom();
        if (detectedStore) break;
        await new Promise(r => setTimeout(r, 1000));
      }
    }

    if (detectedStore) {
      addStep(steps, `Loja auto-detectada no AVA PRO: "${detectedStore}"`);
    }

    // ── Step 3: Enable API proxying ───────────────────────────────────────────
    addStep(steps, 'Ativando proxy apiv2 no Puppeteer...');
    await attachInterceptionToTarget(page, tokenRef, steps, avaJwt, requestCountRef);

    // ── Step 4: Navigate to /dashboard ─────────────────────────────────────────
    addStep(steps, `Navegando para ${DASHBOARD_URL}...`);
    await page.goto(DASHBOARD_URL, { waitUntil: 'networkidle2', timeout: 30000 }).catch(() => {});
    addStep(steps, `URL atual: ${page.url()}`);

    if (!detectedStore) {
      detectedStore = await tryExtractStoreFromDom();
      if (detectedStore) {
        addStep(steps, `Loja auto-detectada no dashboard AVA PRO: "${detectedStore}"`);
      }
    }

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
          if (signal && signal.aborted) {
            clearTimeout(timeout);
            clearInterval(checkInterval);
            reject(new JobCancelledError());
            return;
          }
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

    throwIfAborted(signal);
    addStep(steps, 'Autenticação e obtenção de MWCToken concluídas com sucesso.');

    // Save tokens in tokenManager cache
    tokenManager.setTokens(matricula, { avaJwt, pbiToken: tokenRef.value, detectedStore, password });

    return {
      token: tokenRef.value,
      pbiToken: tokenRef.value,
      avaJwt,
      detectedStore,
      authStatus:  'success',
      authMessage: 'Autenticação bem-sucedida',
      powerbiLoaded,
      loginSuccess,
      steps,
    };

  } catch (err) {
    if (isAbortError(err) || (signal && signal.aborted)) {
      throw new JobCancelledError();
    }
    if (err instanceof AuthError) throw err;
    const errMsg = err.message || 'Erro desconhecido';
    addStep(steps, `ERRO: ${errMsg}`);
    throw new AuthError('error', errMsg, steps, powerbiLoaded, loginSuccess);
  } finally {
    if (signal) {
      signal.removeEventListener('abort', onAbort);
    }
    await browser.close().catch(() => {});
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
 * @param {AbortSignal|null} [signal]
 */
async function getOrFetchTokens(matricula, password, store = null, forceRefresh = false, signal = null) {
  throwIfAborted(signal);
  if (!forceRefresh) {
    const cached = tokenManager.getTokens(matricula, password);
    if (cached && cached.pbiToken) {
      console.log(`[Auth] Reutilizando token PowerBI em cache para matrícula ${matricula}`);
      return {
        token: cached.pbiToken,
        pbiToken: cached.pbiToken,
        avaJwt: cached.avaJwt,
        detectedStore: cached.store || null,
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

  return await getTokenFromLogin(matricula, password, store, signal);
}

/**
 * Fast credential verification that only validates AVA PRO login (avaJwt)
 * without loading the PowerBI dashboard or waiting 120s for MWCToken.
 *
 * @param {string} matricula
 * @param {string} password
 * @param {string|null} [store]
 * @param {boolean} [forceRefresh]
 * @param {AbortSignal|null} [signal]
 */
async function verifyCredentialsOnly(matricula, password, store = null, forceRefresh = false, signal = null) {
  throwIfAborted(signal);
  if (!forceRefresh) {
    const cached = tokenManager.getTokens(matricula, password) || tokenManager.getTokens(`${matricula}_consultor`, password);
    if (cached && (cached.pbiToken || cached.avaJwt)) {
      return {
        token: cached.pbiToken || null,
        pbiToken: cached.pbiToken || null,
        avaJwt: cached.avaJwt || null,
        detectedStore: cached.store || store || null,
        authStatus: 'success',
        authMessage: 'Credenciais validadas em cache.',
        powerbiLoaded: Boolean(cached.pbiToken),
        loginSuccess: true,
        steps: [`[Auth] Credenciais para matrícula ${matricula} já validadas recentemente.`],
      };
    }
  }

  return await getTokenFromLogin(matricula, password, store, signal, { loginOnly: true });
}

module.exports = {
  getTokenFromLogin,
  getOrFetchTokens,
  verifyCredentialsOnly,
  AuthError,
  detectLoginErrorText,
  findLoginErrorInCandidates,
  collectPageErrorCandidates
};
