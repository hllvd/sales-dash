# CSV Utility Script

A TypeScript utility for CSV file processing and template generation with timestamp-based output files.

## Features

- ✅ **Argument validation** with clear error messages
- ✅ **CSV and XLSX conversion** and template generation
- ✅ **Automatic column removal** (removes 'Total' and 'Numeric' columns for `to-csv`)
- ✅ **Timestamp-based output** files
- ✅ **Type-safe** TypeScript implementation
- ✅ **Unit tested** with Jest
- ✅ **Clean code** structure with modular design

## Installation

```bash
cd scripts/util
npm install
```

## Usage

The script requires an input file specified with the `-i` flag and a command:

```bash
npm start -- -i <input-file> <command>
```

### Available Commands

#### `to-csv`
Converts input file to CSV format (copies to output with timestamp).

```bash
npm start -- -i input.csv to-csv
```

**Output:** `/output/to-csv-YYYYMMDD-HHMMSS.csv`

---

#### `user-temp`
Generates a user template CSV from input CSV data.
**Note:** Removes duplicates based on name and adds matricula columns.

```bash
npm start -- -i users.csv user-temp
```

**Output:** `/data/output/user-template-YYYYMMDD-HHMMSS.csv`

**Template columns:**
- Name
- Email
- Surname
- Role
- ParentEmail
- Matricula
- Owner_Matricula

---

#### `pv-temp`
Generates a Ponto de Venda (Point of Sale) template CSV.

```bash
npm start -- -i data.csv pv-temp
```

**Output:** `/output/pv-temp-YYYYMMDD-HHMMSS.csv`

**Template columns:**
- Código PV
- Nome
- Endereço
- Cidade
- Estado

---

#### `pv-mat`
Generates a Matricula template CSV.

```bash
npm start -- -i data.csv pv-mat
```

**Output:** `/output/pv-mat-YYYYMMDD-HHMMSS.csv`

**Template columns:**
- Matrícula
- Nome
- Código PV
- Status
- Data Ativação

---

## Error Handling

The script validates all parameters and provides clear error messages:

```bash
# Missing input file
❌ Error: Input file is required. Use -i <file> to specify the input file.

# File not found
❌ Error: Input file not found: /path/to/file.csv

# Invalid file format
❌ Error: Input file must be a CSV file. Got: .txt

# Unknown command
❌ Error: Unknown command 'invalid'
Valid commands: to-csv, user-temp, pv-temp, pv-mat
```

## Development

### Build

Compile TypeScript to JavaScript:

```bash
npm run build
```

Output will be in the `dist/` folder.

### Testing

Run unit tests:

```bash
npm test
```

Run tests in watch mode:

```bash
npm run test:watch
```

Generate coverage report:

```bash
npm run test:coverage
```

### Project Structure

```
scripts/util/
├── src/
│   ├── index.ts              # Main entry point with yargs
│   ├── commands/
│   │   ├── toCsv.ts          # to-csv command handler
│   │   ├── userTemplate.ts   # user-temp command handler
│   │   ├── pvTemplate.ts     # pv-temp command handler
│   │   └── pvMatTemplate.ts  # pv-mat command handler
│   └── utils/
│       ├── fileValidator.ts  # Input file validation
│       └── outputGenerator.ts # Output path and timestamp generation
├── tests/
│   ├── fileValidator.test.ts
│   └── outputGenerator.test.ts
├── package.json
├── tsconfig.json
├── jest.config.js
└── README.md
```

## Output Location

All generated files are saved to `/output` folder at the project root with timestamp-based naming:

```
/output/
├── to-csv-20260122-073000.csv
├── user-template-20260122-073100.csv
├── pv-temp-20260122-073200.csv
└── pv-mat-20260122-073300.csv
```

## Dependencies

- **yargs**: Command-line argument parsing (Google's recommended library)
- **csv-parser**: CSV file parsing
- **csv-writer**: CSV file writing
- **TypeScript**: Type-safe JavaScript
- **Jest**: Testing framework

## License

MIT
