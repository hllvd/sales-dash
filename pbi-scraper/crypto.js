// crypto.js
const crypto = require('crypto');

const ALGORITHM = 'aes-256-gcm';
const IV_LENGTH = 12; // 12 bytes standard for GCM
const AUTH_TAG_LENGTH = 16; // 16 bytes standard for GCM
const KEY_HEX_LENGTH = 64; // 32 bytes = 64 hex characters

/**
 * Validates encryption key format.
 * @param {string} keyHex - 64 character hex string
 * @returns {Buffer} - 32-byte key Buffer
 */
function parseAndValidateKey(keyHex) {
  if (!keyHex || typeof keyHex !== 'string') {
    throw new Error('Encryption key must be a non-empty string');
  }
  const cleanHex = keyHex.trim();
  if (cleanHex.length !== KEY_HEX_LENGTH) {
    throw new Error(`Encryption key must be exactly 64 hex characters (32 bytes). Received length: ${cleanHex.length}`);
  }
  const keyBuffer = Buffer.from(cleanHex, 'hex');
  if (keyBuffer.length !== 32) {
    throw new Error('Encryption key hex decoding failed to produce 32 bytes');
  }
  return keyBuffer;
}

/**
 * Decrypts an AES-256-GCM encrypted field.
 * Pure deterministic function - no side effects.
 * 
 * @param {string} cipherTextB64 - base64 encoded ciphertext
 * @param {string} ivB64         - base64 encoded 12-byte IV
 * @param {string} authTagB64    - base64 encoded 16-byte Auth Tag
 * @param {string} keyHex        - 64-char hex string (32 bytes)
 * @returns {string}             - Decrypted UTF-8 plaintext
 */
function decryptField(cipherTextB64, ivB64, authTagB64, keyHex) {
  if (!cipherTextB64 || !ivB64 || !authTagB64) {
    throw new Error('Missing cipherText, iv, or authTag parameter for decryption');
  }

  const key = parseAndValidateKey(keyHex);
  const iv = Buffer.from(ivB64, 'base64');
  const authTag = Buffer.from(authTagB64, 'base64');
  const cipherText = Buffer.from(cipherTextB64, 'base64');

  if (iv.length !== IV_LENGTH) {
    throw new Error(`Invalid IV length: expected ${IV_LENGTH} bytes, got ${iv.length}`);
  }

  if (authTag.length !== AUTH_TAG_LENGTH) {
    throw new Error(`Invalid AuthTag length: expected ${AUTH_TAG_LENGTH} bytes, got ${authTag.length}`);
  }

  const decipher = crypto.createDecipheriv(ALGORITHM, key, iv);
  decipher.setAuthTag(authTag);

  const decrypted = Buffer.concat([
    decipher.update(cipherText),
    decipher.final()
  ]);

  return decrypted.toString('utf8');
}

/**
 * Encrypts a plaintext string with AES-256-GCM.
 * Generates a fresh random 12-byte IV for each call.
 * 
 * @param {string} plaintext - text to encrypt
 * @param {string} keyHex    - 64-char hex string (32 bytes)
 * @returns {{ cipherText: string, iv: string, authTag: string }} - base64 encoded strings
 */
function encryptField(plaintext, keyHex) {
  if (typeof plaintext !== 'string') {
    throw new Error('Plaintext must be a string');
  }

  const key = parseAndValidateKey(keyHex);
  const iv = crypto.randomBytes(IV_LENGTH);

  const cipher = crypto.createCipheriv(ALGORITHM, key, iv);
  const encrypted = Buffer.concat([
    cipher.update(Buffer.from(plaintext, 'utf8')),
    cipher.final()
  ]);
  const authTag = cipher.getAuthTag();

  return {
    cipherText: encrypted.toString('base64'),
    iv: iv.toString('base64'),
    authTag: authTag.toString('base64')
  };
}

module.exports = {
  decryptField,
  encryptField,
  parseAndValidateKey
};
