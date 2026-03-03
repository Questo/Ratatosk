# Project Structure in Ratatosk

This document maps the repository layout to the responsibilities of each layer. Read it alongside `DDD.md` for the conceptual background; this file focuses on where things live and how they depend on each other.

---

## High-level layout

- `src/Core` — DDD building blocks and abstractions shared by all layers: `AggregateRoot`, `DomainEvent`, result primitives, and interfaces such as `IEventStore`, `IEventBus`, and `IAggregateRepository`. No domain or infrastructure types should leak in here.
- `src/Domain` — Pure domain model organized by bounded context (e.g., `Catalog`, `Inventoring`, `Ordering`, `Identity`). Contains aggregates, domain services, and events (`Product`, `Inventory`, `OrderCreated`, etc.).
- `src/Application` — Application services, command/query handlers, and ports that orchestrate domain logic. Example folders: `Catalog` (product commands/queries), `Authentication` (token issuance), `Shared` (cross-cutting helpers like pagination), `Configuration` (DI wiring).
- `src/Infrastructure` — Adapters and implementations for the ports declared in `Core`/`Application`: event stores (`Persistence/EventSourcing`), repositories and unit of work (`Persistence/Repositories`), read models (`Persistence/ReadModels`), polymorphic serialization, projection wiring, domain service implementations, and authentication plumbing.
- `src/API` — Minimal API surface (endpoints, DTOs, request models, middleware, configuration) that hosts the application layer. Keeps controllers thin; calls into `Application` services.
- `tests` — `UnitTests` (organized by layer: Application/Core/Domain/Shared) and `IntegrationTests` (exercise end-to-end flows against configured infrastructure).
- `docs` — Architecture notes (`DDD.md`) and this structure guide.
- `scripts` — Convenience scripts for running unit/integration test suites.
- Root configs — `Directory.Packages.props`, `Ratatosk.sln`, Docker compose files, and tool configs for building locally or in CI.

---

## Dependency flow (outer layers depend inward)

`API → Application → Domain → Core`

Infrastructure sits beside `Application`/`Domain` and is composed in at runtime via DI. Tests can reach across layers but prefer the same dependency direction within unit tests.

---

## Key folders at a glance

- `src/Core/Abstractions` — Contracts for event stores, repositories, domain event dispatching, builders, clocks, and command/event markers.
- `src/Core/BuildingBlocks` & `src/Core/Primitives` — Base classes for aggregates, entities, value objects, and functional primitives (`Result`, `Maybe`, guards).
- `src/Domain/Catalog` — Product aggregate + snapshot, domain services, SKU generation; mirrors DDD glossary in `DDD.md`.
- `src/Application/Catalog` — Command/query handlers and `IProductReadModelRepository` interface that the read side must implement.
- `src/Infrastructure/Persistence/EventSourcing` — Event store & snapshot store implementations (in-memory, file-backed, Postgres).
- `src/Infrastructure/Persistence/Repositories` — `AggregateRepository` and `UnitOfWork` glue that persist events and coordinate projections.
- `src/Infrastructure/Persistence/ReadModels` — Read models (`ProductReadModel`, `UserAuthReadModel`) consumed by query handlers.
- `src/Infrastructure/Projections` & `ProjectionRegistrationService` — Registers projectors to consume domain events and update read models.
- `src/API/Products` & `src/API/Auth` — HTTP endpoints, DTOs, and request contracts for products and auth flows.

---

## Working with the structure

- Adding a new aggregate (e.g., `Order`): define it under `src/Domain/<Context>`, expose commands/queries and handlers in `src/Application/<Context>`, wire repository/projector pieces in `src/Infrastructure`, and surface endpoints in `src/API` if needed.
- Introducing a new persistence backend: implement `IEventStore`/`ISnapshotStore` (under `Infrastructure/Persistence/EventSourcing`), and register via `InfrastructureServiceCollectionExtensions`.
- Creating a new read model: add the model + repository to `Infrastructure/Persistence/ReadModels`, expose an interface in `Application`, and project domain events via `Infrastructure/Projections`.
- Keeping boundaries clean: domain types should not reference infrastructure; infrastructure depends on core/application abstractions; API references only application services and DTOs.

---

## Quick pathfinder

- Event sourcing plumbing: `src/Infrastructure/Persistence/EventSourcing/*`
- Projection registration: `src/Infrastructure/Projections`, `src/Infrastructure/ProjectionRegistrationService.cs`
- Authentication: `src/Application/Authentication`, `src/Infrastructure/Authentication`
- Cross-cutting helpers: `src/Core/Extensions`, `src/Application/Shared`, `src/Infrastructure/Shared`
- Endpoint wiring: `src/API/Configuration`, `src/API/Middleware`

Use this alongside `DDD.md` to understand how the conceptual pieces map onto concrete folders and files.
