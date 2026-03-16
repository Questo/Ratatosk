# Commit Message Guide

This project follows [Conventional Commits](https://www.conventionalcommits.org/). Commit messages drive automated versioning and changelog generation — see [docs/RELEASING.md](RELEASING.md) for how that works.

## Format

```
type(scope): short description

[optional body]

[optional footer(s)]
```

- **type** — what kind of change (see below)
- **scope** — what area of the codebase was touched (optional but encouraged)
- **description** — imperative, lowercase, no trailing period ("add" not "added" or "adds")

## Types

| Type | When to use | Triggers release? |
|---|---|---|
| `feat` | A new feature | Yes — minor bump |
| `fix` | A bug fix | Yes — patch bump |
| `perf` | A performance improvement | Yes — patch bump |
| `feat!` / `BREAKING CHANGE:` | A breaking API change | Yes — major bump |
| `refactor` | Code restructuring with no behaviour change | No |
| `test` | Adding or updating tests | No |
| `docs` | Documentation only | No |
| `ci` | CI/CD pipeline changes | No |
| `chore` | Maintenance, dependency updates, tooling | No |
| `style` | Formatting, whitespace (no logic change) | No |

## Scopes

Use a scope to give quick context. Common scopes in this repo:

| Scope | Area |
|---|---|
| `auth` | Authentication, JWT, refresh tokens |
| `products` | Products domain |
| `api` | API endpoints, middleware, routing |
| `infra` | Infrastructure, persistence, Dapper, Postgres |
| `domain` | Domain model, events, aggregates |
| `ci` | GitHub Actions workflows |
| `deps` | Dependency updates |

## Examples

```
feat(auth): add refresh token rotation
fix(auth): return 401 instead of 400 on invalid credentials
feat(products): add pagination to product list endpoint
refactor(infra): replace raw SQL with query constants
test(auth): add unit tests for RefreshTokenCommandHandler
ci: add Docker layer caching to build jobs
docs: add commit message guide
chore(deps): update Dapper to 2.1.35
```

## Breaking changes

Add `!` after the type, or include a `BREAKING CHANGE:` footer:

```
feat(api)!: remove v1 authentication endpoints

feat(auth): redesign token response shape

BREAKING CHANGE: /auth/login now returns { accessToken, refreshToken }
instead of { token }
```

Both forms trigger a major version bump.
