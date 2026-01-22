import { createObjectCsvWriter } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';

/**
 * Generates a Ponto de Venda (Point of Sale) template CSV
 */
export async function pvTemplate(inputFile: string): Promise<string> {
  const outputDir = getOutputDirectory();
  ensureOutputDirectory(outputDir);
  
  const outputPath = generateOutputPath('pv-temp', outputDir);
  
  // Define PV template structure
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: [
      { id: 'codigoPv', title: 'Código PV' },
      { id: 'nome', title: 'Nome' },
      { id: 'endereco', title: 'Endereço' },
      { id: 'cidade', title: 'Cidade' },
      { id: 'estado', title: 'Estado' }
    ]
  });
  
  // Write empty template (placeholder data can be added later)
  const placeholderRows = [
    {
      codigoPv: '',
      nome: '',
      endereco: '',
      cidade: '',
      estado: ''
    }
  ];
  
  await csvWriter.writeRecords(placeholderRows);
  return outputPath;
}
