import * as fs from 'fs';
import * as path from 'path';
import * as XLSX from 'xlsx';
import csvParser from 'csv-parser';
import { Transform } from 'stream';

export interface CsvRow {
  [key: string]: string;
}

/**
 * Simple Transform stream to strip UTF-8 BOM (EF BB BF)
 */
class StripBomTransformer extends Transform {
  private _firstChunk = true;

  _transform(chunk: any, encoding: string, callback: Function) {
    if (this._firstChunk) {
      this._firstChunk = false;
      // Buffer chunk can be checked for BOM
      if (Buffer.isBuffer(chunk) && chunk.length >= 3) {
        if (chunk[0] === 0xEF && chunk[1] === 0xBB && chunk[2] === 0xBF) {
          chunk = chunk.slice(3);
        }
      }
    }
    this.push(chunk);
    callback();
  }
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
        .pipe(new StripBomTransformer())
        .pipe(csvParser())
        .on('data', (row: CsvRow) => rows.push(row))
        .on('end', () => resolve(rows))
        .on('error', (error) => reject(error));
    });
  }
}
