// worker.js
if (process.env.NODE_ENV !== 'production') {
  try {
    require('dotenv').config();
  } catch {
    // ignore if dotenv is not available
  }
}

const fs = require('fs');
const path = require('path');
const axios = require('axios');
const { SQSClient, ReceiveMessageCommand, DeleteMessageCommand } = require('@aws-sdk/client-sqs');
const { runScrapeJob } = require('./scrape');
const { decryptField } = require('./crypto');
const { uploadToS3 } = require('./s3Uploader');
const { publishToQueue } = require('./sqsPublisher');
const tokenManager = require('./tokenManager');

// Configuration
const AWS_REGION = process.env.AWS_REGION || 'us-east-1';
const SQS_JOBS_QUEUE_URL = (process.env.SQS_JOBS_QUEUE_URL || process.env.SQS_QUEUE_URL || '').trim();
const SQS_RESULTS_QUEUE_URL = (process.env.SQS_RESULTS_QUEUE_URL || '').trim();
const SCRAPER_ENCRYPTION_KEY = (process.env.SCRAPER_ENCRYPTION_KEY || '').trim();
const SCRAPE_S3_BUCKET = process.env.SCRAPE_S3_BUCKET || 'hdev-sales-dash';
const SCRAPE_S3_PREFIX = (process.env.SCRAPE_S3_PREFIX || 'scrape-results/').replace(/^\/+/, '');
const CALLBACK_BASE_URL = process.env.CALLBACK_BASE_URL ? process.env.CALLBACK_BASE_URL.replace(/\/+$/, '') : '';
const OUTPUT_DIR = process.env.OUTPUT_DIR || './outputs';
const RESULTS_CONSUMER_MODE = process.env.RESULTS_CONSUMER_MODE === 'true';
const SCALE_TO_ZERO = RESULTS_CONSUMER_MODE ? false : process.env.SCALE_TO_ZERO !== 'false';
const IDLE_TIMEOUT_MS = parseInt(process.env.IDLE_TIMEOUT_MS || '300000', 10); // 5 minutes default (300,000 ms)
const LOCK_FILE_PATH = process.env.LOCK_FILE_PATH || '/tmp/worker.lock';
const MOCK_QUEUE_FILE = path.join(OUTPUT_DIR, 'mock_queue.json');

let shuttingDown = false;
let isProcessing = false;
let sqsClient = null;

// Ensure output directory exists
if (!fs.existsSync(OUTPUT_DIR)) {
  try {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  } catch (err) {
    console.error(`[Worker] Failed to create output directory: ${err.message}`);
  }
}

/**
 * Creates lock file for ECS Fargate container health checks.
 */
function createHealthLockFile() {
  try {
    fs.writeFileSync(LOCK_FILE_PATH, `running: ${new Date().toISOString()}\n`, 'utf8');
  } catch (err) {
    console.warn(`[Worker] Could not write lock file at ${LOCK_FILE_PATH}: ${err.message}`);
  }
}

/**
 * Removes health check lock file on exit.
 */
function removeHealthLockFile() {
  try {
    if (fs.existsSync(LOCK_FILE_PATH)) {
      fs.unlinkSync(LOCK_FILE_PATH);
    }
  } catch (err) {
    console.warn(`[Worker] Could not remove lock file at ${LOCK_FILE_PATH}: ${err.message}`);
  }
}

/**
 * Structured logger.
 */
function log(level, message, metadata = {}) {
  const logEntry = {
    timestamp: new Date().toISOString(),
    level,
    service: 'pbi-scraper-worker',
    message,
    ...metadata
  };
  console.log(JSON.stringify(logEntry));
}

/**
 * Handles graceful shutdown on SIGTERM / SIGINT.
 */
function setupSignalHandlers() {
  const handleShutdown = (signal) => {
    if (shuttingDown) return;
    shuttingDown = true;
    log('INFO', `Received ${signal}. Initiating graceful shutdown...`);
    removeHealthLockFile();
    // Allow short drain window
    setTimeout(() => {
      log('INFO', 'Graceful shutdown complete. Exiting.');
      process.exit(0);
    }, 1000).unref();
  };

  process.on('SIGTERM', () => handleShutdown('SIGTERM'));
  process.on('SIGINT', () => handleShutdown('SIGINT'));
}

