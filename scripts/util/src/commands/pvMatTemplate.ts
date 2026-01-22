import { createObjectCsvWriter } from 'csv-writer';
import { ensureOutputDirectory, generateOutputPath, getOutputDirectory } from '../utils/outputGenerator';

/**
 * Generates a Matricula template CSV
 */
export async function pvMatTemplate(inputFile: string): Promise<string> {
  const outputDir = getOutputDirectory();
  ensureOutputDirectory(outputDir);
  
  const outputPath = generateOutputPath('pv-mat', outputDir);
  
  // Define Matricula template structure
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: [
      { id: 'matricula', title: 'Matrícula' },
      { id: 'nome', title: 'Nome' },
      { id: 'codigoPv', title: 'Código PV' },
      { id: 'status', title: 'Status' },
      { id: 'dataAtivacao', title: 'Data Ativação' }
    ]
  });
  
  // Write empty template (placeholder data can be added later)
  const placeholderRows = [
    {
      matricula: '',
      nome: '',
      codigoPv: '',
      status: '',
      dataAtivacao: ''
    }
  ];
  
  await csvWriter.writeRecords(placeholderRows);
  return outputPath;
}
