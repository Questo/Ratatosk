# Changelog

All notable changes to this project will be documented in this file. This file is automatically updated by [semantic-release](https://semantic-release.gitbook.io/) on every release to `main`. See [docs/RELEASING.md](docs/RELEASING.md) for how releases work.

---

## [0.1.1](https://github.com/Questo/Ratatosk/compare/v0.1.0...v0.1.1) (2026-03-16)


### Bug Fixes

* **ci:** correct YAML syntax in compute image tags step ([0b62e6a](https://github.com/Questo/Ratatosk/commit/0b62e6a3154bf64ec32b87055dab15fda760c946))


## 0.1.0 (2026-03-16)

Initial release. Establishes the core project structure including:

* DDD/Event Sourcing/CQRS foundation with domain events and snapshotting
* JWT authentication with refresh token rotation and role-based authorization
* PostgreSQL persistence via Dapper (event store, snapshot store, read models)
* CI/CD pipeline with automated testing, coverage enforcement, CodeQL, Dependabot, and semantic-release
