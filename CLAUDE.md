# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Run the API locally with Docker (PostgreSQL + API)
docker-compose up

# Build only
dotnet build

# Run the API without Docker (always needs PostgreSQL: the read models and unit of work live there,
# whichever event store is configured; the schema is upgraded on startup)
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

## Software factory

`/factory <feature>` (`.claude/skills/factory/SKILL.md`) runs spec → build → review → approve, each job in its own worktree. The agents read this section:

- **Fresh checkout setup**: `dotnet tool restore && dotnet restore`.
- **Dev server on a port**: start Postgres first (`docker compose up -d db`, reads `.env`; it's shared, so start it only if `localhost:5432` isn't already up), then `ASPNETCORE_ENVIRONMENT=Development Authentication__Secret=factory-dev-secret-at-least-32-bytes dotnet run --project src/API --no-launch-profile --urls http://localhost:<port>`. There is no UI, so exercise endpoints with `curl` instead of a browser.
- **Validation**: `dotnet build`, `./scripts/run-unit-tests.sh` (75% coverage threshold), and `dotnet format --verify-no-changes --severity error` (what CI runs; pass `--include <changed files>` to scope it, never run `dotnet format` without `--verify-no-changes`). Integration tests (`./scripts/run-integration-tests.sh`) use a fixed container on port 5433, so run them only for changes to Infrastructure persistence, and never from two jobs at once.

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

**Infrastructure** — Concrete implementations: three event store backends (`InMemory`, `File`, `PostgresEventStore`) with matching snapshot stores, `AggregateRepository<T>` (load from snapshot + events; commit), `IUnitOfWork`, `EventHandlerDispatcher` (delivers events to every `IDomainEventHandler`), `Argon2PasswordHasher`, `JwtTokenIssuer`.

**API** — Minimal API endpoints organized by feature (`/auth`, `/products`, `/inventory`, `/orders`). `CleanupResponseMiddleware` wraps all responses in a `Result` envelope; `UnitOfWorkEndpointFilter` commits the request's transaction on success (status < 400) and rolls it back otherwise.

## Event Sourcing Flow

Write path: Command → Handler → `IAggregateRepository<T>.LoadAsync` → domain method → `RaiseEvent()` → `CommitAsync`. `CommitAsync` is the only place the persistence sequence lives: append in raise order with an expected-version check (`ConcurrencyException` on conflict), commit, clear, snapshot when due, then publish. Handlers never commit or publish themselves. Only the Sql event store appends inside the transaction; if the transaction fails after an InMemory/File append, the stored events are still published and the error rethrown.

Event handling: `EventHandlerDispatcher` runs each handler in its own DI scope and transaction, depth-first and synchronously. A failing handler is rolled back and logged; it never fails the publisher or the event's other handlers. Read models that can see events out of order or concurrently (`OrderProjection`, `InventoryProjection`) rebuild from the committed aggregate instead of applying deltas; the stock record also carries the Inventory version so an older rebuild never overwrites a newer one.

Read path: Query → Handler → Repository hits denormalized read model table directly (no event replay).

Snapshots: When a commit carries an aggregate across a multiple of `SnapshotFrequency`, a snapshot is saved after the commit, in its own transaction and best-effort (a failure is logged, never fails the commit), keyed by aggregate type + id (an Inventory shares its Product's id). On load, the repository restores the snapshot via the aggregate's `RestoreSnapshot` override, then replays only later events. An aggregate that creates snapshots must also override `RestoreSnapshot`.

## Configuration

Event store backend is controlled by `appsettings.json`:
```json
"EventStore": { "Type": "InMemory" }  // InMemory | File | Sql
```

For `Sql`, set `DatabaseOptions:ConnectionString`. Auth secrets are in `AuthOptions` (JWT issuer, audience, secret).

The JWT carries the user id as `ClaimTypes.NameIdentifier`, serialized under its full claim URI (`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`), not the short `nameid`; `GET /orders` lists only the orders of that id, never one from the request, and `POST /orders` places the Order for that id. `GET /orders/{orderId}` returns 404 unless the caller placed the Order or is an Admin, and a token with no user id gets 401 from the Order endpoints.

## Key Patterns

- **Result<T>**: Used for all fallible operations, domain methods included (e.g. `Inventory.ReserveStock` returns `Result`). Propagate by checking `IsFailure` and returning early; reserve exceptions for broken invariants and infrastructure failures.
- **Value Objects**: Extend `ValueObject`, override `GetEqualityComponents()`. All domain primitives (Price, Email, SKU, etc.) are value objects.
- **Adding a new bounded context**: Create domain aggregate in `Domain/`, add commands/queries and projections (`IDomainEventHandler<T>`, auto-registered) in `Application/`, read-model repositories in `Infrastructure/Persistence/ReadModels/`, register endpoints in `API/`.
- **Serialization**: Events and snapshots use `JsonPolymorphicSerializer`, which stores each type's full name (namespace + class). Moving or renaming an event class changes that name and breaks stored history, so keep event types where they are. Each new value object needs a converter in `ValueObjectConverters.cs` that rebuilds it through an `internal static FromStored(...)` factory (skipping validation, so old events stay readable when rules tighten).
- **DI registration**: Each layer has a `DependencyInjection.cs` with an `Add*` extension method called from `Program.cs`. The InMemory stores are singletons (they are the storage).
- **Testing handlers**: drive them through `tests/UnitTests/Shared/TestHost.cs` (production wiring with in-memory adapters) and assert on streams and read models; mock only true externals (password hasher, token issuer).
