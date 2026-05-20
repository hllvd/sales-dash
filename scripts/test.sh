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

# Check if macOS is sandboxing this execution context (Operation not permitted)
if ls /var/run/docker.sock 2>&1 | grep -q "Operation not permitted" || \
   ls ~/.docker/config.json 2>&1 | grep -q "Operation not permitted"; then
  echo ""
  echo "⚠️  SANDBOX LIMITATION DETECTED ⚠️"
  echo "========================================================================="
  echo "macOS is blocking this agent/IDE from accessing your Docker socket."
  echo "This is because the app running Antigravity is sandboxed by macOS."
  echo ""
  echo "To run these tests, please run this command directly in your macOS"
  echo "Terminal or iTerm app (where Docker has permission):"
  echo ""
  echo "  cd $(pwd) && ./test.sh $1"
  echo "========================================================================="
  echo ""
  exit 1
fi

mkdir -p artifacts

FILTER='[Ff]ail(ed)?|[Ee]xception|[Pp]anic|[Ff]atal|[Ee]rror:?|Assert\.|FAILED|✗|\[ERR\]|\[WRN\]|\[CRIT\]|Connection refused|\b50[023]\b'
# Lines to always exclude even if they match FILTER (e.g. Playwright progress lines like [N/M] test name)
# Both alternatives are anchored to ^ so real errors containing these strings mid-line are NOT suppressed.
EXCLUDE='^\[[0-9]+/[0-9]+\]|^\[tear-|npm [wW]arn|domexception'

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

  if [ "$EXIT_CODE" -eq 0 ]; then
    # Start a background process to stream and filter logs for errors/warnings during startup
    echo "Streaming logs for errors/warnings during startup..."
    docker-compose logs --tail=0 -f \
      | grep -Ei --line-buffered "error|fail|fatal|panic|exception|critical|502|503|connection refused|\[ERR\]|\[CRIT\]|DbCommand" &
    local LOG_PID=$!

    # Wait for a brief start period (15 seconds) to catch early crashes, warnings, or database migration logs
    sleep 15

    # Terminate the background log follower
    kill $LOG_PID >/dev/null 2>&1 || true
    wait $LOG_PID >/dev/null 2>&1 || true

    # Scan the full container logs for critical errors
    docker-compose logs > artifacts/startup-docker.log 2>&1
    local CRITICAL_ERRORS
    CRITICAL_ERRORS=$(grep -Ei "\[error\]|\[ERR\]|\[CRIT\]|Exception|Failed executing DbCommand|502 Bad Gateway|503 Service Unavailable|Connection refused|panic|fatal" artifacts/startup-docker.log || true)

    if [ -n "$CRITICAL_ERRORS" ]; then
      echo ""
      echo "❌ CRITICAL CONTAINER ERROR DETECTED DURING STARTUP!"
      echo "========================================================================="
      printf "%s\n" "$CRITICAL_ERRORS" | head -n 30
      echo "========================================================================="
      echo ""
      EXIT_CODE=1
    fi
  fi

  summarize_log \
    artifacts/full-build.log \
    artifacts/build-errors.log

  if [ "$EXIT_CODE" -ne 0 ] && [ -n "$CRITICAL_ERRORS" ]; then
    # Append the critical errors to build-errors.log to ensure the runner reports it
    printf "%s\n" "$CRITICAL_ERRORS" >> artifacts/build-errors.log
  fi

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