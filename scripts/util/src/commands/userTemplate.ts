import * as fs from 'fs';
import * as path from 'path';
import { createObjectCsvWriter } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';
import { readInputFile } from '../utils/fileReader';

interface CsvRow {
  [key: string]: string;
}

import { UniqueFilterTransformer } from '../transformers/UniqueFilterTransformer';

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
  // Added 'matricula' and 'owner_matricula' as requested
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: [
      { id: 'name', title: 'Name' },
      { id: 'email', title: 'Email' },
      { id: 'surname', title: 'Surname' },
      { id: 'role', title: 'Role' },
      { id: 'parentEmail', title: 'ParentEmail' },
      { id: 'matricula', title: 'Matricula' },
      { id: 'owner_matricula', title: 'Owner_Matricula' }
    ]
  });
  
  // Transform input rows to user template format
  const userRows = rows.map(row => ({
    name: row.name || row.Name || '',
    email: row.email || row.Email || '',
    surname: row.surname || row.Surname || '',
    role: row.role || row.Role || '',
    parentEmail: row.parentEmail || row.ParentEmail || '',
    matricula: row.matricula || row.Matricula || '',
    owner_matricula: row.owner_matricula || row.Owner_Matricula || ''
  }));

  // Remove duplicates based on 'name'
  const deduplicator = new UniqueFilterTransformer('name');
  const uniqueRows = deduplicator.transform(userRows);
  
  await csvWriter.writeRecords(uniqueRows);
  return outputPath;
}
