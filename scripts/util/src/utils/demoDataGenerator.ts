import * as fs from 'fs';
import * as path from 'path';

interface DemoUser {
  name: string;
  email: string;
  role: string;
  parentEmail: string;
  matricula: string;
  owner_matricula: string;
}

/**
 * Converts a name like "John Wicker" to "john.wicker@example.com"
 */
function nameToEmail(name: string): string {
  return name.toLowerCase().trim().replace(/\s+/g, '.') + '@example.com';
}

const FIRST_NAMES = [
  'Carlos', 'Ana', 'Roberto', 'Patricia', 'João', 'Maria', 'Pedro', 'Juliana',
  'Lucas', 'Fernanda', 'Rafael', 'Camila', 'Bruno', 'Amanda', 'Thiago', 'Larissa',
  'Gabriel', 'Beatriz', 'Felipe', 'Letícia', 'Ricardo', 'Sônia', 'Marcelo', 'Débora'
];

const LAST_NAMES = [
  'Silva', 'Santos', 'Lima', 'Costa', 'Oliveira', 'Ferreira', 'Alves', 'Rocha',
  'Martins', 'Souza', 'Pereira', 'Dias', 'Carvalho', 'Ribeiro', 'Gomes', 'Mendes',
  'Barbosa', 'Rodrigues', 'Almeida', 'Nascimento', 'Cardoso', 'Teixeira', 'Borges'
];

function getRandomItem<T>(array: T[]): T {
  return array[Math.floor(Math.random() * array.length)];
}

function getRandomName(): string {
  return `${getRandomItem(FIRST_NAMES)} ${getRandomItem(LAST_NAMES)}`;
}

/**
 * Generates realistic demo data based on input matriculas
 */
export function generateDemoData(inputRows: any[]): DemoUser[] {
  // Map input rows to preliminary demo users, preserving matricula
  const users: DemoUser[] = inputRows.map(row => {
    // Detect matricula column case-insensitively
    const matriculaKey = Object.keys(row).find(k => k.toLowerCase() === 'matricula' || k.toLowerCase() === 'matrícula');
    const matricula = matriculaKey ? String(row[matriculaKey]) : '0000';
    
    const name = getRandomName();
    const role = Math.random() > 0.8 ? 'admin' : 'user'; // Basic distribution, will be adjusted
    
    return {
      name,
      email: nameToEmail(name),
      role,
      parentEmail: '', // Will be assigned later
      matricula,
      owner_matricula: '0' // Will be assigned later
    };
  });

  // Ensure at least one superadmin for the whole set if it's large enough, or just promote the first admin
  if (users.length > 0) {
    const adminIndex = users.findIndex(u => u.role === 'admin');
    if (adminIndex !== -1) {
      users[adminIndex].role = 'superadmin';
    } else {
      users[0].role = 'superadmin';
    }
  }

  // Group all users by matricula to ensure exactly one owner per matricula
  const matriculaGroups = new Map<string, DemoUser[]>();
  users.forEach(user => {
    if (!matriculaGroups.has(user.matricula)) {
      matriculaGroups.set(user.matricula, []);
    }
    matriculaGroups.get(user.matricula)!.push(user);
  });

  // Apply ownership logic per matricula group
  matriculaGroups.forEach((groupUsers) => {
    // First, clear all ownership
    groupUsers.forEach(u => u.owner_matricula = '0');
    
    // Determine which user will be the owner
    // Prioritize superadmin or admins
    const priorityOwner = groupUsers.find(u => u.role === 'superadmin' || u.role === 'admin');
    
    if (priorityOwner) {
      priorityOwner.owner_matricula = '1';
    } else {
      // Pick one user randomly from the group to be the owner
      const randomIndex = Math.floor(Math.random() * groupUsers.length);
      groupUsers[randomIndex].owner_matricula = '1';
    }
  });
  
  // Create a pool of all matricula owners
  const ownersPool = users.filter(u => u.owner_matricula === '1').map(u => u.email);
  
  // Assign parentEmail randomly from the owners pool
  users.forEach(user => {
    // SuperAdmins are roots (no parent)
    if (user.role === 'superadmin') {
      user.parentEmail = '';
      return;
    }
    
    // Pick a random parent from the pool, excluding self
    const potentialParents = ownersPool.filter(email => email !== user.email);
    
    if (potentialParents.length > 0) {
      const randomIndex = Math.floor(Math.random() * potentialParents.length);
      user.parentEmail = potentialParents[randomIndex];
    } else {
      user.parentEmail = '';
    }
  });

  return users;
}

/**
 * Writes demo data to a CSV file
 */
export async function writeDemoDataToFile(outputPath: string, inputRows: any[]): Promise<void> {
  const users = generateDemoData(inputRows);
  
  // Create CSV content
  const header = 'Name,Email,Role,ParentEmail,Matricula,Owner_Matricula\n';
  const rows = users.map(user => 
    `"${user.name}","${user.email}","${user.role}","${user.parentEmail}","${user.matricula}","${user.owner_matricula}"`
  ).join('\n');
  
  const csvContent = header + rows;
  
  // Ensure directory exists
  const dir = path.dirname(outputPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  
  // Write file
  fs.writeFileSync(outputPath, csvContent, 'utf-8');
}

