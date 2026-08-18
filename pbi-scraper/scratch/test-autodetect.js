// scratch/test-autodetect.js
// Tests store (Loja) auto-detection from AVA PRO header upon Puppeteer login.
//
// Usage:
//   MATRICULA="11177" PASSWORD="your_password" node scratch/test-autodetect.js
//
// Or using curl against the API:
//   curl -X POST http://localhost:5000/api/scrape/configs \
//     -H "Content-Type: application/json" \
//     -d '{"matricula":"11177","powerBiPassword":"your_password","store":""}'

const { getOrFetchTokens } = require('../auth');
const tokenManager = require('../tokenManager');

const MATRICULA = process.env.MATRICULA || '11177';
const PASSWORD  = process.env.PASSWORD || 'Alexi@t&26';
const STORE     = process.env.STORE || '';

async function runTest() {
  console.log('====================================================');
  console.log('   Testing Store (Loja) Auto-Detection from AVA PRO  ');
  console.log('====================================================');
  console.log(`Matrícula: "${MATRICULA}"`);
  console.log(`Store (Provided in CLI): "${STORE || 'None (Tentar selecionar automaticamente)'}"\n`);

  tokenManager.clearAllTokens();

  console.log('>>> Launching Puppeteer & authenticating to AVA PRO...');
  const startTime = Date.now();

  try {
    const authInfo = await getOrFetchTokens(MATRICULA, PASSWORD, STORE, true);
    const elapsed = ((Date.now() - startTime) / 1000).toFixed(2);

    console.log('\n====================================================');
    console.log(`✅ Authentication Completed in ${elapsed}s`);
    console.log('====================================================');
    console.log(`🔑 Login Success:     ${authInfo.loginSuccess}`);
    console.log(`📊 PowerBI Loaded:    ${authInfo.powerbiLoaded}`);
    console.log(`🏪 Auto-Detected Loja: ${authInfo.detectedStore ? `"${authInfo.detectedStore}"` : '❌ Not detected (null)'}`);
    console.log(`🎟️ MWCToken Captured:  ${authInfo.token ? `${authInfo.token.substring(0, 35)}...` : 'None'}`);

    console.log('\n📋 Diagnostic Steps Executed:');
    if (authInfo.steps && authInfo.steps.length > 0) {
      authInfo.steps.forEach(s => console.log(`   ${s}`));
    }

    if (authInfo.detectedStore) {
      console.log(`\n🎉 SUCCESS: Store auto-detected as "${authInfo.detectedStore}"!`);
      console.log('This exact string will be used for DAX queries and SQLite DB updates.');
    } else {
      console.log('\n⚠️ WARNING: Store name could not be automatically extracted from DOM.');
    }

  } catch (err) {
    console.error('\n❌ Test Failed with Error:');
    console.error(`Message: ${err.message || err}`);
    if (err.steps) {
      console.log('\nDiagnostic Steps:');
      err.steps.forEach(s => console.log(`   ${s}`));
    }
  }
}

runTest();
