import * as fs from 'fs';
import { createObjectCsvWriter, createObjectCsvStringifier } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';
import { readInputFile } from '../utils/fileReader';

/**
 * Helper function to get column value with case-insensitive matching
 */
function getColumnValue(row: any, ...columnNames: string[]): string {
  for (const colName of columnNames) {
    // Try exact match first
    if ((row[colName] ?? '') !== '') {
      return row[colName];
    }
    
    // Try case-insensitive match
    const key = Object.keys(row).find(k => k.toLowerCase() === colName.toLowerCase());
    if (key && (row[key] ?? '') !== '') {
      return row[key];
    }
  }
  return '';
}

/**
 * Generates a Ponto de Venda (Point of Sale) template CSV
 */
export async function pvTemplate(inputFile: string, outputPath?: string): Promise<string> {
  const outputDir = getOutputDirectory();
  
  const finalOutputPath = outputPath || generateOutputPath('pv-temp', outputDir, inputFile);
  
  // Read input file
  const rows = await readInputFile(inputFile);
  
  const header = [
    { id: 'codigoPv', title: 'Código PV' },
    { id: 'nome', title: 'PV' }
  ];

  // Write BOM and headers first to ensure Excel compatibility + presence of columns
  const stringifier = createObjectCsvStringifier({ header });
  const headerRow = stringifier.getHeaderString();
  fs.writeFileSync(finalOutputPath, '\uFEFF' + (headerRow || ''));

  // Define PV template structure
  const csvWriter = createObjectCsvWriter({
    path: finalOutputPath,
    header,
    append: true
  });
  
  // Transform input rows to PV template format
  const pvRows = rows.map(row => {
    // Priority: 'Código PV' should be copied as is. Expand patterns to be more robust.
    const codigoPv = getColumnValue(row, 'Código PV', 'codigoPv', 'codigo_pv', 'PvId', 'pvid', 'PV ID', 'PV_ID', 'Id', 'id', 'Código', 'codigo');
    
    // PV field from the source should be Name on the target file
    const nome = getColumnValue(row, 'PV', 'pv', 'Nome', 'Name', 'name');
    
    return {
      codigoPv: codigoPv,
      nome: nome,
      _pvKey: String(codigoPv).toLowerCase().trim()
    };
  });

  // Remove duplicates based on PV
  const seen = new Set<string>();
  const uniqueRows = pvRows.filter(row => {
    if (seen.has(row._pvKey)) {
      return false;
    }
    seen.add(row._pvKey);
    return true;
  });
  
  // Remove the PV key before writing
  const outputRows = uniqueRows.map(({ _pvKey, ...rest }) => rest);
  
  await csvWriter.writeRecords(outputRows);
  return finalOutputPath;
}
