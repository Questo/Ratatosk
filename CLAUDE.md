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

# Run the back-office web app: docker-compose up serves it at http://localhost:3000.
# Without Docker, start the API and then the Vite dev server, which proxies /api to API_URL
# (default http://localhost:5000):
cd src/Web && npm ci && API_URL=http://localhost:5000 npm run dev
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

# Web app (in src/Web): lint, Vitest + Testing Library + MSW tests, type-check and build
npm run lint
npm test
npm run build

# Playwright smoke test of the back office against a running stack (in src/Web; not part of npm test or CI).
# Once: npx playwright install chromium. Start the stack with docker-compose up, then run it with
# ADMIN_EMAIL/ADMIN_PASSWORD set to the .env values; E2E_BASE_URL (default http://localhost:3000)
# points it at another origin, such as a Vite dev server.
set -a && . ../../.env && set +a && npm run test:e2e
```

Coverage is reported for `Core`, `Domain`, and `Application` assemblies only (not Infrastructure or API).

## Software factory

`/factory <feature>` (`.claude/skills/factory/SKILL.md`) runs spec → build → review → approve, each job in its own worktree. The agents read this section:

- **Fresh checkout setup**: `dotnet tool restore && dotnet restore && npm ci --prefix src/Web && scripts/factory-db.sh create <job-id>`. The last step gives the job its own database, `factory_<n>`, in the shared Postgres container (starting it from the main checkout's `.env` if `localhost:5432` isn't up), so parallel jobs never see each other's data.
- **Job teardown** (after the job merges): `scripts/factory-db.sh drop <job-id>`.
- **Dev server on a port**: `ASPNETCORE_ENVIRONMENT=Development Authentication__Secret=factory-dev-secret-at-least-32-bytes Admin__Email=admin@factory.test Admin__Password=factory-admin-password Database__ConnectionString="$(scripts/factory-db.sh url <job-id>)" dotnet run --project src/API --no-launch-profile --urls http://localhost:<port>`, which runs against the job's own database (never the connection string in `appsettings.Development.json`) and seeds `admin@factory.test` there. For the web app, run the API on one port and then `API_URL=http://localhost:<api port> npm run dev --prefix src/Web -- --port <port> --strictPort`, which proxies `/api` to that API. Check the web app's screens in a browser, and exercise the API's endpoints with `curl`.
- **Validation**: `dotnet build`, `./scripts/run-unit-tests.sh` (75% coverage threshold), and `dotnet format --verify-no-changes --severity error` (what CI runs; pass `--include <changed files>` to scope it, never run `dotnet format` without `--verify-no-changes`). For changes under `src/Web`, also run `npm run lint`, `npm test` and `npm run build` in `src/Web`. Jobs that change the web app or the endpoints it drives also run the Playwright smoke test against their dev servers: `E2E_BASE_URL=http://localhost:<web port> ADMIN_EMAIL=admin@factory.test ADMIN_PASSWORD=factory-admin-password npm run test:e2e` in `src/Web` (optional for other jobs). Integration tests (`./scripts/run-integration-tests.sh`) use one fixed container on port 5433, so run them only for changes to Infrastructure persistence; the script holds a lock shared by every worktree, so a second job's run waits for the first.

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

**API** — Minimal API endpoints organized by feature (`/auth`, `/products`, `/inventory`, `/orders`). `CleanupResponseMiddleware` wraps all responses in a `Result` envelope; `UnitOfWorkEndpointFilter` commits the request's transaction on success (status < 400) and rolls it back otherwise. `GET /changes` is an Admin-only Server-Sent Events stream of change signals; it is mapped outside the unit-of-work group (an open stream holds no database connection) and `CleanupResponseMiddleware` passes it through unbuffered.

**Web** — `src/Web` is the Admin-only back office (React, TypeScript, Vite, Mantine, TanStack Query), a standalone npm project. The browser sees the API on its own origin under `/api` (ADR 0003): the Vite dev server proxies it in development and nginx in the `web` compose service, both stripping the prefix. All calls go through `src/api/client.ts`, which never handles tokens; the app reads the session from `GET /auth/me`. The shell's navbar switches between the Products screen (search, add, edit, remove), the Stock screen (on hand, reserved and available per SKU; Restock, by-hand Reservation and Release) and the Orders screen (filter by status; detail with a Details tab showing Order lines, status and cancellation reason, and a History tab that steps backward and forward through the Order's events, one version at a time). The shell keeps one `EventSource` on `/api/changes` open while an Admin is signed in (`src/changes/useChangeStream.ts`); `order` signals invalidate the Order queries, and a stream the browser closed (e.g. a 401 once the access cookie expired) is reopened after refreshing the session through `GET /auth/me`.

## Event Sourcing Flow

Write path: Command → Handler → `IAggregateRepository<T>.LoadAsync` → domain method → `RaiseEvent()` → `CommitAsync`. `CommitAsync` is the only place the persistence sequence lives: append in raise order with an expected-version check (`ConcurrencyException` on conflict), commit, clear, snapshot when due, then publish. Handlers never commit or publish themselves. Only the Sql event store appends inside the transaction; if the transaction fails after an InMemory/File append, the stored events are still published and the error rethrown.

