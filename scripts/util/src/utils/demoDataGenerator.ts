import * as fs from 'fs';
import * as path from 'path';
import { createObjectCsvWriter, createObjectCsvStringifier } from 'csv-writer';

interface DemoUser {
  Name: string;
  Email: string;
  Role: string;
  ParentEmail: string;
  Matricula: string;
  Owner_Matricula: string;
}

/**
 * Converts a name like "Jo\u00e3o da Silva" to "joaodasilva@example.com"
 */
function nameToEmail(name: string): string {
  return name.toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "") // remove accents
    .replace(/\s+/g, '')            // remove spaces (no dots)
    .replace(/[^a-z0-9@]/g, '')     // safety: strip anything not alphanumeric/@
    + '@example.com';
}

function getRandomItem<T>(array: T[]): T {
  return array[Math.floor(Math.random() * array.length)];
}

/**
 * Generates realistic demo data based on input names and matriculas from users.csv
 */
export function generateDemoData(inputRows: any[]): DemoUser[] {
  // 1. Generate users with matriculas and names from input
  const users: DemoUser[] = inputRows.map(row => {
    // Detect matricula column case-insensitively
    const matriculaKey = Object.keys(row).find(k => k.toLowerCase() === 'matricula' || k.toLowerCase() === 'matrícula');
    const matricula = matriculaKey ? String(row[matriculaKey]) : '0000';
    
    // Detect name column case-insensitively
    const nameKey = Object.keys(row).find(k => k.toLowerCase() === 'name' || k.toLowerCase() === 'nome');
    const name = nameKey ? String(row[nameKey]) : 'Guest User';
    
    return {
      Name: name,
      Email: nameToEmail(name),
      Role: 'user',
      ParentEmail: '',
      Matricula: matricula,
      Owner_Matricula: '0'
    };
  });

  // 2. Collect all generated emails for random parent assignment
  const emailPool = users.map(u => u.Email);

  // 3. Assign ParentEmail and Owner logic
  users.forEach(user => {
    // Only 10% of users get a ParentEmail
    if (Math.random() < 0.1) {
      // 30% of those 10% will be owners (ParentEmail == Email)
      // 70% of those 10% will have a random parent
      if (Math.random() < 0.3) {
        user.ParentEmail = user.Email;
      } else {
        user.ParentEmail = getRandomItem(emailPool);
      }
    } else {
      user.ParentEmail = '';
    }

    // "When the parent is equal the mail, this is a matricula owner and it's true, otherwise it's false."
    user.Owner_Matricula = (user.ParentEmail === user.Email) ? '1' : '0';
  });

  return users;
}

/**
 * Writes demo data to a CSV file
 */
export async function writeDemoDataToFile(outputPath: string, inputRows: any[]): Promise<void> {
  const users = generateDemoData(inputRows);
  
  // Ensure directory exists
  const dir = path.dirname(outputPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }

  const header = [
    { id: 'Name', title: 'Name' },
    { id: 'Email', title: 'Email' },
    { id: 'Role', title: 'Role' },
    { id: 'ParentEmail', title: 'ParentEmail' },
    { id: 'Matricula', title: 'Matricula' },
    { id: 'Owner_Matricula', title: 'Owner_Matricula' }
  ];

  // Write BOM and headers first to ensure Excel compatibility + presence of columns
  const stringifier = createObjectCsvStringifier({ header });
  const headerRow = stringifier.getHeaderString();
  fs.writeFileSync(outputPath, '\uFEFF' + (headerRow || ''));

  const csvWriter = createObjectCsvWriter({
    path: outputPath,
    header,
    append: true
  });
  
  await csvWriter.writeRecords(users);
}

