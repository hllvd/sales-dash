#!/bin/bash
echo "Running integration tests using the .NET 9 SDK Docker image..."

# Determine the docker network name (usually directory_name_default)
NETWORK_NAME=$(docker network ls --format "{{.Name}}" | grep -E "sales-?dash" | head -n 1)

if [ -z "$NETWORK_NAME" ]; then
    echo "Warning: Could not automatically determine docker network name."
    DOCKER_NETWORK_FLAG=""
else
    echo "Using Docker network: $NETWORK_NAME"
    DOCKER_NETWORK_FLAG="--network $NETWORK_NAME"
fi

# We use host.docker.internal to reach the host's localhost (where DynamoDB is likely mapped)
# On some environments, we might need --add-host host.docker.internal:host-gateway
DOCKER_HOST_FLAG="--add-host host.docker.internal:host-gateway"

docker run --rm $DOCKER_NETWORK_FLAG $DOCKER_HOST_FLAG \
    -v "$(pwd)":/app \
    -w /app \
    -e DYNAMODB_URL=http://host.docker.internal:8000 \
    mcr.microsoft.com/dotnet/sdk:9.0 \
    dotnet test SalesApp.IntegrationTests/SalesApp.IntegrationTests.csproj --filter "ScrapeDynamoDbIntegrationTests"
