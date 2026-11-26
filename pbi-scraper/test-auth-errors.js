// test-auth-errors.js
// Unit tests for detectLoginErrorText in auth.js

const assert = require('assert');
const { detectLoginErrorText, findLoginErrorInCandidates, inspectInitialLoginState, ensureLoginFormReady } = require('./auth');

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

// Test 10: Sua senha expirou
const exp3 = detectLoginErrorText('Sua senha expirou. Por favor efetue a troca.');
assert(exp3 !== null, 'Should detect "sua senha expirou"');
assert.strictEqual(exp3.isFatalAuth, true);
assert(exp3.reason.includes('Senha expirada'), 'Reason should mention expired password');
console.log('✔ Test 10 passed: "sua senha expirou" detected');

// Test 11: Senha vencida
const exp4 = detectLoginErrorText('Senha vencida');
assert(exp4 !== null, 'Should detect "senha vencida"');
assert.strictEqual(exp4.isFatalAuth, true);
console.log('✔ Test 11 passed: "senha vencida" detected');

// Test 12: Não autorizado / usuário inválido
const unauth = detectLoginErrorText('Usuário não autorizado');
assert(unauth !== null, 'Should detect unauth');
assert.strictEqual(unauth.isFatalAuth, true);
console.log('✔ Test 12 passed: "Usuário não autorizado" detected');

// Test 13: Static login form prompt must be ignored (regression for "Insira a sua matrícula de consultor e a senha cadastrada.")
const staticPrompt = detectLoginErrorText('Insira a sua matrícula de consultor e a senha cadastrada.');
assert.strictEqual(staticPrompt, null, 'Static login prompt containing "senha" must be ignored');
assert.strictEqual(detectLoginErrorText('Esqueceu sua senha?'), null, 'Forgot password link must be ignored');
console.log('✔ Test 13 passed: Static login form prompts ignored');

// Test 14: findLoginErrorInCandidates skips static lines and finds the real error below
const candidateLines = [
  'Bem-vindo ao AVA PRO',
  'Insira a sua matrícula de consultor e a senha cadastrada.',
  'Matrícula',
  'Senha',
  'Esqueceu sua senha?',
  'Usuário ou senha inválida'
];
const found = findLoginErrorInCandidates(candidateLines);
assert(found !== null, 'Should skip static prompt and find actual error in candidates');
assert.strictEqual(found.isFatalAuth, true);
assert.strictEqual(found.reason, 'Usuário ou senha inválida no AVA PRO.');
console.log('✔ Test 14 passed: findLoginErrorInCandidates skips static prompt and detects error');

// Test 15: Limite de tentativas excedido
const limitExc = detectLoginErrorText('Limite de tentativas excedido. Será necessário redefinir a sua senha.');
assert(limitExc !== null, 'Should detect limite de tentativas excedido');
assert.strictEqual(limitExc.isFatalAuth, true);
console.log('✔ Test 15 passed: "Limite de tentativas excedido" detected');

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

console.log('Testing inspectInitialLoginState and ensureLoginFormReady...');

// Test: inspectInitialLoginState with null/undefined
assert.strictEqual(inspectInitialLoginState(null), 'unknown');
assert.strictEqual(inspectInitialLoginState(undefined), 'unknown');
assert.strictEqual(inspectInitialLoginState({}), 'unknown');

// Test: inspectInitialLoginState with input[type="text"]
const docWithInput = {
  querySelector: (sel) => sel === 'input[type="text"]' ? {} : null,
  querySelectorAll: () => []
};
assert.strictEqual(inspectInitialLoginState(docWithInput), 'input');

// Test: inspectInitialLoginState with welcome screen button
const docWithWelcome = {
  querySelector: () => null,
  querySelectorAll: (sel) => [
    { textContent: ' Outro ' },
    { textContent: 'Entrar' }
  ]
};
assert.strictEqual(inspectInitialLoginState(docWithWelcome), 'welcome');

// Test: inspectInitialLoginState with unrelated buttons
const docUnrelated = {
  querySelector: () => null,
  querySelectorAll: (sel) => [
    { textContent: 'Ajuda' }
  ]
};
assert.strictEqual(inspectInitialLoginState(docUnrelated), 'unknown');
console.log('✔ inspectInitialLoginState pure helper tests passed');

// Test: ensureLoginFormReady with mock page (direct input)
(async () => {
  let selectorWaited = null;
  const mockPageDirect = {
    waitForFunction: async (fn) => ({ jsonValue: async () => 'input' }),
    waitForSelector: async (sel) => { selectorWaited = sel; }
  };
  await ensureLoginFormReady(mockPageDirect);
  assert.strictEqual(selectorWaited, 'input[type="text"]');

  // Test: ensureLoginFormReady with mock page (welcome screen)
  let clicked = false;
  let loggedMsg = null;
  const mockPageWelcome = {
    waitForFunction: async (fn) => ({ jsonValue: async () => 'welcome' }),
    evaluateHandle: async () => ({
      asElement: () => ({
        click: async () => { clicked = true; }
      })
    }),
    waitForSelector: async (sel) => { selectorWaited = sel; }
  };
  await ensureLoginFormReady(mockPageWelcome, (msg) => { loggedMsg = msg; });
  assert.strictEqual(clicked, true, 'Welcome button should have been clicked');
  assert.strictEqual(selectorWaited, 'input[type="text"]');
  assert(loggedMsg && loggedMsg.includes('Tela de boas-vindas'), 'Logger should receive welcome screen step');

  console.log('✔ ensureLoginFormReady mock page tests passed successfully!\n');
})().catch((err) => {
  console.error('Failure in ensureLoginFormReady tests:', err);
  process.exit(1);
});
