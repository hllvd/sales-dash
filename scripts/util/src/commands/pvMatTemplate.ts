import * as fs from 'fs';
import { createObjectCsvWriter } from 'csv-writer';
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
 * Generates a Matricula template CSV
 * Can accept user-temp output as input
 */
export async function pvMatTemplate(inputFile: string): Promise<string> {
  const outputDir = getOutputDirectory();
  
  const outputPath = generateOutputPath('mat-temp', outputDir, inputFile);
  
  // Read input file
  const rows = await readInputFile(inputFile);
  
  // Calculate date 2 years ago in YYYY-MM-DD format
  const twoYearsAgo = new Date();
  twoYearsAgo.setFullYear(twoYearsAgo.getFullYear() - 2);
  const startDate = twoYearsAgo.toISOString().split('T')[0]; // Format: YYYY-MM-DD
  
  // Write BOM first to ensure Excel compatibility with UTF-8
  fs.writeFileSync(outputPath, '\uFEFF');

  // Define Matricula template structure
  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header: [
      { id: 'matriculaNumber', title: 'matriculaNumber' },
      { id: 'userEmail', title: 'userEmail' },
      { id: 'isOwner', title: 'isOwner' },
      { id: 'startDate', title: 'startDate' },
      { id: 'endDate', title: 'endDate' }
    ],
    append: true
  });
  
  // Transform input rows to matricula template format
  const matRows = rows.map(row => {
    const matricula = getColumnValue(row, 'matriculaNumber', 'matricula', 'Matricula', 'Matrícula');
    
    return {
      matriculaNumber: matricula,
      userEmail: getColumnValue(row, 'userEmail', 'email', 'Email', 'e-mail', 'E-mail', 'EMAIL', 'E-MAIL'),
      isOwner: getColumnValue(row, 'isOwner', 'IsOwner', 'owner_matricula', 'Owner_Matricula') || '0',
      startDate: startDate, // Date 2 years ago
      endDate: null,
      _matKey: String(matricula).toLowerCase().trim()
    };
  });

  // Group rows by matricula to handle multiple users and enforce single ownership
  const groups = new Map<string, any[]>();
  
  matRows.forEach(row => {
    if (!row._matKey || row._matKey === '') return;
    
    if (!groups.has(row._matKey)) {
      groups.set(row._matKey, []);
    }
    groups.get(row._matKey)!.push(row);
  });

  const processedRows: any[] = [];
  
  groups.forEach((groupUsers) => {
    // Deduplicate by email within the same matricula group
    const emailSeen = new Set<string>();
    const uniqueGroupUsers = groupUsers.filter(u => {
      const emailLower = u.userEmail.toLowerCase().trim();
      if (!emailLower || emailSeen.has(emailLower)) return false;
      emailSeen.add(emailLower);
      return true;
    });

    // Enforce single owner per matricula: exactly one owner per group
    let ownerAssigned = false;
    
    // First pass: identify the first explicit owner and demote others
    uniqueGroupUsers.forEach(u => {
      const isOwnerVal = String(u.isOwner).toLowerCase().trim();
      const isExplicitOwner = isOwnerVal === '1' || isOwnerVal === 'true' || isOwnerVal === 'yes';
      
      if (isExplicitOwner) {
        if (!ownerAssigned) {
          u.isOwner = '1';
          ownerAssigned = true;
        } else {
          u.isOwner = '0'; // Demote subsequent owners
        }
      } else {
        u.isOwner = '0';
      }
    });

    // Second pass: if no owner was assigned, force-assign the first user in the group
    if (!ownerAssigned && uniqueGroupUsers.length > 0) {
      uniqueGroupUsers[0].isOwner = '1';
    }

    processedRows.push(...uniqueGroupUsers);
  });
  
  // Remove the matricula key before writing
  const outputRows = processedRows.map(({ _matKey, ...rest }) => rest);
  
  await csvWriter.writeRecords(outputRows);
  return outputPath;
}
