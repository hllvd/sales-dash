#!/bin/bash

# Function to run and report
run_step() {
    echo "Running: $1"
    if ! eval $1; then
        echo "❌ Error detected in $1. Sending to Antigravity..."
        # Assuming antigravity has a command to take the last output or a log file
        antigravity analyze --cmd "$1" 
        exit 1
    fi
}

run_step "dotnet test"
run_step "docker run --rm -v \$(pwd):/app -w /app -e NUGET_PACKAGES=/app/.nuget/packages mcr.microsoft.com/dotnet/sdk:9.0 dotnet test"
run_step "docker-compose up --build -d"
run_step "cd client/e2e && npx playwright test"