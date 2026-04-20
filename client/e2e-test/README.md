# SalesApp E2E Tests

End-to-end tests for the SalesApp using Playwright.

## Prerequisites

- [Node.js](https://nodejs.org/) (v18 or higher)
- Clean `node_modules` (run `npm install`)

## Installation

To install dependencies and required browser binaries (Chromium):

```bash
# From the project root
cd client/e2e-test

# Install node dependencies
npm install

# Install Playwright browsers (Fixes "Executable doesn't exist" error)
npx playwright install chromium
```

## Running Tests

```bash
# Run all tests (executes TEAR 1 -> TEAR 2 -> TEAR 3)
npm test

# Run tests with UI mode
npm run test:ui
```

## Test Structure (TEARS)

To handle application state and dependencies, tests are organized into sequential tiers (TEARS):

1. **TEAR 1 (Setup & Import)**: Handles the initial data import from CSV/Excel.
2. **TEAR 2 (Account Setup)**: Configures user roles and roles-based access (e.g., promoting Carlos Mendes to Admin).
3. **TEAR 3 (General Logic)**: Verifies functional logic like filtering, basic login, and smoke tests.

Each Tier verifies if its state is already achieved (e.g., checking if data exists) and will skip redundant heavy work if possible.
44: 
45: ## Temporary Files
46: 
47: Any files generated during test execution (e.g., downloads, enriched exports) should be stored in the `./temp/` directory. 
48: 
49: - This directory is ignored by Git (`.gitignore`).
50: - Tests should ideally clean up their own files in an `afterAll` hook to keep the environment clean.
51: - Do **not** use the `test-data/` folder for generated outputs to avoid polluting the repository.

