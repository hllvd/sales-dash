const fs = require('fs');
const csv = require('csv-parser');
const createCsvWriter = require('csv-writer').createObjectCsvWriter;

// Configuration
const INPUT_FILE = 'users.csv';
const OUTPUT_FILE = 'users-with-emails.csv';

// Helper function to normalize strings (remove accents and convert to lowercase)
function normalizeString(str) {
  return str.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
}

// Generate email from full name
function generateEmail(fullName) {
  if (!fullName || typeof fullName !== 'string') {
    return 'unknown@test.com';
  }

  // Normalize: remove accents and convert to lowercase
  const normalized = fullName
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim();
  
  // Split by spaces and filter empty strings
  const parts = normalized.split(/\s+/).filter(p => p.length > 0);
  
  if (parts.length === 0) {
    return 'unknown@test.com';
  }
  
  if (parts.length === 1) {
    // Only one name part, use it as both first and last
    const clean = parts[0].replace(/[^a-z0-9]/g, '');
    return `${clean}@test.com`;
  }
  
  // First name is first part, surname is last part
  const firstName = parts[0];
  const surname = parts[parts.length - 1];
  
  // Remove non-alphanumeric characters
  const cleanFirst = firstName.replace(/[^a-z0-9]/g, '');
  const cleanSurname = surname.replace(/[^a-z0-9]/g, '');
  
  return `${cleanFirst}.${cleanSurname}@test.com`;
}

// Main processing function
async function processUsers() {
  console.log('🚀 Starting user email generation...\n');

  // Check if input file exists
  if (!fs.existsSync(INPUT_FILE)) {
    console.error(`❌ Error: Input file "${INPUT_FILE}" not found.`);
    console.log(`   Please create a ${INPUT_FILE} file in this directory.\n`);
    process.exit(1);
  }

  const users = [];
  let matriculaColumn = null;
  let nomeColumn = null;
  let headerProcessed = false;

  // Read and parse CSV
  return new Promise((resolve, reject) => {
    fs.createReadStream(INPUT_FILE)
      .pipe(csv())
      .on('headers', (headers) => {
        // Find column names (case and accent insensitive)
        matriculaColumn = headers.find(h => normalizeString(h) === 'matricula');
        nomeColumn = headers.find(h => {
          const normalized = normalizeString(h);
          return normalized === 'nome' || normalized === 'name' || normalized === 'comissionado';
        });

        if (!matriculaColumn) {
          console.error('❌ Error: "Matricula" column not found in CSV.');
          console.log('   Available columns:', headers.join(', '));
          process.exit(1);
        }

        if (!nomeColumn) {
          console.error('❌ Error: "Nome" column not found in CSV.');
          console.log('   Available columns:', headers.join(', '));
          process.exit(1);
        }

        console.log('✅ Found columns:');
        console.log(`   - Matricula: "${matriculaColumn}"`);
        console.log(`   - Nome: "${nomeColumn}"\n`);
        headerProcessed = true;
      })
      .on('data', (row) => {
        if (!headerProcessed) return;

        const matricula = row[matriculaColumn];
        const nome = row[nomeColumn];

        if (!matricula || !nome) {
          console.warn(`⚠️  Skipping row with missing data: ${JSON.stringify(row)}`);
          return;
        }

        const email = generateEmail(nome);
        
        users.push({
          [matriculaColumn]: matricula,
          [nomeColumn]: nome,
          Email: email
        });
      })
      .on('end', async () => {
        if (users.length === 0) {
          console.error('❌ No valid users found in the CSV file.');
          process.exit(1);
        }

        console.log(`📊 Processed ${users.length} users\n`);

        // Write output CSV
        const csvWriter = createCsvWriter({
          path: OUTPUT_FILE,
          header: [
            { id: matriculaColumn, title: matriculaColumn },
            { id: nomeColumn, title: nomeColumn },
            { id: 'Email', title: 'Email' }
          ]
        });

        try {
          await csvWriter.writeRecords(users);
          console.log(`✅ Successfully created "${OUTPUT_FILE}"`);
          console.log(`\n📧 Sample emails generated:`);
          users.slice(0, 5).forEach(user => {
            console.log(`   ${user[nomeColumn]} → ${user.Email}`);
          });
          if (users.length > 5) {
            console.log(`   ... and ${users.length - 5} more`);
          }
          console.log('\n✨ Done!\n');
          resolve();
        } catch (error) {
          console.error('❌ Error writing output file:', error.message);
          reject(error);
        }
      })
      .on('error', (error) => {
        console.error('❌ Error reading CSV file:', error.message);
        reject(error);
      });
  });
}

// Run the script
processUsers().catch(error => {
  console.error('❌ Fatal error:', error);
  process.exit(1);
});
