// checkpointManager.js
const fs = require('fs');
const path = require('path');

/**
 * Returns the file path for a given matricula's checkpoint.
 */
function getCheckpointPath(outputDir, matricula) {
  const dir = path.join(outputDir || './outputs', 'checkpoints');
  if (!fs.existsSync(dir)) {
    try {
      fs.mkdirSync(dir, { recursive: true });
    } catch (_) {}
  }
  return path.join(dir, `checkpoint_${matricula}_consultor.json`);
}

/**
 * Compares two arrays of strings for equality regardless of order.
 */
function areMonthsEqual(a, b) {
  if (!Array.isArray(a) || !Array.isArray(b)) return false;
  if (a.length !== b.length) return false;
  const sortedA = [...a].sort();
  const sortedB = [...b].sort();
  return sortedA.every((val, idx) => val === sortedB[idx]);
}

/**
 * Saves the current pagination state to disk.
 */
function saveCheckpoint(outputDir, matricula, data) {
  try {
    const filePath = getCheckpointPath(outputDir, matricula);
    const checkpoint = {
      matricula,
      targetMonths: data.targetMonths || [],
      completedPages: data.completedPages || 0,
      restartTokens: data.restartTokens || null,
      paginationContext: data.paginationContext || { prev: [] },
      rowCount: data.rowCount || (data.allRows ? data.allRows.length : 0),
      allRows: data.allRows || [],
      updatedAt: new Date().toISOString()
    };
    fs.writeFileSync(filePath, JSON.stringify(checkpoint), 'utf8');
    return filePath;
  } catch (err) {
    console.warn(`[CheckpointManager] Falha ao salvar checkpoint para ${matricula}: ${err.message}`);
    return null;
  }
}

/**
 * Loads a recent compatible checkpoint if available.
 */
function loadCheckpoint(outputDir, matricula, targetMonths, maxAgeHours = 24) {
  try {
    const filePath = getCheckpointPath(outputDir, matricula);
    if (!fs.existsSync(filePath)) return null;

    const raw = fs.readFileSync(filePath, 'utf8');
    const checkpoint = JSON.parse(raw);

    if (checkpoint.matricula !== matricula) return null;
    if (!areMonthsEqual(checkpoint.targetMonths || [], targetMonths || [])) return null;

    const ageMs = Date.now() - new Date(checkpoint.updatedAt).getTime();
    if (ageMs > maxAgeHours * 3600 * 1000) {
      console.log(`[CheckpointManager] Checkpoint expirado (${Math.round(ageMs / 3600000)}h > ${maxAgeHours}h). Descartando.`);
      clearCheckpoint(outputDir, matricula);
      return null;
    }

    if (!checkpoint.restartTokens || !checkpoint.allRows || checkpoint.allRows.length === 0) {
      return null;
    }

    return checkpoint;
  } catch (err) {
    console.warn(`[CheckpointManager] Falha ao carregar checkpoint para ${matricula}: ${err.message}`);
    return null;
  }
}

/**
 * Deletes the checkpoint file upon successful extraction completion.
 */
function clearCheckpoint(outputDir, matricula) {
  try {
    const filePath = getCheckpointPath(outputDir, matricula);
    if (fs.existsSync(filePath)) {
      fs.unlinkSync(filePath);
      console.log(`[CheckpointManager] Checkpoint removido para matrícula ${matricula}.`);
    }
  } catch (err) {
    console.warn(`[CheckpointManager] Falha ao limpar checkpoint para ${matricula}: ${err.message}`);
  }
}

module.exports = {
  getCheckpointPath,
  saveCheckpoint,
  loadCheckpoint,
  clearCheckpoint,
  areMonthsEqual
};
