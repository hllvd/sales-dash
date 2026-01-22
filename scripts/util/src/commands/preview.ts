import { readInputFile } from '../utils/fileReader';

/**
 * Previews the first N rows of the input file
 */
export async function preview(inputFile: string, rowCount: number = 10): Promise<void> {
  // Read input file
  const rows = await readInputFile(inputFile);
  
  if (rows.length === 0) {
    console.log('📭 File is empty - no data to preview');
    return;
  }
  
  // Get the number of rows to display
  const displayCount = Math.min(rowCount, rows.length);
  const previewRows = rows.slice(0, displayCount);
  
  console.log(`\n📊 Preview of first ${displayCount} row(s):\n`);
  
  // Display as a formatted table
  console.table(previewRows);
  
  console.log(`\n📈 Total rows in file: ${rows.length}`);
  
  if (rows.length > displayCount) {
    console.log(`   Showing ${displayCount} of ${rows.length} rows\n`);
  }
}
