// server.js
const express = require('express');
const { v4: uuidv4 } = require('uuid');
const PQueue = require('p-queue').default;
const axios = require('axios');
const fs = require('fs');
const path = require('path');
const { scrape } = require('./extractor');

const app = express();
app.use(express.json());

const PORT = process.env.PORT || 3001;
const PBI_TOKEN = process.env.PBI_TOKEN;
const OUTPUT_DIR = process.env.OUTPUT_DIR || './outputs';

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)) {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

// Queue for concurrency control (max 3 concurrent jobs)
const queue = new PQueue({ concurrency: 3 });

app.post('/jobs', (req, res) => {
  const { store, matricula, callbackUrl, jobId: externalJobId } = req.body;

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
      const { rows, csv } = await scrape(store, matricula, PBI_TOKEN);
      
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
      result.status = 'Failed';
      result.error = err.message;
    }

    // Call back to C# API
    try {
      console.log(`[Job ${jobId}] Sending callback to ${callbackUrl}`);
      await axios.put(callbackUrl, result);
    } catch (callbackErr) {
      console.error(`[Job ${jobId}] Callback failed:`, callbackErr.message);
    }
  });

  res.status(202).json({ jobId, status: 'Accepted' });
});

app.get('/health', (req, res) => res.send('OK'));

app.listen(PORT, () => {
  console.log(`PBI Scraper Service running on port ${PORT}`);
  if (!PBI_TOKEN) console.warn('WARNING: PBI_TOKEN environment variable is not set!');
});
