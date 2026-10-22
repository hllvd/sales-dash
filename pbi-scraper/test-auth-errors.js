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

console.log('All detectLoginErrorText tests passed successfully!');
