// intercept_ademicon.js
const { chromium } = require('playwright');
const fs = require('fs');

const URL = `https://avapro.ademicon.com.br/dashboard`;

async function intercept() {
  const browser = await chromium.launch({ headless: false }); // headed so you can log in
  const context = await browser.newContext();
  const page = await context.newPage();

  const captured = {
    pbiCalls: [],      // calls to analysis.windows.net / api.powerbi.com
    appCalls: [],      // calls to dashboardbi.ademicon.com.br
    tokens: {},        // any tokens found
    cookies: []        // session cookies
  };

  // Intercept ALL requests
  page.on('request', request => {
    const url = request.url();
    const headers = request.headers();

    // Power BI internal API calls
    if (
      url.includes('analysis.windows.net') ||
      url.includes('api.powerbi.com') ||
      url.includes('powerbi') ||
      url.includes('executeQueries') ||
      url.includes('querydata') ||
      url.includes('reportEmbed')
    ) {
      captured.pbiCalls.push({
        url,
        method: request.method(),
        headers,
        body: request.postData()
      });
      console.log(`[PBI] ${request.method()} ${url.substring(0, 90)}`);
    }

    // Their own app API calls (might expose data directly)
    if (url.includes('ademicon')) {
      captured.appCalls.push({
        url,
        method: request.method(),
        headers,
        body: request.postData()
      });
      console.log(`[APP] ${request.method()} ${url.substring(0, 90)}`);
    }
  });

  // Also intercept responses to find tokens
  page.on('response', async response => {
    const url = response.url();
    if (url.includes('ademicon') && response.status() === 200) {
      try {
        const ct = response.headers()['content-type'] || '';
        if (ct.includes('json')) {
          const body = await response.json();
          // Look for tokens in API responses
          const str = JSON.stringify(body);
          if (str.includes('token') || str.includes('embedToken')) {
            console.log(`\n[TOKEN FOUND IN RESPONSE] ${url}`);
            console.log(JSON.stringify(body, null, 2).substring(0, 500));
            captured.tokens[url] = body;
          }
        }
      } catch (e) {}
    }
  });

  // Navigate — handle login manually if needed
  await page.goto(URL);
  console.log('\n>>> If login is required, complete it manually in the browser <<<\n');

  // Wait long enough for everything to load + you to log in
  await page.waitForTimeout(30000);

  // Capture cookies after login
  captured.cookies = await context.cookies();

  // Save everything
  fs.writeFileSync('./captured.json', JSON.stringify(captured, null, 2));
  console.log(`\nSaved:`);
  console.log(`  PBI calls:  ${captured.pbiCalls.length}`);
  console.log(`  App calls:  ${captured.appCalls.length}`);
  console.log(`  Cookies:    ${captured.cookies.length}`);

  // Print summary of most useful items
  console.log('\n=== MOST USEFUL CALLS ===');
  for (const call of captured.pbiCalls) {
    if (call.headers.authorization) {
      console.log(`\nURL: ${call.url.substring(0, 80)}`);
      console.log(`Token type: ${call.headers.authorization.substring(0, 30)}...`);
    }
  }

  await browser.close();
}

intercept().catch(console.error);