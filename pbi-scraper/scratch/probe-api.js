// scratch/probe-api.js
// Probes apiv2.ademitech.com.br/previas/* endpoints directly from Node.js
// to determine what they return (and whether they hang).
//
// Usage:
//   node scratch/probe-api.js
//   JWT=<token> node scratch/probe-api.js

const axios = require('axios');

// Use JWT from env or the one captured from the last diagnostic run
const JWT = process.env.JWT ||
  'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpZCI6IjY4NjI4NTBlMWYwODAzYWYzM2MzODYzZCIsImlhdCI6MTc4NjkwNjI1Mn0.7oREK4od8opHM9i-KGC0-iYPs8vVOkqKmRfCESGLC3E';

const BASE = 'https://apiv2.ademitech.com.br';

const ENDPOINTS = [
  // The one that hangs in the browser
  '/previas/cadastro/switch',
  // Likely embed-config / powerbi endpoints
  '/previas/cadastro/powerbi',
  '/previas/cadastro/embed',
  '/previas/cadastro/embed-token',
  '/previas/powerbi',
  '/previas/switch',
  // Tenant-level PowerBI
  '/powerbi/embed-config',
  '/powerbi/token',
  '/reports/embed',
];

async function probe(path) {
  const url = `${BASE}${path}`;
  console.log(`\n→ GET ${url}`);
  try {
    const res = await axios.get(url, {
      headers: {
        'Authorization': `Bearer ${JWT}`,
        'Origin': 'https://dashboardbi.ademicon.com.br',
        'Referer': 'https://dashboardbi.ademicon.com.br/',
        'Accept': 'application/json, text/plain, */*',
      },
      timeout: 8000,
      validateStatus: () => true,
    });
    const body = typeof res.data === 'object' ? JSON.stringify(res.data) : String(res.data);
    console.log(`   HTTP ${res.status} → ${body.substring(0, 400)}`);
    return { path, status: res.status, data: res.data };
  } catch (err) {
    if (err.code === 'ECONNABORTED') {
      console.log(`   ⏱  TIMEOUT after 8s`);
    } else {
      console.log(`   ❌ ERROR: ${err.message}`);
    }
    return { path, error: err.message };
  }
}

(async () => {
  console.log(`JWT: ${JWT.substring(0, 30)}...`);
  console.log('Probing apiv2.ademitech.com.br endpoints...\n');

  for (const ep of ENDPOINTS) {
    await probe(ep);
  }

  console.log('\n--- Done ---');
})();
