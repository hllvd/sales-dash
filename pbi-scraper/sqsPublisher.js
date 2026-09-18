const { SQSClient, SendMessageCommand } = require('@aws-sdk/client-sqs');

const AWS_REGION = process.env.AWS_REGION || 'us-east-1';
let sqsClient = null;

function getSqsClient() {
  if (!sqsClient) {
    const config = { region: AWS_REGION };
    if (process.env.AWS_ACCESS_KEY_ID && process.env.AWS_SECRET_ACCESS_KEY) {
      config.credentials = {
        accessKeyId: process.env.AWS_ACCESS_KEY_ID,
        secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY,
      };
    }
    sqsClient = new SQSClient(config);
  }
  return sqsClient;
}

/**
 * Publish a message to SQS
 * @param {string} queueUrl - SQS Queue URL
 * @param {object} payload - Object to be serialized as JSON message body
 * @returns {Promise<string>} MessageId
 */
async function publishToQueue(queueUrl, payload) {
  if (!queueUrl) {
    throw new Error('SQS_QUEUE_URL is required to publish message');
  }

  const client = getSqsClient();
  const command = new SendMessageCommand({
    QueueUrl: queueUrl,
    MessageBody: JSON.stringify(payload),
  });

  const response = await client.send(command);
  console.log(`[SqsPublisher] Message published to SQS: ${response.MessageId}`);
  return response.MessageId;
}

module.exports = {
  publishToQueue,
  getSqsClient,
};
