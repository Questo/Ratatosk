# Project Structure in Ratatosk

This document maps the repository layout to the responsibilities of each layer. Read it alongside `DDD.md` for the conceptual background; this file focuses on where things live and how they depend on each other.

---

## High-level layout

- `src/Core` — DDD building blocks and abstractions shared by all layers: `AggregateRoot`, `DomainEvent`, result primitives, and interfaces such as `IEventStore`, `IEventBus`, and `IAggregateRepository`. No domain or infrastructure types should leak in here.
- `src/Domain` — Pure domain model organized by bounded context (e.g., `Catalog`, `Inventoring`, `Ordering`, `Identity`). Contains aggregates, domain services, and events (`Product`, `Inventory`, `OrderCreated`, etc.).
- `src/Application` — Command/query handlers, event handlers (projections and processes), and ports that orchestrate domain logic. Example folders: `Catalog` (product commands/queries), `Inventoring` (`StockKeeper`, stock projection), `Ordering` (`OrderReservationProcess`, order projection), `Authentication` (token issuance), `Shared` (pagination).
- `src/Infrastructure` — Adapters and implementations for the ports declared in `Core`/`Application`: event stores (`Persistence/EventSourcing`), repositories and unit of work (`Persistence/Repositories`), read models (`Persistence/ReadModels`), polymorphic serialization, projection wiring, domain service implementations, and authentication plumbing.
- `src/API` — Minimal API surface (endpoints, DTOs, request models, middleware) that hosts the application layer. Endpoints dispatch commands and queries through `IDispatcher`; `UnitOfWorkEndpointFilter` ends each request's transaction.
- `src/Web` — The Admin-only back-office web app (React, TypeScript, Vite, Mantine, TanStack Query), a standalone npm project. The browser reaches the API only through the same-origin `/api` proxy (the Vite dev server, or nginx in its Docker image), signed in through httpOnly cookies (ADR 0003).
- `tests` — `UnitTests` (organized by layer: Application/Core/Domain/Infrastructure; `Shared/TestHost.cs` runs the application with in-memory adapters) and `IntegrationTests` (Postgres adapters against a real database).
- `docs` — Architecture notes (`DDD.md`) and this structure guide.
- `scripts` — Convenience scripts for running unit/integration test suites.
- Root configs — `Directory.Packages.props`, `Ratatosk.sln`, Docker compose files, and tool configs for building locally or in CI.

---

## Dependency flow (outer layers depend inward)

`API → Application → Domain → Core`

Infrastructure sits beside `Application`/`Domain` and is composed in at runtime via DI. Tests can reach across layers but prefer the same dependency direction within unit tests.

---

## Key folders at a glance

- `src/Core/Abstractions` — Contracts for event stores, snapshot stores, serializers, the aggregate repository, the event bus, event handlers, and requests.
- `src/Core/BuildingBlocks` & `src/Core/Primitives` — Base classes for aggregates, entities, value objects, and functional primitives (`Result`, `Maybe`, guards).
- `src/Domain/Catalog` — Product aggregate + snapshot, domain services, SKU generation; mirrors DDD glossary in `DDD.md`.
- `src/Application/Catalog` — Command/query handlers and `IProductReadModelRepository` interface that the read side must implement.
- `src/Infrastructure/Persistence/EventSourcing` — Event store & snapshot store implementations (in-memory, file-backed, Postgres).
- `src/Infrastructure/Persistence/Repositories` — `AggregateRepository` (load from snapshot + events; commit = append, commit, snapshot, publish) and `UnitOfWork`.
- `src/Infrastructure/Persistence/ReadModels` — Postgres read-model repositories (product, stock, order, user auth, refresh tokens) consumed by query handlers.
- `src/Infrastructure/EventHandlerDispatcher.cs` — Delivers each published event to every `IDomainEventHandler<T>`, each in its own scope and transaction.
- `src/API/Products`, `src/API/Inventory`, `src/API/Orders` & `src/API/Auth` — HTTP endpoints, DTOs, and request contracts.
- `src/Application/Changes` — `ChangeSignal`, the `IChangeBroadcaster` port, and `ChangeSignalHandler`, which turns product, stock and order events into change signals (registered after every other handler).
- `src/Infrastructure/Changes` — `InProcessChangeBroadcaster`: fans signals out to this process's subscribers, one bounded channel each, dropping signals for a full one.
- `src/API/Changes` — `GET /changes`, the Admin-only Server-Sent Events change stream (mapped outside the unit-of-work group, not buffered by `CleanupResponseMiddleware`).
- `src/Web/src` — `api/client.ts` (the one fetch wrapper: relative `/api` URLs, envelope unwrapping, one refresh and retry on 401), `auth` (session from `GET /auth/me`, login screen), `shell` (app shell with a navbar that switches screens), `products` (search, list, add, edit and remove screens), `stock` (Stock list, Restock, by-hand Reservation and Release), `test` (Vitest setup, MSW server, render helper). `vite.config.ts` holds the dev proxy, and `nginx.conf` + `Dockerfile` the production serving.

---

## Working with the structure

- Adding a new aggregate (e.g., `Order`): define it under `src/Domain/<Context>`, expose commands/queries and handlers in `src/Application/<Context>`, add projections as `IDomainEventHandler<T>` in `src/Application/<Context>` and read-model repositories in `src/Infrastructure`, and surface endpoints in `src/API` if needed.
- Introducing a new persistence backend: implement `IEventStore`/`ISnapshotStore` (under `Infrastructure/Persistence/EventSourcing`), and register it in `src/Infrastructure/DependencyInjection.cs`.
- Creating a new read model: add the model + repository to `Infrastructure/Persistence/ReadModels`, expose an interface in `Application`, and project domain events with an `IDomainEventHandler<T>` in `Application`.
- Keeping boundaries clean: domain types should not reference infrastructure; infrastructure depends on core/application abstractions; API references application requests and DTOs.

---

## Quick pathfinder

- Event sourcing plumbing: `src/Infrastructure/Persistence/EventSourcing/*`
- Event handler dispatch: `src/Infrastructure/EventHandlerDispatcher.cs`
- Authentication: `src/Application/Authentication`, `src/Infrastructure/Authentication`
- Cross-cutting helpers: `src/Application/Shared`, `src/Infrastructure/Shared`
- Endpoint wiring: `src/API/Program.cs`, `src/API/Middleware`
- Live change stream: `src/Application/Changes`, `src/Infrastructure/Changes`, `src/API/Changes`

Use this alongside `DDD.md` to understand how the conceptual pieces map onto concrete folders and files.
