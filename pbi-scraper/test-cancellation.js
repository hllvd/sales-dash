// test-cancellation.js
const assert = require('assert');
const {
  JobCancelledError,
  isAbortError,
  throwIfAborted,
  sleepAbortable
} = require('./cancellationHelper');

async function runTests() {
  console.log('Testing Cooperative Cancellation Helper...');

  // 1. throwIfAborted does nothing on active controller and throws JobCancelledError on aborted
  const ac1 = new AbortController();
  throwIfAborted(ac1.signal);
  ac1.abort();
  assert.throws(() => throwIfAborted(ac1.signal), (err) => {
    return err instanceof JobCancelledError && isAbortError(err);
  });
  console.log('✔ Test 1: throwIfAborted throws JobCancelledError when aborted');

  // 2. isAbortError recognizes Axios CanceledError / AbortError
  assert.strictEqual(isAbortError({ name: 'CanceledError', code: 'ERR_CANCELED' }), true);
  assert.strictEqual(isAbortError({ name: 'AbortError' }), true);
  assert.strictEqual(isAbortError(new Error('Normal timeout')), false);
  console.log('✔ Test 2: isAbortError accurately identifies cancellation errors');

  // 3. sleepAbortable resolves normally if not aborted
  const ac2 = new AbortController();
  const startOk = Date.now();
  await sleepAbortable(30, ac2.signal);
  assert.ok(Date.now() - startOk >= 20);
  console.log('✔ Test 3: sleepAbortable resolves normally without abort');

  // 4. sleepAbortable aborts immediately (does not wait 60s!) when aborted mid-sleep
  const ac3 = new AbortController();
  const startAbort = Date.now();
  setTimeout(() => ac3.abort(), 30);
  let caughtCancel = false;
  try {
    await sleepAbortable(60000, ac3.signal);
  } catch (err) {
    caughtCancel = isAbortError(err);
  }
  const elapsed = Date.now() - startAbort;
  assert.strictEqual(caughtCancel, true);
  assert.ok(elapsed < 500, `Expected immediate abort under 500ms, got ${elapsed}ms`);
  console.log(`✔ Test 4: sleepAbortable interrupted 60s backoff in ${elapsed}ms`);

  console.log('\nAll Cancellation Helper tests passed successfully! 🎉');
}

runTests().catch((err) => {
  console.error('Test failure:', err);
  process.exit(1);
});
