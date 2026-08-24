// test-crypto.js
const { encryptField, decryptField, parseAndValidateKey } = require('./crypto');
const crypto = require('crypto');

function runTests() {
  console.log('Running crypto.js tests...');
  
  // 1. Test Key Validation
  const validKeyHex = crypto.randomBytes(32).toString('hex');
  const parsedKey = parseAndValidateKey(validKeyHex);
  if (parsedKey.length !== 32) {
    throw new Error('Key length test failed');
  }
  console.log('✅ Key validation passed');

  // 2. Test Roundtrip Encrypt & Decrypt
  const testPayload = 'MinhaSenhaSegura123!@#_ComCaracteresEspeciais';
  const encrypted = encryptField(testPayload, validKeyHex);
  
  if (!encrypted.cipherText || !encrypted.iv || !encrypted.authTag) {
    throw new Error('Encryption output missing fields');
  }

  const decrypted = decryptField(encrypted.cipherText, encrypted.iv, encrypted.authTag, validKeyHex);
  if (decrypted !== testPayload) {
    throw new Error(`Roundtrip mismatch: expected "${testPayload}", got "${decrypted}"`);
  }
  console.log('✅ Roundtrip encryption/decryption passed');

  // 3. Test Tampering Detection (GCM Auth Tag)
  let tamperingCaught = false;
  try {
    // Tamper with ciphertext by altering last character
    const tamperedCipher = encrypted.cipherText.slice(0, -2) + (encrypted.cipherText.slice(-2) === 'AA' ? 'BB' : 'AA');
    decryptField(tamperedCipher, encrypted.iv, encrypted.authTag, validKeyHex);
  } catch (err) {
    tamperingCaught = true;
    console.log('✅ Tampered ciphertext correctly rejected by AuthTag check');
  }

  if (!tamperingCaught) {
    throw new Error('Tampering test failed: corrupted ciphertext was not rejected');
  }

  // 4. Test Wrong Key
  const wrongKeyHex = crypto.randomBytes(32).toString('hex');
  let wrongKeyCaught = false;
  try {
    decryptField(encrypted.cipherText, encrypted.iv, encrypted.authTag, wrongKeyHex);
  } catch (err) {
    wrongKeyCaught = true;
    console.log('✅ Decryption with incorrect key correctly failed');
  }

  if (!wrongKeyCaught) {
    throw new Error('Wrong key test failed');
  }

  console.log('\nAll crypto tests passed successfully! 🎉');
}

runTests();