/**
 * Initializes SQS Client if SQS_JOBS_QUEUE_URL is provided.
 */
function initSqsClient() {
  if (SQS_JOBS_QUEUE_URL) {
    sqsClient = new SQSClient({ region: AWS_REGION });
    log('INFO', `SQS client initialized for jobs queue: ${SQS_JOBS_QUEUE_URL}`);
  } else {
    log('INFO', `No SQS_JOBS_QUEUE_URL configured. Using local mock queue at ${MOCK_QUEUE_FILE}`);
  }
}

/**
 * Reads and pops one message from the local mock queue file.
 */
function pollMockQueue() {
  if (!fs.existsSync(MOCK_QUEUE_FILE)) {
    return null;
  }
  try {
    const raw = fs.readFileSync(MOCK_QUEUE_FILE, 'utf8');
    const queue = JSON.parse(raw);
    if (!Array.isArray(queue) || queue.length === 0) {
      return null;
    }
    const message = queue.shift();
    fs.writeFileSync(MOCK_QUEUE_FILE, JSON.stringify(queue, null, 2), 'utf8');
    return {
      ReceiptHandle: `mock-${Date.now()}`,
      Body: typeof message === 'string' ? message : JSON.stringify(message)
    };
  } catch (err) {
    log('ERROR', `Error reading mock queue: ${err.message}`);
    return null;
  }
}

/**
 * Polls for one message from SQS jobs queue or mock queue.
 */
async function receiveNextMessage() {
  const targetQueueUrl = RESULTS_CONSUMER_MODE ? SQS_RESULTS_QUEUE_URL : SQS_JOBS_QUEUE_URL;

  if (!sqsClient) {
    // Local mock queue mode
    const msg = pollMockQueue();
    if (!msg) {
      // Simulate polling delay
      await new Promise(resolve => setTimeout(resolve, 1000));
    }
    return msg;
  }

  const command = new ReceiveMessageCommand({
    QueueUrl: targetQueueUrl,
    MaxNumberOfMessages: 1,
    WaitTimeSeconds: 20, // Long-polling
    VisibilityTimeout: RESULTS_CONSUMER_MODE ? 60 : 900 // 1 minute for results ingestion vs 15 min for scrape
  });

  const response = await sqsClient.send(command);
  if (response.Messages && response.Messages.length > 0) {
    return response.Messages[0];
  }
  return null;
}

/**
 * Deletes a processed message from SQS jobs queue or mock queue.
 */
async function deleteMessage(receiptHandle, customQueueUrl = null) {
  if (!sqsClient || receiptHandle.startsWith('mock-')) {
    log('DEBUG', `Mock message ${receiptHandle} marked as deleted`);
    return;
  }

  const targetQueueUrl = customQueueUrl || (RESULTS_CONSUMER_MODE ? SQS_RESULTS_QUEUE_URL : SQS_JOBS_QUEUE_URL);

  const command = new DeleteMessageCommand({
    QueueUrl: targetQueueUrl,
    ReceiptHandle: receiptHandle
  });

  await sqsClient.send(command);
  log('DEBUG', `Message successfully deleted from SQS queue (${targetQueueUrl})`);
}

/**
 * Decrypts credentials from payload.
 * Plain text matrícula is standard; password is encrypted with AES-256-GCM.
 * 
 * @param {object} payload
 * @param {string} [encryptionKey]
 * @returns {{ username: string, password: string }}
 */
