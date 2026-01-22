import * as fs from 'fs';
import * as path from 'path';
import { createObjectCsvWriter } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';
import { readInputFile } from '../utils/fileReader';
import { ColumnFilterTransformer } from '../transformers/ColumnFilterTransformer';

/**
 * Converts input file (CSV or XLSX) to CSV format, removing 'Total' and 'Numeric' columns
 */
export async function toCsv(inputFile: string): Promise<string> {
  const outputDir = getOutputDirectory();
  ensureOutputDirectory(outputDir);
  
  const outputPath = generateOutputPath('to-csv', outputDir);
  
  // Read input file
  const rows = await readInputFile(inputFile);
  
  if (rows.length === 0) {
    throw new Error('Input file is empty or has no data');
  }
  
  // Apply transformation
  // Using an extensible transformer pattern
  const transformer = new ColumnFilterTransformer(['Total', 'Numeric']);
  const processedRows = transformer.transform(rows);
  
  if (processedRows.length === 0 || Object.keys(processedRows[0]).length === 0) {
    throw new Error('No columns remaining after transformation');
  }
  
  // Get headers from first row
  const headers = Object.keys(processedRows[0]).map(key => ({
    id: key,
    title: key
  }));
  
  // Write to CSV
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: headers
  });
  
  await csvWriter.writeRecords(processedRows);
  
  return outputPath;
}
