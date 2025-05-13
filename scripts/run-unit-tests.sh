#!/bin/bash
set -e

# Resolve to repo root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."
COVERAGE_DIR="$REPO_ROOT/coverage"
HTML_REPORT_DIR="$COVERAGE_DIR/coverage-report"
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-70}"
RUNSETTINGS_PATH="$REPO_ROOT/tests/UnitTests/coverlet.runsettings"

mkdir -p "$COVERAGE_DIR"

echo "Running unit tests..."
dotnet test tests/UnitTests \
  --no-restore \
  --collect:"XPlat Code Coverage" \
  --results-directory "$COVERAGE_DIR" \
  --settings "$RUNSETTINGS_PATH" \
  --logger trx \
  --verbosity normal

echo "Generating HTML + Summary report..."
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.*
export PATH="$PATH:$HOME/.dotnet/tools"

REPORT_FILE=$(find "$COVERAGE_DIR" -name 'coverage.cobertura.xml' | head -n 1)
if [ -z "$REPORT_FILE" ]; then
  echo "❌ No coverage report found!"
  exit 1
fi

reportgenerator \
  -reports:"$REPORT_FILE" \
  -targetdir:"$HTML_REPORT_DIR" \
  -reporttypes:Html,XmlSummary,Cobertura \
  -assemblyfilters:+Ratatosk*

SUMMARY_FILE="$HTML_REPORT_DIR/Summary.xml"

echo "Checking coverage threshold from $SUMMARY_FILE..."

COVERAGE_PERCENT=$(python3 - <<EOF
import xml.etree.ElementTree as ET
try:
    tree = ET.parse("$SUMMARY_FILE")
    coverage = tree.find(".//CoverageSummary").attrib["linecoverage"]
    print(round(float(coverage)))
except Exception:
    print("")
EOF
)

if ! [[ "$COVERAGE_PERCENT" =~ ^[0-9]+$ ]]; then
  echo "❌ Failed to parse coverage percentage."
  exit 1
fi

echo "📊 Filtered Coverage: ${COVERAGE_PERCENT}% (threshold: ${COVERAGE_THRESHOLD}%)"

if [ "$COVERAGE_PERCENT" -lt "$COVERAGE_THRESHOLD" ]; then
  echo "❌ Coverage below threshold!"
  exit 1
fi

echo "✅ Coverage threshold met."
