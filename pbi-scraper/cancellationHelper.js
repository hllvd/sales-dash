// cancellationHelper.js
/**
 * Explicit error thrown when a scrape job is cooperatively cancelled by a user.
 */
class JobCancelledError extends Error {
  constructor(message = 'Extração cancelada manualmente pelo usuário.') {
    super(message);
    this.name = 'JobCancelledError';
    this.isCancelled = true;
  }
}

/**
 * Checks whether an error represents an abort/cancellation from AbortSignal or axios.
 * Pure deterministic function.
 */
function isAbortError(err) {
  if (!err) return false;
  return (
    err instanceof JobCancelledError ||
    err.isCancelled === true ||
    err.name === 'JobCancelledError' ||
    err.name === 'AbortError' ||
    err.name === 'CanceledError' ||
    err.code === 'ERR_CANCELED'
  );
}

/**
 * Throws JobCancelledError immediately if the provided AbortSignal is already aborted.
 */
function throwIfAborted(signal) {
  if (signal && signal.aborted) {
    throw new JobCancelledError();
  }
}

/**
 * Resolves after `ms` milliseconds, or rejects immediately with JobCancelledError if `signal` aborts.
 */
function sleepAbortable(ms, signal) {
  throwIfAborted(signal);

  return new Promise((resolve, reject) => {
    let onAbort;
    const timer = setTimeout(() => {
      if (signal && onAbort) {
        signal.removeEventListener('abort', onAbort);
      }
      resolve();
    }, ms);

    if (signal) {
      onAbort = () => {
        clearTimeout(timer);
        reject(new JobCancelledError());
      };
      signal.addEventListener('abort', onAbort, { once: true });
    }
  });
}

module.exports = {
  JobCancelledError,
  isAbortError,
  throwIfAborted,
  sleepAbortable
};
