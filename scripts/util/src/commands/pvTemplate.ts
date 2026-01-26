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
 * Generates a Ponto de Venda (Point of Sale) template CSV
 */
export async function pvTemplate(inputFile: string): Promise<string> {
  const outputDir = getOutputDirectory();
  ensureOutputDirectory(outputDir);
  
  const outputPath = generateOutputPath('pv-temp', outputDir);
  
  // Read input file
  const rows = await readInputFile(inputFile);
  
  // Define PV template structure (added matricula)
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: [
      { id: 'codigoPv', title: 'Código PV' },
      { id: 'nome', title: 'Nome' },
      { id: 'endereco', title: 'Endereço' },
      { id: 'matricula', title: 'Matricula' }
    ]
  });
  
  // Transform input rows to PV template format
  const pvRows = rows.map(row => {
    // Priority: 'Código PV' should be copied as is
    const codigoPv = getColumnValue(row, 'Código PV', 'codigoPv', 'codigo_pv');
    
    // PV field from the source should be Name on the target file
    const nome = getColumnValue(row, 'PV', 'pv', 'Nome', 'Name', 'name');
    
    return {
      codigoPv: codigoPv,
      nome: nome,
      endereco: getColumnValue(row, 'endereco', 'Endereço', 'address', 'Address'),
      matricula: getColumnValue(row, 'matricula', 'Matricula', 'Matrícula'),
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
  return outputPath;
}
