#!/usr/bin/env bash

set -e

# Resolve symbolic links to find the actual script directory
TARGET="${BASH_SOURCE[0]}"
while [ -h "$TARGET" ]; do
  DIR="$( cd -P "$( dirname "$TARGET" )" && pwd )"
  TARGET="$(readlink "$TARGET")"
  [[ $TARGET != /* ]] && TARGET="$DIR/$TARGET"
done
SCRIPT_DIR="$( cd -P "$( dirname "$TARGET" )" && pwd )"

# Ensure the script is run from the project root directory
cd "$SCRIPT_DIR/.."

# Check if macOS is sandboxing the unix docker socket (Operation not permitted).
# If so, automatically fall back to the TCP socket (localhost:2375).
# To enable TCP: Docker Desktop → Settings → General →
#   ☑ "Expose daemon on tcp://localhost:2375 without TLS"
if ls /var/run/docker.sock 2>&1 | grep -q "Operation not permitted" || \
   ls ~/.docker/config.json 2>&1 | grep -q "Operation not permitted"; then

  # Try TCP fallback first — no TCC restriction applies to localhost TCP
  if curl -s --max-time 2 http://localhost:2375/info > /dev/null 2>&1; then
    export DOCKER_HOST="tcp://localhost:2375"
    echo "ℹ️  Sandbox detected — using Docker TCP socket (localhost:2375)"
  else
    echo ""
    echo "⚠️  SANDBOX LIMITATION DETECTED ⚠️"
    echo "========================================================================="
    echo "macOS is blocking access to the Docker socket and no TCP fallback found."
    echo ""
    echo "Option A (permanent fix, one checkbox):"
    echo "  Docker Desktop → Settings → General →"
    echo "  ☑ Expose daemon on tcp://localhost:2375 without TLS"
    echo "  Then re-run: ./test.sh $1"
    echo ""
    echo "Option B (run directly in your host terminal):"
    echo "  cd $(pwd) && ./test.sh $1"
    echo "========================================================================="
    echo ""
    exit 1
  fi
fi

mkdir -p artifacts

FILTER='[Ff]ail(ed)?|[Ee]xception|[Pp]anic|[Ff]atal|[Ee]rror:?|Assert\.|FAILED|✗'
# Lines to always exclude even if they match FILTER (e.g. Playwright progress lines like [N/M] test name)
# Both alternatives are anchored to ^ so real errors containing these strings mid-line are NOT suppressed.
EXCLUDE='^\[[0-9]+/[0-9]+\]|^\[tear-'

# Optional user-maintained exclude file: scripts/exclude.txt
# Each non-empty, non-comment line is treated as an ERE pattern to suppress.
EXCLUDE_FILE="$SCRIPT_DIR/exclude.txt"

print_header() {
  echo ""
  echo "=================================="
  echo "$1"
  echo "=================================="
}

summarize_log() {
  local LOG_FILE=$1
  local ERROR_FILE=$2

  local CLEANED
  CLEANED=$(sed 's/\x1b\[[0-9;]*[A-Za-z]//g' "$LOG_FILE" \
    | grep -Ei "$FILTER" \
    | grep -Ev "$EXCLUDE" || true)

  # Apply user-defined exclusions from exclude.txt if it exists
  if [ -f "$EXCLUDE_FILE" ]; then
    local USER_EXCLUDE
    USER_EXCLUDE=$(grep -Ev '^\s*(#|$)' "$EXCLUDE_FILE" | paste -sd '|' -)
    if [ -n "$USER_EXCLUDE" ]; then
      CLEANED=$(echo "$CLEANED" | grep -Ev "$USER_EXCLUDE" || true)
    fi
  fi

  printf '%s' "$CLEANED" > "$ERROR_FILE"

  local ERROR_COUNT
  ERROR_COUNT=$(grep -c . "$ERROR_FILE" || true)

  echo ""
  echo "Summary:"
  echo "- Full log: $LOG_FILE"
  echo "- Error log: $ERROR_FILE"
  echo "- Error lines found: $ERROR_COUNT"
  echo ""

  if [ "$ERROR_COUNT" -eq 0 ]; then
    echo "✅ No relevant errors found"
  else
    echo "Top errors:"
    echo "----------------------------------"

    head -n 30 "$ERROR_FILE"

    echo "----------------------------------"

    if [ "$ERROR_COUNT" -gt 30 ]; then
      echo "... truncated ($ERROR_COUNT total error lines)"
    fi
  fi

  echo ""
}

build() {
  print_header "BUILD"

  local EXIT_CODE=0
  docker-compose up --build -d \
    > artifacts/full-build.log 2>&1 || EXIT_CODE=$?

  summarize_log \
    artifacts/full-build.log \
    artifacts/build-errors.log

  return $EXIT_CODE
}

integration() {
  print_header "INTEGRATION TESTS"

  local EXIT_CODE=0
  docker run --rm \
    -v "$(pwd):/src" \
    -w /src \
    mcr.microsoft.com/dotnet/sdk:9.0 \
    dotnet test SalesApp.IntegrationTests/SalesApp.IntegrationTests.csproj \
    > artifacts/full-integration.log 2>&1 || EXIT_CODE=$?

  summarize_log \
    artifacts/full-integration.log \
    artifacts/integration-errors.log

  return $EXIT_CODE
}

e2e() {
  print_header "PLAYWRIGHT E2E"

  local EXIT_CODE=0
  (cd client/e2e-test && npx playwright test) \
    > artifacts/full-e2e.log 2>&1 || EXIT_CODE=$?

  summarize_log \
    artifacts/full-e2e.log \
    artifacts/e2e-errors.log

  return $EXIT_CODE
}

all() {
  build
  integration
  e2e
}

logs() {
  cat artifacts/*.log
}

clean() {
  rm -rf artifacts
  echo "✅ Artifacts cleaned"
}

case "$1" in
  build)
    build
    ;;
  integration)
    integration
    ;;
  e2e)
    e2e
    ;;
  all)
    all
    ;;
  logs)
    logs
    ;;
  clean)
    clean
    ;;
  *)
    echo "Usage:"
    echo "./test.sh build"
    echo "./test.sh integration"
    echo "./test.sh e2e"
    echo "./test.sh all"
    echo "./test.sh logs"
    echo "./test.sh clean"
    exit 1
    ;;
esac