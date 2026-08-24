// test-scrape.js
const { normalizeScrapeDates, mergeCsvParts, countEffectiveRows } = require('./scrape');

function runTests() {
  console.log('Running scrape.js unit tests...');

  // 1. normalizeScrapeDates
  const d1 = normalizeScrapeDates('2026-01', null);
  if (JSON.stringify(d1) !== JSON.stringify(['2026-01'])) throw new Error('normalizeScrapeDates single date failed');

  const d2 = normalizeScrapeDates(null, ['2026-01', '2026-02']);
  if (JSON.stringify(d2) !== JSON.stringify(['2026-01', '2026-02'])) throw new Error('normalizeScrapeDates array failed');

  const d3 = normalizeScrapeDates('2026-01, 2026-02', null);
  if (JSON.stringify(d3) !== JSON.stringify(['2026-01', '2026-02'])) throw new Error('normalizeScrapeDates comma separated failed');

  const d4 = normalizeScrapeDates(null, null);
  if (JSON.stringify(d4) !== JSON.stringify([null])) throw new Error('normalizeScrapeDates null failed');
  console.log('✅ normalizeScrapeDates tests passed');

  // 2. mergeCsvParts
  const csv1 = 'header1,header2\nval1,val2\nval3,val4';
  const csv2 = 'header1,header2\nval5,val6';
  const merged = mergeCsvParts([csv1, csv2]);
  const expectedMerged = 'header1,header2\nval1,val2\nval3,val4\nval5,val6';
  if (merged !== expectedMerged) {
    throw new Error(`mergeCsvParts failed: got "${merged}"`);
  }
  console.log('✅ mergeCsvParts tests passed');

  // 3. countEffectiveRows
  const count = countEffectiveRows(merged, []);
  if (count !== 3) {
    throw new Error(`countEffectiveRows failed: expected 3, got ${count}`);
  }
  console.log('✅ countEffectiveRows tests passed');

  console.log('\nAll scrape helper tests passed successfully! 🎉');
}

runTests();
