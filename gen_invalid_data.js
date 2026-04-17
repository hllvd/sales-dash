const XLSX = require('./client/e2e-test/node_modules/xlsx');
const path = require('path');

const data = [
  ['Name', 'Email', 'ParentEmail', 'Matricula', 'Owner_Matricula', 'Password'],
  ['Missing Email', '', '', 'MAT-ERR1', 1, 'pw123'], // Invalid: Missing Email
  ['', 'no.name@test.com', '', 'MAT-ERR2', 1, 'pw123'], // Invalid: Missing Name
  ['Missing Matricula', 'test@test.com', '', '', 1, 'pw123'], // Invalid: Missing Matricula
];

const ws = XLSX.utils.aoa_to_sheet(data);
const wb = XLSX.utils.book_new();
XLSX.utils.book_append_sheet(wb, ws, 'Users');

const targetPath = path.resolve(__dirname, 'client/e2e-test/test-data/users_missing_fields.xlsx');
XLSX.writeFile(wb, targetPath);

console.log(`Created: ${targetPath}`);
