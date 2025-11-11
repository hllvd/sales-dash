#!/bin/bash

# Kill any existing processes on common ports
for port in 5016 5017 5018 5019 5020; do
    lsof -ti:$port | xargs kill -9 2>/dev/null || true
done

# Start the application
dotnet run