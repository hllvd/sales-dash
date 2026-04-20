// scratch/compare_results.js
// Utility to compare two JSON output files from the PowerBI scraper.
// Usage: node scratch/compare_results.js <file1.json> <file2.json>

const fs = require('fs');
const path = require('path');

function compareFiles(path1, path2) {
  if (!fs.existsSync(path1) || !fs.existsSync(path2)) {
    console.error('Error: One or both files do not exist.');
    console.log(`Path 1: ${path1}`);
    console.log(`Path 2: ${path2}`);
    process.exit(1);
  }

  const data1 = JSON.parse(fs.readFileSync(path1, 'utf8'));
  const data2 = JSON.parse(fs.readFileSync(path2, 'utf8'));

  console.log('--- Comparison Results ---');
  console.log(`File A: ${path.basename(path1)} (${data1.length} rows)`);
  console.log(`File B: ${path.basename(path2)} (${data2.length} rows)`);
  console.log('--------------------------\n');

  // Extract the Contract Number (usually the last part of a semicolon-separated string)
  const extractContractNumber = (val) => {
    if (typeof val !== 'string') return val;
    const parts = val.split(';');
    return parts[parts.length - 1].trim();
  };

  // Use "Contract Number" as the unique identifier for matching rows
  const getKey = (row) => {
    const rawCota = row['Cota'] || row['2_Medidas_Tabela.id_cota'] || row['1_Medidas.Obs Restrições Cota'];
    if (rawCota) return extractContractNumber(rawCota);
    return row['id_bi'] || row['Sum(tbl_cotas.rn)'] || JSON.stringify(row);
  };

  const map1 = new Map(data1.map(r => [getKey(r), r]));
  const map2 = new Map(data2.map(r => [getKey(r), r]));

  const keys1 = Array.from(map1.keys());
  const keys2 = Array.from(map2.keys());

  const onlyIn1 = keys1.filter(k => !map2.has(k));
  const onlyIn2 = keys2.filter(k => !map1.has(k));
  const common = keys1.filter(k => map2.has(k));

  console.log(`Contracts unique to File A: ${onlyIn1.length}`);
  if (onlyIn1.length > 0) {
    console.log('Sample Contract Numbers unique to A:', onlyIn1.slice(0, 5).join(', '));
  }

  console.log(`Contracts unique to File B: ${onlyIn2.length}`);
  if (onlyIn2.length > 0) {
    console.log('Sample Contract Numbers unique to B:', onlyIn2.slice(0, 5).join(', '));
  }

  console.log(`Shared contracts:           ${common.length}`);

  // Check for value differences in shared rows
  let differencesCount = 0;
  common.forEach(k => {
    const r1 = map1.get(k);
    const r2 = map2.get(k);
    
    // Compare basic fields (ignore case/whitespace if needed, but strict for now)
    const diffs = [];
    Object.keys(r1).forEach(field => {
      if (JSON.stringify(r1[field]) !== JSON.stringify(r2[field])) {
        diffs.push(`${field}: "${r1[field]}" vs "${r2[field]}"`);
      }
    });

    if (diffs.length > 0 && differencesCount < 5) {
      console.log(`\n[Diff] Row Key ${k}:`);
      diffs.forEach(d => console.log(`  - ${d}`));
      differencesCount++;
    }
  });

  if (differencesCount >= 5) {
    console.log('\n... (more differences found, but truncated) ...');
  } else if (common.length > 0 && differencesCount === 0) {
    console.log('\nAll shared rows match exactly.');
  }

  console.log('\n--- End of Report ---');
}

// Get arguments from CLI
const args = process.argv.slice(2);
if (args.length < 2) {
  console.log('Usage: node scratch/compare_results.js <path_to_json1> <path_to_json2>');
  process.exit(1);
}

compareFiles(args[0], args[1]);
