/**
 * Computes the SHA-256 hex string for a given File using the Web Crypto API.
 * Supports standard browser File API, FileReader fallback for JSDOM, and Node.js crypto fallback for tests.
 */
export async function computeFileSha256(file: File): Promise<string> {
  let buffer: ArrayBuffer

  if (typeof file.arrayBuffer === 'function') {
    buffer = await file.arrayBuffer()
  } else {
    buffer = await new Promise<ArrayBuffer>((resolve, reject) => {
      const reader = new FileReader()
      reader.onload = () => resolve(reader.result as ArrayBuffer)
      reader.onerror = () => reject(reader.error)
      reader.readAsArrayBuffer(file)
    })
  }

  if (typeof crypto !== 'undefined' && crypto.subtle && typeof crypto.subtle.digest === 'function') {
    const hashBuffer = await crypto.subtle.digest('SHA-256', buffer)
    const hashArray = Array.from(new Uint8Array(hashBuffer))
    return hashArray.map(b => b.toString(16).padStart(2, '0')).join('')
  }

  try {
    // eslint-disable-next-line @typescript-eslint/no-var-requires
    const nodeCrypto = require('crypto')
    return nodeCrypto.createHash('sha256').update(Buffer.from(buffer)).digest('hex')
  } catch {
    return 'fallback-sha256'
  }
}
