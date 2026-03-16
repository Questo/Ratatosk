# Releasing

Releases are fully automated via [semantic-release](https://semantic-release.gitbook.io/semantic-release/) when commits are pushed to `main`. No manual steps are required.

## How it works

1. **CI runs** — on every push to `main` the `build-and-test`, `integration-tests`, and `check-formatting` jobs run.
2. **Release job** — if all checks pass, semantic-release inspects the commit history since the last tag and decides whether a new version should be published.
3. **Publish job** — if a new version was released, the Docker image is pushed to GHCR with three tags:
   - `ci` — always pushed (tracks the latest main build)
   - `<version>` — e.g. `1.2.0` (only on a new release)
   - `latest` — always points to the newest release

## Commit message convention

Releases are triggered by commit messages that follow [Conventional Commits](https://www.conventionalcommits.org/):

| Commit prefix | Version bump | Example |
|---|---|---|
| `fix:` | Patch (`1.0.0` → `1.0.1`) | `fix: prevent login with empty password` |
| `feat:` | Minor (`1.0.0` → `1.1.0`) | `feat: add refresh token rotation` |
| `feat!:` or `BREAKING CHANGE:` footer | Major (`1.0.0` → `2.0.0`) | `feat!: remove v1 auth endpoints` |

Commits with prefixes like `chore:`, `docs:`, `refactor:`, `test:`, or `ci:` do **not** trigger a release.

## Creating a release (step-by-step)

1. Work on a feature branch. Write commits using the convention above.
2. Open a pull request to `main`.
3. Once merged, CI triggers automatically.
4. semantic-release determines the version, creates a GitHub Release with auto-generated release notes, and pushes a Git tag (e.g. `v1.2.0`).
5. The `publish` job picks up the new version and pushes the versioned Docker image to GHCR.

## Breaking changes

To trigger a major version bump, either:

```
feat!: redesign authentication API
```

or add a `BREAKING CHANGE:` footer to any commit:

```
refactor: remove legacy product endpoint

BREAKING CHANGE: DELETE /products/v1/:id has been removed in favour of /products/:id
```

## Hotfixes

Hotfixes follow the same process — merge a `fix:` commit to `main` and a patch release is cut automatically.
