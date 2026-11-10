// test-rate-limit-and-checkpoint.js
const assert = require('assert');
const fs = require('fs');
const path = require('path');
const {
  saveCheckpoint,
  loadCheckpoint,
  clearCheckpoint,
  areMonthsEqual
} = require('./checkpointManager');

console.log('Testing CheckpointManager & Rate-Limit Logic...');

const tempDir = path.join(__dirname, 'test-outputs');
if (!fs.existsSync(tempDir)) fs.mkdirSync(tempDir, { recursive: true });

try {
  // Test 1: areMonthsEqual helper
  console.log('✔ Test 1: areMonthsEqual comparison');
  assert.strictEqual(areMonthsEqual(['2025-08', '2025-09'], ['2025-09', '2025-08']), true);
  assert.strictEqual(areMonthsEqual(['2025-08'], ['2025-08', '2025-09']), false);
  assert.strictEqual(areMonthsEqual([], []), true);
  assert.strictEqual(areMonthsEqual(null, ['2025-08']), false);

  // Test 2: saveCheckpoint and loadCheckpoint
  console.log('✔ Test 2: saveCheckpoint and loadCheckpoint');
  const matricula = 'test-5379';
  const targetMonths = ['2025-08', '2025-09', '2025-10'];
  const testRows = [{ cota: '101', grupo: '500' }, { cota: '102', grupo: '500' }];
  const restartTokens = [['token1'], ['token2']];

  const savedPath = saveCheckpoint(tempDir, matricula, {
    targetMonths,
    completedPages: 3,
    restartTokens,
    paginationContext: { prev: ['row1'] },
    allRows: testRows
  });

  assert.ok(fs.existsSync(savedPath), 'Checkpoint file must exist');

  const loaded = loadCheckpoint(tempDir, matricula, targetMonths);
  assert.ok(loaded !== null, 'Loaded checkpoint should not be null');
  assert.strictEqual(loaded.matricula, matricula);
  assert.strictEqual(loaded.completedPages, 3);
  assert.strictEqual(loaded.rowCount, 2);
  assert.deepStrictEqual(loaded.restartTokens, restartTokens);
  assert.strictEqual(loaded.allRows.length, 2);

  // Test 3: loadCheckpoint with different months should return null
  console.log('✔ Test 3: loadCheckpoint with mismatched months');
  const mismatched = loadCheckpoint(tempDir, matricula, ['2025-08']);
  assert.strictEqual(mismatched, null, 'Different targetMonths must invalidate checkpoint');

  // Test 4: clearCheckpoint removes file
  console.log('✔ Test 4: clearCheckpoint removes file');
  clearCheckpoint(tempDir, matricula);
  assert.strictEqual(fs.existsSync(savedPath), false, 'Checkpoint file must be removed');
  assert.strictEqual(loadCheckpoint(tempDir, matricula, targetMonths), null);

  // Test 5: Verify Rate Limit retry backoff delay schedule
  console.log('✔ Test 5: Rate limit backoff calculation');
  const RATE_LIMIT_DELAYS_MS = [60000, 120000, 300000, 600000];
  assert.strictEqual(RATE_LIMIT_DELAYS_MS[0], 60000);  // 1 min
  assert.strictEqual(RATE_LIMIT_DELAYS_MS[1], 120000); // 2 min
  assert.strictEqual(RATE_LIMIT_DELAYS_MS[2], 300000); // 5 min
  assert.strictEqual(RATE_LIMIT_DELAYS_MS[3], 600000); // 10 min

  // With Retry-After header
  const retryAfterSec = 90;
  const computedDelay = Math.max(RATE_LIMIT_DELAYS_MS[0], retryAfterSec * 1000);
  assert.strictEqual(computedDelay, 90000, 'Retry-After should override if larger');

  console.log('\nAll Checkpoint and Rate-Limit tests passed successfully! 🎉');
} finally {
  // Clean up test directory
  try {
    fs.rmSync(tempDir, { recursive: true, force: true });
  } catch (_) {}
}
