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
      { id: 'cidade', title: 'Cidade' },
      { id: 'estado', title: 'Estado' },
      { id: 'matricula', title: 'Matricula' }
    ]
  });
  
  // Transform input rows to PV template format
  const pvRows = rows.map(row => {
    const pv = getColumnValue(row, 'pv', 'PV', 'Código PV', 'codigo_pv', 'codigoPv');
    
    return {
      codigoPv: pv,
      nome: getColumnValue(row, 'nome', 'Nome', 'name', 'Name'),
      endereco: getColumnValue(row, 'endereco', 'Endereço', 'endereco', 'address', 'Address'),
      cidade: getColumnValue(row, 'cidade', 'Cidade', 'city', 'City'),
      estado: getColumnValue(row, 'estado', 'Estado', 'state', 'State'),
      matricula: getColumnValue(row, 'matricula', 'Matricula', 'Matrícula'),
      _pvKey: String(pv).toLowerCase().trim()
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
