// test-extractorConsultor.js
// Unit test for pure helper functions in extractorConsultor.js

const assert = require('assert');
const {
  normalizeTargetMonths,
  buildDateRangeFilter
} = require('./extractorConsultor');

console.log('Testing normalizeTargetMonths...');
assert.deepStrictEqual(normalizeTargetMonths(null), []);
assert.deepStrictEqual(normalizeTargetMonths(''), []);
assert.deepStrictEqual(normalizeTargetMonths('2024-06, 2024-07'), ['2024-06', '2024-07']);
assert.deepStrictEqual(normalizeTargetMonths(['2024-06-01', '2024-07-15']), ['2024-06', '2024-07']);
console.log('✅ normalizeTargetMonths passed!');

console.log('Testing buildDateRangeFilter...');
const df1 = buildDateRangeFilter('2', ['2026-07']);
assert.strictEqual(df1.length, 1);
const leftComp = df1[0].Condition.And.Left.Comparison;
const rightComp = df1[0].Condition.And.Right.Comparison;
assert.strictEqual(leftComp.ComparisonKind, 2); // >=
assert.strictEqual(leftComp.Right.Literal.Value, "datetime'2026-07-01T00:00:00'");
assert.strictEqual(rightComp.ComparisonKind, 3); // <
assert.strictEqual(rightComp.Right.Literal.Value, "datetime'2026-08-01T00:00:00'");
console.log('✅ buildDateRangeFilter passed!');

console.log('\n🎉 ALL UNIT TESTS PASSED!');
