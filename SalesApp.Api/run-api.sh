#!/bin/bash

# Port Configuration
export ASPNETCORE_URLS=http://localhost:5017

# Kill any existing processes on common ports
for port in 5016 5017 5018 5019 5020; do
    lsof -ti:$port | xargs kill -9 2>/dev/null || true
done

# Check for E2E parameter as second argument
if [ "$2" = "e2e" ]; then
    echo "🌍 Environment set to E2E (Database will be reset)"
    export ASPNETCORE_ENVIRONMENT=E2E
    E2E_MODE=true
    # Aggressively kill any process holding a lock on the E2E DB files
    DB_PIDS=$(lsof -t SalesApp.E2E.db SalesApp.E2E.db-shm SalesApp.E2E.db-wal 2>/dev/null)
    if [ ! -z "$DB_PIDS" ]; then
        echo "🔒 Removing E2E DB locks (Processes: $DB_PIDS)..."
        kill -9 $DB_PIDS 2>/dev/null || true
    fi
    # Explicitly remove the E2E database file if it exists
    rm -f SalesApp.E2E.db SalesApp.E2E.db-shm SalesApp.E2E.db-wal
    sleep 1
fi

# Check argument
if [ "$1" = "start" ]; then
    echo "Running tests before starting API..."
    cd ../SalesApp.Tests
    dotnet test
    if [ $? -ne 0 ]; then
        echo "Tests failed. Aborting API start."
        exit 1
    fi
    cd ../SalesApp.Api
    echo "Tests passed. Starting API..."
elif [ "$1" = "fast-start" ]; then
    echo "Starting API without tests..."
else
    echo "Usage: $0 {start|fast-start} [e2e]"
    echo "  start      - Run tests first, then start API"
    echo "  fast-start - Start API immediately without tests"
    echo "  e2e        - (Optional) Run in E2E mode with database reset"
    exit 1
fi

# Start the application
if [ "$E2E_MODE" = true ]; then
    dotnet run --environment E2E --no-launch-profile
else
    dotnet run
fi