function resolveCredentials(payload, encryptionKey = (process.env.SCRAPER_ENCRYPTION_KEY ? process.env.SCRAPER_ENCRYPTION_KEY.trim() : '')) {
  let plainUsername = payload.matricula || '';
  let plainPassword = payload.password || payload.avaproPassword || '';

  // Legacy support if encrypted username is present
  if (payload.encryptedUsername && payload.usernameIv && payload.usernameAuthTag) {
    if (!encryptionKey) {
      throw new Error('SCRAPER_ENCRYPTION_KEY is required to decrypt username');
    }
    plainUsername = decryptField(
      payload.encryptedUsername,
      payload.usernameIv,
      payload.usernameAuthTag,
      encryptionKey
    );
  }

  // Decrypt password with AES-256-GCM
  if (payload.encryptedPassword && payload.passwordIv && payload.passwordAuthTag) {
    if (!encryptionKey) {
      throw new Error('SCRAPER_ENCRYPTION_KEY is required to decrypt password');
    }
    plainPassword = decryptField(
      payload.encryptedPassword,
      payload.passwordIv,
      payload.passwordAuthTag,
      encryptionKey
    );
  }

  if (!plainUsername || !plainPassword) {
    throw new Error('Missing credentials (matricula or password) in message payload');
  }

  return { username: plainUsername, password: plainPassword };
}

/**
 * Processes a message from the results queue (RESULTS_CONSUMER_MODE = true).
 * Downloads CSV from S3 and calls API import callback.
 */
async function processResultMessage(payload, receiptHandle) {
  const { s3Bucket, s3Key, jobId, runId, userId, matricula } = payload;
  const targetCallbackUrl = CALLBACK_BASE_URL ? `${CALLBACK_BASE_URL}/api/scrape/import-s3` : null;

  log('INFO', `[Results Consumer] Recebida notificação de conclusão para job ${jobId}`, {
    s3Bucket,
    s3Key,
    matricula,
    runId
  });

  if (!s3Key) {
    log('WARN', `[Results Consumer] Mensagem ${jobId} sem s3Key. Descartando mensagem.`, { payload });
    await deleteMessage(receiptHandle, SQS_RESULTS_QUEUE_URL);
    return;
  }

  if (!targetCallbackUrl) {
    log('ERROR', `[Results Consumer] CALLBACK_BASE_URL não configurado. Não é possível acionar a importação da API.`);
    return;
  }

  try {
    const response = await axios.post(targetCallbackUrl, {
      s3Bucket: s3Bucket || SCRAPE_S3_BUCKET,
      s3Key,
      jobId,
      runId,
      userId,
      matricula
    }, { timeout: 120000 });

    log('INFO', `[Results Consumer] Importação concluída com sucesso para job ${jobId}`, {
      status: response.status,
      data: response.data
    });

    await deleteMessage(receiptHandle, SQS_RESULTS_QUEUE_URL);
  } catch (err) {
    log('ERROR', `[Results Consumer] Falha ao acionar importação da API para job ${jobId}: ${err.message}`, {
      response: err.response?.data
    });
    // Se a chave não existir no S3, remove a mensagem para não travar
    if (err.response?.status === 404 || (err.response?.data?.message && err.response.data.message.includes('NoSuchKey'))) {
      log('WARN', `[Results Consumer] Arquivo no S3 inexistente para job ${jobId}. Deletando mensagem órfã.`);
      await deleteMessage(receiptHandle, SQS_RESULTS_QUEUE_URL);
    }
  }
}

/**
 * Processes a single scrape job message.
 */
