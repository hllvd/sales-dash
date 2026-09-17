// scrape.js
const path = require('path');
const { scrapeWithReauth } = require('./extractor');
const { getOrFetchTokens, AuthError } = require('./auth');
const { scrapeConsultorDirect } = require('./extractorConsultor');

/**
 * Normalizes input date parameter into an array of date strings (YYYY-MM or YYYY-MM-DD or null).
 * Pure deterministic function.
 * 
 * @param {string|string[]|null|undefined} reqScrapeDate
 * @param {string|string[]|null|undefined} reqScrapeDates
 * @returns {Array<string|null>}
 */
function normalizeScrapeDates(reqScrapeDate, reqScrapeDates) {
  const datesRaw = reqScrapeDates || reqScrapeDate;

  if (Array.isArray(datesRaw)) {
    const cleaned = datesRaw.map(d => (d !== null && d !== undefined ? String(d).trim() : '')).filter(Boolean);
    return cleaned.length > 0 ? cleaned : [null];
  }

  if (typeof datesRaw === 'string') {
    if (datesRaw.includes(',')) {
      const splitDates = datesRaw.split(',').map(d => d.trim()).filter(Boolean);
      return splitDates.length > 0 ? splitDates : [null];
    }
    const trimmed = datesRaw.trim();
    if (trimmed) {
      return [trimmed];
    }
  }

  return [null];
}

/**
 * Merges multiple CSV text parts, preserving the header from the first part.
 * Pure deterministic function.
 * 
 * @param {string[]} csvParts - Array of CSV string outputs
 * @returns {string} - Combined CSV text
 */
function mergeCsvParts(csvParts) {
  if (!Array.isArray(csvParts) || csvParts.length === 0) {
    return '';
  }

  const validParts = csvParts.filter(p => typeof p === 'string' && p.trim().length > 0);
  if (validParts.length === 0) {
    return '';
  }

  const firstLines = validParts[0].split('\n').filter(Boolean);
  if (firstLines.length === 0) {
    return '';
  }

  const header = firstLines[0];
  const dataRows = [firstLines.slice(1).join('\n')].filter(Boolean);

  for (let i = 1; i < validParts.length; i++) {
    const pLines = validParts[i].split('\n').filter(Boolean);
    if (pLines.length > 1) {
      dataRows.push(pLines.slice(1).join('\n'));
    }
  }

  return [header, ...dataRows].filter(Boolean).join('\n');
}

/**
 * Counts the effective data rows from CSV text or array of rows.
 * Pure deterministic function.
 * 
 * @param {string} csvText 
 * @param {Array} rowsArray 
 * @returns {number}
 */
function countEffectiveRows(csvText, rowsArray) {
  if (csvText && typeof csvText === 'string') {
    const nonEmptyLines = csvText.split('\n').filter(line => line.trim().length > 0);
    if (nonEmptyLines.length > 1) {
      return nonEmptyLines.length - 1; // subtract header
    }
  }
  return Array.isArray(rowsArray) ? rowsArray.length : 0;
}

/**
 * Executes a scraping job without HTTP or process-level side effects.
 * 
 * @param {object} params
 * @param {string} params.jobId - Unique identifier for the job
 * @param {string} [params.store] - Store name (or null for auto-detection)
 * @param {string} params.matricula - AvaPro username / matricula
 * @param {string} params.password - Plaintext password
 * @param {string|string[]|null} [params.scrapeDate] - Target date(s) (YYYY-MM)
 * @param {string|string[]|null} [params.scrapeDates] - Target date(s) alternative
 * @param {number} [params.maxReauthRetries=3] - Maximum automatic reauth attempts
 * @returns {Promise<object>} - Structured result of the scrape run
 */
