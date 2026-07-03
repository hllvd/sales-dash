const xlsx = require('xlsx');
const path = require('path');
const fs = require('fs');

const testDataDir = path.resolve(__dirname, '../test-data');
if (!fs.existsSync(testDataDir)) {
  fs.mkdirSync(testDataDir, { recursive: true });
}

// Common headers matching the validator expectations
const headers = [
  'Contrato', 'Código PV', 'PV', 'Matrícula', 'Comissionado',
  'Grupo', 'Cota', 'Data da Venda', 'Valor', 'Nome do Cliente',
  'Tipo', 'Status'
];

function generateFile(filename, rows) {
  const ws = xlsx.utils.aoa_to_sheet([headers, ...rows]);
  const wb = xlsx.utils.book_new();
  xlsx.utils.book_append_sheet(wb, ws, 'Sheet1');
  const filePath = path.join(testDataDir, filename);
  xlsx.writeFile(wb, filePath);
  console.log(`Generated: ${filePath}`);
}

// 1. Contracts with blank numbers (hard block case)
generateFile('contracts_with_blanks.xlsx', [
  ['', '1234', 'PV TEST', 'MAT-001', 'Seller One', 'G1', '101', '2024-01-01', '150000', 'Client A', 'Imóvel', 'Ativa'],
  ['1100223344', '1234', 'PV TEST', 'MAT-001', 'Seller One', 'G1', '102', '2024-01-02', '150000', 'Client B', 'Imóvel', 'Ativa']
]);

// 2. Contracts with short numbers (length <= 3, requiring agree checkbox)
generateFile('contracts_with_short_numbers.xlsx', [
  ['12', '1234', 'PV TEST', 'MAT-001', 'Seller One', 'G1', '101', '2024-01-01', '150000', 'Client A', 'Imóvel', 'Ativa'],
  ['999', '1234', 'PV TEST', 'MAT-001', 'Seller One', 'G1', '102', '2024-01-02', '150000', 'Client B', 'Imóvel', 'Ativa'],
  ['1100223344', '1234', 'PV TEST', 'MAT-001', 'Seller One', 'G1', '103', '2024-01-03', '150000', 'Client C', 'Imóvel', 'Ativa']
]);
