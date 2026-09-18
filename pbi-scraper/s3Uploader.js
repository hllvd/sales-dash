const fs = require('fs');
const { S3Client, PutObjectCommand } = require('@aws-sdk/client-s3');

const AWS_REGION = process.env.AWS_REGION || 'us-east-1';
let s3Client = null;

function getS3Client() {
  if (!s3Client) {
    const config = { region: AWS_REGION };
    if (process.env.AWS_ACCESS_KEY_ID && process.env.AWS_SECRET_ACCESS_KEY) {
      config.credentials = {
        accessKeyId: process.env.AWS_ACCESS_KEY_ID,
        secretAccessKey: process.env.AWS_SECRET_ACCESS_KEY,
      };
    }
    s3Client = new S3Client(config);
  }
  return s3Client;
}

/**
 * Upload a local file to S3
 * @param {string} localFilePath - Path to local file
 * @param {string} s3Key - Destination key in S3
 * @param {string} bucket - S3 bucket name
 * @param {string} [contentType='text/csv'] - Content-Type
 * @returns {Promise<{ bucket: string, key: string }>}
 */
async function uploadToS3(localFilePath, s3Key, bucket, contentType = 'text/csv') {
  if (!fs.existsSync(localFilePath)) {
    throw new Error(`Local file not found for S3 upload: ${localFilePath}`);
  }

  const client = getS3Client();
  const fileStream = fs.createReadStream(localFilePath);

  const expiresDate = new Date(Date.now() + 24 * 60 * 60 * 1000);

  const command = new PutObjectCommand({
    Bucket: bucket,
    Key: s3Key,
    Body: fileStream,
    ContentType: contentType,
    Expires: expiresDate,
  });

  await client.send(command);
  console.log(`[S3Uploader] File successfully uploaded to s3://${bucket}/${s3Key}`);
  return { bucket, key: s3Key };
}

module.exports = {
  uploadToS3,
  getS3Client,
};
