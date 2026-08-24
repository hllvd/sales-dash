#!/usr/bin/env node
// scripts/push-test-message.js
const fs = require('fs');
const path = require('path');
const { SQSClient, SendMessageCommand } = require('@aws-sdk/client-sqs');
const { encryptField } = require('../crypto');

function parseArgs() {
  const args = process.argv.slice(2);
  const options = {
    matricula: '12345',
    password: 'Password123!',
    store: 'AHU - PR',
    scrapeDate: '2026-07',
    userId: '00000000-0000-0000-0000-000000000001',
    runId: `run-${Date.now()}`,
    jobId: `job-${Date.now()}`,
    key: process.env.SCRAPER_ENCRYPTION_KEY || '',
    queueUrl: process.env.SQS_QUEUE_URL || '',
    region: process.env.AWS_REGION || 'us-east-1',
    encrypt: true
  };

  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '--matricula' && args[i + 1]) options.matricula = args[++i];
    else if (arg === '--password' && args[i + 1]) options.password = args[++i];
    else if (arg === '--store' && args[i + 1]) options.store = args[++i];
    else if (arg === '--scrapeDate' && args[i + 1]) options.scrapeDate = args[++i];
    else if (arg === '--userId' && args[i + 1]) options.userId = args[++i];
    else if (arg === '--runId' && args[i + 1]) options.runId = args[++i];
    else if (arg === '--jobId' && args[i + 1]) options.jobId = args[++i];
    else if (arg === '--key' && args[i + 1]) options.key = args[++i];
    else if (arg === '--queueUrl' && args[i + 1]) options.queueUrl = args[++i];
    else if (arg === '--no-encrypt') options.encrypt = false;
  }

  return options;
}

async function main() {
  const options = parseArgs();
  console.log('Building test message with options:', {
    matricula: options.matricula,
    store: options.store,
    scrapeDate: options.scrapeDate,
    encrypt: options.encrypt,
    hasKey: Boolean(options.key),
    queueUrl: options.queueUrl || '(local mock queue)'
  });

  const payload = {
    jobId: options.jobId,
    runId: options.runId,
    userId: options.userId,
    matricula: options.matricula,
    store: options.store,
    scrapeDate: options.scrapeDate
  };

  if (options.encrypt) {
    if (!options.key) {
      console.warn('⚠️  No SCRAPER_ENCRYPTION_KEY provided. Generating a random key for this message...');
      options.key = require('crypto').randomBytes(32).toString('hex');
      console.log(`Generated Key: ${options.key}`);
    }

    const encryptedMatricula = encryptField(options.matricula, options.key);
    const encryptedPassword = encryptField(options.password, options.key);

    payload.encryptedUsername = encryptedMatricula.cipherText;
    payload.usernameIv = encryptedMatricula.iv;
    payload.usernameAuthTag = encryptedMatricula.authTag;

    payload.encryptedPassword = encryptedPassword.cipherText;
    payload.passwordIv = encryptedPassword.iv;
    payload.passwordAuthTag = encryptedPassword.authTag;
  } else {
    payload.password = options.password;
  }

  const payloadJson = JSON.stringify(payload, null, 2);

  if (options.queueUrl) {
    console.log(`Sending message to SQS queue: ${options.queueUrl}`);
    const client = new SQSClient({ region: options.region });
    const command = new SendMessageCommand({
      QueueUrl: options.queueUrl,
      MessageBody: JSON.stringify(payload)
    });
    const result = await client.send(command);
    console.log(`✅ Sent to SQS. Message ID: ${result.MessageId}`);
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
    console.log(`✅ Appended message to mock queue at: ${mockFile}`);
  }

  console.log('\nPayload content:\n', payloadJson);
  if (options.encrypt) {
    console.log(`\nTo run worker locally with this key:`);
    console.log(`SCRAPER_ENCRYPTION_KEY="${options.key}" SCALE_TO_ZERO=false node worker.js\n`);
  }
}

main().catch(err => {
  console.error('Fatal error:', err.message);
  process.exit(1);
});
