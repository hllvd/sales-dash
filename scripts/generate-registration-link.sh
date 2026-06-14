#!/usr/bin/env bash

# Exit immediately if a command exits with a non-zero status
set -e

# Detect Operating System
OS="$(uname -s)"

show_usage() {
  echo "Usage: $0 [duration] [base_url]"
  echo ""
  echo "Arguments:"
  echo "  duration  Validity duration (e.g. 24h, 2h, 7d, 30m, -1h). Default: 24h"
  echo "  base_url  The base URL of the frontend application. Default: https://ademicon.hagadev.com"
  echo ""
  echo "Examples:"
  echo "  $0 2h"
  echo "  $0 7d http://localhost"
  echo "  $0 -1h (generates an expired link for testing)"
  exit 1
}

# Print help
if [ "$1" = "--help" ] || [ "$1" = "-h" ]; then
  show_usage
fi

DURATION="${1:-24h}"
BASE_URL="${2:-https://ademicon.hagadev.com}"

# Strip trailing slash from BASE_URL if present
BASE_URL="${BASE_URL%/}"

# Parse number and unit (e.g., 24h -> VALUE=24, UNIT=h; -2h -> VALUE=-2, UNIT=h)
VALUE="${DURATION%[a-zA-Z]*}"
UNIT="${DURATION##*[0-9]}"
UNIT="$(echo "$UNIT" | tr '[:upper:]' '[:lower:]')"

# Compute expiration timestamp in YYYY-MM-DD:HH:MM format
if [ "$OS" = "Darwin" ]; then
  # BSD date syntax (macOS)
  case "$UNIT" in
    d) DateAdjust="-v+${VALUE}d" ;;
    h) DateAdjust="-v+${VALUE}H" ;;
    m) DateAdjust="-v+${VALUE}M" ;;
    *) echo "Error: Invalid unit '$UNIT'. Use 'd', 'h', or 'm'." >&2; show_usage ;;
  esac
  TARGET_DATE="$(date "$DateAdjust" "+%Y-%m-%d:%H:%M")"
else
  # GNU date syntax (Linux/Windows Git Bash)
  case "$UNIT" in
    d) DateAdjust="+${VALUE} days" ;;
    h) DateAdjust="+${VALUE} hours" ;;
    m) DateAdjust="+${VALUE} minutes" ;;
    *) echo "Error: Invalid unit '$UNIT'. Use 'd', 'h', or 'm'." >&2; show_usage ;;
  esac
  TARGET_DATE="$(date -d "$DateAdjust" "+%Y-%m-%d:%H:%M")"
fi

# Convert TARGET_DATE to hyphenless decimal ASCII representation
# Each character is converted to its 2-digit decimal ASCII code
TOKEN=""
for (( i=0; i<${#TARGET_DATE}; i++ )); do
  char="${TARGET_DATE:$i:1}"
  # printf "%d" "'$char" converts the char to its ASCII code
  code=$(printf "%d" "'$char")
  TOKEN="${TOKEN}${code}"
done

# Output the generated registration URL
echo ""
echo "Registration Link Generated Successfully:"
echo "------------------------------------------------------------------"
echo "${BASE_URL}/#/user/registration/admin?d=${TOKEN}"
echo "------------------------------------------------------------------"
echo "Expires at: $TARGET_DATE"
echo ""
