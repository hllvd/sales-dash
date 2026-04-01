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
# Run all tests headlessly
npm test

# Run tests with UI mode
npm run test:ui
```
