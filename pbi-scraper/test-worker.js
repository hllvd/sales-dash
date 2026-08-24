// test-worker.js
const { resolveCredentials } = require('./worker');
const { encryptField } = require('./crypto');
const crypto = require('crypto');

function runTests() {
  console.log('Running worker.js unit tests...');

  const keyHex = crypto.randomBytes(32).toString('hex');
  process.env.SCRAPER_ENCRYPTION_KEY = keyHex;

  // 1. Test Plaintext Fallback
  const plainPayload = {
    matricula: 'plainUser123',
    password: 'plainPassword123!'
  };
  const creds1 = resolveCredentials(plainPayload);
  if (creds1.username !== 'plainUser123' || creds1.password !== 'plainPassword123!') {
    throw new Error('resolveCredentials failed for plaintext payload');
  }
  console.log('✅ resolveCredentials with plaintext passed');

  // 2. Test Encrypted Credentials
  const encUsername = encryptField('myMatricula999', keyHex);
  const encPassword = encryptField('mySecretPass$$$', keyHex);

  const encryptedPayload = {
    encryptedUsername: encUsername.cipherText,
    usernameIv: encUsername.iv,
    usernameAuthTag: encUsername.authTag,
    encryptedPassword: encPassword.cipherText,
    passwordIv: encPassword.iv,
    passwordAuthTag: encPassword.authTag
  };

  const creds2 = resolveCredentials(encryptedPayload);
  if (creds2.username !== 'myMatricula999' || creds2.password !== 'mySecretPass$$$') {
    throw new Error(`resolveCredentials failed for encrypted payload: got ${JSON.stringify(creds2)}`);
  }
  console.log('✅ resolveCredentials with AES-256-GCM encrypted payload passed');

  // 3. Test Missing Credentials Error
  let errorCaught = false;
  try {
    resolveCredentials({});
  } catch (err) {
    errorCaught = true;
    console.log('✅ Empty payload correctly throws error');
  }
  if (!errorCaught) throw new Error('Empty payload did not throw error');

  console.log('\nAll worker unit tests passed successfully! 🎉');
}

runTests();
