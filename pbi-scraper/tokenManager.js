// tokenManager.js
// In-memory token cache for Avapro JWT and PowerBI MWCToken per matricula.

const tokenCache = new Map();

/**
 * Retrieves cached tokens for a given matricula.
 * @param {string} matricula
 * @returns {{ avaJwt: string|null, pbiToken: string|null, createdAt: number } | null}
 */
function getTokens(matricula) {
  if (!matricula) return null;
  const key = String(matricula).trim();
  const cached = tokenCache.get(key);
  if (!cached) return null;
  return cached;
}

/**
 * Stores tokens in cache for a given matricula.
 * @param {string} matricula
 * @param {{ avaJwt?: string, pbiToken?: string, token?: string }} tokens
 */
function setTokens(matricula, { avaJwt = null, pbiToken = null, token = null, store = null, detectedStore = null }) {
  if (!matricula) return;
  const key = String(matricula).trim();
  const resolvedPbiToken = pbiToken || token || null;
  const resolvedStore = detectedStore || store || null;

  tokenCache.set(key, {
    avaJwt: avaJwt || null,
    pbiToken: resolvedPbiToken,
    store: resolvedStore,
    createdAt: Date.now(),
  });
  console.log(`[TokenManager] Cached tokens for matricula "${key}" (Store: "${resolvedStore || 'N/A'}").`);
}

/**
 * Invalidates cached tokens for a given matricula.
 * @param {string} matricula
 */
function invalidateTokens(matricula) {
  if (!matricula) return;
  const key = String(matricula).trim();
  if (tokenCache.has(key)) {
    tokenCache.delete(key);
    console.log(`[TokenManager] Invalidated cached tokens for matricula "${key}".`);
  }
}

/**
 * Clears all cached tokens.
 */
function clearAllTokens() {
  tokenCache.clear();
  console.log('[TokenManager] Cleared all cached tokens.');
}

module.exports = {
  getTokens,
  setTokens,
  invalidateTokens,
  clearAllTokens,
};
