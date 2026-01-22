import * as fs from 'fs';
import * as path from 'path';

/**
 * Generates a timestamp string in the format YYYYMMDD-HHMMSS
 */
export function generateTimestamp(): string {
  const now = new Date();
  const year = now.getFullYear();
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  const hours = String(now.getHours()).padStart(2, '0');
  const minutes = String(now.getMinutes()).padStart(2, '0');
  const seconds = String(now.getSeconds()).padStart(2, '0');
  
  return `${year}${month}${day}-${hours}${minutes}${seconds}`;
}

/**
 * Ensures the output directory exists
 */
export function ensureOutputDirectory(outputDir: string): void {
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }
}

/**
 * Generates an output file path with timestamp
 */
export function generateOutputPath(templateName: string, outputDir: string): string {
  const timestamp = generateTimestamp();
  const filename = `${templateName}-${timestamp}.csv`;
  return path.join(outputDir, filename);
}

/**
 * Gets the output directory (relative to scripts/util)
 */
export function getOutputDirectory(): string {
  // Navigate from dist/utils to scripts/util root, then to data/output
  const utilRoot = path.resolve(__dirname, '..', '..');
  return path.join(utilRoot, 'data', 'output');
}
