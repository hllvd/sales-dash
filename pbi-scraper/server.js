// server.js
const express = require('express');
const { v4: uuidv4 } = require('uuid');
const PQueue = require('p-queue').default;
const axios = require('axios');
const fs = require('fs');
const path = require('path');
const { scrapeWithReauth, probeStartDate } = require('./extractor');
const { scrapeConsultorDirect } = require('./extractorConsultor');
const { getOrFetchTokens, AuthError } = require('./auth');
const tokenManager = require('./tokenManager');
const { uploadToS3 } = require('./s3Uploader');
const { publishToQueue } = require('./sqsPublisher');

const app = express();
app.use(express.json());

const PORT       = process.env.PORT || 3001;
const PBI_TOKEN  = process.env.PBI_TOKEN;
const OUTPUT_DIR = process.env.OUTPUT_DIR || './outputs';

// If PBI_TOKEN env var is set statically, pre-populate tokenManager
if (PBI_TOKEN) {
  console.log('[Server] Pre-populando tokenManager com PBI_TOKEN estático de ambiente.');
}

/**
 * Normalizes input date parameter into an array of date strings (YYYY-MM or YYYY-MM-DD).
 */
function normalizeScrapeDates(reqScrapeDate, reqScrapeDates) {
  let datesRaw = reqScrapeDates || reqScrapeDate;

  if (Array.isArray(datesRaw)) {
    return datesRaw.map(d => String(d).trim()).filter(Boolean);
  }

  if (typeof datesRaw === 'string' && datesRaw.includes(',')) {
    return datesRaw.split(',').map(d => d.trim()).filter(Boolean);
  }

  if (typeof datesRaw === 'string' && datesRaw.trim()) {
    return [datesRaw.trim()];
  }

  return [null];
}

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)) {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

// Queue for concurrency control (default concurrency: 1 to avoid competing browser sessions)
const scraperConcurrency = parseInt(process.env.SCRAPER_CONCURRENCY || '1', 10);
const queue = new PQueue({ concurrency: scraperConcurrency });

app.post('/jobs', (req, res) => {
  const {
    store,
    matricula,
    callbackUrl,
    jobId: externalJobId,
    avaproUsername,
    avaproPassword,
    scrapeDate: reqScrapeDate,
    scrapeDates: reqScrapeDates,
    scrapeType: reqScrapeType,
    outputMode: reqOutputMode
  } = req.body;

  const scrapeType = (reqScrapeType || 'geral').toLowerCase();
  const outputMode = (reqOutputMode || 'direct').toLowerCase();

  if (!matricula || !callbackUrl) {
    return res.status(400).json({ error: 'Missing matricula or callbackUrl' });
  }

  if (scrapeType !== 'consultor' && !store) {
    return res.status(400).json({ error: 'Missing store for geral scrape' });
  }

  const scrapeDates = normalizeScrapeDates(reqScrapeDate, reqScrapeDates);
  const jobId = externalJobId || uuidv4();

  // Enqueue the work but respond immediately
  queue.add(async () => {
    console.log(`[Job ${jobId}] Starting ${scrapeType} scrape for ${store || 'Consultor'} - ${matricula} (Dates: ${scrapeDates.join(', ')})`);
    
    const jobStartTime = Date.now();
    const jobStartDate = new Date(jobStartTime);

    let result = {
      jobId, 
      status: 'Succeeded',
      rowCount: 0,
      fileRelativePath: null,
      error: null,
      authStatus: 'success',
      authMessage: 'Autenticação bem-sucedida',
      powerbiLoaded: true,
      authSteps: [],
      scrapeDate: scrapeDates.filter(Boolean).join(',') || null,
      detectedStore: store || (scrapeType === 'consultor' ? 'Consultor' : null),
      durationSeconds: 0,
      durationFormatted: '0s',
      startedAt: jobStartDate.toISOString(),
      completedAt: null
    };

    const combinedRows = [];
    const csvParts = [];
    const passwordToUse = avaproPassword;
    const matriculaToUse = avaproUsername || matricula;

    let totalRetryCount = 0;

    try {
      if (scrapeType === 'consultor') {
        console.log(`[Job ${jobId}] Executing Consultor direct scrape for ${matriculaToUse}...`);
        const consultorRes = await scrapeConsultorDirect({
          matricula: matriculaToUse,
          password: passwordToUse,
          scrapeDates,
          outputDir: OUTPUT_DIR
        });

        result.status = consultorRes.status || 'Succeeded';
        result.rowCount = consultorRes.totalRows;
        result.fileRelativePath = consultorRes.csvFile ? path.basename(consultorRes.csvFile) : null;
        result.detectedStore = store || 'Consultor';
        result.scrapeDate = scrapeDates.filter(Boolean).join(',');
        result.authSteps = consultorRes.steps || [];
        result.durationSeconds = consultorRes.durationSeconds || Math.round((Date.now() - jobStartTime) / 1000);
        result.durationFormatted = consultorRes.durationFormatted || `${result.durationSeconds}s`;
        result.startedAt = consultorRes.startedAt || jobStartDate.toISOString();
        result.completedAt = consultorRes.finishedAt || new Date().toISOString();

        if (consultorRes.totalRows === 0) {
          result.status = 'Failed';
          result.error = 'Nenhum registro retornado pelo relatório PowerBI Consultor';
        }
      } else {
        for (const targetDate of scrapeDates) {
          console.log(`[Job ${jobId}] Scraping date ${targetDate}...`);

          const scrapeRes = await scrapeWithReauth(
            store,
            matriculaToUse,
            passwordToUse,
            targetDate,
            getOrFetchTokens,
            3 // max 3 automatic re-auth retries
          );

          if (scrapeRes.detectedStore) {
            result.detectedStore = scrapeRes.detectedStore;
          }

          if (scrapeRes.retryCount) {
            totalRetryCount += scrapeRes.retryCount;
          }

          if (scrapeRes.authSteps && scrapeRes.authSteps.length > 0) {
            result.authSteps = [...(result.authSteps || []), ...scrapeRes.authSteps];
          }

          if (scrapeRes.rows) combinedRows.push(...scrapeRes.rows);
          if (scrapeRes.csv) csvParts.push(scrapeRes.csv);
        }

        result.retryCount = totalRetryCount;
        result.scrapeDate = scrapeDates.join(',');

        // Merge CSV outputs from multiple dates if applicable
        let mergedCsv = '';
        if (csvParts.length > 0) {
          const lines = csvParts[0].split('\n').filter(Boolean);
          const header = lines[0];
          const dataRows = [lines.slice(1).join('\n')];

          for (let i = 1; i < csvParts.length; i++) {
            const pLines = csvParts[i].split('\n').filter(Boolean);
            if (pLines.length > 1) {
              dataRows.push(pLines.slice(1).join('\n'));
            }
          }
          mergedCsv = [header, ...dataRows].filter(Boolean).join('\n');
        }

        const csvRowCount = mergedCsv ? mergedCsv.split('\n').filter(Boolean).length - 1 : 0;
        const effectiveCount = csvRowCount > 0 ? csvRowCount : combinedRows.length;

        if (effectiveCount > 0) {
          const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
          const filename = `scrape_${jobId}_${timestamp}.csv`;
          const filePath = path.join(OUTPUT_DIR, filename);
          
          fs.writeFileSync(filePath, mergedCsv, 'utf8');
          
          result.rowCount = effectiveCount;
          result.fileRelativePath = filename;

          const elapsedSec = Math.round((Date.now() - jobStartTime) / 1000);
          result.durationSeconds = elapsedSec;
          result.durationFormatted = elapsedSec >= 60 ? `${Math.floor(elapsedSec / 60)}m ${elapsedSec % 60}s` : `${elapsedSec}s`;
          result.startedAt = jobStartDate.toISOString();
          result.completedAt = new Date().toISOString();
        } else {
          result.status = 'Failed';
          result.error = 'Nenhum registro retornado pelo relatório PowerBI';
          const elapsedSec = Math.round((Date.now() - jobStartTime) / 1000);
          result.durationSeconds = elapsedSec;
          result.durationFormatted = elapsedSec >= 60 ? `${Math.floor(elapsedSec / 60)}m ${elapsedSec % 60}s` : `${elapsedSec}s`;
          result.completedAt = new Date().toISOString();
        }
      }
    } catch (err) {
      console.error(`[Job ${jobId}] Failed:`, err.message);
      
      const elapsedSec = Math.round((Date.now() - jobStartTime) / 1000);
      result.durationSeconds = result.durationSeconds || elapsedSec;
      result.durationFormatted = result.durationFormatted || (elapsedSec >= 60 ? `${Math.floor(elapsedSec / 60)}m ${elapsedSec % 60}s` : `${elapsedSec}s`);
      result.completedAt = new Date().toISOString();

      if (err instanceof AuthError) {
        result.authStatus = err.authStatus;
        result.authMessage = err.authMessage;
        result.powerbiLoaded = err.powerbiLoaded;
        result.loginSuccess = err.loginSuccess || false;
        result.authSteps = err.steps;
      } else if (err.steps) {
        result.authSteps = [...(result.authSteps || []), ...err.steps];
      }

      result.status = 'Failed';
      result.error = err.authMessage || err.message;
    }

    // If successful and outputMode === 'sqs', upload CSV to S3 and publish to SQS
    if (result.status === 'Succeeded' && outputMode === 'sqs' && result.fileRelativePath) {
      try {
        const localFilePath = path.join(OUTPUT_DIR, result.fileRelativePath);
        const s3Bucket = process.env.SCRAPE_S3_BUCKET || 'hdev-sales-dash';
        const s3Prefix = (process.env.SCRAPE_S3_PREFIX || 'scrape-results/').replace(/^\/+/, '');
        const s3Key = `${s3Prefix}${result.fileRelativePath}`;

        console.log(`[Job ${jobId}] Modo SQS ativado. Fazendo upload para S3: s3://${s3Bucket}/${s3Key}`);
        await uploadToS3(localFilePath, s3Key, s3Bucket, 'text/csv');

        const sqsQueueUrl = process.env.SQS_QUEUE_URL;
        if (sqsQueueUrl) {
          const sqsPayload = {
            jobId,
            runId: req.body.runId,
            userId: req.body.userId,
            s3Bucket,
            s3Key,
            rowCount: result.rowCount,
            matricula,
            store: result.detectedStore || store,
            scrapeDate: result.scrapeDate,
            completedAt: result.completedAt || new Date().toISOString(),
            durationSeconds: result.durationSeconds,
            durationFormatted: result.durationFormatted
          };
          console.log(`[Job ${jobId}] Publicando notificação no SQS...`);
          await publishToQueue(sqsQueueUrl, sqsPayload);
        } else {
          console.warn(`[Job ${jobId}] SQS_QUEUE_URL não configurada; arquivo subiu para S3 mas notificação SQS não foi enviada.`);
        }

        result.outputMode = 'sqs';
        result.s3Key = s3Key;
        result.s3Bucket = s3Bucket;
      } catch (sqsErr) {
        console.error(`[Job ${jobId}] Falha no upload S3 ou publicação SQS:`, sqsErr.message);
        result.status = 'Failed';
        result.error = `Falha no upload S3 / publicação SQS: ${sqsErr.message}`;
      }
    }

    // Call back to C# API
    try {
      console.log(`[Job ${jobId}] Sending callback to ${callbackUrl}`);
      const callbackResult = { 
        ...result, 
        userId: req.body.userId, 
        runId: req.body.runId,
        store: result.detectedStore || store, 
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
  const { matricula, password, store } = req.body;

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
    const authInfo = await getOrFetchTokens(matricula, password, store || null, true);
    let detectedStartDate = null;
    let detectedStore = authInfo.detectedStore || (store && typeof store === 'string' && store.trim() ? store.trim() : null);

    if (authInfo.token) {
      try {
        const probeRes = await probeStartDate(detectedStore || '', matricula, authInfo.token);
        if (probeRes) {
          detectedStartDate = typeof probeRes === 'object' ? probeRes.detectedStartDate : probeRes;
          if (!detectedStore && probeRes.detectedStore) {
            detectedStore = probeRes.detectedStore;
          }
        }
      } catch (probeErr) {
        console.warn(`[Test Auth] StartDate probing skipped/failed: ${probeErr.message}`);
      }
    }
    res.json({ 
      success: true, 
      loginSuccess: true,
      message: authInfo.authMessage || 'Autenticação bem-sucedida.',
      authStatus: authInfo.authStatus,
      powerbiLoaded: authInfo.powerbiLoaded,
      detectedStore: detectedStore || null,
      detectedStartDate: detectedStartDate,
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
    console.log('PBI_TOKEN não configurado. Autenticação automatizada utilizará tokenManager cache.');
  }
});
