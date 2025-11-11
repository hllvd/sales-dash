#!/bin/bash

# Kill any existing processes on common ports
for port in 5016 5017 5018 5019 5020; do
    lsof -ti:$port | xargs kill -9 2>/dev/null || true
done

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
    echo "Usage: $0 {start|fast-start}"
    echo "  start      - Run tests first, then start API"
    echo "  fast-start - Start API immediately without tests"
    exit 1
fi

# Start the application
dotnet run