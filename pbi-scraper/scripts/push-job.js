#!/usr/bin/env node
// scripts/push-job.js
const fs = require('fs');
const path = require('path');
const { SQSClient, SendMessageCommand } = require('@aws-sdk/client-sqs');
const { encryptField } = require('../crypto');

function parseArgs() {
  const args = process.argv.slice(2);
  const options = {
    matricula: '8203',
    password: 'Password123!',
    store: null,
    scrapeType: 'consultor',
    scrapeDates: '2026-09',
    url: null,
    userId: '00000000-0000-0000-0000-000000000001',
    runId: `run-${Date.now()}`,
    jobId: `job-${Date.now()}`,
    key: process.env.SCRAPER_ENCRYPTION_KEY || '',
    queueUrl: process.env.SQS_JOBS_QUEUE_URL || process.env.SQS_QUEUE_URL || '',
    region: process.env.AWS_REGION || 'us-east-1',
    encrypt: true
  };

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '--matricula' && args[i + 1]) options.matricula = args[++i];
    else if (arg === '--password' && args[i + 1]) options.password = args[++i];
    else if ((arg === '--store' || arg === '--unit') && args[i + 1]) options.store = args[++i];
    else if (arg === '--scrapeType' && args[i + 1]) options.scrapeType = args[++i];
    else if (arg === '--scrapeDates' && args[i + 1]) options.scrapeDates = args[++i];
    else if (arg === '--url' && args[i + 1]) options.url = args[++i];
    else if (arg === '--userId' && args[i + 1]) options.userId = args[++i];
    else if (arg === '--runId' && args[i + 1]) options.runId = args[++i];
    else if (arg === '--jobId' && args[i + 1]) options.jobId = args[++i];
    else if (arg === '--key' && args[i + 1]) options.key = args[++i];
    else if (arg === '--queueUrl' && args[i + 1]) options.queueUrl = args[++i];
    else if (arg === '--region' && args[i + 1]) options.region = args[++i];
    else if (arg === '--no-encrypt') options.encrypt = false;
  }

  return options;
}

async function main() {
  const options = parseArgs();
  console.log('--- Push Job to SQS ---');
  console.log('Building job payload with parameters:', {
    matricula: options.matricula,
    scrapeType: options.scrapeType,
    scrapeDates: options.scrapeDates,
    store: options.store,
    url: options.url,
    encrypt: options.encrypt,
    hasKey: Boolean(options.key),
    queueUrl: options.queueUrl || '(local mock queue)'
  });

  const payload = {
    jobId: options.jobId,
    runId: options.runId,
    userId: options.userId,
    url: options.url,
    matricula: options.matricula, // Plain text
    store: options.store,
    scrapeType: options.scrapeType,
    scrapeDates: options.scrapeDates ? options.scrapeDates.split(',').map(s => s.trim()) : null
  };

  if (options.encrypt) {
    if (!options.key) {
      console.warn('⚠️  No SCRAPER_ENCRYPTION_KEY provided. Generating a temporary 32-byte key for this message...');
      options.key = require('crypto').randomBytes(32).toString('hex');
      console.log(`Generated Key: ${options.key}`);
    }

    // Encrypt ONLY the password with AES-256-GCM
    const encryptedPassword = encryptField(options.password, options.key);

    payload.encryptedPassword = encryptedPassword.cipherText;
    payload.passwordIv = encryptedPassword.iv;
    payload.passwordAuthTag = encryptedPassword.authTag;
  } else {
    payload.password = options.password;
  }

  const payloadJson = JSON.stringify(payload, null, 2);

  if (options.queueUrl) {
    console.log(`Sending job to AWS SQS Queue: ${options.queueUrl}`);
    const client = new SQSClient({ region: options.region });
    const command = new SendMessageCommand({
      QueueUrl: options.queueUrl,
      MessageBody: JSON.stringify(payload)
    });
    const result = await client.send(command);
    console.log(`✅ Job enqueued successfully. Message ID: ${result.MessageId}`);
  } else {
    const outputDir = path.join(__dirname, '..', 'outputs');
    if (!fs.existsSync(outputDir)) {
      fs.mkdirSync(outputDir, { recursive: true });
    }
    const mockFile = path.join(outputDir, 'mock_queue.json');
    let currentQueue = [];
    if (fs.existsSync(mockFile)) {
      try {
        currentQueue = JSON.parse(fs.readFileSync(mockFile, 'utf8'));
      } catch {
        currentQueue = [];
      }
    }
    currentQueue.push(payload);
    fs.writeFileSync(mockFile, JSON.stringify(currentQueue, null, 2), 'utf8');
    console.log(`✅ Appended message to mock queue file at: ${mockFile}`);
  }

  console.log('\nPayload JSON:\n', payloadJson);
  console.log('\n--- How to Run the Worker ---');
  if (options.encrypt) {
    console.log(`Local CLI:`);
    console.log(`  SCRAPER_ENCRYPTION_KEY="${options.key}" node worker.js\n`);
  }
  console.log(`Docker Compose:`);
  console.log(`  docker compose --profile worker up pbi-worker\n`);
}

main().catch(err => {
  console.error('Fatal error:', err.message);
  process.exit(1);
});
