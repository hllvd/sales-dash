import * as fs from 'fs';
import * as path from 'path';
import { createObjectCsvWriter, createObjectCsvStringifier } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';
import { readInputFile } from '../utils/fileReader';
import { ColumnFilterTransformer } from '../transformers/ColumnFilterTransformer';

/**
 * Converts input file (CSV or XLSX) to CSV format, removing 'Total' and 'Numeric' columns
 */
export async function toCsv(inputFile: string, outputPath?: string): Promise<string> {
  const outputDir = getOutputDirectory();
  
  const finalOutputPath = outputPath || generateOutputPath('to-csv', outputDir, inputFile);
  
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
  
  // Write BOM and headers first to ensure Excel compatibility + presence of columns
  const stringifier = createObjectCsvStringifier({ header: headers });
  const headerRow = stringifier.getHeaderString();
  fs.writeFileSync(finalOutputPath, '\uFEFF' + (headerRow || ''));

  // Write to CSV
  const csvWriter = createObjectCsvWriter({
    path: finalOutputPath,
    header: headers,
    append: true
  });
  
  await csvWriter.writeRecords(processedRows);
  
  return finalOutputPath;
}
