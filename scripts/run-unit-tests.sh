#!/bin/bash
set -e

# Resolve to repo root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."
COVERAGE_DIR="$REPO_ROOT/coverage"
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-70}"

mkdir -p "$COVERAGE_DIR"

echo "Running unit tests..."
dotnet test tests/UnitTests \
  --no-restore \
  --collect:"XPlat Code Coverage" \
  --results-directory "$COVERAGE_DIR" \
  --logger trx \
  --verbosity normal

echo "Checking coverage threshold..."

REPORT_FILE=$(find "$COVERAGE_DIR" -name 'coverage.cobertura.xml' | head -n 1)
if [ -z "$REPORT_FILE" ]; then
  echo "❌ No coverage report found!"
  exit 1
fi

COVERAGE_PERCENT=$(python3 - <<EOF
import xml.etree.ElementTree as ET
try:
    tree = ET.parse("$REPORT_FILE")
    root = tree.getroot()
    rate = float(root.attrib.get("line-rate", 0))
    print(round(rate * 100))
except Exception as e:
    print("")
EOF
)

# Ensure the result is an integer
if ! [[ "$COVERAGE_PERCENT" =~ ^[0-9]+$ ]]; then
  echo "❌ Failed to parse coverage percentage."
  exit 1
fi

echo "📊 Coverage: ${COVERAGE_PERCENT}% (threshold: ${COVERAGE_THRESHOLD}%)"

if [ "$COVERAGE_PERCENT" -lt "$COVERAGE_THRESHOLD" ]; then
  echo "❌ Coverage below threshold!"
  exit 1
fi

echo "✅ Coverage threshold met."
