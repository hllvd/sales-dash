#!/bin/bash

# Exit on error
set -e

# Configuration
API_DIR="../SalesApp.Api"
CLIENT_DIR="../client/sales-dash"
E2E_DIR="../client/e2e-test"
API_URL="http://localhost:5000"
CLIENT_URL="http://localhost:3000"

echo "🚀 Starting E2E Test Suite..."

# 1. Start Backend in E2E mode
echo "📂 Starting Backend in E2E mode (this will reset the E2E database)..."
cd "$API_DIR"
export ASPNETCORE_ENVIRONMENT=E2E
dotnet run &
API_PID=$!

# 2. Start Frontend
echo "🌐 Starting Frontend..."
cd "$CLIENT_DIR"
npm start &
CLIENT_PID=$!

# Function to cleanup background processes
cleanup() {
    echo "🧹 Cleaning up background processes..."
    kill $API_PID || true
    kill $CLIENT_PID || true
}
trap cleanup EXIT

# 3. Wait for readiness
echo "⏳ Waiting for API to be ready at $API_URL..."
npx wait-on "$API_URL/swagger/v1/swagger.json" --timeout 60000

echo "⏳ Waiting for Frontend to be ready at $CLIENT_URL..."
npx wait-on "$CLIENT_URL" --timeout 60000

# 4. Run E2E Tests
echo "🧪 Running E2E tests..."
cd "$E2E_DIR"
npm test

echo "✅ E2E Tests Completed Successfully!"
