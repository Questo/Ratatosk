# Releasing

Releases are fully automated via [semantic-release](https://semantic-release.gitbook.io/semantic-release/) when commits are pushed to `main`. No manual steps are required.

## How it works

1. **CI runs**: on every push to `main` the `build-and-test`, `integration-tests`, and `check-formatting` jobs run. Pushes to `main` are not path-filtered, so every merge can produce a release. Pull requests only run CI when code, build or release configuration changes.
2. **Image push**: once all checks pass, `push-image` pushes the Docker image to GHCR as:
   - `sha-<commit>`: the immutable image for that exact commit
   - `ci`: tracks the latest `main` build that passed every check
3. **Release job**: semantic-release inspects the commit history since the last tag and decides whether a new version should be published. On a release it updates `CHANGELOG.md` in a `chore(release): <version> [skip ci]` commit, creates a GitHub Release and pushes a Git tag.
4. **Publish job**: if a new version was released, the `sha-<commit>` image is retagged as:
   - `<version>`: e.g. `1.2.0`
   - `latest`: always points to the newest release

Pull request builds never push to GHCR. The release tooling is pinned in `package.json` / `package-lock.json` and kept current by Dependabot.

### Dependabot updates

Minor and patch Dependabot PRs are merged automatically by `dependabot-auto-merge.yml` once CI passes; major bumps arrive as individual PRs for manual review. Merges made with `GITHUB_TOKEN` do not trigger `push` workflows, so the auto-merge job starts CI on `main` itself via `workflow_dispatch`.

## Commit message convention

Releases are triggered by commit messages that follow [Conventional Commits](https://www.conventionalcommits.org/):

| Commit prefix | Version bump | Example |
|---|---|---|
| `fix:` | Patch (`1.0.0` → `1.0.1`) | `fix: prevent login with empty password` |
| `perf:` | Patch (`1.0.0` → `1.0.1`) | `perf: cache product read model lookups` |
| `feat:` | Minor (`1.0.0` → `1.1.0`) | `feat: add refresh token rotation` |
| `feat!:` or `BREAKING CHANGE:` footer | Major (`1.0.0` → `2.0.0`) | `feat!: remove v1 auth endpoints` |

Commits with prefixes like `chore:`, `docs:`, `refactor:`, `test:`, or `ci:` do **not** trigger a release.

## Creating a release (step-by-step)

1. Work on a feature branch. Write commits using the convention above.
2. Open a pull request to `main`.
3. Once merged, CI triggers automatically.
4. semantic-release determines the version, commits the updated `CHANGELOG.md`, creates a GitHub Release with auto-generated release notes, and pushes a Git tag (e.g. `v1.2.0`).
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
