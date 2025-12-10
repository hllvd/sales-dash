# Generate Users Email Script

A Node.js script that processes a CSV file containing user data and generates email addresses in the format `{firstname}.{lastname}@test.com`.

## Features

- ✅ Reads CSV files with `Matricula` and `Nome` columns
- ✅ Accent-insensitive column detection (accepts "Matrícula", "Nome", "Comissionado", etc.)
- ✅ Generates emails by splitting full names into first and last names
- ✅ Normalizes names (removes accents, converts to lowercase)
- ✅ Handles edge cases (single names, special characters)
- ✅ Provides detailed console feedback
- ✅ Creates output CSV with email column added

## Requirements

- Node.js (v12 or higher)
- npm

## Installation

```bash
cd scripts/generate-users
npm install
```

## Usage

1. Place your `users.csv` file in the `scripts/generate-users` directory
2. Run the script:

```bash
npm start
```

3. The script will create `users-with-emails.csv` with the email column added

## Input File Format

Your `users.csv` should have at least these columns:
- `Matricula` (or `Matrícula`) - User ID/registration number
- `Nome` (or `Name`, `Comissionado`) - Full name

Example:
```csv
Matricula,Nome
12345,João da Silva
67890,Maria Santos
```

## Output File Format

The script creates `users-with-emails.csv` with an additional `Email` column:

```csv
Matricula,Nome,Email
12345,João da Silva,joao.silva@test.com
67890,Maria Santos,maria.santos@test.com
```

## Email Generation Logic

The script:
1. Splits the full name by spaces
2. Takes the first word as the first name
3. Takes the last word as the surname
4. Removes accents and special characters
5. Converts to lowercase
6. Formats as `{firstname}.{surname}@test.com`

### Examples:

| Full Name | Generated Email |
|-----------|----------------|
| João da Silva | joao.silva@test.com |
| María García | maria.garcia@test.com |
| Pedro | pedro@test.com |
| Ana Paula Costa | ana.costa@test.com |

## Error Handling

The script will:
- Check if the input file exists
- Validate that required columns are present
- Skip rows with missing data (with warnings)
- Provide clear error messages if something goes wrong

## Notes

- The script uses accent-insensitive column matching, so it works with both "Matricula" and "Matrícula"
- Column names are case-insensitive
- Empty or invalid rows are skipped with warnings
- The output file will overwrite any existing `users-with-emails.csv`
