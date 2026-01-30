import * as fs from 'fs';
import * as path from 'path';
import { createObjectCsvWriter, createObjectCsvStringifier } from 'csv-writer';
import { readInputFile } from '../utils/fileReader';
import { generateIdempotentPath } from '../utils/outputGenerator';

/**
 * Maps input "conferencia" values to backend ContractStatus
 */
function mapConferenciaToStatus(conferencia: string): string {
  const normalized = conferencia.trim().toUpperCase();
  
  switch (normalized) {
    case 'NORMAL':
      return 'Active';
    case 'NCONT 1 AT':
      return 'Late1';
    case 'NCONT 2 AT':
      return 'Late2';
    case 'SUJ. A CANCELAMENTO':
      return 'Late3';
    case 'EXCLUIDO':
    case 'DESISTENTE':
      return 'Defaulted';
    default:
      return 'Active'; // Default to Active if unknown
  }
}

/**
 * Helper function to get column value with case-insensitive matching
 */
function getColumnValue(row: any, ...columnNames: string[]): string {
  for (const colName of columnNames) {
    if ((row[colName] ?? '') !== '') {
      return String(row[colName]).trim();
    }
    const key = Object.keys(row).find(k => k.toLowerCase() === colName.toLowerCase());
    if (key && (row[key] ?? '') !== '') {
      return String(row[key]).trim();
    }
  }
  return '';
}

/**
 * Generates contracts.csv by enriching the input file with emails from the user source
 */
export async function enrichContracts(inputFile: string, userSourcePath: string): Promise<string> {
  // 1. Read users/demo users to build the lookup map
  const users = await readInputFile(userSourcePath);
  const emailLookup = new Map<string, string>();

  users.forEach(u => {
    const name = getColumnValue(u, 'Name', 'name').toLowerCase();
    const matricula = getColumnValue(u, 'Matricula', 'matricula').toLowerCase();
    const email = getColumnValue(u, 'Email', 'email');
    
    if (name && email) {
      emailLookup.set(`${name}|${matricula}`, email);
    }
  });

  // 2. Read the original input file
  const originalRows = await readInputFile(inputFile);
  if (originalRows.length === 0) {
    throw new Error('Input file is empty');
  }

  // 3. Prepare enrichment
  const enrichedRows = originalRows.map(row => {
    const comissionado = getColumnValue(row, 'comissionado', 'Comissionado');
    const name = comissionado || getColumnValue(row, 'name', 'Name');
    const matricula = getColumnValue(row, 'matricula', 'Matricula', 'Matrícula');
    
    const lookupKey = `${name.toLowerCase()}|${matricula.toLowerCase()}`;
    const email = emailLookup.get(lookupKey) || '';
    
    // Resolve Status from "conferencia"
    const conferencia = getColumnValue(row, 'conferencia', 'Conferência');
    const status = mapConferenciaToStatus(conferencia);
    
    // Create enriched row and format any Date objects found
    const enrichedRow: any = { ...row, email };
    
    // Overwrite or add Status column
    const statusKey = Object.keys(row).find(k => k.toLowerCase() === 'status') || 'Status';
    enrichedRow[statusKey] = status;
    
    for (const key of Object.keys(enrichedRow)) {
      const value = enrichedRow[key];
      if (value instanceof Date) {
        // Format as MM/DD/YYYY
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        const year = value.getFullYear();
        enrichedRow[key] = `${month}/${day}/${year}`;
      }
    }
    
    return enrichedRow;
  });

  // 4. Generate header for csv-writer (all original columns + email)
  // We use the first row to determine the keys
  const originalKeys = Object.keys(originalRows[0]);
  const header = [
    ...originalKeys.map(key => ({ id: key, title: key })),
    { id: 'email', title: 'Email' }
  ];

  const outputPath = generateIdempotentPath('contracts.csv', inputFile);

  // 5. Write to CSV
  const stringifier = createObjectCsvStringifier({ header });
  const headerRow = stringifier.getHeaderString();
  fs.writeFileSync(outputPath, '\uFEFF' + (headerRow || ''));

  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header,
    append: true
  });
  
  await csvWriter.writeRecords(enrichedRows);
  
  return outputPath;
}
