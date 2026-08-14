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
      '--disable-web-security',
      '--disable-features=IsolateOrigins,site-per-process',
      '--allow-running-insecure-content',
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

    // Known PowerBI/Azure API URL patterns
    const PBI_HOSTS = [
      'pbidedicated.windows.net',
      'analysis.windows.net',
      'api.powerbi.com',
      'powerbi.com',
      'msit.pbidedicated.windows.net',
      'dashboardbi.ademicon.com.br',
    ];

    // Function to set captured token if valid PowerBI token from dedicated API or JS runtime
    function checkAndSetToken(candidate, source) {
      if (capturedToken || !candidate || typeof candidate !== 'string') return false;
      
      const cleanCandidate = candidate.trim();
      if (cleanCandidate.length < 30) return false;

      // Reject HTML/JS code snippets
      if (cleanCandidate.includes('<') || cleanCandidate.includes('function') || cleanCandidate.includes('var ')) return false;

      let formattedToken = null;
      if (cleanCandidate.startsWith('EmbedToken ') || cleanCandidate.startsWith('MWCToken ') || cleanCandidate.startsWith('Bearer ')) {
        formattedToken = cleanCandidate;
      } else if (cleanCandidate.startsWith('H4sI') || cleanCandidate.startsWith('ey')) {
        formattedToken = `EmbedToken ${cleanCandidate}`;
      }

      if (formattedToken) {
        capturedToken = formattedToken;
        addStep(steps, `Token PowerBI (${capturedToken.substring(0, 20)}...) capturado via ${source}!`);
        return true;
      }
      return false;
    }

    // Intercept requests for token capture — strictly from PowerBI query endpoints
    page.on('request', (req) => {
      const url = req.url();
      const headers = req.headers();
      const authHeader = headers['authorization'] || headers['Authorization'] || headers['x-powerbi-token'] || headers['x-access-token'];
      const postData = req.postData() || '';

      const isDedicatedPbiHost = url.includes('pbidedicated.windows.net') || url.includes('analysis.windows.net') || url.includes('api.powerbi.com');

      if (isDedicatedPbiHost) {
        console.log(`[PBI Dedicated Request] ${req.method()} ${url.substring(0, 120)} | auth=${authHeader ? authHeader.substring(0, 40) + '...' : 'none'}`);
        if (!capturedToken && authHeader && authHeader.length > 20) {
          checkAndSetToken(authHeader, `cabeçalho de requisição PBI [${req.method()}] em ${url.substring(0, 60)}`);
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
    // Give the PBI embed iframe time to load and fire its first authenticated request
    await new Promise(r => setTimeout(r, 5000));

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

    if (!capturedToken) {
      addStep(steps, 'Token não capturado via requisições/respostas — buscando H4sI EmbedToken no DOM e runtime JS...');

      // Wait a bit for the dashboard JS to fully initialize and fetch its token
      await new Promise(r => setTimeout(r, 4000));

      try {
        const tokenFromJs = await page.evaluate(() => {
          // Approach 1: Check window.powerbi
          if (window.powerbi && window.powerbi.embeds && window.powerbi.embeds.length > 0) {
            const embed = window.powerbi.embeds[0];
            if (embed && embed.config && embed.config.accessToken) return embed.config.accessToken;
          }
          // Approach 2: Check __NEXT_DATA__
          if (window.__NEXT_DATA__) {
            const str = JSON.stringify(window.__NEXT_DATA__);
            const match = str.match(/(H4sI[A-Za-z0-9+/=_%-]{30,})/);
            if (match) return match[1];
          }
          // Approach 3: Scan all script tags for H4sI tokens
          const scripts = Array.from(document.querySelectorAll('script'));
          for (const script of scripts) {
            const match = script.textContent.match(/(H4sI[A-Za-z0-9+/=_%-]{30,})/);
            if (match) return match[1];
          }
          // Approach 4: Scan entire document body HTML
          if (document.body) {
            const match = document.body.innerHTML.match(/(H4sI[A-Za-z0-9+/=_%-]{30,})/);
            if (match) return match[1];
          }
          return null;
        });

        if (tokenFromJs) {
          checkAndSetToken(tokenFromJs, 'runtime JavaScript da página principal');
        }
      } catch (evalErr) {
        addStep(steps, `Aviso ao tentar extrair token do JS principal: ${evalErr.message}`);
      }

      // Also search inside iframe contexts
      if (!capturedToken) {
        try {
          const frames = page.frames();
          addStep(steps, `Inspecionando ${frames.length} frame(s) em busca do EmbedToken (H4sI)...`);
          for (const frame of frames) {
            if (capturedToken) break;
            try {
              const frameUrl = frame.url();
              if (!frameUrl || frameUrl === 'about:blank') continue;
              const tokenFromFrame = await frame.evaluate(() => {
                if (window.powerbi && window.powerbi.embeds && window.powerbi.embeds.length > 0) {
                  const embed = window.powerbi.embeds[0];
                  if (embed && embed.config && embed.config.accessToken) return embed.config.accessToken;
                }
                const scripts = Array.from(document.querySelectorAll('script'));
                for (const script of scripts) {
                  const match = script.textContent.match(/(H4sI[A-Za-z0-9+/=_%-]{30,})/);
                  if (match) return match[1];
                }
                if (document.body) {
                  const match = document.body.innerHTML.match(/(H4sI[A-Za-z0-9+/=_%-]{30,})/);
                  if (match) return match[1];
                }
                return null;
              });

              if (tokenFromFrame) {
                checkAndSetToken(tokenFromFrame, `frame "${frameUrl.substring(0, 60)}"`);
                break;
              }
            } catch (frameErr) {
              // Cross-origin frame access may throw — not fatal
            }
          }
        } catch (framesErr) {
          addStep(steps, `Aviso ao inspecionar frames: ${framesErr.message}`);
        }
      }
    }

    // Step 8: Wait for PowerBI query token (60s timeout)
    if (!capturedToken) {
      addStep(steps, 'Aguardando requisição interna do PowerBI para capturar o token de acesso (tempo limite: 60s)...');
      await new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
          reject(new AuthError('timeout', 'Tempo limite de 60s excedido aguardando token do PowerBI. O iframe do dashboard pode não ter carregado ou não fez requisição autenticada.', steps, powerbiLoaded, loginSuccess));
        }, 60000);
        
        const interval = setInterval(() => {
          if (capturedToken) {
            clearTimeout(timeout);
            clearInterval(interval);
            resolve();
          }
        }, 500);
      });
    } else {
      addStep(steps, 'Token PowerBI já capturado durante o carregamento do dashboard.');
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
