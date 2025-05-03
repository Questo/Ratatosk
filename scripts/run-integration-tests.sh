#!/bin/bash
set -e

# Set environment variables (adjust if needed)
export TEST_POSTGRES_DB=ratatosk_test
export TEST_POSTGRES_USER=postgres
export TEST_POSTGRES_PASSWORD=postgres

echo "Starting containers..."
docker compose --profile local -f docker-compose.test.yml up -d --remove-orphans

echo "Waiting for API to become healthy..."
for i in {1..10}; do
  STATUS=$(docker inspect -f '{{.State.Health.Status}}' ratatosk-api-local || echo "notfound")
  if [ "$STATUS" == "healthy" ]; then
    echo "✅ API is healthy!"
    break
  fi
  echo "⏳ Waiting... ($i/10)"
  sleep 3
done

if [ "$STATUS" != "healthy" ]; then
  echo "❌ API did not become healthy in time."
  docker compose -f docker-compose.test.yml logs
  docker compose -f docker-compose.test.yml down
  exit 1
fi

echo "Running integration tests..."
dotnet test tests/IntegrationTests \
  --configuration Release \
  --no-restore \
  --verbosity normal

echo "Tearing down containers..."
docker compose -f docker-compose.test.yml down
