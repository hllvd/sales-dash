// scratch/test_date_logic.js
// Verify that getCalendarFilters generates the correct PowerBI conditions

const { scrape } = require('../extractor');

// Mock axios.post to capture the payload
const axios = require('axios');
let capturedPayloads = [];
axios.post = async (url, payload) => {
  capturedPayloads.push(payload);
  return { status: 200, data: { results: [] } };
};

async function testDate(dateStr) {
  capturedPayloads = [];
  console.log(`\nTesting Date Format: "${dateStr || 'Default'}"`);
  
  try {
    await scrape('Store', '123', 'token', dateStr);
    
    const p1 = capturedPayloads[0];
    const where = p1.queries[0].Query.Where;
    
    // Find calendar filters
    const calendarFilters = where.filter(w => 
      w.Condition?.In?.Expressions?.[0]?.Column?.Expression?.SourceRef?.Source === 'c'
    );
    
    console.log(`Filters generated: ${calendarFilters.length}`);
    calendarFilters.forEach((f, i) => {
      const prop = f.Condition.In.Expressions[0].Column.Property;
      const val = f.Condition.In.Values[0][0].Literal.Value;
      console.log(`  [${i}] ${prop}: ${val}`);
    });

    // Validations
    if (!dateStr) {
      const year = new Date().getFullYear();
      if (calendarFilters[0]?.Condition?.In?.Values[0][0]?.Literal.Value !== `${year}L`) {
        console.error('FAIL: Default year incorrect');
      }
    } else {
      const parts = dateStr.split('-');
      if (parts[0] && !calendarFilters.some(f => f.Condition.In.Expressions[0].Column.Property === 'Ano' && f.Condition.In.Values[0][0].Literal.Value === `${parts[0]}L`)) {
        console.error('FAIL: Year filter missing or incorrect');
      }
      if (parts[1] && !calendarFilters.some(f => f.Condition.In.Expressions[0].Column.Property === 'Mês' && f.Condition.In.Values[0][0].Literal.Value === `${parseInt(parts[1])}L`)) {
        console.error('FAIL: Month filter missing or incorrect');
      }
      if (parts[2] && !calendarFilters.some(f => f.Condition.In.Expressions[0].Column.Property === 'Dia' && f.Condition.In.Values[0][0].Literal.Value === `${parseInt(parts[2])}L`)) {
        console.error('FAIL: Day filter missing or incorrect');
      }
    }
  } catch (err) {
    console.error('Test error:', err.message);
  }
}

async function runTests() {
  await testDate('');           // Default
  await testDate('2024');       // Year only
  await testDate('2024-05');    // Year and Month
  await testDate('2024-05-15'); // Year, Month, Day
  console.log('\n--- Date Logic Verification Complete ---');
}

runTests();
