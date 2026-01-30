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
 * Gets the base input directory (relative to scripts/util)
 */
export function getInputDirectory(): string {
  // Navigate to scripts/util root, then to data/in
  const utilRoot = path.resolve(process.cwd());
  return path.join(utilRoot, 'data', 'in');
}

/**
 * Gets the base output directory (relative to scripts/util)
 */
export function getOutputDirectory(): string {
  // Navigate to scripts/util root, then to data/output
  const utilRoot = path.resolve(process.cwd());
  return path.join(utilRoot, 'data', 'output');
}

/**
 * Generates an output file path with timestamp, mirroring input subfolders
 */
export function generateOutputPath(templateName: string, baseOutputDir: string, inputFile: string): string {
  const timestamp = generateTimestamp();
  const inputDir = getInputDirectory();
  const absoluteInputFile = path.resolve(inputFile);
  
  let targetOutputDir = baseOutputDir;
  
  // If the input file is inside our data/in directory, calculate the subfolder
  if (absoluteInputFile.startsWith(inputDir)) {
    const relativeDir = path.dirname(path.relative(inputDir, absoluteInputFile));
    if (relativeDir !== '.') {
      targetOutputDir = path.join(baseOutputDir, relativeDir);
    }
  }
  
  // Ensure the specific subfolder exists
  ensureOutputDirectory(targetOutputDir);
  
  const filename = `${templateName}-${timestamp}.csv`;
  return path.join(targetOutputDir, filename);
}

/**
 * Generates an idempotent output file path (fixed filename in same directory as input)
 */
export function generateIdempotentPath(filename: string, inputFile: string): string {
  const inputDir = path.dirname(path.resolve(inputFile));
  return path.join(inputDir, filename);
}
