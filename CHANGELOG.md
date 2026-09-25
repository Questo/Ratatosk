# [0.3.0](https://github.com/Questo/Ratatosk/compare/v0.2.0...v0.3.0) (2026-09-25)


### Bug Fixes

* **ordering:** address critical/important findings from final review ([d4a0007](https://github.com/Questo/Ratatosk/commit/d4a00078b11c0b9a780c8a424d1efd3c78743f34))


### Features

* **inventoring:** correlate stock reservation events with an order id ([bca29fb](https://github.com/Questo/Ratatosk/commit/bca29fb9bc8891f9b5c0f287de1a9ab98c88c084))
* **inventoring:** react to order placement and cancellation events ([9f5492d](https://github.com/Questo/Ratatosk/commit/9f5492db516f0f0e4d69d38277845d50d4963154))
* **ordering:** add PlaceOrder/GetOrderById use cases and IOrderService ([91ee056](https://github.com/Questo/Ratatosk/commit/91ee0563d03aaad9a10e981a50de60c9bbbe8749))
* **ordering:** add Postgres read-model persistence and event serialization ([78c13ae](https://github.com/Questo/Ratatosk/commit/78c13aef65e0297d9cc64498b0a235118fd8e8fe))
* **ordering:** add read-model projection and reservation-outcome handlers ([1ac9bf8](https://github.com/Questo/Ratatosk/commit/1ac9bf8ae56e0f9b0dc13027b7bb65dc480a5056))
* **ordering:** expose PlaceOrder and GetOrderById API endpoints ([d9b63f0](https://github.com/Questo/Ratatosk/commit/d9b63f01ec9c23c798173a39236b9e11ccd371df))
* **ordering:** implement Order aggregate with line-by-line reservation tracking ([37540cf](https://github.com/Questo/Ratatosk/commit/37540cf1b3dacf424535e196ffdc92c02ef69dfe))

# [0.2.0](https://github.com/Questo/Ratatosk/compare/v0.1.4...v0.2.0) (2026-09-25)


### Features

* **inventory:** implement Inventoring bounded context ([35bef7d](https://github.com/Questo/Ratatosk/commit/35bef7d2621bcdfcb139bc6c594852e461882822))

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
