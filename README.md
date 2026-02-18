# 🐿️ Ratatosk

**Ratatosk** is a small, event-driven C# application based on Domain-Driven Design (DDD) principles. It leverages event sourcing, CQRS, and snapshotting to manage domain logic and state.

## 🚀 Features

- **Event Sourcing**: Stores all changes to the application state as a sequence of events.
- **CQRS (Command Query Responsibility Segregation)**: Separates read and write operations for better performance and scalability.
- **Snapshotting**: Periodically captures the state of aggregates to improve rehydration performance.
- **Pluggable Infrastructure**: Supports multiple storage backends, including in-memory, file-based, and SQL databases.
- **Extensible Core**: Provides a core library with reusable DDD building blocks.

## 🧱 Project Structure

- **Core**: Contains foundational building blocks like `AggregateRoot`, `DomainEvent`, and utility classes (`Result`, `Maybe`, `Guard`).
- **Domain**: Houses domain-specific logic and aggregates, such as the `Product` aggregate in the catalog domain.
- **Application**: Contains services, command and query handlers, and orchestrates application logic.
- **Infrastructure**: Implements storage solutions, serialization, and external configurations.
- **API**: Provides a minimal API interface for interacting with the application.

## Getting started

1. **Clone the Repository**:

    ```bash
    git clone https://github.com/questo/ratatosk.git
    ```

2. **Build and Run**:

    ```bash
    cd src/API
    dotnet run
    ```

3. **Configuration**:  
    Adjust your configuration settings in `appsettings.json` or environment variables.

## Scripts
**Run tests**
```bash
./scripts/run-unit-tests.sh
./scripts/run-integration-tests.sh
```
