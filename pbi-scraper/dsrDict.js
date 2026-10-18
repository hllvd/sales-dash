'use strict';

/**
 * Pure, deterministic resolver for PowerBI DSR ValueDicts.
 *
 * In PowerBI DSR, columns associated with a ValueDict (DN property in schema) can supply:
 * 1. An integer dictionary index (number) referencing an entry in ValueDicts[DN] (e.g. 0..99).
 * 2. A literal value (typically string) when the value was not present in the dictionary
 *    or when the dictionary limit was reached (e.g. '82', '2', '28', '50').
 *
 * In JavaScript, accessing an array with a string index like arr['82'] coerces the string to
 * an integer, inadvertently replacing valid literal strings with whatever value happens to
 * occupy that dictionary index.
 *
 * resolveDictValue ensures that:
 * - Literals (strings, booleans, objects) are returned as-is and never used as dictionary indices.
 * - Only non-negative integers strictly within the bounds of the dictionary array are resolved.
 * - Out-of-bounds or non-integer numbers are returned as-is (graceful fallback).
 *
 * @param {Array|undefined|null} dict - The ValueDict array (e.g. dicts[dictKey])
 * @param {*} val - The raw resolved value from the row entry
 * @param {object} [colMeta] - Optional column schema metadata (e.g. { N, T, DN })
 * @returns {*} The resolved value
 */
function resolveDictValue(dict, val, colMeta) {
  if (!Array.isArray(dict) || dict.length === 0) {
    return val;
  }

  if (val === null || val === undefined) {
    return val;
  }

  // String values are ALWAYS literals in DSR; never treat a string as an array index.
  if (typeof val === 'string') {
    return val;
  }

  // Only integer numbers within array bounds are valid dictionary indices.
  if (typeof val === 'number' && Number.isInteger(val) && val >= 0 && val < dict.length) {
    return dict[val];
  }

  // Fallback for non-index numbers, floats, or unexpected types
  return val;
}

module.exports = {
  resolveDictValue
};
