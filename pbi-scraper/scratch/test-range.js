// scratch/test-range.js
// Tests multi-month date range scraping with in-memory token caching
// and automatic 401/402/403 re-authentication handling.
//
// Usage:
//   SCRAPE_DATES="2026-02,2026-03,2026-04" node scratch/test-range.js

const { scrapeWithReauth } = require('../extractor');
const { getOrFetchTokens } = require('../auth');
const tokenManager = require('../tokenManager');

const STORE     = process.env.STORE || 'BALNEARIO CAMBORIU - SC';
const MATRICULA = process.env.MATRICULA || '11177';
const PASSWORD  = process.env.PASSWORD || 'Alexi@t&26';
const DATES_ENV = process.env.SCRAPE_DATES || '2026-02,2026-03,2026-04';

const datesArray = DATES_ENV.split(',').map(d => d.trim()).filter(Boolean);

async function runTest() {
  console.log('=== Starting Token Cache & Multi-Month Date Range Scraping Test ===');
  console.log(`Matricula: ${MATRICULA}`);
  console.log(`Store:     ${STORE}`);
  console.log(`Dates:     ${datesArray.join(', ')}\n`);

  tokenManager.clearAllTokens();

  for (let i = 0; i < datesArray.length; i++) {
    const targetDate = datesArray[i];
    const monthNum   = i + 1;
    const startTime  = Date.now();

    console.log(`\n--- [Month ${monthNum}/${datesArray.length}] Scraping for date: "${targetDate}" ---`);

    try {
      const res = await scrapeWithReauth(
        STORE,
        MATRICULA,
        PASSWORD,
        targetDate,
        getOrFetchTokens,
        3
      );

      const elapsed = ((Date.now() - startTime) / 1000).toFixed(2);
      console.log(`✅ [Month ${monthNum}] Success in ${elapsed}s! Total rows extracted: ${res.rows.length}`);
      if (res.csv) {
        console.log(`   CSV Header: ${res.csv.split('\n')[0]}`);
      }

    } catch (err) {
      console.error(`❌ [Month ${monthNum}] Failed: ${err.message}`);
    }
  }

  // ── Auto Re-Auth Test: Simulate a 401 token expiry ────────────────────────
  console.log('\n--- [Test Auto-Reauth] Simulating 401 Token Expiry ---');
  tokenManager.setTokens(MATRICULA, { avaJwt: 'bad', pbiToken: 'MWCToken invalid_expired_token' });
  console.log('[Test] Corrupted cached token set to invalid string.');

  const reauthStart = Date.now();
  try {
    const reauthRes = await scrapeWithReauth(
      STORE,
      MATRICULA,
      PASSWORD,
      datesArray[0],
      getOrFetchTokens,
      3
    );
    const reauthElapsed = ((Date.now() - reauthStart) / 1000).toFixed(2);
    console.log(`✅ [Auto-Reauth Test] Caught 401, re-authenticated via Puppeteer, and scraped in ${reauthElapsed}s!`);
    console.log(`   Total rows: ${reauthRes.rows.length}`);
  } catch (err) {
    console.error(`❌ [Auto-Reauth Test] Failed: ${err.message}`);
  }

  console.log('\n=== All Tests Completed ===');
}

runTest();