async function runScrapeJob(params) {
  if (!params || typeof params !== 'object') {
    throw new Error('Invalid runScrapeJob parameters: expected an object');
  }

  const {
    jobId,
    store,
    matricula,
    password,
    scrapeDate,
    scrapeDates: reqScrapeDates,
    scrapeType = 'geral',
    outputDir = './outputs',
    maxReauthRetries = 3
  } = params;

  if (!matricula || !password) {
    throw new Error('Matrícula and password are required to execute a scrape job');
  }

  const targetDates = normalizeScrapeDates(scrapeDate, reqScrapeDates);
  
  let result = {
    jobId: jobId || 'job-local',
    status: 'Succeeded',
    rowCount: 0,
    csv: '',
    rows: [],
    error: null,
    authStatus: 'success',
    authMessage: 'Autenticação bem-sucedida',
    powerbiLoaded: true,
    loginSuccess: true,
    authSteps: [],
    detectedStore: null,
    retryCount: 0,
    scrapeDate: targetDates.filter(Boolean).join(',') || null
  };

  // Branch for Consultor direct extraction
  if (String(scrapeType).toLowerCase() === 'consultor') {
    try {
      const consultorRes = await scrapeConsultorDirect({
        matricula,
        password,
        scrapeDates: targetDates,
        outputDir
      });

      result.status = consultorRes.status || 'Succeeded';
      result.rowCount = consultorRes.totalRows;
      result.rows = consultorRes.rows || [];
      result.csv = consultorRes.csv || '';
      result.fileRelativePath = consultorRes.csvFile ? path.basename(consultorRes.csvFile) : null;
      result.detectedStore = store || 'Consultor';
      result.authStatus = 'success';
      result.authMessage = 'Autenticação bem-sucedida';
      result.powerbiLoaded = true;
      result.loginSuccess = true;

      if (consultorRes.totalRows === 0) {
        result.status = 'Failed';
        result.error = 'Nenhum registro retornado pelo relatório PowerBI Consultor';
      }

      return result;
    } catch (err) {
      result.status = 'Failed';
      result.error = err.message || 'Falha no scrape de Consultor';
      result.authStatus = 'error';
      result.authMessage = err.message;
      result.powerbiLoaded = false;
      result.loginSuccess = false;
      return result;
    }
  }

  const combinedRows = [];
  const csvParts = [];
  let totalRetryCount = 0;

  try {
    for (const targetDate of targetDates) {
      const scrapeRes = await scrapeWithReauth(
        store || '',
        matricula,
        password,
        targetDate,
        getOrFetchTokens,
        maxReauthRetries
      );

      if (scrapeRes.detectedStore) {
        result.detectedStore = scrapeRes.detectedStore;
      }

      if (scrapeRes.retryCount) {
        totalRetryCount += scrapeRes.retryCount;
      }

      if (scrapeRes.authSteps && scrapeRes.authSteps.length > 0) {
        result.authSteps = [...(result.authSteps || []), ...scrapeRes.authSteps];
      }

      if (scrapeRes.rows && Array.isArray(scrapeRes.rows)) {
        combinedRows.push(...scrapeRes.rows);
      }

      if (scrapeRes.csv) {
        csvParts.push(scrapeRes.csv);
      }
    }

    result.retryCount = totalRetryCount;
    result.rows = combinedRows;

    const mergedCsv = mergeCsvParts(csvParts);
    result.csv = mergedCsv;
    const effectiveCount = countEffectiveRows(mergedCsv, combinedRows);
    result.rowCount = effectiveCount;

    if (effectiveCount === 0) {
      result.status = 'Failed';
      result.error = 'Nenhum registro retornado pelo relatório PowerBI';
    }

    return result;
  } catch (err) {
    if (err instanceof AuthError) {
      result.status = 'Failed';
      result.authStatus = err.authStatus || 'error';
      result.authMessage = err.authMessage || err.message;
      result.powerbiLoaded = err.powerbiLoaded || false;
      result.loginSuccess = err.loginSuccess || false;
      result.authSteps = err.steps || [];
      result.error = err.authMessage || err.message;
      return result;
    }

    if (err.steps && Array.isArray(err.steps)) {
      result.authSteps = [...(result.authSteps || []), ...err.steps];
    }

    result.status = 'Failed';
    result.error = err.authMessage || err.message || 'Erro inesperado durante scraping';
    return result;
  }
}

module.exports = {
  runScrapeJob,
  normalizeScrapeDates,
  mergeCsvParts,
  countEffectiveRows
};
