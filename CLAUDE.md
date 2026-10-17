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

**Application** — Command/query handlers (dispatched via `IDispatcher`, which endpoints call directly), event handlers, read model types, and repository interfaces. Commands return `Result<T>`. Cross-context coordination lives in modules here: `OrderReservationProcess` (the Order → stock saga) and `StockKeeper` (every stock change, with concurrency retry).

**Infrastructure** — Concrete implementations: three event store backends (`InMemory`, `File`, `PostgresEventStore`) with matching snapshot stores, `AggregateRepository<T>` (load from snapshot + events; commit), `IUnitOfWork`, `ProjectionRegistrationService` (delivers events to every `IDomainEventHandler`), `Argon2PasswordHasher`, `JwtTokenIssuer`.

**API** — Minimal API endpoints organized by feature (`/auth`, `/products`, `/inventory`, `/orders`). `CleanupResponseMiddleware` wraps all responses in a `Result` envelope; `UnitOfWorkEndpointFilter` commits the request's transaction on success (status < 400) and rolls it back otherwise.

## Event Sourcing Flow

Write path: Command → Handler → `IAggregateRepository<T>.LoadAsync` → domain method → `RaiseEvent()` → `CommitAsync`. `CommitAsync` is the only place the persistence sequence lives: append in raise order with an expected-version check (`ConcurrencyException` on conflict), snapshot when due, commit, clear, then publish. Handlers never commit or publish themselves.

Event handling: `ProjectionRegistrationService` runs each handler in its own DI scope and transaction, depth-first and synchronously. A failing handler is rolled back and logged; it never fails the publisher or the event's other handlers. Read models that can see events out of order (e.g. `OrderProjection`) rebuild from the committed aggregate instead of applying deltas.

Read path: Query → Handler → Repository hits denormalized read model table directly (no event replay).

Snapshots: When a commit carries an aggregate across a multiple of `SnapshotFrequency`, a snapshot is saved, keyed by aggregate type + id (an Inventory shares its Product's id). On load, the repository restores the snapshot via the aggregate's `RestoreSnapshot` override, then replays only later events. An aggregate that creates snapshots must also override `RestoreSnapshot`.

## Configuration

Event store backend is controlled by `appsettings.json`:
```json
"EventStore": { "Type": "InMemory" }  // InMemory | File | Sql
```

For `Sql`, set `DatabaseOptions:ConnectionString`. Auth secrets are in `AuthOptions` (JWT issuer, audience, secret).

## Key Patterns

- **Result<T>**: Used for all fallible operations, domain methods included (e.g. `Inventory.ReserveStock` returns `Result`). Propagate by checking `IsFailure` and returning early; reserve exceptions for broken invariants and infrastructure failures.
- **Value Objects**: Extend `ValueObject`, override `GetEqualityComponents()`. All domain primitives (Price, Email, SKU, etc.) are value objects.
- **Adding a new bounded context**: Create domain aggregate in `Domain/`, add commands/queries and projections (`IDomainEventHandler<T>`, auto-registered) in `Application/`, read-model repositories in `Infrastructure/Persistence/ReadModels/`, register endpoints in `API/`.
- **Serialization**: Events and snapshots use `JsonPolymorphicSerializer`, which stores each type's full name (namespace + class). Moving or renaming an event class changes that name and breaks stored history, so keep event types where they are. Each new value object needs a converter in `ValueObjectConverters.cs` that rebuilds it through an `internal static FromStored(...)` factory (skipping validation, so old events stay readable when rules tighten).
- **DI registration**: Each layer has a `DependencyInjection.cs` with an `Add*` extension method called from `Program.cs`. The InMemory stores are singletons (they are the storage).
- **Testing handlers**: drive them through `tests/UnitTests/Shared/TestHost.cs` (production wiring with in-memory adapters) and assert on streams and read models; mock only true externals (password hasher, token issuer).
