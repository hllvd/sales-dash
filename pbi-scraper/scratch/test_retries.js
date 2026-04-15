// scratch/test_retries.js
// Mock test to verify retry and timeout logic in extractor.js

const axios = require('axios');
const { scrape } = require('../extractor');

// Mock axios.post
let callCount = { 'Query 1': 0, 'Query 2': 0 };

axios.post = async (url, payload, config) => {
  const label = payload.queries[0].Query.Commands[0].SemanticQueryDataShapeCommand.Query.Select[0].Measure ? 'Query 1' : 'Query 2';
  callCount[label]++;

  console.log(`[MOCK] Received ${label} - Attempt ${callCount[label]}`);

  // Query 1: Fail once, then succeed
  if (label === 'Query 1') {
    if (callCount[label] === 1) {
      throw new Error('Network Error');
    }
    return { status: 200, data: { results: [{ result: { data: { descriptor: { Select: [] }, dsr: { DS: [{ ValueDicts: {}, PH: [] }] } } } }] } };
  }

  // Query 2: Timeout once, then succeed
  if (label === 'Query 2') {
    if (callCount[label] === 1) {
      const err = new Error('timeout of 90000ms exceeded');
      err.code = 'ECONNABORTED';
      throw err;
    }
    return { status: 200, data: { results: [{ result: { data: { descriptor: { Select: [] }, dsr: { DS: [{ ValueDicts: {}, PH: [] }] } } } }] } };
  }
};

async function runTest() {
  process.env.SCRAPE_MAX_RETRIES = '2';
  process.env.SCRAPE_TIMEOUT_MS = '100'; // Short timeout for testing

  console.log('--- Starting Retry Logic Test ---');
  try {
    const result = await scrape('Store', '12345', 'mock-token');
    console.log('\n--- Test Success ---');
    console.log(`Query 1 attempts: ${callCount['Query 1']}`);
    console.log(`Query 2 attempts: ${callCount['Query 2']}`);
    
    if (callCount['Query 1'] === 2 && callCount['Query 2'] === 2) {
      console.log('VERIFIED: Both queries retried and eventually succeeded.');
    } else {
      console.error('FAILED: Incorrect number of retry attempts.');
    }
  } catch (err) {
    console.error('\n--- Test Failed ---');
    console.error(err.message);
  }
}

runTest();
