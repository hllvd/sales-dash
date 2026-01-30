#!/usr/bin/env node

import yargs from 'yargs';
import { hideBin } from 'yargs/helpers';
import { validateInputFile } from './utils/fileValidator';
import { toCsv } from './commands/toCsv';
import { userTemplate } from './commands/userTemplate';
import { pvTemplate } from './commands/pvTemplate';
import { pvMatTemplate } from './commands/pvMatTemplate';
import { readInputFile } from './utils/fileReader';
import { preview } from './commands/preview';
import { writeDemoDataToFile } from './utils/demoDataGenerator';
import { generateIdempotentPath } from './utils/outputGenerator';
import * as path from 'path';
import * as fs from 'fs';

interface Arguments {
  i?: string;
  demo?: boolean;
  _: (string | number)[];
  $0: string;
}

async function main() {
  const argv = yargs(hideBin(process.argv))
    .usage('Usage: $0 -i <input-file> <command> [--demo]')
    .option('i', {
      alias: 'input',
      describe: 'Input CSV or XLSX file path',
      type: 'string',
      demandOption: true,
      requiresArg: true
    })
    .option('demo', {
      describe: 'Generate demo data during Step 1',
      type: 'boolean',
      default: false
    })
    .command('step1', 'Step 1: Validate input, generate PV, User templates, and optionally Demo data')
    .command('step2', 'Step 2: Generate Matricula template')
    .command('to-csv', 'Convert input file to CSV format')
    .command('user-temp', 'Generate user template CSV from input CSV')
    .command('pv-temp', 'Generate Ponto de Venda template CSV')
    .command('mat-temp', 'Generate Matricula template CSV')
    .command('preview', 'Preview first 10 rows of the input file')
    .demandCommand(1, 'You must specify a command')
    .help('h')
    .alias('h', 'help')
    .example('$0 -i input.xlsx step1', 'Start process with Step 1')
    .example('$0 -i input.xlsx step1 --demo', 'Start process with Step 1 and generate demo data')
    .example('$0 -i input.xlsx step2', 'Continue with Step 2')
    .strict()
    .parseSync() as Arguments;

  let inputFile = argv.i!;
  const command = argv._[0] as string;
  const useDemo = argv.demo === true;

  try {
    // Validate file exists
    validateInputFile(inputFile);
    
    let outputPath: string;

    // Execute command
    switch (command) {
      case 'step1':
        console.log('🚀 Starting Step 1...');
        
        // 1. Generate PV CSV
        const pvOut = generateIdempotentPath('pv.csv', inputFile);
        await pvTemplate(inputFile, pvOut);
        console.log(`✅ PV template created: ${pvOut}`);
        
        // 2. Generate Users CSV
        const usersOut = generateIdempotentPath('users.csv', inputFile);
        await userTemplate(inputFile, usersOut);
        console.log(`✅ User template created: ${usersOut}`);

        // 3. Generate Demo Data if requested
        if (useDemo) {
          console.log('🎲 Demo mode enabled. Generating demo data...');
          const usersDemoPath = generateIdempotentPath('users-demo.csv', inputFile);
          const usersData = await readInputFile(usersOut);
          await writeDemoDataToFile(usersDemoPath, usersData);
          console.log(`✅ Demo data created: ${usersDemoPath}`);
        }
        
        console.log('\n✨ Step 1 completed successfully!');
        console.log('👉 You can now:');
        console.log('   1. Fill out missing information in users.csv manually');
        console.log('   2. Run Step 2: util -i <input> step2');
        break;

      case 'step2':
        console.log('🚀 Starting Step 2...');
        
        const usersCsvPath = generateIdempotentPath('users.csv', inputFile);
        const usersDemoPath = generateIdempotentPath('users-demo.csv', inputFile);
        let userSourcePath = usersCsvPath;

        if (fs.existsSync(usersDemoPath)) {
          console.log('💡 Found users-demo.csv, using it as source.');
          userSourcePath = usersDemoPath;
        } else if (!fs.existsSync(usersCsvPath)) {
          console.error(`❌ Error: users.csv not found in ${path.dirname(usersCsvPath)}`);
          console.error('Please run step1 first or ensure the file exists.');
          process.exit(1);
        }

        console.log(`📖 Using ${path.basename(userSourcePath)} as input for matriculas.`);
        
        const matOut = generateIdempotentPath('matricula.csv', inputFile);
        await pvMatTemplate(userSourcePath, matOut);
        console.log(`✅ Matricula template created: ${matOut}`);
        
        console.log('\n✨ Step 2 completed successfully!');
        break;

      case 'to-csv':
        console.log('🔄 Converting to CSV...');
        outputPath = await toCsv(inputFile);
        console.log(`✅ CSV file created: ${outputPath}`);
        break;

      case 'user-temp':
        if (useDemo) {
          console.log('🎲 Demo mode enabled. Generating demo data...');
          const inputRows = await readInputFile(inputFile);
          await writeDemoDataToFile(inputFile, inputRows);
        }
        console.log('👥 Generating user template...');
        outputPath = await userTemplate(inputFile);
        console.log(`✅ User template created: ${outputPath}`);
        break;

      case 'pv-temp':
        console.log('🏪 Generating Ponto de Venda template...');
        outputPath = await pvTemplate(inputFile);
        console.log(`✅ PV template created: ${outputPath}`);
        break;

      case 'mat-temp':
        console.log('🎓 Generating Matricula template...');
        outputPath = await pvMatTemplate(inputFile);
        console.log(`✅ Matricula template created: ${outputPath}`);
        break;

      case 'preview':
        await preview(inputFile, 10);
        break;

      default:
        console.error(`❌ Error: Unknown command '${command}'`);
        process.exit(1);
    }

  } catch (error) {
    if (error instanceof Error) {
      console.error(`❌ Error: ${error.message}`);
    } else {
      console.error('❌ An unexpected error occurred');
    }
    process.exit(1);
  }
}

main();
