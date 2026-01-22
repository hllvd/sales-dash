import * as fs from 'fs';
import * as path from 'path';

/**
 * Validates if a file exists and is accessible
 */
export function validateFileExists(filePath: string): void {
  if (!filePath) {
    throw new Error('Input file path is required. Use -i <file> to specify the input file.');
  }

  if (!fs.existsSync(filePath)) {
    throw new Error(`Input file not found: ${filePath}`);
  }

  const stats = fs.statSync(filePath);
  if (!stats.isFile()) {
    throw new Error(`Path is not a file: ${filePath}`);
  }
}

/**
 * Validates if a file has CSV or XLSX extension
 */
export function validateCsvOrXlsxFile(filePath: string): void {
  const ext = path.extname(filePath).toLowerCase();
  if (ext !== '.csv' && ext !== '.xlsx') {
    throw new Error(`Input file must be a CSV or XLSX file. Got: ${ext}`);
  }
}

/**
 * Validates the input file for CSV/XLSX operations
 */
export function validateInputFile(filePath: string): void {
  validateFileExists(filePath);
  validateCsvOrXlsxFile(filePath);
}
