#!/bin/bash
set -e

# Set environment variables (adjust if needed)
export TEST_POSTGRES_DB=ratatosk_test
export TEST_POSTGRES_USER=postgres
export TEST_POSTGRES_PASSWORD=postgres

# Resolve paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."
TEST_PROJECT="$REPO_ROOT/tests/IntegrationTests"
COVERAGE_THRESHOLD="${COVERAGE_THRESHOLD:-75}"

TEST_RESULTS_DIR="$TEST_PROJECT/TestResults"
HTML_REPORT_DIR="$REPO_ROOT/coverage-report/integration"

mkdir -p "$HTML_REPORT_DIR"

rm -rf "$TEST_RESULTS_DIR"/*
rm -rf "$HTML_REPORT_DIR"/*

echo "Starting containers..."
docker compose --profile local -f docker-compose.test.yml up ratatosk-testdb -d --remove-orphans

echo "Running integration tests..."
dotnet test tests/IntegrationTests \
  --configuration Release \
  --no-restore \
  --verbosity normal \
#  --collect:"XPlat Code Coverage" \
  #--logger "trx"

echo "Tearing down containers..."
docker compose -f docker-compose.test.yml down
