// server.js
const express = require('express');
const { v4: uuidv4 } = require('uuid');
const PQueue = require('p-queue').default;
const axios = require('axios');
const fs = require('fs');
const path = require('path');
const { scrape } = require('./extractor');
const { getTokenFromLogin, AuthError } = require('./auth');

const app = express();
app.use(express.json());

const PORT       = process.env.PORT || 3001;
const PBI_TOKEN  = process.env.PBI_TOKEN;
const OUTPUT_DIR = process.env.OUTPUT_DIR || './outputs';

// Cached resolved token (refreshed per request if empty)
let resolvedToken = PBI_TOKEN || null;

/**
 * Returns a valid PowerBI token and diagnostic details.
 */
async function getToken(username, password) {
  if (resolvedToken) {
    return {
      token: resolvedToken,
      authStatus: 'success',
      authMessage: 'PBI_TOKEN reutilizado',
      powerbiLoaded: true,
      steps: ['[Server] PBI_TOKEN estático de ambiente utilizado.']
    };
  }

  if (!username || !password) {
    throw new AuthError(
      'invalid-credentials',
      'Matrícula e Senha são obrigatórias na configuração de extração.',
      ['[Server] Erro: Matrícula ou Senha não fornecidas na configuração de extração.']
    );
  }

  console.log(`[Server] Autenticando para matrícula ${username}...`);
  return await getTokenFromLogin(username, password);
}

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)) {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

// Queue for concurrency control (max 3 concurrent jobs)
const queue = new PQueue({ concurrency: 3 });

app.post('/jobs', (req, res) => {
  const { store, matricula, callbackUrl, jobId: externalJobId, avaproUsername, avaproPassword, scrapeDate: reqScrapeDate } = req.body;

  let scrapeDate = reqScrapeDate;
  if (!scrapeDate) {
    // Default to the previous month's data
    const d = new Date();
    d.setMonth(d.getMonth() - 1);
    const yyyy = d.getFullYear();
    const mm = String(d.getMonth() + 1).padStart(2, '0');
    scrapeDate = `${yyyy}-${mm}`;
  }

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
      error: null,
      authStatus: 'success',
      authMessage: 'Autenticação bem-sucedida',
      powerbiLoaded: true,
      authSteps: []
    };

    try {
      const authInfo = await getToken(avaproUsername || matricula, avaproPassword);
      result.authStatus = authInfo.authStatus;
      result.authMessage = authInfo.authMessage;
      result.powerbiLoaded = authInfo.powerbiLoaded;
      result.authSteps = authInfo.steps;

      const { rows, csv, steps: extractorSteps } = await scrape(store, matricula, authInfo.token, scrapeDate);

      // Append extractor steps (Query results, contract count) to the auth steps log
      if (extractorSteps && extractorSteps.length > 0) {
        result.authSteps = [...(result.authSteps || []), ...extractorSteps];
      }

      // rowCount = only the CSV/contract detail rows (Query 2), not the combined total
      const csvRowCount = csv ? csv.split('\n').filter(Boolean).length - 1 : 0;
      const effectiveCount = csvRowCount > 0 ? csvRowCount : rows.length;

      if (effectiveCount > 0) {
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const filename = `scrape_${jobId}_${timestamp}.csv`;
        const filePath = path.join(OUTPUT_DIR, filename);
        
        fs.writeFileSync(filePath, csv, 'utf8');
        
        result.rowCount = effectiveCount;
        result.fileRelativePath = filename;
      } else {
        result.status = 'Failed';
        result.error = 'Nenhum registro retornado pelo relatório PowerBI';
      }
    } catch (err) {
      console.error(`[Job ${jobId}] Failed:`, err.message);
      
      if (err instanceof AuthError) {
        result.authStatus = err.authStatus;
        result.authMessage = err.authMessage;
        result.powerbiLoaded = err.powerbiLoaded;
        result.loginSuccess = err.loginSuccess || false;
        result.authSteps = err.steps;
      } else if (err.steps) {
        result.authSteps = [...(result.authSteps || []), ...err.steps];
      }

      if (err.message && (err.message.includes('401') || err.message.includes('403') || err.message.includes('Unauthorized'))) {
        console.warn('[Server] Token expirado ou inválido. Limpando token em cache.');
        resolvedToken = PBI_TOKEN || null;
      }
      
      result.status = 'Failed';
      result.error = err.authMessage || err.message;
    }

    // Call back to C# API
    try {
      console.log(`[Job ${jobId}] Sending callback to ${callbackUrl}`);
      const callbackResult = { 
        ...result, 
        userId: req.body.userId, 
        runId: req.body.runId,
        store, 
        matricula 
      };
      await axios.put(callbackUrl, callbackResult);
    } catch (callbackErr) {
      console.error(`[Job ${jobId}] Callback failed:`, callbackErr.message);
    }
  });

  res.status(202).json({ jobId, status: 'Accepted' });
});

app.post('/test-auth', async (req, res) => {
  const { matricula, password } = req.body;

  if (!matricula || !password) {
    return res.status(400).json({ 
      success: false, 
      loginSuccess: false,
      message: 'Matrícula e Senha são obrigatórias para testar a autenticação.',
      steps: ['[Server] Matrícula ou Senha ausentes.']
    });
  }

  console.log(`[Test Auth] Testing credentials for ${matricula}...`);
  try {
    const authInfo = await getTokenFromLogin(matricula, password);
    res.json({ 
      success: true, 
      loginSuccess: true,
      message: authInfo.authMessage || 'Autenticação bem-sucedida.',
      authStatus: authInfo.authStatus,
      powerbiLoaded: authInfo.powerbiLoaded,
      steps: authInfo.steps
    });
  } catch (err) {
    console.error(`[Test Auth] Failed for ${matricula}:`, err.message);
    res.json({ 
      success: false, 
      loginSuccess: err.loginSuccess || false,
      message: err.authMessage || err.message,
      authStatus: err.authStatus || 'error',
      powerbiLoaded: err.powerbiLoaded || false,
      steps: err.steps || [`[Test Auth] Erro: ${err.message}`]
    });
  }
});

app.get('/health', (req, res) => res.send('OK'));

app.listen(PORT, () => {
  console.log(`PBI Scraper Service running on port ${PORT}`);
  if (!PBI_TOKEN) {
    console.log('PBI_TOKEN não configurado. Autenticação será realizada por requisição utilizando credenciais da configuração.');
  }
});
