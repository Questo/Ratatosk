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

# The test database is one fixed container on port 5433, shared by every worktree of this repo,
# so runs take turns: the lock lives in the common git dir, which all worktrees share.
LOCK_FILE="$(git -C "$REPO_ROOT" rev-parse --path-format=absolute --git-common-dir)/integration-tests.lock"
exec 9>"$LOCK_FILE"
if ! flock -n 9; then
  echo "Another integration test run holds $LOCK_FILE; waiting for it..."
  flock 9
fi

# One project name for every worktree, so a run can tear down a container another worktree left.
COMPOSE=(docker compose -p ratatosk-test -f "$REPO_ROOT/docker-compose.test.yml")

mkdir -p "$HTML_REPORT_DIR"

rm -rf "$TEST_RESULTS_DIR"/*
rm -rf "$HTML_REPORT_DIR"/*

echo "Starting containers..."
trap '"${COMPOSE[@]}" down' EXIT
"${COMPOSE[@]}" --profile local up ratatosk-testdb -d --remove-orphans

echo "Waiting for Postgres to be ready..."
RETRIES=30
until "${COMPOSE[@]}" exec -T ratatosk-testdb pg_isready -U "$TEST_POSTGRES_USER" > /dev/null 2>&1; do
  RETRIES=$((RETRIES - 1))
  if [ "$RETRIES" -eq 0 ]; then
    echo "Postgres did not become ready in time."
    exit 1
  fi
  sleep 1
done
echo "Postgres is ready."

echo "Running integration tests..."
dotnet test "$TEST_PROJECT" \
  --configuration Release \
  --no-restore \
  --verbosity normal
