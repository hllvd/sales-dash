#!/bin/bash

# Exit on error
set -e

# Port Configuration
export ASPNETCORE_URLS=http://localhost:5017

# Configuration
API_DIR="../SalesApp.Api"
CLIENT_DIR="../client/sales-dash"
E2E_DIR="../client/e2e-test"
API_URL="http://localhost:5017"
CLIENT_URL="http://localhost:3000"

echo "🚀 Starting E2E Test Suite..."

# 1. Start Backend in E2E mode
echo "📂 Starting Backend in E2E mode (this will reset the E2E database)..."
# Aggressively kill any process holding a lock on the E2E DB files
DB_PIDS=$(lsof -t SalesApp.E2E.db SalesApp.E2E.db-shm SalesApp.E2E.db-wal 2>/dev/null)
if [ ! -z "$DB_PIDS" ]; then
    echo "🔒 Removing E2E DB locks (Processes: $DB_PIDS)..."
    kill -9 $DB_PIDS 2>/dev/null || true
fi
# Explicitly remove the E2E database file to ensure a clean start
rm -f SalesApp.E2E.db SalesApp.E2E.db-shm SalesApp.E2E.db-wal
sleep 1
echo "🚀 Starting Backend with --environment E2E..."
dotnet run --environment E2E --no-launch-profile &
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
