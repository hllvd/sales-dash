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

// Configuration
const AWS_REGION = process.env.AWS_REGION || 'us-east-1';
const SQS_QUEUE_URL = process.env.SQS_QUEUE_URL ? process.env.SQS_QUEUE_URL.trim() : '';
const SCRAPER_ENCRYPTION_KEY = process.env.SCRAPER_ENCRYPTION_KEY ? process.env.SCRAPER_ENCRYPTION_KEY.trim() : '';
const CALLBACK_BASE_URL = process.env.CALLBACK_BASE_URL ? process.env.CALLBACK_BASE_URL.replace(/\/+$/, '') : 'http://salesapp-api:5000';
const OUTPUT_DIR = process.env.OUTPUT_DIR || './outputs';
const SCALE_TO_ZERO = process.env.SCALE_TO_ZERO !== 'false';
const MAX_EMPTY_POLLS = parseInt(process.env.MAX_EMPTY_POLLS || '3', 10);
const LOCK_FILE_PATH = process.env.LOCK_FILE_PATH || '/tmp/worker.lock';
const MOCK_QUEUE_FILE = path.join(OUTPUT_DIR, 'mock_queue.json');

let shuttingDown = false;
let emptyPollCount = 0;
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
 * Initializes SQS Client if SQS_QUEUE_URL is provided.
 */
function initSqsClient() {
  if (SQS_QUEUE_URL) {
    sqsClient = new SQSClient({ region: AWS_REGION });
    log('INFO', `SQS client initialized for queue: ${SQS_QUEUE_URL}`);
  } else {
    log('INFO', `No SQS_QUEUE_URL configured. Using local mock queue at ${MOCK_QUEUE_FILE}`);
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
 * Polls for one message from SQS or mock queue.
 */
async function receiveNextMessage() {
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
    QueueUrl: SQS_QUEUE_URL,
    MaxNumberOfMessages: 1,
    WaitTimeSeconds: 20, // Long-polling
    VisibilityTimeout: 900 // 15 minutes visibility window for scrape execution
  });

  const response = await sqsClient.send(command);
  if (response.Messages && response.Messages.length > 0) {
    return response.Messages[0];
  }
  return null;
}

/**
 * Deletes a processed message from SQS or mock queue.
 */
async function deleteMessage(receiptHandle) {
  if (!sqsClient || receiptHandle.startsWith('mock-')) {
    log('DEBUG', `Mock message ${receiptHandle} marked as deleted`);
    return;
  }

  const command = new DeleteMessageCommand({
    QueueUrl: SQS_QUEUE_URL,
    ReceiptHandle: receiptHandle
  });

  await sqsClient.send(command);
  log('DEBUG', 'Message successfully deleted from SQS');
}

/**
 * Decrypts credentials from payload.
 * Pure/deterministic function.
 * 
 * @param {object} payload
 * @param {string} [encryptionKey]
 * @returns {{ username: string, password: string }}
 */
function resolveCredentials(payload, encryptionKey = (process.env.SCRAPER_ENCRYPTION_KEY ? process.env.SCRAPER_ENCRYPTION_KEY.trim() : '')) {
  let plainUsername = payload.matricula || '';
  let plainPassword = payload.password || payload.avaproPassword || '';

  // If encrypted username is present
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

  // If encrypted password is present
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

  const jobId = payload.jobId || `job-${Date.now()}`;
  const runId = payload.runId || null;
  const userId = payload.userId || null;
  const matricula = payload.matricula || '';
  const store = payload.store || null;
  const scrapeDate = payload.scrapeDate || null;
  const scrapeDates = payload.scrapeDates || null;
  const scrapeType = (payload.scrapeType || 'geral').toLowerCase();
  const callbackUrl = payload.callbackUrl || `${CALLBACK_BASE_URL}/api/scrape/callback`;

  log('INFO', `Starting ${scrapeType} scrape processing for job ${jobId}`, { jobId, runId, matricula, store, scrapeDate, scrapeType });

  let credentials;
  try {
    credentials = resolveCredentials(payload);
  } catch (credErr) {
    log('ERROR', `Credential resolution failed for job ${jobId}: ${credErr.message}`, { jobId });
    
    // Notify backend callback about failure
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

    // Invalid credentials payload is permanent failure - delete message
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

  // Save CSV output to file if rows were returned
  let fileRelativePath = null;
  if (scrapeResult.rowCount > 0 && scrapeResult.csv) {
    try {
      const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
      const filename = `scrape_${jobId}_${timestamp}.csv`;
      const fullFilePath = path.join(OUTPUT_DIR, filename);
      fs.writeFileSync(fullFilePath, scrapeResult.csv, 'utf8');
      fileRelativePath = filename;
      log('INFO', `CSV saved to ${fullFilePath}`, { jobId, filename, rowCount: scrapeResult.rowCount });
    } catch (fsErr) {
      log('ERROR', `Failed to write CSV file for job ${jobId}: ${fsErr.message}`, { jobId });
    }
  }

  // Build callback payload
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
    error: scrapeResult.error,
    authStatus: scrapeResult.authStatus,
    authMessage: scrapeResult.authMessage,
    powerbiLoaded: scrapeResult.powerbiLoaded,
    loginSuccess: scrapeResult.loginSuccess,
    authSteps: scrapeResult.authSteps,
    retryCount: scrapeResult.retryCount,
    scrapeDate: scrapeResult.scrapeDate
  };

  // Send callback to API
  try {
    log('INFO', `Sending callback to ${callbackUrl}`, { jobId, status: callbackPayload.status });
    await axios.put(callbackUrl, callbackPayload, { timeout: 30000 });
  } catch (cbErr) {
    log('ERROR', `Callback to ${callbackUrl} failed: ${cbErr.message}`, { jobId });
    // If callback fails, do NOT delete message so it can be retried
    return;
  }

  // If scrape succeeded or failed cleanly with AuthError, delete message
  if (scrapeResult.status === 'Succeeded' || scrapeResult.authStatus === 'error' || scrapeResult.loginSuccess === false) {
    try {
      await deleteMessage(receiptHandle);
      log('INFO', `Job ${jobId} finished and message deleted`, { jobId });
    } catch (delErr) {
      log('ERROR', `Failed to delete message for job ${jobId}: ${delErr.message}`, { jobId });
    }
  } else {
    log('WARN', `Job ${jobId} failed with status: ${scrapeResult.status}. Leaving message for retry.`, { jobId });
  }
}

/**
 * Main worker loop.
 */
async function runWorker() {
  log('INFO', 'Starting PBI Scraper Worker...', {
    scaleToZero: SCALE_TO_ZERO,
    maxEmptyPolls: MAX_EMPTY_POLLS,
    awsRegion: AWS_REGION,
    hasSqsUrl: Boolean(SQS_QUEUE_URL),
    callbackBaseUrl: CALLBACK_BASE_URL
  });

  createHealthLockFile();
  setupSignalHandlers();
  initSqsClient();

  while (!shuttingDown) {
    try {
      const message = await receiveNextMessage();

      if (message) {
        emptyPollCount = 0;
        await processMessage(message);
      } else {
        emptyPollCount++;
        log('DEBUG', `No message received. Empty poll count: ${emptyPollCount}/${MAX_EMPTY_POLLS}`);

        if (SCALE_TO_ZERO && emptyPollCount >= MAX_EMPTY_POLLS) {
          log('INFO', `Queue empty after ${emptyPollCount} consecutive polls. Exiting (scale-to-zero).`);
          break;
        }
      }
    } catch (err) {
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
