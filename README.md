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
- **Web** (`src/Web`): The Admin-only back-office web app (React, TypeScript, Vite, Mantine), served on the same origin as the API with `/api` proxied to it.

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
    Adjust your configuration settings via environment variables (recommended). At minimum set a 32-byte HS256 signing key, e.g. in `.env`:

    ```bash
    AUTHENTICATION_SECRET=RC9r7YtLzYVn0xOkGzAIxx7am6i83hgx1ot1nzR6m38=
    ADMIN_EMAIL=admin@example.com
    ADMIN_PASSWORD=change-me  # pick a strong password: this account controls the catalog and stock
    ```

    Docker compose already passes `Authentication__Secret` from `.env`; `appsettings*.json` intentionally leave `Authentication:Secret` blank to avoid committing secrets.
    It also passes `ADMIN_EMAIL`/`ADMIN_PASSWORD` as `Admin__Email`/`Admin__Password`, which seed one Admin User at startup (skipped when either is unset; an existing User with that email is left as is). Adding, updating and removing Products, Restock, and Reserving and Releasing stock by hand require that Admin role.

4. **Run the web app**:
    `docker-compose up` serves the back office at http://localhost:3000, with nginx proxying `/api` to the API. For development, run the API and then the Vite dev server, which proxies `/api` to `API_URL` (default `http://localhost:5000`):

    ```bash
    cd src/Web
    npm ci
    API_URL=http://localhost:5000 npm run dev
    ```

    Only Admin accounts can sign in.

## Scripts
**Run tests**
```bash
./scripts/run-unit-tests.sh
./scripts/run-integration-tests.sh
```

**Web app checks** (in `src/Web`)
```bash
npm run lint
npm test
npm run build
```

**Web app smoke test** (in `src/Web`, against a running `docker-compose up` stack; not part of `npm test` or CI)
```bash
npx playwright install chromium   # once
set -a && . ../../.env && set +a && npm run test:e2e
```
It signs in as the seeded Admin (`ADMIN_EMAIL`/`ADMIN_PASSWORD`), adds a Product, Restocks it, places an Order through the API and waits for it to show up Confirmed on the Orders screen. Set `E2E_BASE_URL` to run it against another origin, such as a Vite dev server.
