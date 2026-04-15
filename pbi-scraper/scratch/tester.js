// scratch/tester.js
// Manual tester for auth.js and extractor.js logic.
// Usage: node scratch/tester.js

const { getTokenFromLogin } = require('../auth');
const { scrape } = require('../extractor');

// HARDCODED CREDENTIALS FOR TESTING
const MATRICULA = '11177'; // Replace with real matricula
const PASSWORD = 'Al%exia09';  // Replace with real password
const STORE = 'BALNEARIO CAMBORIU - SC';     // Replace with a real store name (e.g., 'BALNEARIO CAMBORIU - SC')

async function runTest() {
  console.log('--- Starting Production Test ---');
  console.log(`Matricula: ${MATRICULA}`);
  console.log(`Store:     ${STORE}`);

  if (MATRICULA === 'YOUR_MATRICULA' || PASSWORD === 'YOUR_PASSWORD') {
    console.error('ERROR: Please replace "YOUR_MATRICULA" and "YOUR_PASSWORD" with real credentials before running.');
    process.exit(1);
  }

  try {
    // 1. Get Token
    console.log('\n[1/2] Authenticating via Puppeteer...');
    const token = await getTokenFromLogin(MATRICULA, PASSWORD);
    console.log('Token successfully captured.');

    // 2. Run Scraper
    console.log('\n[2/2] Running extractor with captured token...');
    const { rows, csv } = await scrape(STORE, MATRICULA, token);

    console.log(`\n--- Test Success ---`);
    console.log(`Rows returned: ${rows.length}`);
    if (rows.length > 0) {
      console.log('First row sample:', JSON.stringify(rows[0], null, 2));
      console.log('\nCSV Header:', csv.split('\n')[0]);
    } else {
      console.warn('Warning: No data rows returned. Check if the store/matricula combination is valid.');
    }

  } catch (err) {
    console.error('\n--- Test Failed ---');
    console.error(err);
    process.exit(1);
  }
}

runTest();