Event handling: `EventHandlerDispatcher` runs each handler in its own DI scope and transaction, depth-first and synchronously. A failing handler is rolled back and logged; it never fails the publisher or the event's other handlers. Read models that can see events out of order or concurrently (`OrderProjection`, `InventoryProjection`) rebuild from the committed aggregate instead of applying deltas; the stock record also carries the Inventory version so an older rebuild never overwrites a newer one.

Change signals: `ChangeSignalHandler` turns every event that changes the Product, stock or Order read model into a `ChangeSignal` (`kind` = `product`/`stock`/`order`, `id`, `version` = the event's `Version` in its own stream) and hands it to `IChangeBroadcaster`, which feeds the open `/changes` streams. It is registered after all other handlers, so a signal only goes out once its read model is written; clients refetch through the read endpoints. Depth-first delivery means signals can arrive out of version order (e.g. `OrderConfirmed` before `OrderCreated`), so clients should ignore a signal older than the version they hold. `InProcessChangeBroadcaster` only reaches clients of its own process, so live updates need a single API instance (ADR 0002). The back office uses the stream this way: it doesn't compare versions, it invalidates its TanStack Query caches for the signal's kind (only `order` so far) and refetches the latest state, so out-of-order signals don't matter.

Read path: Query → Handler → Repository hits denormalized read model table directly (no event replay). The one exception is Order history (`GetOrderHistoryQuery`), which loads the full stream through `IAggregateRepository<T>.LoadHistoryAsync` (snapshots ignored) and replays it one event at a time.

Snapshots: When a commit carries an aggregate across a multiple of `SnapshotFrequency`, a snapshot is saved after the commit, in its own transaction and best-effort (a failure is logged, never fails the commit), keyed by aggregate type + id (an Inventory shares its Product's id). On load, the repository restores the snapshot via the aggregate's `RestoreSnapshot` override, then replays only later events. An aggregate that creates snapshots must also override `RestoreSnapshot`.

## Configuration

Event store backend is controlled by `appsettings.json`:
```json
"EventStore": { "Type": "InMemory" }  // InMemory | File | Sql
```

For `Sql`, set `DatabaseOptions:ConnectionString`. Auth secrets are in `AuthOptions` (JWT issuer, audience, secret).

`Admin__Email`/`Admin__Password` (`AdminOptions`) seed one Admin User at startup (`AdminSeeder`). Seeding is skipped when either is unset, and an existing User with that email is left as is. Adding, updating and removing Products, Restock, and by-hand Reservation and Release require the Admin role.

The JWT carries the user id as `ClaimTypes.NameIdentifier`, serialized under its full claim URI (`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`), not the short `nameid`. `GET /orders` gives a non-Admin only their own Orders (that id, never one from the request) and an Admin every Order, filterable by `status` and `customerId`; `POST /orders` places the Order for that id. `GET /orders/{orderId}` returns 404 unless the caller placed the Order or is an Admin, and a token with no user id gets 401 from the Order endpoints. The Admin-only `GET /orders/{orderId}/history` replays the Order's events and returns, per event, its version, name and time, the Order as it was right after it, and the reserved SKUs then.

Login and refresh also set the tokens as httpOnly, `Secure`, `SameSite=Strict` cookies: `access_token` (path `/`) and `refresh_token` (scoped to `/api/auth/refresh`, the path the browser sees behind the proxy). The API accepts the JWT from the `access_token` cookie when no `Authorization` header is sent (the header wins), refresh falls back to the `refresh_token` cookie when the body has no token, `POST /auth/logout` clears both cookies, and `GET /auth/me` returns the caller's `{ email, role }` so the web app can tell who is signed in without reading the cookie (ADR 0003, `docs/adr/0003-web-app-auth-in-httponly-cookies.md`).

## Key Patterns

- **Result<T>**: Used for all fallible operations, domain methods included (e.g. `Inventory.ReserveStock` returns `Result`). Propagate by checking `IsFailure` and returning early; reserve exceptions for broken invariants and infrastructure failures.
- **Value Objects**: Extend `ValueObject`, override `GetEqualityComponents()`. All domain primitives (Price, Email, SKU, etc.) are value objects.
- **Adding a new bounded context**: Create domain aggregate in `Domain/`, add commands/queries and projections (`IDomainEventHandler<T>`, auto-registered) in `Application/`, read-model repositories in `Infrastructure/Persistence/ReadModels/`, register endpoints in `API/`.
- **Serialization**: Events and snapshots use `JsonPolymorphicSerializer`, which stores each type's full name (namespace + class). Moving or renaming an event class changes that name and breaks stored history, so keep event types where they are. Each new value object needs a converter in `ValueObjectConverters.cs` that rebuilds it through an `internal static FromStored(...)` factory (skipping validation, so old events stay readable when rules tighten).
- **DI registration**: Each layer has a `DependencyInjection.cs` with an `Add*` extension method called from `Program.cs`. The InMemory stores are singletons (they are the storage).
- **Testing handlers**: drive them through `tests/UnitTests/Shared/TestHost.cs` (production wiring with in-memory adapters) and assert on streams and read models; mock only true externals (password hasher, token issuer).
