#!/bin/bash
# Gives each factory job its own database in the shared ratatosk-db container, so parallel
# jobs' dev servers never see each other's data.
#
#   scripts/factory-db.sh create <job-id>   create factory_<n> and its schema (does nothing if it exists)
#   scripts/factory-db.sh url <job-id>      print its connection string
#   scripts/factory-db.sh drop <job-id>     drop it
#
# <n> is the job's number (003-rate-limiting → factory_003).
set -euo pipefail

COMMAND="${1:-}"
JOB_ID="${2:-}"
case "$COMMAND" in create|url|drop) ;; *) JOB_ID= ;; esac
if [[ ! "$JOB_ID" =~ ^([0-9]{3})- ]]; then
  echo "usage: $0 create|url|drop <job-id>" >&2
  exit 2
fi
DB="factory_${BASH_REMATCH[1]}"
CONTAINER=ratatosk-db

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# The main checkout holds .env, which docker compose needs to start the container.
MAIN_CHECKOUT="$(cd "$(git -C "$SCRIPT_DIR" rev-parse --path-format=absolute --git-common-dir)/.." && pwd)"

if [ -z "$(docker ps -q -f "name=^${CONTAINER}$")" ]; then
  docker compose --project-directory "$MAIN_CHECKOUT" -f "$MAIN_CHECKOUT/docker-compose.yml" up -d db >&2
fi
RETRIES=30
until docker exec "$CONTAINER" pg_isready -q; do
  RETRIES=$((RETRIES - 1))
  if [ "$RETRIES" -eq 0 ]; then
    echo "Postgres did not become ready in time." >&2
    exit 1
  fi
  sleep 1
done

USER="$(docker exec "$CONTAINER" printenv POSTGRES_USER)"
psql() { docker exec -i "$CONTAINER" psql -v ON_ERROR_STOP=1 -q -U "$USER" "$@"; }

case "$COMMAND" in
  create)
    if [ -z "$(psql -d postgres -Atc "SELECT 1 FROM pg_database WHERE datname = '$DB'")" ]; then
      psql -d postgres -c "CREATE DATABASE $DB"
      # The schema comes from the init scripts of the checkout this script runs from (the job's worktree).
      for f in "$SCRIPT_DIR"/../postgres/init/*.sql; do
        psql -d "$DB" < "$f"
      done
    fi
    ;;
  url)
    PASSWORD="$(docker exec "$CONTAINER" printenv POSTGRES_PASSWORD)"
    echo "Host=localhost;Port=5432;Database=$DB;Username=$USER;Password=$PASSWORD"
    ;;
  drop)
    psql -d postgres -c "DROP DATABASE IF EXISTS $DB WITH (FORCE)"
    ;;
esac
