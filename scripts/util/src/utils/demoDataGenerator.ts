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
 * Generates realistic demo data for user template
 */
export function generateDemoData(): DemoUser[] {
  const users: DemoUser[] = [];
  
  // SuperAdmin (no parent, owns matricula 1000)
  users.push({
    name: 'Carlos Silva',
    email: 'carlos.silva@example.com',
    role: 'superadmin',
    parentEmail: '',
    matricula: '1000',
    owner_matricula: '1'
  });
  
  // Admins (report to superadmin, own their matriculas)
  const admins = [
    { name: 'Ana Santos', email: 'ana.santos@example.com', matricula: '2001' },
    { name: 'Roberto Lima', email: 'roberto.lima@example.com', matricula: '2002' },
    { name: 'Patricia Costa', email: 'patricia.costa@example.com', matricula: '2003' }
  ];
  
  admins.forEach(admin => {
    users.push({
      name: admin.name,
      email: admin.email,
      role: 'admin',
      parentEmail: 'carlos.silva@example.com',
      matricula: admin.matricula,
      owner_matricula: '1'
    });
  });
  
  // Users (distributed among admins, some own matriculas, some use admin's)
  const userGroups = [
    // Ana's team
    [
      { name: 'João Oliveira', email: 'joao.oliveira@example.com', matricula: '3001', isOwner: '1' },
      { name: 'Maria Ferreira', email: 'maria.ferreira@example.com', matricula: '3002', isOwner: '1' },
      { name: 'Pedro Alves', email: 'pedro.alves@example.com', matricula: '2001', isOwner: '0' }, // Uses Ana's
      { name: 'Juliana Rocha', email: 'juliana.rocha@example.com', matricula: '2001', isOwner: '0' } // Uses Ana's
    ],
    // Roberto's team
    [
      { name: 'Lucas Martins', email: 'lucas.martins@example.com', matricula: '3003', isOwner: '1' },
      { name: 'Fernanda Souza', email: 'fernanda.souza@example.com', matricula: '3004', isOwner: '1' },
      { name: 'Rafael Pereira', email: 'rafael.pereira@example.com', matricula: '2002', isOwner: '0' }, // Uses Roberto's
      { name: 'Camila Dias', email: 'camila.dias@example.com', matricula: '2002', isOwner: '0' } // Uses Roberto's
    ],
    // Patricia's team
    [
      { name: 'Bruno Carvalho', email: 'bruno.carvalho@example.com', matricula: '3005', isOwner: '1' },
      { name: 'Amanda Ribeiro', email: 'amanda.ribeiro@example.com', matricula: '3006', isOwner: '1' },
      { name: 'Thiago Gomes', email: 'thiago.gomes@example.com', matricula: '2003', isOwner: '0' }, // Uses Patricia's
      { name: 'Larissa Mendes', email: 'larissa.mendes@example.com', matricula: '2003', isOwner: '0' } // Uses Patricia's
    ]
  ];
  
  userGroups.forEach((group, adminIndex) => {
    const adminEmail = admins[adminIndex].email;
    group.forEach(user => {
      users.push({
        name: user.name,
        email: user.email,
        role: 'user',
        parentEmail: adminEmail,
        matricula: user.matricula,
        owner_matricula: user.isOwner
      });
    });
  });
  
  return users;
}

/**
 * Writes demo data to a CSV file
 */
export function writeDemoDataToFile(outputPath: string): void {
  const users = generateDemoData();
  
  // Create CSV content
  const header = 'Name,Email,Role,ParentEmail,Matricula,Owner_Matricula\n';
  const rows = users.map(user => 
    `${user.name},${user.email},${user.role},${user.parentEmail},${user.matricula},${user.owner_matricula}`
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
