# Domain-Driven Design (DDD) in Ratatosk

This document explains the core DDD concepts used in this repository and how they map to the codebase. Examples use the Product aggregate and the Product read model (projection) to illustrate the flow from commands to queries.

---

## Overview

Domain-Driven Design (DDD) is an approach to software development that prioritizes the business domain and uses domain models to represent business rules and behavior. This project uses DDD concepts together with Event Sourcing and CQRS (Command Query Responsibility Segregation) to separate write and read concerns.

Key pieces in this repository you will encounter:

- `Aggregate` (e.g., Product aggregate): encapsulates domain state and behavior.
- `Domain Event`: represents facts that happened in the domain (immutable).
- `Event Store`: the append-only storage of domain events.
- `Snapshot Store`: optional storage to speed up aggregate rehydration.
- `Projections` / `Read Models`: denormalized views derived from events for efficient queries.
- `Repositories` / `Unit of Work`: infrastructure to load and persist aggregates and read models.

Relevant implementation files (examples):

- Event store implementations: `src/Infrastructure/Persistence/EventSourcing/PostgresEventStore.cs`, `src/Infrastructure/Persistence/EventSourcing/FileEventStore.cs`, `src/Infrastructure/Persistence/EventSourcing/InMemoryEventStore.cs`.
- Product read model repository: `src/Infrastructure/Persistence/ReadModels/ProductReadModel.cs`.
- Aggregate persistence: `src/Infrastructure/Persistence/Repositories/AggregateRepository.cs` (load and commit), `src/Infrastructure/Persistence/Repositories/UnitOfWork.cs`.

---

## Core DDD Concepts (brief)

- Entity: An object with identity and lifecycle (e.g., a Product with `Id`).
- Value Object: An immutable object defined by its values (e.g., `Money`, `Email`).
- Aggregate: A cluster of domain objects (entities/value objects) treated as a single unit for consistency. The aggregate has one root (aggregate root).
- Aggregate Root: The single entry point to modify the aggregate. Only aggregate roots are loaded/saved by repositories.
- Domain Event: A record that something relevant happened in the domain (e.g., `ProductCreated`, `ProductUpdated`, `ProductRemoved`).
- Repository: A pattern for retrieving and persisting aggregates (often maps to event store in event-sourced systems).
- Unit of Work: Tracks changes within a transaction and coordinates persistence.

---

## Event Sourcing + CQRS in this solution

This solution separates command processing (writes) from queries (reads) and persists state changes as events.

Write path (commands):

1. A Command arrives (e.g., `AddProductCommand`).
2. Application layer finds the `Aggregate` (via `AggregateRepository`) or creates a new one.
3. The aggregate executes domain logic and emits domain events (e.g., `ProductCreated`, `ProductPriceChanged`).
4. The handler calls `AggregateRepository.CommitAsync`, which appends the events to the `Event Store` (rejecting a stale version), stores a `Snapshot` when one is due, commits the transaction, and then publishes the events to their handlers.

Read path (queries):

1. The application queries a read model (projection), e.g., a `ProductReadModel`.
2. Read models are updated synchronously, in-process, right after the events are committed: each event handler (projector) runs in its own scope and transaction and writes to Postgres read-model tables.
3. Queries return results directly from the read model, optimized for read performance.

This separation enables the domain model to remain pure and focused on business rules while allowing read models to be shaped for performance and client needs.

---

## Product example (write flow)

Consider creating a new product:

1. `AddProductCommand` is handled by `AddProductCommandHandler` in the application layer.
2. The handler checks the SKU is unique and calls `Product.Create`.
3. The `Product` aggregate enforces invariants and emits a `ProductCreated` event that contains the minimal set of facts (ProductId, Name, SKU, Description, Price).
4. `AggregateRepository.CommitAsync` persists the `ProductCreated` event to the `Event Store` (see `src/Infrastructure/Persistence/EventSourcing/PostgresEventStore.cs`) and publishes it.
5. Handlers react: `ProductProjection` writes the `ProductReadModel`, `InventoryProjection` creates the stock record, and `InventoryProvisioningHandler` creates the product's Inventory aggregate.

This means the source of truth for product state is the stream of events. The read model is a derived, denormalized view optimized for queries.

---

## Projections & Read Models (detailed)

Projections are processes that subscribe to the event stream and maintain read models. Read models are simple, query-optimized representations (rows, documents, caches) built from the event history.

How projections are represented here:

- Projectors are `IDomainEventHandler<T>` classes in `src/Application/<Context>` (e.g., `ProductProjection`, `OrderProjection`). `ProjectionRegistrationService` delivers each committed event to them.
- Read model repositories (e.g., `ProductReadModel.cs`) provide CRUD-like access to the derived view used by query handlers.

Example responsibilities of `ProductReadModelRepository`:

- Create or update a `ProductReadModel` when a `ProductCreated`/`ProductUpdated` event is handled.
- Provide query methods for listing products, pagination, or retrieving a product by id.

Common projection patterns:

- Synchronous update: Projector runs immediately after event persistence in the same transaction (simpler, but couples write latency to projection work).
- Asynchronous update: Events are published to a queue or consumed later; projector updates happen eventually (scales better, requires eventual consistency handling).

This repository contains read model implementations under `src/Infrastructure/Persistence/ReadModels`.

---

## Snapshotting

Long event streams can make aggregate rehydration slow. Snapshot stores save condensed aggregate state at a point in time so rehydration can start from the snapshot and apply only newer events.

This project includes snapshot store implementations alongside event stores in `src/Infrastructure/Persistence/EventSourcing` (`InMemorySnapshotStore.cs`, `FileSnapshotStore.cs`, `PostgresSnapshotStore.cs`). Snapshots are keyed by aggregate type and id. An aggregate opts in by overriding both `CreateSnapshot` and `RestoreSnapshot` (see `Product`).

---

## Where to look in the code

- Command handlers and event handlers: `src/Application` (e.g., `AddProductCommandHandler`, `OrderReservationProcess`).
- Aggregate and domain logic: `src/Domain` (search for `Product` domain types and domain events).
- Event store implementations: `src/Infrastructure/Persistence/EventSourcing`.
- Read model repositories: `src/Infrastructure/Persistence/ReadModels`.
- Persistence glue (aggregate repository, unit of work): `src/Infrastructure/Persistence/Repositories`.

---

## Recommendations for contributors

- Keep domain models (aggregates, entities, value objects, domain events) free of infrastructure concerns. They should not reference EF, Dapper, or event-store APIs.
- Write projector code as pure functions where possible — take an event and return the read model update operation.
- Prefer small, well-named events that represent facts rather than commands.
- When adding a new aggregate (e.g., `Order`), create: aggregate, events, command/handler, repository hookup, and projector/read-model repository.

---

## Glossary (quick)

- Aggregate: Consistency boundary and behavioral unit.
- Event Store: Append-only store of domain events.
- Projection: Consumer of events that builds a read model.
- Read Model: Denormalized representation optimized for queries.
- Snapshot: Shortcut to speed up aggregate rehydration.