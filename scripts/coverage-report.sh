#!/bin/bash
# Builds the HTML + Summary.xml coverage report from a test results directory and
# fails if line coverage is below COVERAGE_THRESHOLD. Shared by run-unit-tests.sh and CI.
#
# Usage: coverage-report.sh <test-results-dir> <report-dir>
set -e

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <test-results-dir> <report-dir>"
  exit 1
fi

TEST_RESULTS_DIR="$1"
REPORT_DIR="$2"
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-75}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."

echo "Locating Cobertura coverage report..."
REPORT_FILE=$(find "$TEST_RESULTS_DIR" -name 'coverage.cobertura.xml' | head -n 1)

if [ -z "$REPORT_FILE" ]; then
  echo "❌ No coverage report found!"
  exit 1
fi

echo "Generating HTML + Summary report..."
(cd "$REPO_ROOT" && dotnet tool restore > /dev/null)

dotnet tool run reportgenerator \
  -reports:"$REPORT_FILE" \
  -targetdir:"$REPORT_DIR" \
  -reporttypes:Html,XmlSummary,Cobertura \
  -assemblyfilters:+Ratatosk.Core*,+Ratatosk.Domain*,+Ratatosk.Application* \
  -classfilters:-System.Text.RegularExpressions.Generated.* \
  -filefilters:-*RegexGenerator.g.cs,-*obj/*

SUMMARY_FILE="$REPORT_DIR/Summary.xml"

echo "Checking coverage threshold from $SUMMARY_FILE..."

# Compare the unrounded percentage so 74.6% does not pass a 75% threshold
python3 - "$SUMMARY_FILE" "$COVERAGE_THRESHOLD" <<'EOF'
import sys
import xml.etree.ElementTree as ET

summary_file, threshold = sys.argv[1], float(sys.argv[2])
try:
    percent = float(ET.parse(summary_file).find(".//Linecoverage").text)
except Exception as e:
    print("❌ Failed to parse coverage percentage:", e)
    sys.exit(1)

print(f"📊 Coverage: {percent:.1f}% (threshold: {threshold:g}%)")
if percent < threshold:
    print("❌ Coverage below threshold!")
    sys.exit(1)
print("✅ Coverage threshold met.")
EOF
