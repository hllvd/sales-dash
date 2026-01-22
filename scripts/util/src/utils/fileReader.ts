import * as fs from 'fs';
import * as path from 'path';
import * as XLSX from 'xlsx';
import csvParser from 'csv-parser';

export interface CsvRow {
  [key: string]: string;
}

/**
 * Reads data from CSV or XLSX file
 * Returns an array of objects representing the rows
 */
export async function readInputFile(inputFile: string): Promise<any[]> {
  const ext = path.extname(inputFile).toLowerCase();
  
  if (ext === '.xlsx') {
    // Read XLSX file
    const workbook = XLSX.readFile(inputFile);
    // Use the first sheet
    const sheetName = workbook.SheetNames[0];
    if (!sheetName) {
      throw new Error('XLSX file has no sheets');
    }
    const worksheet = workbook.Sheets[sheetName];
    // Convert to JSON
    // defval: '' ensures empty cells are empty strings instead of undefined
    return XLSX.utils.sheet_to_json(worksheet, { defval: '' });
  } else {
    // Read CSV file
    return new Promise((resolve, reject) => {
      const rows: CsvRow[] = [];
      fs.createReadStream(inputFile)
        .pipe(csvParser())
        .on('data', (row: CsvRow) => rows.push(row))
        .on('end', () => resolve(rows))
        .on('error', (error) => reject(error));
    });
  }
}