async function processMessage(rawMessage) {
  const receiptHandle = rawMessage.ReceiptHandle;
  let payload;

  try {
    payload = JSON.parse(rawMessage.Body);
  } catch (err) {
    log('ERROR', `Invalid JSON in message body: ${err.message}`, { body: rawMessage.Body });
    // Corrupted message format - delete to avoid poison loop
    await deleteMessage(receiptHandle);
    return;
  }

  if (RESULTS_CONSUMER_MODE) {
    await processResultMessage(payload, receiptHandle);
    return;
  }

  const jobId = payload.jobId || `job-${Date.now()}`;
  const runId = payload.runId || null;
  const userId = payload.userId || null;
  const matricula = payload.matricula || '';
  const store = payload.store || payload.unit || null;
  const scrapeDate = payload.scrapeDate || null;
  const scrapeDates = payload.scrapeDates || null;
  const scrapeType = (payload.scrapeType || 'geral').toLowerCase();
  const callbackUrl = payload.callbackUrl || (CALLBACK_BASE_URL ? `${CALLBACK_BASE_URL}/api/scrape/callback` : null);

  log('INFO', `Starting ${scrapeType} scrape processing for job ${jobId}`, { jobId, runId, matricula, store, scrapeDate, scrapeType });

  let credentials;
  try {
    credentials = resolveCredentials(payload);
  } catch (credErr) {
    log('ERROR', `Credential resolution failed for job ${jobId}: ${credErr.message}`, { jobId });
    
    // Notify backend callback if configured
    if (callbackUrl) {
      try {
        await axios.put(callbackUrl, {
          jobId,
          runId,
          userId,
          matricula,
          store,
          status: 'Failed',
          error: `Falha nas credenciais: ${credErr.message}`,
          authStatus: 'error',
          authMessage: credErr.message,
          powerbiLoaded: false,
          loginSuccess: false
        });
      } catch (cbErr) {
        log('ERROR', `Failed to send callback for bad credentials: ${cbErr.message}`, { jobId });
      }
    }

    // Publish failure notification to results queue if configured
    if (SQS_RESULTS_QUEUE_URL) {
      try {
        await publishToQueue(SQS_RESULTS_QUEUE_URL, {
          jobId,
          runId,
          userId,
          matricula,
          store,
          status: 'Failed',
          error: `Falha nas credenciais: ${credErr.message}`,
          timestamp: new Date().toISOString()
        });
      } catch (sqsErr) {
        log('ERROR', `Failed to publish credential failure to results queue: ${sqsErr.message}`, { jobId });
      }
    }

    // Invalid credentials payload is permanent failure - delete message
    await deleteMessage(receiptHandle);
    return;
  }

  const matriculaToUse = credentials.username || matricula;

  // Circuit Breaker check: abort immediately if account failed or run is aborted to prevent Ava Pro lockout
  const isLocked = tokenManager.isAuthLocked(matriculaToUse);
  const isBatchAborted = runId && tokenManager.isRunAborted(runId);

  if (isLocked || isBatchAborted) {
    const lockInfo = tokenManager.getAuthFailureReason(matriculaToUse);
    const abortReason = isLocked
      ? `Disjuntor de segurança ativo para matrícula ${matriculaToUse}: ${lockInfo?.reason || 'Senha incorreta detectada anteriormente'}. Execução cancelada para evitar bloqueio no AVA PRO.`
      : `Lote ${runId} abortado por falha anterior. Execução cancelada para evitar bloqueio no AVA PRO.`;

    log('WARN', `Job ${jobId} abortado pelo disjuntor de segurança: ${abortReason}`, { jobId, matricula: matriculaToUse, runId });

    if (callbackUrl) {
      try {
        await axios.put(callbackUrl, {
          jobId,
          runId,
          userId,
          matricula: matriculaToUse,
          store,
          status: 'Failed',
          rowCount: 0,
          error: abortReason,
          authStatus: 'wrong-password',
          authMessage: abortReason,
          powerbiLoaded: false,
          loginSuccess: false,
          authSteps: ['[Disjuntor SQS] Mensagem descartada para proteção contra bloqueio no AVA PRO.'],
          scrapeDate: scrapeDate || (scrapeDates ? scrapeDates.join(',') : null)
        }, { timeout: 30000 });
      } catch (cbErr) {
        log('WARN', `Abort callback failed for ${jobId}: ${cbErr.message}`, { jobId });
      }
    }

    // Delete message from SQS queue to avoid poison loops and failed retries
    await deleteMessage(receiptHandle);
    return;
  }

  // Execute Scraping
  let scrapeResult;
  try {
    scrapeResult = await runScrapeJob({
      jobId,
      store,
      matricula: credentials.username,
      password: credentials.password,
      scrapeDate,
      scrapeDates,
      scrapeType,
      outputDir: OUTPUT_DIR,
      maxReauthRetries: 3
    });
  } catch (err) {
    log('ERROR', `Unexpected exception in runScrapeJob for ${jobId}: ${err.message}`, { jobId });
    // Let message retry via SQS visibility timeout on transient crash
    return;
  }

  if (scrapeResult.authStatus === 'wrong-password') {
    log('WARN', `Senha incorreta detectada ('wrong-password'). Acionando disjuntor para matrícula ${matriculaToUse}...`, { jobId, matricula: matriculaToUse, runId });
    tokenManager.markAuthFailure(matriculaToUse, scrapeResult.authMessage || scrapeResult.error);
    if (runId) {
      tokenManager.abortRun(runId, scrapeResult.authMessage || scrapeResult.error);
    }
  }

  // Save CSV output to file if rows were returned
  let fileRelativePath = null;
  let fullFilePath = null;
  if (scrapeResult.rowCount > 0 && scrapeResult.csv) {
    try {
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `scrape_${jobId}_${timestamp}.csv`;
      fullFilePath = path.join(OUTPUT_DIR, filename);
      fs.writeFileSync(fullFilePath, scrapeResult.csv, 'utf8');
      fileRelativePath = filename;
      log('INFO', `CSV saved to ${fullFilePath}`, { jobId, filename, rowCount: scrapeResult.rowCount });
    } catch (fsErr) {
      log('ERROR', `Failed to write CSV file for job ${jobId}: ${fsErr.message}`, { jobId });
    }
  } else if (scrapeResult.fileRelativePath) {
    fileRelativePath = scrapeResult.fileRelativePath;
    fullFilePath = path.join(OUTPUT_DIR, fileRelativePath);
  }

  // Upload to S3 if successful and file exists
  let s3Key = null;
  if (scrapeResult.status === 'Succeeded' && fullFilePath && fs.existsSync(fullFilePath)) {
    try {
      s3Key = `${SCRAPE_S3_PREFIX}${fileRelativePath}`;
      log('INFO', `Uploading CSV to S3: s3://${SCRAPE_S3_BUCKET}/${s3Key}`, { jobId, bucket: SCRAPE_S3_BUCKET, key: s3Key });
      await uploadToS3(fullFilePath, s3Key, SCRAPE_S3_BUCKET, 'text/csv');
      log('INFO', `Upload to S3 completed: s3://${SCRAPE_S3_BUCKET}/${s3Key}`, { jobId });
    } catch (s3Err) {
      log('ERROR', `Failed to upload CSV to S3: ${s3Err.message}`, { jobId, bucket: SCRAPE_S3_BUCKET, key: s3Key });
      // If S3 upload fails, do not mark completed - let message retry
      return;
    }
  }

  // Publish notification to SQS Results Queue if configured
  if (SQS_RESULTS_QUEUE_URL && scrapeResult.status === 'Succeeded') {
    try {
      const resultsPayload = {
        jobId,
        runId,
        userId,
        s3Bucket: SCRAPE_S3_BUCKET,
        s3Key: s3Key,
        rowCount: scrapeResult.rowCount,
        matricula,
        store: scrapeResult.detectedStore || store,
        scrapeDate: scrapeResult.scrapeDate,
        scrapeType,
        status: scrapeResult.status,
        durationSeconds: scrapeResult.durationSeconds || null,
        durationFormatted: scrapeResult.durationFormatted || null,
        authStatus: scrapeResult.authStatus || 'success',
        authMessage: scrapeResult.authMessage || 'Autenticação bem-sucedida',
        powerbiLoaded: scrapeResult.powerbiLoaded !== false,
        authSteps: scrapeResult.authSteps || [],
        retryCount: scrapeResult.retryCount || 0,
        completedAt: new Date().toISOString(),
        timestamp: new Date().toISOString()
      };
      log('INFO', `Publishing result to SQS queue: ${SQS_RESULTS_QUEUE_URL}`, { jobId, resultsPayload });
      await publishToQueue(SQS_RESULTS_QUEUE_URL, resultsPayload);
      log('INFO', `Result notification published to SQS results queue`, { jobId });
    } catch (sqsErr) {
      log('ERROR', `Failed to publish result to SQS results queue: ${sqsErr.message}`, { jobId });
      // If SQS publishing fails, do not delete message so it can be retried
      return;
    }
  }

  // Optional HTTP callback fallback
  if (callbackUrl) {
    const callbackPayload = {
      jobId,
      runId,
      userId,
      matricula,
      store: scrapeResult.detectedStore || store,
      detectedStore: scrapeResult.detectedStore,
      status: scrapeResult.status,
      rowCount: scrapeResult.rowCount,
      fileRelativePath,
      s3Bucket: SCRAPE_S3_BUCKET,
      s3Key,
      error: scrapeResult.error,
      authStatus: scrapeResult.authStatus,
      authMessage: scrapeResult.authMessage,
      powerbiLoaded: scrapeResult.powerbiLoaded,
      loginSuccess: scrapeResult.loginSuccess,
      authSteps: scrapeResult.authSteps,
      retryCount: scrapeResult.retryCount,
      scrapeDate: scrapeResult.scrapeDate
    };

    try {
      log('INFO', `Sending callback to ${callbackUrl}`, { jobId, status: callbackPayload.status });
      await axios.put(callbackUrl, callbackPayload, { timeout: 30000 });
    } catch (cbErr) {
      log('WARN', `Optional callback to ${callbackUrl} failed: ${cbErr.message}`, { jobId });
    }
  }

  // If scrape succeeded or failed cleanly with AuthError, delete message from jobs queue
  if (scrapeResult.status === 'Succeeded' || scrapeResult.authStatus === 'error' || scrapeResult.loginSuccess === false) {
    try {
      await deleteMessage(receiptHandle);
      log('INFO', `Job ${jobId} finished and message deleted from jobs queue`, { jobId });
    } catch (delErr) {
      log('ERROR', `Failed to delete message for job ${jobId}: ${delErr.message}`, { jobId });
    }
  } else {
    log('WARN', `Job ${jobId} failed with status: ${scrapeResult.status}. Leaving message for retry.`, { jobId });
  }
}

