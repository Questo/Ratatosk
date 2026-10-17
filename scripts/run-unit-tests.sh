#!/bin/bash
set -e

start_time=$(date +%s.%N)

# Resolve paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."
TEST_PROJECT="$REPO_ROOT/tests/UnitTests"

TEST_RESULTS_DIR="$TEST_PROJECT/TestResults"
HTML_REPORT_DIR="$REPO_ROOT/coverage-report"

mkdir -p "$HTML_REPORT_DIR"

rm -rf "$TEST_RESULTS_DIR"/*
rm -rf "$HTML_REPORT_DIR"/*

echo "Running unit tests..."
dotnet test "$TEST_PROJECT" --collect:"XPlat Code Coverage" --logger "trx"

"$SCRIPT_DIR/coverage-report.sh" "$TEST_RESULTS_DIR" "$HTML_REPORT_DIR"

# --- Timing report ---
end_time=$(date +%s.%N)
elapsed=$(echo "$end_time - $start_time" | bc)
printf "🕒 Script execution time: %.1f seconds\n" "$elapsed"
