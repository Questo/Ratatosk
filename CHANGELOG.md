# [2.0.0](https://github.com/Questo/Ratatosk/compare/v1.1.0...v2.0.0) (2026-10-19)


* feat(api)!: wrap GET /products and PUT /products/{id} in the Response envelope ([d0e334f](https://github.com/Questo/Ratatosk/commit/d0e334f4cfa33d24d0fb9b0cc42cabe5d71c5080))


### Bug Fixes

* **api:** wrap GET /inventory in the response envelope ([54b8bcf](https://github.com/Questo/Ratatosk/commit/54b8bcfcc45bc80fd9d7f13d69d821c6ec4374e7))
* **api:** wrap GET /orders and GET /orders/{orderId} in the response envelope ([64edd02](https://github.com/Questo/Ratatosk/commit/64edd029ab449d25dbdc8c0b2d8f7b38a9d537a5))
* **identity:** look up users by normalized email in seed, sign-up and login ([6a6add0](https://github.com/Questo/Ratatosk/commit/6a6add0097c0ae5e208eea1f09bf77db6be77bea))
* **web:** collapse the navbar behind a burger on small screens ([9553959](https://github.com/Questo/Ratatosk/commit/955395987af4899b89ddae87c33baf953ad88bc5))
* **web:** keep the open-entity seq increasing across navbar clicks ([a062b79](https://github.com/Questo/Ratatosk/commit/a062b797e8cee17d1e27e2a3cb9a65d6d9363e6f))
* **web:** let the login card shrink on narrow screens ([d92bfc5](https://github.com/Questo/Ratatosk/commit/d92bfc5da512239f90529a7f77b1464c3f4d8e86))
* **web:** put the History position line under the step buttons ([c127f5c](https://github.com/Questo/Ratatosk/commit/c127f5c032fe97a3caf7e8da3f07c04ad7b3766c))
* **web:** start every new search term on page 1, including a cleared one ([d05c521](https://github.com/Questo/Ratatosk/commit/d05c52129e24d2f11d9f060db2d29fc372f96f52))
* **web:** stream /api/changes through nginx unbuffered ([3454da9](https://github.com/Questo/Ratatosk/commit/3454da9f7658e22868444f5f8c50b9cb13c05332))


### Features

* **api:** add Admin-only GET /orders/{orderId}/history replaying the Order's events ([f8bb595](https://github.com/Questo/Ratatosk/commit/f8bb5954f6562375b747ea895817e89e9ed42364))
* **api:** add Admin-only GET /search and envelope GET /products/{id} ([8920abe](https://github.com/Questo/Ratatosk/commit/8920abe34f0a0a9ec63351b580bec6155194cf8c))
* **api:** add optional maxAvailable low-stock filter to GET /inventory ([9be12f4](https://github.com/Questo/Ratatosk/commit/9be12f4d5ecc434b13fa950ed9ee29b53ebdf23b))
* **api:** require Admin for catalog and stock writes, rename unreserve to release ([a15de1c](https://github.com/Questo/Ratatosk/commit/a15de1c90204d53e28b5bfd377e6656222032292))
* **api:** stream change signals to Admins over SSE at GET /changes ([3cfe3d4](https://github.com/Questo/Ratatosk/commit/3cfe3d45779637dbbe2318e1b64de1593f6ee942))
* **app:** add SearchQuery fanning out to Product and Order search providers ([bee270b](https://github.com/Questo/Ratatosk/commit/bee270b046d46837164c1a44edd84e80ab05add6))
* **auth:** add GET /auth/me returning the caller's email and role ([4bcb017](https://github.com/Questo/Ratatosk/commit/4bcb017346f434e9475c7dfd4324e08e51f33df9))
* **auth:** set token cookies on login and refresh, accept the JWT cookie, add logout ([5dd5da3](https://github.com/Questo/Ratatosk/commit/5dd5da309ee802dbed63c0547ce580e6dd6511e6))
* **changes:** publish change signals after the read models through an in-process broadcaster ([6e7a01a](https://github.com/Questo/Ratatosk/commit/6e7a01ac849892a149e317e8834594ecd5c27e37))
* **core:** add IAggregateRepository.LoadHistoryAsync for an aggregate's full stream ([902fbb6](https://github.com/Questo/Ratatosk/commit/902fbb6cd0a22c85e348ec62ebb283fabf43fe74))
* **identity:** seed an Admin User at startup from Admin configuration ([8f81d8e](https://github.com/Questo/Ratatosk/commit/8f81d8ea0b87d2c18a8cffe2435756239f137b22))
* **inventory:** add an Admin-only paged stock list at GET /inventory ([d347a38](https://github.com/Questo/Ratatosk/commit/d347a38b2a45f31cf1a89b82fef4413f9ac01f32))
* **ordering:** record the cancellation reason in the Order read model ([2bade43](https://github.com/Questo/Ratatosk/commit/2bade43f0251da813ba6ff2ec30b06c0f0089f8a))
* **orders:** let Admins list every order, filterable by status and customer ([6885971](https://github.com/Questo/Ratatosk/commit/688597197e823be7b410b3ba5912064fe5e9bdee))
* **orders:** take the customer from the token and hide other customers' orders ([7a31804](https://github.com/Questo/Ratatosk/commit/7a3180462cfaad667a71f2089742edb0d55ed7bf))
* **persistence:** migrate Created order read models to Placed on startup ([a5b7f3e](https://github.com/Questo/Ratatosk/commit/a5b7f3e6ddb977b0c5d3d91444c12320e27cc824))
* **web:** add a Ctrl/Cmd+K search palette that opens Products and Orders ([8bdf830](https://github.com/Questo/Ratatosk/commit/8bdf8309447c43ce555e80fdfae5305c33cb72be))
* **web:** add a Dashboard landing screen with Order counts, low stock and recent activity ([19015cb](https://github.com/Questo/Ratatosk/commit/19015cb4435fdeb33cdf8fb64adbf0be5c67f545))
* **web:** add a History tab that steps through an Order's events ([f0caa52](https://github.com/Questo/Ratatosk/commit/f0caa5277f46a7ca7312c71bff9d3a8f32f9f524))
* **web:** add put and delete to the API client ([a76f24a](https://github.com/Questo/Ratatosk/commit/a76f24a59f6743e2577c7d10c4c7513b2d2d44b7))
* **web:** add the Orders screens, updated live from the change stream ([a3e9d15](https://github.com/Questo/Ratatosk/commit/a3e9d156f24abc678fd7281e9980e6082f37f458))
* **web:** add the Products screen with search, paging, add, edit and removal ([34c2801](https://github.com/Questo/Ratatosk/commit/34c28012bb7792d0766ae865efe61df9737256ef))
* **web:** add the Stock screen and a navbar to switch screens ([ab9603d](https://github.com/Questo/Ratatosk/commit/ab9603dbdaee951bd01af688976499f81bd67546))
* **web:** scaffold the back-office app with Admin login and app shell ([28775be](https://github.com/Questo/Ratatosk/commit/28775be629c3620800c91ce4d984a5ca15347807))
* **web:** serve the web app from nginx in docker-compose and keep it out of the API image ([d55d6c7](https://github.com/Questo/Ratatosk/commit/d55d6c7d1443b02be6f10223da6781912a44fc43))


### BREAKING CHANGES

* GET /products returns the page under `data`, and PUT /products/{id} returns a Response whose `message` carries the validation error instead of a bare string.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>

# [1.1.0](https://github.com/Questo/Ratatosk/compare/v1.0.0...v1.1.0) (2026-10-17)


### Features

* **catalog:** add minPrice and maxPrice filters to product search ([bea5b81](https://github.com/Questo/Ratatosk/commit/bea5b813fc364dea68d4fe29377053212caf7b41))
* **catalog:** return specific product name validation errors ([bc76763](https://github.com/Questo/Ratatosk/commit/bc767633ccb1d22263b9a7631d37f2420e6c4c31))
* **factory:** track tokens per agent run and show them on the dashboard ([7ed54c3](https://github.com/Questo/Ratatosk/commit/7ed54c3a2ea1678b3fd1085d4e4690f9f1754ea2))
* **identity:** give users a stable id and carry it in the token ([72af39d](https://github.com/Questo/Ratatosk/commit/72af39df15d5d25846cf7d1a301f8763acc52e91))
* **ordering:** list the signed-in customer's orders at GET /orders ([5512411](https://github.com/Questo/Ratatosk/commit/55124116e65386ea3a65f37d3b72ee9b36f8a6dc))

# [1.0.0](https://github.com/Questo/Ratatosk/compare/v0.3.0...v1.0.0) (2026-10-17)


* feat!: deepen the commit path, stock model and order reservation ([#135](https://github.com/Questo/Ratatosk/issues/135)) ([15dad27](https://github.com/Questo/Ratatosk/commit/15dad270edac201300fb7297ae4c67536c2c9c5c))


### BREAKING CHANGES

* the stock record returned by /inventory gains onHand and
version, and available now means on hand minus reserved (it used to be
the amount on hand). Existing databases are upgraded on startup.

* fix(products): reject an invalid description or price on update

An invalid description or price produced no value, which the handler read as "omitted": it kept the old one and reported success. Both results are now checked and their failure returned.

* refactor(core): remove AggregateRoot.Rehydrate, which only tests used

The repository loads through LoadFromHistory; the tests now do too.

* refactor(infra): rename ProjectionRegistrationService to EventHandlerDispatcher

It delivers every event to every IDomainEventHandler, not only projections.

* test(auth): drive sign-up through TestHost

Sign-up tests now run on the production wiring with in-memory user and refresh-token stores, asserting on what was stored. Only the password hasher and token issuer, the true externals, are mocked.

* docs: align docs and glossary with the commit sequence, stock terms and renames

CLAUDE.md: PostgreSQL is always required; the commit sequence snapshots after the commit, best-effort; non-transactional appends are published even when the transaction fails; InventoryProjection rebuilds from the aggregate. GLOSSARY.md: defines on hand, and names the stored events that keep the avoided terms (StockAdded, StockReservationFailed).

* docs: remove the Ordering bounded context spec and plan

The Ordering context is implemented; the design spec and implementation plan are no longer needed.

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
