// auth.js
// Authenticates to avapro.ademicon.com.br using Puppeteer.
// Navigates to the dashboard, intercepts the first PowerBI "query" request
// and returns the Authorization token from that request (not the login token).

const puppeteer = require('puppeteer');

const AVA_URL      = 'https://avapro.ademicon.com.br/';
const DASHBOARD_URL = 'https://avapro.ademicon.com.br/dashboard';
const QUERY_HOST   = 'pbidedicated.windows.net';

/**
 * Launches a browser, logs in with the given credentials,
 * waits for the dashboard to fire a PowerBI "query" request,
 * and returns the Bearer token from that request.
 *
 * @param {string} matricula - The user's matricula (username)
 * @param {string} password  - The user's password
 * @returns {Promise<string>} The Authorization header value (e.g. "Bearer eyJ...")
 */
async function getTokenFromLogin(matricula, password) {
  console.log('[Auth] Launching browser for authentication...');

  const browser = await puppeteer.launch({
    headless: true,
    args: [
      '--no-sandbox',
      '--disable-setuid-sandbox',
      '--disable-dev-shm-usage',
    ],
  });

  const page = await browser.newPage();
  page.setDefaultNavigationTimeout(180000);
  page.setDefaultTimeout(180000);

  // Intercept all requests so we can capture the PowerBI query token
  let capturedToken = null;

  await page.setRequestInterception(true);

  page.on('request', (req) => {
    const url = req.url();
    const headers = req.headers();
    const authHeader = headers['authorization'];

    // LOGGING: Check for any requests to the relevant hosts to debug capture
    if (url.includes('pbidedicated') || url.includes('/query')) {
      console.log(`[Auth] Intercepted: ${url.substring(0, 100)}${url.length > 100 ? '...' : ''}`);
      if (authHeader) {
        console.log(`[Auth]   - Has Authorization header: ${authHeader.substring(0, 20)}...`);
      } else {
        console.log(`[Auth]   - No Authorization header found in this request.`);
      }
    }

    // The token we want is from the PowerBI query endpoint, not the login
    if (url.includes(QUERY_HOST) && url.includes('/query') && authHeader) {
      if (!capturedToken) {
        console.log(`[Auth] Found matching PowerBI query token!`);
        capturedToken = authHeader;
      }
    }

    req.continue();
  });

  try {
    // Step 1: Navigate to login page
    console.log('[Auth] Navigating to login page...');
    await page.goto(AVA_URL, { waitUntil: 'networkidle2', timeout: 180000 });

    // Step 2: Fill in credentials
    // The login form has a text input for 'Matricula' and a password input for 'Senha'
    console.log('[Auth] Filling in credentials...');
    await page.waitForSelector('input[type="text"]', { timeout: 30000 });
    await page.type('input[type="text"]', matricula, { delay: 50 });

    await page.waitForSelector('input[type="password"]', { timeout: 30000 });
    await page.type('input[type="password"]', password, { delay: 50 });

    // Step 3: Click the login button
    console.log('[Auth] Submitting login form...');
    
    // Attempt to click the button by selector provided by user, or fallback to finding by text
    try {
      await page.waitForSelector('button', { timeout: 30000 });
      const [button] = await page.$x("//button[contains(., 'Entrar') or contains(., 'entrar')]");
      if (button) {
        await button.click();
      } else {
        // Fallback to the specific class the user found
        await page.click('button.inline-flex');
      }
    } catch (err) {
      console.warn('[Auth] Standard login button click failed, trying specific selector...');
      await page.click('button.inline-flex');
    }

    // Step 4: Wait for navigation to dashboard
    console.log('[Auth] Waiting for post-login navigation...');
    await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 180000 });

    // Step 5: Navigate to the dashboard if not already there
    const currentUrl = page.url();
    if (!currentUrl.includes('/dashboard')) {
      console.log(`[Auth] Current URL is ${currentUrl}, navigating to ${DASHBOARD_URL}...`);
      await page.goto(DASHBOARD_URL, { waitUntil: 'networkidle2', timeout: 180000 });
    } else {
      console.log('[Auth] Already on dashboard page.');
    }

    // Wait for the PowerBI iframe to start appearing, which usually triggers the query
    console.log('[Auth] Waiting for potential report indicators...');
    try {
      await page.waitForSelector('iframe, .powerbi-container, .report-container', { timeout: 30000 });
      console.log('[Auth] Report container detected.');
    } catch (e) {
      console.log('[Auth] No specific report container found, continuing to wait for network...');
    }

    // Wait up to 30s for the query token to be captured
    if (!capturedToken) {
      console.log('[Auth] Waiting for PowerBI query request...');
      await new Promise((resolve, reject) => {
        const timeout = setTimeout(() => reject(new Error('Timed out waiting for PowerBI query request')), 180000);
        const interval = setInterval(() => {
          if (capturedToken) {
            clearTimeout(timeout);
            clearInterval(interval);
            resolve();
          }
        }, 500);
      });
    }

    console.log('[Auth] Token captured successfully.');
    console.log(`[Auth] Token Preview: ${capturedToken.substring(0, 30)}...`);
    return capturedToken;
  } finally {
    await browser.close();
    console.log('[Auth] Browser closed.');
  }
}

module.exports = { getTokenFromLogin };
