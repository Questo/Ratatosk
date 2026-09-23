## [0.1.4](https://github.com/Questo/Ratatosk/compare/v0.1.3...v0.1.4) (2026-09-23)


### Bug Fixes

* **ci:** checkout repo before using local action in publish job ([9a9f1de](https://github.com/Questo/Ratatosk/commit/9a9f1de8c1fb7557fa87f41e77d338c76be3c025))

## [0.1.3](https://github.com/Questo/Ratatosk/compare/v0.1.2...v0.1.3) (2026-09-23)


### Bug Fixes

* **ci:** gate release on formatting and run integration tests after build ([996163e](https://github.com/Questo/Ratatosk/commit/996163ed9eb7b9a1c2e51265a7be86b9281d53c0))

## [0.1.2](https://github.com/Questo/Ratatosk/compare/v0.1.1...v0.1.2) (2026-09-23)


### Bug Fixes

* **deps:** resolve NuGet package downgrade errors after 10.0.11 bumps ([f1043bd](https://github.com/Questo/Ratatosk/commit/f1043bd1331b486445ad2d9bb11efba6756fc128))

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
