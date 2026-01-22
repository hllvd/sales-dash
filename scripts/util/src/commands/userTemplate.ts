import * as fs from 'fs';
import * as path from 'path';
import { createObjectCsvWriter } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';
import { readInputFile } from '../utils/fileReader';

interface CsvRow {
  [key: string]: string;
}

/**
 * Helper function to get column value with case-insensitive matching
 */
function getColumnValue(row: any, ...columnNames: string[]): string {
  for (const colName of columnNames) {
    // Try exact match first
    if (row[colName]) {
      return row[colName];
    }
    
    // Try case-insensitive match
    const key = Object.keys(row).find(k => k.toLowerCase() === colName.toLowerCase());
    if (key && row[key]) {
      return row[key];
    }
  }
  return '';
}

/**
 * Generates a user template CSV from input CSV or XLSX
 */
export async function userTemplate(inputFile: string): Promise<string> {
  const outputDir = getOutputDirectory();
  ensureOutputDirectory(outputDir);
  
  const outputPath = generateOutputPath('user-template', outputDir);
  
  // Read input file (CSV or XLSX)
  const rows = await readInputFile(inputFile);
  
  // Define user template structure
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: [
      { id: 'name', title: 'Name' },
      { id: 'email', title: 'Email' },
      { id: 'role', title: 'Role' },
      { id: 'parentEmail', title: 'ParentEmail' },
      { id: 'matricula', title: 'Matricula' },
      { id: 'owner_matricula', title: 'Owner_Matricula' }
    ]
  });
  
  // Transform input rows to user template format
  const userRows = rows.map(row => {
    // Use 'comissionado' for name if it exists, otherwise fall back to 'name'
    const comissionado = getColumnValue(row, 'comissionado', 'Comissionado');
    const name = comissionado || getColumnValue(row, 'name', 'Name');
    const matricula = getColumnValue(row, 'matricula', 'Matricula', 'Matrícula');
    
    return {
      name,
      email: getColumnValue(row, 'email', 'Email'),
      role: getColumnValue(row, 'role', 'Role'),
      parentEmail: getColumnValue(row, 'parentEmail', 'ParentEmail'),
      matricula,
      owner_matricula: getColumnValue(row, 'owner_matricula', 'Owner_Matricula'),
      // Add composite key for deduplication (convert to string to handle any type)
      _compositeKey: `${String(name).toLowerCase().trim()}_${String(matricula).toLowerCase().trim()}`
    };
  });

  // Remove duplicates based on composite key (comissionado + matricula)
  const seen = new Set<string>();
  const uniqueRows = userRows.filter(row => {
    if (seen.has(row._compositeKey)) {
      return false;
    }
    seen.add(row._compositeKey);
    return true;
  });
  
  // Remove the composite key before writing
  const outputRows = uniqueRows.map(({ _compositeKey, ...rest }) => rest);
  
  await csvWriter.writeRecords(outputRows);
  return outputPath;
}
