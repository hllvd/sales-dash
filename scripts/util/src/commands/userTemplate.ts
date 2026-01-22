import * as fs from 'fs';
import * as path from 'path';
import { createObjectCsvWriter } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';
import { readInputFile } from '../utils/fileReader';

interface CsvRow {
  [key: string]: string;
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
      { id: 'surname', title: 'Surname' },
      { id: 'role', title: 'Role' },
      { id: 'parentEmail', title: 'ParentEmail' }
    ]
  });
  
  // Transform input rows to user template format
  const userRows = rows.map(row => ({
    name: row.name || row.Name || '',
    email: row.email || row.Email || '',
    surname: row.surname || row.Surname || '',
    role: row.role || row.Role || '',
    parentEmail: row.parentEmail || row.ParentEmail || ''
  }));
  
  await csvWriter.writeRecords(userRows);
  return outputPath;
}