/**
 * Main worker loop with 5-minute idle timeout lifetime.
 */
async function runWorker() {
  log('INFO', 'Starting PBI Scraper Worker...', {
    scaleToZero: SCALE_TO_ZERO,
    idleTimeoutMs: IDLE_TIMEOUT_MS,
    idleTimeoutMinutes: IDLE_TIMEOUT_MS / 60000,
    awsRegion: AWS_REGION,
    jobsQueueUrl: SQS_JOBS_QUEUE_URL || '(local mock queue)',
    resultsQueueUrl: SQS_RESULTS_QUEUE_URL || '(none)',
    s3Bucket: SCRAPE_S3_BUCKET,
    s3Prefix: SCRAPE_S3_PREFIX
  });

  createHealthLockFile();
  setupSignalHandlers();
  initSqsClient();

  let idleDeadline = Date.now() + IDLE_TIMEOUT_MS;
  log('INFO', `Worker active. Idle deadline set to ${new Date(idleDeadline).toISOString()} (${IDLE_TIMEOUT_MS / 1000}s).`);

  while (!shuttingDown) {
    try {
      const message = await receiveNextMessage();

      if (message) {
        isProcessing = true;
        log('INFO', 'Message received. Processing job...');
        await processMessage(message);
        isProcessing = false;

        // Renew idle deadline for another 5 minutes after completing a job
        idleDeadline = Date.now() + IDLE_TIMEOUT_MS;
        log('INFO', `Job processing complete. Idle deadline renewed to ${new Date(idleDeadline).toISOString()}`);
      } else {
        const remainingSeconds = Math.max(0, Math.round((idleDeadline - Date.now()) / 1000));
        log('DEBUG', `No message in queue. Remaining idle time: ${remainingSeconds}s`);

        if (SCALE_TO_ZERO && !isProcessing && Date.now() >= idleDeadline) {
          log('INFO', `No messages received for ${IDLE_TIMEOUT_MS / 1000}s and no active processing. Exiting cleanly (scale-to-zero).`);
          break;
        }
      }
    } catch (err) {
      isProcessing = false;
      log('ERROR', `Worker loop error: ${err.message}`);
      if (!shuttingDown) {
        await new Promise(resolve => setTimeout(resolve, 2000));
      }
    }
  }

  removeHealthLockFile();
  log('INFO', 'Worker process stopped cleanly.');
  process.exit(0);
}

// Start worker if run directly
if (require.main === module) {
  runWorker().catch(err => {
    log('FATAL', `Fatal error during worker execution: ${err.message}`);
    removeHealthLockFile();
    process.exit(1);
  });
}

module.exports = {
  runWorker,
  processMessage,
  resolveCredentials
};
