/**
 * Safely formats a value for RFC 4180 CSV format.
 * - Wraps in double quotes if it contains a comma, double quote, or newline.
 * - Escapes double quotes with another double quote.
 */
function formatCsvValue(val: string | undefined | null): string {
  if (val === undefined || val === null) {
    return '';
  }
  const str = String(val);
  const needsQuotes = str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r');
  if (needsQuotes) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

/**
 * Converts an array of row objects to an RFC 4180 CSV string.
 * Automatically derives column order: all keys from the first row,
 * with ERR always as the last column.
 * Ensures the input array is not mutated.
 */
export function rowsToCsv(rows: Record<string, string>[]): string {
  if (!rows || rows.length === 0) {
    return '';
  }

  // Collect all unique keys from the rows (without mutating the input array or rows)
  const allKeysSet = new Set<string>();
  for (const row of rows) {
    for (const key of Object.keys(row)) {
      if (key !== 'ERR') {
        allKeysSet.add(key);
      }
    }
  }

  const columns = Array.from(allKeysSet);
  // Always append 'ERR' at the end
  columns.push('ERR');

  // Build header row
  const headerLine = columns.map(formatCsvValue).join(',');

  // Build data rows
  const dataLines = rows.map(row => {
    return columns.map(col => formatCsvValue(row[col])).join(',');
  });

  return [headerLine, ...dataLines].join('\r\n');
}

/**
 * Triggers a browser download of a CSV file containing the failed rows.
 * If rows is empty or undefined, no download occurs.
 */
export function downloadFailedRowsCsv(
  rows: Record<string, string>[] | null | undefined,
  filenamePrefix: string = 'import_errors'
): void {
  if (!rows || rows.length === 0) {
    return;
  }

  const csvContent = rowsToCsv(rows);
  const bom = '\uFEFF';
  const blob = new Blob([bom + csvContent], { type: 'text/csv;charset=utf-8;' });

  const dateStr = new Date().toISOString().split('T')[0];
  const filename = `${filenamePrefix}_${dateStr}.csv`;

  const link = document.createElement('a');
  if (link.download !== undefined) {
    const url = URL.createObjectURL(blob);
    link.setAttribute('href', url);
    link.setAttribute('download', filename);
    link.style.visibility = 'hidden';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }
}
