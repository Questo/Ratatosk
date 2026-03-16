#!/bin/bash
set -e

# Set environment variables (adjust if needed)
export TEST_POSTGRES_DB=ratatosk_test
export TEST_POSTGRES_USER=testuser
export TEST_POSTGRES_PASSWORD=testpass

# Resolve paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$SCRIPT_DIR/.."
TEST_PROJECT="$REPO_ROOT/tests/IntegrationTests"

TEST_RESULTS_DIR="$TEST_PROJECT/TestResults"
HTML_REPORT_DIR="$REPO_ROOT/coverage-report/integration"

mkdir -p "$HTML_REPORT_DIR"

rm -rf "$TEST_RESULTS_DIR"/*
rm -rf "$HTML_REPORT_DIR"/*

echo "Starting containers..."
docker compose --profile local -f docker-compose.test.yml up ratatosk-testdb -d --remove-orphans

echo "Waiting for Postgres to be ready..."
RETRIES=30
until docker compose -f docker-compose.test.yml exec -T ratatosk-testdb pg_isready -U "$TEST_POSTGRES_USER" > /dev/null 2>&1; do
  RETRIES=$((RETRIES - 1))
  if [ "$RETRIES" -eq 0 ]; then
    echo "Postgres did not become ready in time."
    docker compose -f docker-compose.test.yml down
    exit 1
  fi
  sleep 1
done
echo "Postgres is ready."

echo "Running integration tests..."
dotnet test tests/IntegrationTests \
  --configuration Release \
  --no-restore \
  --verbosity normal

echo "Tearing down containers..."
docker compose -f docker-compose.test.yml down
