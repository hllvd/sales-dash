// tokenManager.js
// In-memory token cache for Avapro JWT and PowerBI MWCToken per matricula,
// plus Authentication Circuit Breaker state to prevent account lockout in AVA PRO.

const tokenCache = new Map();

// Circuit Breaker state:
// Maps matricula -> { reason: string, timestamp: number }
const authFailureMap = new Map();

// Set of aborted runIds
const abortedRunIds = new Set();

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

/**
 * Marks a matricula as having a fatal authentication failure (wrong-password).
 * Automatically invalidates cached tokens for this matricula and trips the circuit breaker.
 * @param {string} matricula
 * @param {string} [reason]
 */
function markAuthFailure(matricula, reason = 'Credenciais inválidas') {
  if (!matricula) return;
  const key = String(matricula).trim();
  authFailureMap.set(key, {
    reason,
    timestamp: Date.now()
  });
  invalidateTokens(matricula);
  console.warn(`[TokenManager/CircuitBreaker] Disjuntor ATIVADO para matrícula "${key}": ${reason}`);
}

/**
 * Checks whether a matricula is currently in an auth-failure lock state.
 * @param {string} matricula
 * @returns {boolean}
 */
function isAuthLocked(matricula) {
  if (!matricula) return false;
  const key = String(matricula).trim();
  return authFailureMap.has(key);
}

/**
 * Gets the auth failure details for a matricula.
 * @param {string} matricula
 * @returns {{ reason: string, timestamp: number } | null}
 */
function getAuthFailureReason(matricula) {
  if (!matricula) return null;
  const key = String(matricula).trim();
  return authFailureMap.get(key) || null;
}

/**
 * Resets the auth failure lock for a matricula (e.g., when new credentials are saved or tested).
 * @param {string} matricula
 */
function resetAuthLock(matricula) {
  if (!matricula) return;
  const key = String(matricula).trim();
  if (authFailureMap.has(key)) {
    authFailureMap.delete(key);
    console.log(`[TokenManager/CircuitBreaker] Disjuntor RESETADO para matrícula "${key}".`);
  }
}

/**
 * Marks a runId as aborted so remaining jobs in the batch are skipped immediately.
 * @param {string} runId
 * @param {string} [reason]
 */
function abortRun(runId, reason = 'Execução cancelada') {
  if (!runId) return;
  const key = String(runId).trim();
  abortedRunIds.add(key);
  console.warn(`[TokenManager/CircuitBreaker] Lote (runId: "${key}") ABORTADO: ${reason}`);
}

/**
 * Checks if a runId has been aborted.
 * @param {string} runId
 * @returns {boolean}
 */
function isRunAborted(runId) {
  if (!runId) return false;
  const key = String(runId).trim();
  return abortedRunIds.has(key);
}

/**
 * Clears all circuit breaker locks and aborted runs.
 */
function clearAllAuthLocks() {
  authFailureMap.clear();
  abortedRunIds.clear();
  console.log('[TokenManager/CircuitBreaker] Todos os bloqueios de disjuntor foram resetados.');
}

module.exports = {
  getTokens,
  setTokens,
  invalidateTokens,
  clearAllTokens,
  markAuthFailure,
  isAuthLocked,
  getAuthFailureReason,
  resetAuthLock,
  abortRun,
  isRunAborted,
  clearAllAuthLocks,
};
