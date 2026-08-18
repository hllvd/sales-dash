// scratch/inspect-dashboardbi.js
// Downloads next.js bundles from dashboardbi.ademicon.com.br to analyze
// how PowerBI embedding and MWCToken generation are performed.

const axios = require('axios');
const fs    = require('fs');
const path  = require('path');

const BASE = 'https://dashboardbi.ademicon.com.br';

const CHUNKS = [
  '/_next/static/chunks/app/page-4a12a5672be016a2.js',
  '/_next/static/chunks/app/layout-0bd52e693286b7fe.js',
  '/_next/static/chunks/main-app-2ecc3bf42ae2d122.js',
  '/_next/static/chunks/125-46def724761c07b5.js',
];

async function run() {
  const outDir = path.join(__dirname, 'debug', 'next_chunks');
  if (!fs.existsSync(outDir)) fs.mkdirSync(outDir, { recursive: true });

  for (const chunk of CHUNKS) {
    const url = `${BASE}${chunk}`;
    console.log(`Downloading ${url}...`);
    try {
      const res = await axios.get(url, { headers: { 'User-Agent': 'Mozilla/5.0' } });
      const filename = path.basename(chunk);
      fs.writeFileSync(path.join(outDir, filename), res.data, 'utf8');
      console.log(`  Saved ${filename} (${res.data.length} bytes)`);

      // Search for API routes, powerbi, embed, token keywords
      const matches = res.data.match(/(https?:\/\/[^\s"'`]+|\/api\/[^\s"'`]+|powerbi|embed[A-Za-z0-9_-]+|token[A-Za-z0-9_-]+)/gi) || [];
      const unique = Array.from(new Set(matches)).filter(m => m.length > 5 && m.length < 100);
      console.log(`  Found keywords:`, unique.slice(0, 15));
    } catch (e) {
      console.error(`  Failed: ${e.message}`);
    }
  }
}

run();
