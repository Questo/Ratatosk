# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Run the API locally with Docker (PostgreSQL + API)
docker-compose up

# Build only
dotnet build

# Run the API without Docker (requires local PostgreSQL or InMemory event store)
dotnet run --project src/API
```

## Testing

```bash
# Unit tests with coverage report (enforces 75% threshold)
./scripts/run-unit-tests.sh

# Integration tests (spins up Docker Postgres on port 5433)
./scripts/run-integration-tests.sh

# Run a single test class
dotnet test tests/UnitTests --filter "ClassName=ProductTests"

# Run a single test method
dotnet test tests/UnitTests --filter "FullyQualifiedName~ProductTests.Create_ShouldRaiseProductCreatedEvent"
```

Coverage is reported for `Core`, `Domain`, and `Application` assemblies only (not Infrastructure or API).

## Architecture

Ratatosk is a DDD + Event Sourcing + CQRS reference implementation. Dependency direction is strict:

```
API → Application → Domain → Core
              ↑
       Infrastructure
```

**Core** — Framework-agnostic building blocks: `AggregateRoot`, `DomainEvent`, `Result<T>`, `Guard`, `Dispatcher`, and all abstractions (`IEventStore`, `ISnapshotStore`, `IAggregateRepository`, etc.).

**Domain** — Pure domain models with no infrastructure dependencies. Bounded contexts: `Catalog`, `Identity`, `Inventoring`, `Ordering`. Aggregates raise events via `RaiseEvent()` which appends to the uncommitted events list. Each aggregate can produce a `Snapshot` for performance.

**Application** — Command/query handlers (dispatched via `Dispatcher`), service interfaces, read model types, and repository interfaces. Commands return `Result<T>`. Uses `IUnitOfWork` to coordinate persistence.

**Infrastructure** — Concrete implementations: three event store backends (`InMemory`, `File`, `PostgresEventStore`), `AggregateRepository<T>` (rehydrates aggregates from events + snapshots), `ProjectionRegistrationService` (hosted service that registers event handlers to update read models), `Argon2PasswordHasher`, `JwtTokenIssuer`.

**API** — Minimal API endpoints organized by feature (`/auth`, `/products`). `CleanupResponseMiddleware` wraps all responses in a `Result` envelope.

## Event Sourcing Flow

Write path: Command → Handler → Load aggregate (`AggregateRepository.GetById`) → Call domain method → `RaiseEvent()` → Save (`UnitOfWork.Commit`) → Events appended to store → `EventBus` publishes to projections.

Read path: Query → Handler → Repository hits denormalized read model table directly (no event replay).

Snapshots: When an aggregate's version is a multiple of the configured threshold, a snapshot is saved. On load, the repository fetches the latest snapshot first, then replays only events after that version.

## Configuration

Event store backend is controlled by `appsettings.json`:
```json
"EventStore": { "Type": "InMemory" }  // InMemory | File | Sql
```

For `Sql`, set `DatabaseOptions:ConnectionString`. Auth secrets are in `AuthOptions` (JWT issuer, audience, secret).

## Key Patterns

- **Result<T>**: Used for all fallible operations. Propagate with `.Map()`, `.Bind()`, `.Match()`. Don't throw exceptions for domain errors.
- **Value Objects**: Extend `ValueObject`, override `GetEqualityComponents()`. All domain primitives (Price, Email, SKU, etc.) are value objects.
- **Adding a new bounded context**: Create domain aggregate in `Domain/`, add commands/queries in `Application/`, implement projections in `Infrastructure/Projections/`, register endpoints in `API/`.
- **Serialization**: Events and snapshots use `JsonPolymorphicSerializer` with custom `ValueObject` converters. Register new event types there when adding a new bounded context.
- **DI registration**: Each layer has a `*ServiceCollectionExtensions.cs` with an `Add*` extension method called from `Program.cs`.
