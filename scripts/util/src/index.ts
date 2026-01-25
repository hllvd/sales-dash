#!/usr/bin/env node

import yargs from 'yargs';
import { hideBin } from 'yargs/helpers';
import { validateInputFile } from './utils/fileValidator';
import { toCsv } from './commands/toCsv';
import { userTemplate } from './commands/userTemplate';
import { pvTemplate } from './commands/pvTemplate';
import { pvMatTemplate } from './commands/pvMatTemplate';
import { preview } from './commands/preview';
import { writeDemoDataToFile } from './utils/demoDataGenerator';
import * as path from 'path';

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
      describe: 'Input CSV file path',
      type: 'string',
      demandOption: true,
      requiresArg: true
    })
    .option('demo', {
      describe: 'Generate demo data for user-temp command',
      type: 'boolean',
      default: false
    })
    .command('to-csv', 'Convert input file to CSV format')
    .command('user-temp', 'Generate user template CSV from input CSV')
    .command('pv-temp', 'Generate Ponto de Venda template CSV')
    .command('pv-mat', 'Generate Matricula template CSV')
    .command('preview', 'Preview first 10 rows of the input file')
    .demandCommand(1, 'You must specify a command')
    .help('h')
    .alias('h', 'help')
    .example('$0 -i input.csv to-csv', 'Convert input.csv to CSV format')
    .example('$0 -i users.csv user-temp', 'Generate user template from users.csv')
    .example('$0 -i demo.csv user-temp --demo', 'Generate user template with demo data')
    .example('$0 -i data.csv pv-temp', 'Generate PV template')
    .example('$0 -i data.csv pv-mat', 'Generate Matricula template')
    .example('$0 -i data.csv preview', 'Preview first 10 rows of data.csv')
    .strict()
    .parseSync() as Arguments;

  let inputFile = argv.i;
  const command = argv._[0] as string;
  const useDemo = argv.demo === true;

  try {
    // Validate input file
    if (!inputFile) {
      console.error('❌ Error: Input file is required. Use -i <file> to specify the input file.');
      process.exit(1);
    }

    // Generate demo data if --demo flag is set
    if (useDemo) {
      if (command !== 'user-temp') {
        console.error('❌ Error: --demo flag is only supported with user-temp command');
        process.exit(1);
      }
      
      console.log('🎲 Demo mode enabled. Generating demo data...');
      const demoFilePath = path.resolve(inputFile);
      writeDemoDataToFile(demoFilePath);
      console.log(`✅ Demo data written to: ${demoFilePath}`);
      console.log('📊 Demo data includes:');
      console.log('   - 1 SuperAdmin (Carlos Silva)');
      console.log('   - 3 Admins (Ana, Roberto, Patricia)');
      console.log('   - 12 Users (4 per admin team)');
      console.log('   - Realistic email patterns and matricula relationships\n');
    }

    console.log(`📂 Processing file: ${inputFile}`);
    
    // Validate file exists and is CSV
    validateInputFile(inputFile);
    console.log('✅ Input file validated');

    let outputPath: string;

    // Execute command
    switch (command) {
      case 'to-csv':
        console.log('🔄 Converting to CSV...');
        outputPath = await toCsv(inputFile);
        console.log(`✅ CSV file created: ${outputPath}`);
        break;

      case 'user-temp':
        console.log('👥 Generating user template...');
        outputPath = await userTemplate(inputFile);
        console.log(`✅ User template created: ${outputPath}`);
        break;

      case 'pv-temp':
        console.log('🏪 Generating Ponto de Venda template...');
        outputPath = await pvTemplate(inputFile);
        console.log(`✅ PV template created: ${outputPath}`);
        break;

      case 'pv-mat':
        console.log('🎓 Generating Matricula template...');
        outputPath = await pvMatTemplate(inputFile);
        console.log(`✅ Matricula template created: ${outputPath}`);
        break;

      case 'preview':
        await preview(inputFile, 10);
        break;

      default:
        console.error(`❌ Error: Unknown command '${command}'`);
        console.error('Valid commands: to-csv, user-temp, pv-temp, pv-mat, preview');
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
