import { createObjectCsvWriter } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';
import { readInputFile } from '../utils/fileReader';

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
 * Generates a Matricula template CSV
 * Can accept user-temp output as input
 */
export async function pvMatTemplate(inputFile: string): Promise<string> {
  const outputDir = getOutputDirectory();
  ensureOutputDirectory(outputDir);
  
  const outputPath = generateOutputPath('pv-mat', outputDir);
  
  // Read input file
  const rows = await readInputFile(inputFile);
  
  // Calculate date 2 years ago in YYYY-MM-DD format
  const twoYearsAgo = new Date();
  twoYearsAgo.setFullYear(twoYearsAgo.getFullYear() - 2);
  const startDate = twoYearsAgo.toISOString().split('T')[0]; // Format: YYYY-MM-DD
  
  // Define Matricula template structure
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: [
      { id: 'matricula', title: 'Matrícula' },
      { id: 'email', title: 'Email' },
      { id: 'isOwner', title: 'IsOwner' },
      { id: 'startDate', title: 'StartDate' }
    ]
  });
  
  // Transform input rows to matricula template format
  const matRows = rows.map(row => {
    const matricula = getColumnValue(row, 'matricula', 'Matricula', 'Matrícula');
    
    return {
      matricula,
      email: getColumnValue(row, 'email', 'Email'),
      isOwner: 'false', // Default to false
      startDate: startDate, // Date 2 years ago
      _matKey: String(matricula).toLowerCase().trim()
    };
  });

  // Remove duplicates based on matricula
  const seen = new Set<string>();
  const uniqueRows = matRows.filter(row => {
    // Skip rows with empty matricula
    if (!row._matKey || row._matKey === '') {
      return false;
    }
    
    if (seen.has(row._matKey)) {
      return false;
    }
    seen.add(row._matKey);
    return true;
  });
  
  // Remove the matricula key before writing
  const outputRows = uniqueRows.map(({ _matKey, ...rest }) => rest);
  
  await csvWriter.writeRecords(outputRows);
  return outputPath;
}
