require('dotenv').config();
const { SQSClient, ReceiveMessageCommand, DeleteMessageCommand } = require('@aws-sdk/client-sqs');
const axios = require('axios');

const SQS_QUEUE_URL = process.env.SQS_QUEUE_URL ? process.env.SQS_QUEUE_URL.trim() : '';
const AWS_REGION = process.env.AWS_REGION || 'us-east-1';
const API_BASE_URL = (process.env.API_BASE_URL || 'http://localhost:5001').replace(/\/+$/, '');
const API_SECRET = process.env.API_SECRET || process.env.WORKER_API_SECRET || '';
const SCALE_TO_ZERO = process.env.SCALE_TO_ZERO === 'true';
const MAX_EMPTY_POLLS = parseInt(process.env.MAX_EMPTY_POLLS || '3', 10);
const VISIBILITY_TIMEOUT = parseInt(process.env.VISIBILITY_TIMEOUT || '300', 10); // 5 min default

if (!SQS_QUEUE_URL) {
  console.error('[Worker Error] SQS_QUEUE_URL não configurada no arquivo .env.');
  console.error('[Worker Error] Defina SQS_QUEUE_URL no .env antes de iniciar o worker.');
  process.exit(1);
}

const sqsConfig = { region: AWS_REGION };
if (process.env.AWS_ACCESS_KEY_ID && process.env.AWS_SECRET_ACCESS_KEY) {
  sqsConfig.credentials = {
    accessKeyId: process.env.AWS_ACCESS_KEY_ID,
    secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY,
  };
}
const sqsClient = new SQSClient(sqsConfig);

let shuttingDown = false;
let emptyPollCount = 0;

function setupGracefulShutdown() {
  const shutdown = (signal) => {
    if (shuttingDown) return;
    console.log(`\n[Worker] Recebido ${signal}. Encerrando consumidor graciosamente...`);
    shuttingDown = true;
  };
  process.on('SIGINT', () => shutdown('SIGINT'));
  process.on('SIGTERM', () => shutdown('SIGTERM'));
}

async function processMessage(message) {
  const receiptHandle = message.ReceiptHandle;
  const messageId = message.MessageId;

  let body;
  try {
    body = JSON.parse(message.Body);
  } catch (parseErr) {
    console.error(`[Worker] Erro ao decodificar JSON da mensagem ${messageId}:`, parseErr.message);
    return;
  }

  const { jobId, runId, userId, s3Bucket, s3Key, matricula, rowCount } = body;
  console.log(`\n[Worker] ----------------------------------------`);
  console.log(`[Worker] Nova mensagem recebida: ${messageId}`);
  console.log(`[Worker] JobId: ${jobId || 'N/A'}, Matrícula: ${matricula || 'N/A'}, Linhas: ${rowCount ?? 'N/A'}`);
  console.log(`[Worker] Localização S3: s3://${s3Bucket}/${s3Key}`);

  if (!s3Bucket || !s3Key) {
    console.error(`[Worker] Mensagem ${messageId} sem s3Bucket ou s3Key. Ignorando.`);
    return;
  }

  // Chamar API .NET para importar o arquivo CSV do S3
  const importEndpoint = `${API_BASE_URL}/api/scrape/import-from-s3`;
  const headers = { 'Content-Type': 'application/json' };
  if (API_SECRET) {
    headers['X-Worker-Secret'] = API_SECRET;
  }

  try {
    console.log(`[Worker] Enviando requisição de importação para API: ${importEndpoint}...`);
    const response = await axios.post(
      importEndpoint,
      {
        s3Bucket,
        s3Key,
        userId: userId ? String(userId) : null,
        jobId,
        runId,
        matricula,
      },
      { headers, timeout: 180000 } // 3 min timeout para imports grandes
    );

    console.log(`[Worker] Importação concluída com sucesso!`);
    console.log(`[Worker] Contratos processados: ${response.data.importedCount ?? 'OK'}`);

    // Excluir mensagem da fila SQS após importação com sucesso
    console.log(`[Worker] Excluindo mensagem da fila SQS...`);
    await sqsClient.send(
      new DeleteMessageCommand({
        QueueUrl: SQS_QUEUE_URL,
        ReceiptHandle: receiptHandle,
      })
    );
    console.log(`[Worker] Mensagem ${messageId} removida da fila com sucesso.`);
  } catch (apiErr) {
    const errorDetails = apiErr.response?.data?.message || apiErr.message;
    console.error(`[Worker] ERRO ao importar via API: ${errorDetails}`);
    console.error(`[Worker] Mensagem NÃO foi removida da fila. Ficará visível para nova tentativa após o visibility timeout (${VISIBILITY_TIMEOUT}s).`);
  }
}

async function pollLoop() {
  console.log('====================================================');
  console.log('  SalesApp Local SQS Worker iniciado');
  console.log(`  Região AWS: ${AWS_REGION}`);
  console.log(`  Fila SQS:   ${SQS_QUEUE_URL}`);
  console.log(`  API Target: ${API_BASE_URL}`);
  console.log(`  Scale-to-zero: ${SCALE_TO_ZERO}`);
  console.log('====================================================');
  console.log('[Worker] Aguardando mensagens na fila (long polling 20s)...');

  while (!shuttingDown) {
    try {
      const command = new ReceiveMessageCommand({
        QueueUrl: SQS_QUEUE_URL,
        MaxNumberOfMessages: 1,
        WaitTimeSeconds: 20,
        VisibilityTimeout: VISIBILITY_TIMEOUT,
      });

      const response = await sqsClient.send(command);

      if (response.Messages && response.Messages.length > 0) {
        emptyPollCount = 0;
        for (const msg of response.Messages) {
          if (shuttingDown) break;
          await processMessage(msg);
        }
      } else {
        emptyPollCount++;
        if (SCALE_TO_ZERO && emptyPollCount >= MAX_EMPTY_POLLS) {
          console.log(`[Worker] Fila vazia após ${emptyPollCount} consultas consecutivas. Encerrando (scale-to-zero).`);
          process.exit(0);
        }
      }
    } catch (pollErr) {
      if (!shuttingDown) {
        console.error(`[Worker] Erro ao consultar fila SQS: ${pollErr.message}`);
        await new Promise((resolve) => setTimeout(resolve, 5000));
      }
    }
  }

  console.log('[Worker] Processo finalizado com sucesso.');
}

setupGracefulShutdown();
pollLoop();
