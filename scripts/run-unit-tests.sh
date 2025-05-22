#!/bin/bash
set -e

start_time=$(date +%s.%N)

# Resolve paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."
TEST_PROJECT="$REPO_ROOT/tests/UnitTests"
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-80}"

TEST_RESULTS_DIR="$TEST_PROJECT/TestResults"
HTML_REPORT_DIR="$REPO_ROOT/coverage-report"

mkdir -p "$HTML_REPORT_DIR"

rm -rf "$TEST_RESULTS_DIR"/*
rm -rf "$HTML_REPORT_DIR"/*

echo "Running unit tests..."
dotnet test "$TEST_PROJECT" --collect:"XPlat Code Coverage" --logger "trx"

echo "Locating Cobertura coverage report..."
REPORT_FILE=$(find "$TEST_RESULTS_DIR" -name 'coverage.cobertura.xml' | head -n 1)

if [ -z "$REPORT_FILE" ]; then
  echo "❌ No coverage report found!"
  exit 1
fi

echo "Generating HTML + Summary report..."
dotnet tool install --global dotnet-reportgenerator-globaltool --version 5.*
export PATH="$PATH:$HOME/.dotnet/tools"

reportgenerator \
  -reports:"$REPORT_FILE" \
  -targetdir:"$HTML_REPORT_DIR" \
  -reporttypes:Html,XmlSummary,Cobertura \
  -assemblyfilters:+Ratatosk.Core*,+Ratatosk.Domain*,+Ratatosk.Application* \
  -classfilters:-System.Text.RegularExpressions.Generated.* \
  -filefilters:-*RegexGenerator.g.cs,-*obj/*

SUMMARY_FILE="$HTML_REPORT_DIR/Summary.xml"

echo "Checking coverage threshold from $SUMMARY_FILE..."

COVERAGE_PERCENT=$(python3 - <<EOF
import xml.etree.ElementTree as ET
try:
    tree = ET.parse("$SUMMARY_FILE")
    line_coverage = tree.find(".//Linecoverage")
    print(round(float(line_coverage.text)))
except Exception:
    print("")
EOF
)

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

# --- Timing report ---
end_time=$(date +%s.%N)
elapsed=$(echo "$end_time - $start_time" | bc)
printf "🕒 Script execution time: %.1f seconds\n" "$elapsed"
