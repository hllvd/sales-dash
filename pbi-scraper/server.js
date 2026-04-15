// server.js
const express = require('express');
const { v4: uuidv4 } = require('uuid');
const PQueue = require('p-queue').default;
const axios = require('axios');
const fs = require('fs');
const path = require('path');
const { scrape } = require('./extractor');
const { getTokenFromLogin } = require('./auth');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 3001;
const PBI_TOKEN        = process.env.PBI_TOKEN;
const AVAPRO_MATRICULA = process.env.AVAPRO_MATRICULA;
const AVAPRO_PASSWORD  = process.env.AVAPRO_PASSWORD;
const OUTPUT_DIR       = process.env.OUTPUT_DIR || './outputs';

// Cached resolved token (refreshed per request if empty)
let resolvedToken = PBI_TOKEN || null;

/**
 * Returns a valid PowerBI token.
 * If PBI_TOKEN is set, it's used directly.
 * Otherwise it authenticates via Puppeteer using provided or environment credentials.
 */
async function getToken(username, password) {
  if (resolvedToken) return resolvedToken;

  const finalUsername = username || AVAPRO_MATRICULA;
  const finalPassword = password || AVAPRO_PASSWORD;

  if (!finalUsername || !finalPassword) {
    throw new Error('No PBI_TOKEN set and AVAPRO_MATRICULA/AVAPRO_PASSWORD are missing. Cannot authenticate.');
  }

  console.log(`[Server] PBI_TOKEN not set — authenticating via ${username ? 'provided user' : 'environment'} credentials...`);
  resolvedToken = await getTokenFromLogin(finalUsername, finalPassword);
  return resolvedToken;
}

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)) {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

// Queue for concurrency control (max 3 concurrent jobs)
const queue = new PQueue({ concurrency: 3 });

app.post('/jobs', (req, res) => {
  const { store, matricula, callbackUrl, jobId: externalJobId, avaproUsername, avaproPassword } = req.body;

  if (!store || !matricula || !callbackUrl) {
    return res.status(400).json({ error: 'Missing store, matricula, or callbackUrl' });
  }

  const jobId = externalJobId || uuidv4();

  // Enqueue the work but respond immediately
  queue.add(async () => {
    console.log(`[Job ${jobId}] Starting scrape for ${store} - ${matricula}`);
    let result = {
      jobId, 
      status: 'Succeeded',
      rowCount: 0,
      fileRelativePath: null,
      error: null
    };

    try {
      const token = await getToken(avaproUsername, avaproPassword);
      const { rows, csv } = await scrape(store, matricula, token);
      
      if (rows.length > 0) {
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const filename = `scrape_${jobId}_${timestamp}.csv`;
        const filePath = path.join(OUTPUT_DIR, filename);
        
        fs.writeFileSync(filePath, csv, 'utf8');
        
        result.rowCount = rows.length;
        result.fileRelativePath = filename;
      } else {
        result.status = 'Failed';
        result.error = 'No rows returned from PowerBI';
      }
    } catch (err) {
      console.error(`[Job ${jobId}] Failed:`, err.message);
      // If token may have expired, clear it so next request re-authenticates
      if (err.message && (err.message.includes('401') || err.message.includes('403') || err.message.includes('Unauthorized'))) {
        console.warn('[Server] Token may be expired. Clearing cached token for next request.');
        resolvedToken = PBI_TOKEN || null;
      }
      result.status = 'Failed';
      result.error = err.message;
    }

    // Call back to C# API
    try {
      console.log(`[Job ${jobId}] Sending callback to ${callbackUrl}`);
      // Include UserId if provided in the original request (needed for C# callback routing)
      // Actually, C# Orchestrator should probably include UserId in the callbackUrl or the scraper should return everything it received.
      // Let's have the scraper return the userId if it was passed.
      const callbackResult = { ...result, userId: req.body.userId, store, matricula };
      await axios.put(callbackUrl, callbackResult);
    } catch (callbackErr) {
      console.error(`[Job ${jobId}] Callback failed:`, callbackErr.message);
    }
  });

  res.status(202).json({ jobId, status: 'Accepted' });
});

app.get('/health', (req, res) => res.send('OK'));

app.listen(PORT, () => {
  console.log(`PBI Scraper Service running on port ${PORT}`);
  if (!PBI_TOKEN) {
    if (AVAPRO_MATRICULA && AVAPRO_PASSWORD) {
      console.log('PBI_TOKEN not set. Will authenticate via AVAPRO credentials on first job.');
    } else {
      console.warn('WARNING: PBI_TOKEN is not set and AVAPRO_MATRICULA/AVAPRO_PASSWORD are also missing!');
    }
  }
});
