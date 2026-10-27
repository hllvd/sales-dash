// test-auth-errors.js
// Unit tests for detectLoginErrorText in auth.js

const assert = require('assert');
const { detectLoginErrorText } = require('./auth');

console.log('Testing detectLoginErrorText helper...');

// Test 1: Senha expirada
const exp1 = detectLoginErrorText('Senha expirada. Por favor, redefina sua senha.');
assert(exp1 !== null, 'Should detect "Senha expirada"');
assert.strictEqual(exp1.isFatalAuth, true);
assert(exp1.reason.includes('Senha expirada'), 'Reason should mention expired password');
console.log('✔ Test 1 passed: "Senha expirada" detected');

// Test 2: Redefina sua senha variation
const exp2 = detectLoginErrorText('Por favor, redefina sua senha.');
assert(exp2 !== null, 'Should detect "redefina sua senha"');
assert.strictEqual(exp2.isFatalAuth, true);
console.log('✔ Test 2 passed: "redefina sua senha" detected');

// Test 3: Usuário ou senha inválida
const userPass = detectLoginErrorText('Usuário ou senha inválida');
assert(userPass !== null, 'Should detect wrong user/password');
assert.strictEqual(userPass.isFatalAuth, true);
console.log('✔ Test 3 passed: "Usuário ou senha inválida" detected');

// Test 4: Credenciais inválidas
const credInv = detectLoginErrorText('Credenciais inválidas informadas');
assert(credInv !== null, 'Should detect credenciais inválidas');
assert.strictEqual(credInv.isFatalAuth, true);
console.log('✔ Test 4 passed: "Credenciais inválidas" detected');

// Test 5: Safe / neutral text
const neutral = detectLoginErrorText('Boas-vindas ao AVAPRO');
assert.strictEqual(neutral, null, 'Neutral text should return null');
console.log('✔ Test 5 passed: Neutral text ignored');

// Test 6: Null or empty
assert.strictEqual(detectLoginErrorText(null), null);
assert.strictEqual(detectLoginErrorText(''), null);
assert.strictEqual(detectLoginErrorText(undefined), null);
console.log('✔ Test 6 passed: Null/empty inputs safely handled');

// Test 7: Exact text from AVA PRO screenshot 1 (tentativas restantes)
const screenshot1 = detectLoginErrorText('Usuário ou senha inválida. Você ainda possui mais 2 tentativas antes do bloqueio do seu usuário.');
assert(screenshot1 !== null, 'Should detect screenshot 1 warning text');
assert.strictEqual(screenshot1.isFatalAuth, true);
console.log('✔ Test 7 passed: Screenshot 1 text detected');

// Test 8: Tentativas text alone
const tent = detectLoginErrorText('Você ainda possui mais 1 tentativas antes do bloqueio');
assert(tent !== null, 'Should detect tentativas text');
assert.strictEqual(tent.isFatalAuth, true);
console.log('✔ Test 8 passed: Tentativas text detected');

// Test 9: Senha incorreta / errada
const errada = detectLoginErrorText('Senha incorreta, tente novamente.');
assert(errada !== null, 'Should detect senha incorreta');
assert.strictEqual(errada.isFatalAuth, true);
console.log('✔ Test 9 passed: Senha incorreta detected');

console.log('All detectLoginErrorText tests passed successfully!\n');

console.log('Testing tokenManager password hashing & cache invalidation...');
const tokenManager = require('./tokenManager');

const hash1 = tokenManager.hashPassword('senhaCorreta123');
const hash2 = tokenManager.hashPassword('senhaCorreta123');
const hash3 = tokenManager.hashPassword('senhaErrada456');

assert.strictEqual(hash1, hash2, 'Identical passwords should yield identical SHA-256 hashes');
assert.notStrictEqual(hash1, hash3, 'Different passwords must yield different SHA-256 hashes');

// Test cache storage with password
tokenManager.setTokens('11177', {
  pbiToken: 'Bearer test-token-11177',
  avaJwt: 'jwt-11177',
  password: 'senhaCorreta123'
});

// Cache hit with correct password
const hit = tokenManager.getTokens('11177', 'senhaCorreta123');
assert(hit !== null, 'Should return cached tokens for correct password');
assert.strictEqual(hit.pbiToken, 'Bearer test-token-11177');

// Cache miss and invalidation when password changes
const miss = tokenManager.getTokens('11177', 'senhaErrada456');
assert.strictEqual(miss, null, 'Cache should be invalidated and return null when password changes');

// Verify cache was deleted
const afterInvalidation = tokenManager.getTokens('11177');
assert.strictEqual(afterInvalidation, null, 'Cache must be empty after invalidation');

console.log('✔ tokenManager password hash and cache invalidation tests passed successfully!\n');
