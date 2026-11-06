# RimBank — Tech Stack & Architecture Rules

> Part of the RimBank constitution. See also [mission.md](mission.md) and [roadmap.md](roadmap.md).
> Adding, replacing or removing a technology requires an ADR in `docs/adr/`.

## 1. Platform

| Concern | Choice | Notes |
|---|---|---|
| Runtime / language | **.NET (latest GA)**: currently .NET 10 LTS / C# 14 | Pinned via `global.json`. Move to each new GA release after the dependencies support it. |
| Solution format | `RimBank.slnx` | |
| Build settings | `Directory.Build.props` / `Directory.Build.targets` | `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `ImplicitUsings=enable`. Diagnostic severities only in `.editorconfig` files, never suppressed in code ([ADR 0013](../docs/adr/0013-warning-and-package-governance.md)) |
| Package versions | Central Package Management (`Directory.Packages.props`) | No versions in individual `.csproj` files, no `VersionOverride`; committed lock files, locked restore in CI, NuGet audit, nuget.org only via `NuGet.config` ([ADR 0013](../docs/adr/0013-warning-and-package-governance.md)) |
| Database | **PostgreSQL** | One schema per service domain |
| Event store & documents | **Marten** | Event-sourced aggregates (write side); inline and async projections for read models (query side). See §8 |
| Messaging / application pipeline | **Wolverine** | Command and query dispatch, transactional outbox/inbox, integration events, scheduled messages, sagas. Transport is local (in-process, durable) in the modular monolith and RabbitMQ when a context is extracted. |
| API | ASP.NET Core **Minimal APIs** | Built-in OpenAPI document generation and Scalar UI |
| AI agent interface | **Model Context Protocol** using the official C# SDK (`ModelContextProtocol.AspNetCore`) | Streamable HTTP transport at `/mcp`. See §6 |
| Error contract | **RFC 9457 Problem Details** | See §5 and §6 |
| Identity | OpenID Connect / JWT bearer | Keycloak for local development via Aspire |
| Local orchestration | **.NET Aspire** (AppHost + ServiceDefaults) | PostgreSQL, Keycloak and later RabbitMQ as Aspire resources |
| Observability | **OpenTelemetry** (traces, metrics, logs) | Aspire dashboard locally; OTLP exporter in other environments |
| CI | **GitHub Actions** | Build, test, format, CodeQL, Dependabot |
| Containers | `dotnet publish` container images | Non-root, chiseled base images |

## 2. Topology: modular monolith first

- One deployable host (`RimBank.Host`) composes all service domains.
- Each service domain is a fully isolated bounded context with its own PostgreSQL schema **and database role** (it has no grants on other schemas), its own Marten store (event streams, read models and async daemon), no shared tables or views, and no project references to another context's core. Every context's `.Postgresql` registers its store the same way, through `AddServiceDomainStore<TStore>` in `RimBank.Postgresql`: schema `<service_domain>`, login role `rimbank_<service_domain>`, connection string `ConnectionStrings:<ServiceDomain>` ([ADR 0016](../docs/adr/0016-per-context-marten-stores.md)).
- Each context registers itself in the host through a single module entry point, so the host composes contexts without knowing their internals: one `IServiceDomainModule` class per context under `RimBank.Host/Modules/<BusinessDomain>/`, listed explicitly in `ServiceDomainModules.All`, which only calls the context's adapter extension methods ([ADR 0014](../docs/adr/0014-host-composition-and-module-entry-point.md)).
- Contexts communicate **asynchronously through integration events** (Wolverine durable local queues with an outbox). The host runs one Wolverine bus; each context's outbox and inbox tables live in its own schema, its handlers commit through its own store, and its messages go to its own durable local queue. Wolverine's own node bookkeeping lives in the host's `wolverine` schema, on which no context has grants ([ADR 0017](../docs/adr/0017-wolverine-wiring.md)). Synchronous cross-context calls are allowed only through an explicit port and need an ADR.
- Extracting a context into its own service changes only adapters and host wiring, **never the core**.

## 3. Naming & solution layout

### 3.1 Namespace rule

```
RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]
```

- `<BusinessDomain>` and `<ServiceDomain>` use BIAN Service Landscape names in PascalCase.
- **One BIAN service domain = one bounded context.**
- The **core project has no suffix**: `RimBank.Party.PartyReferenceDataDirectory`.
- Adapter projects add a suffix: `.Api`, `.Mcp`, `.Postgresql`, `.Subscriptions`, and later `.RabbitMq` and so on.
- Integration events that other contexts consume live in `.PublishedLanguage` (a DDD term, and the only shared contract). It contains integration events only, never views or domain types.
- `.Subscriptions` is the consuming side (DDD's anti-corruption layer): it subscribes to other contexts' published integration events and translates them into this context's own language (local commands or local reference events). It is the only project allowed to reference another context's `.PublishedLanguage`.

### 3.2 Business domain mapping (initial)

Before a service domain is created, its mapping must be checked against the BIAN v14 Service Landscape and recorded in an ADR.

| Service domain | Business domain | Core namespace |
|---|---|---|
| Party Reference Data Directory | Party | `RimBank.Party.PartyReferenceDataDirectory` |
| Product Directory | ProductManagement | `RimBank.ProductManagement.ProductDirectory` |
| Position Keeping | AccountManagement | `RimBank.AccountManagement.PositionKeeping` |
| Current Account | LoansAndDeposits | `RimBank.LoansAndDeposits.CurrentAccount` |
| Payment Order Initiation | Payments | `RimBank.Payments.PaymentOrderInitiation` |
| Financial Accounting | Finance | `RimBank.Finance.FinancialAccounting` |

### 3.3 Repository layout

```
/
├─ RimBank.slnx
├─ global.json
├─ Directory.Build.props         # shared build settings, central product version
├─ Directory.Build.targets       # build guards: no in-code warning suppression (ADR 0013)
├─ Directory.Packages.props      # the only place package versions live
├─ NuGet.config                  # nuget.org only, package source mapping
├─ .editorconfig                 # code style + solution-wide diagnostic severities; nested ones per project
├─ scripts/                      # repository tooling (roadmap board sync)
├─ specs/                         # constitution (this folder)
├─ docs/adr/                      # architecture decision records
├─ doc-0/                         # project inputs (read-only)
│  ├─ ideas.MD                    # original project brief
│  └─ release14.0.0/              # BIAN v14 specs (ADR 0002)
├─ src/
│  ├─ RimBank.SharedKernel/       # Money, Currency, Result, typed ids, decider abstractions (no infra)
│  ├─ RimBank.Host/               # composition root: wires all contexts (Modules/, ADR 0014), ProblemDetails, the Wolverine bus (Messaging/, ADR 0017), auth, OTel
│  ├─ RimBank.AppHost/            # .NET Aspire orchestration
│  ├─ RimBank.ServiceDefaults/    # Aspire service defaults (OTel, health, resilience)
│  ├─ RimBank.Problems/           # Error → RFC 9457 Problem Details mapping, shared by .Api, .Mcp and the host (ADR 0015)
│  ├─ RimBank.Http/               # HTTP conventions every .Api shares: the Idempotency-Key header (ADR 0018)
│  ├─ RimBank.Postgresql/         # Marten conventions every .Postgresql uses: per-context store, role, views, rebuild (ADR 0016); Wolverine wiring (ADR 0017); command idempotency (ADR 0018)
│  └─ Party/
│     ├─ RimBank.Party.PartyReferenceDataDirectory/                    # core
│     ├─ RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage/
│     ├─ RimBank.Party.PartyReferenceDataDirectory.Api/
│     ├─ RimBank.Party.PartyReferenceDataDirectory.Mcp/
│     ├─ RimBank.Party.PartyReferenceDataDirectory.Postgresql/
│     └─ RimBank.Party.PartyReferenceDataDirectory.Subscriptions/   # only when it consumes other contexts' events
└─ tests/
   ├─ RimBank.Host.Tests/             # host composition, module convention, message bus and health probes (in-process)
   ├─ RimBank.AppHost.Tests/          # Aspire resource model and a real startup with Docker (ADR 0014)
   ├─ RimBank.Architecture.Tests/      # ArchUnitNET rules (CONTRIBUTING "Architecture rules", ADR 0011, ADR 0012)
   ├─ RimBank.Architecture.Fixtures/   # good and bad fake contexts that prove each rule bites (never referenced by src/)
   ├─ RimBank.Problems.Tests/         # the Error → Problem Details mapping (ADR 0015)
   ├─ RimBank.Http.Tests/             # the Idempotency-Key header and its Problem Details (ADR 0018)
   ├─ RimBank.Postgresql.Tests/       # store, messaging and idempotency conventions with two test-only fake contexts, Testcontainers (ADR 0016–0018)
   ├─ RimBank.SharedKernel.Testing/   # shared test helpers: typed ID contract (ADR 0008), FsCheck money generators (ADR 0009), decider/projection helpers (0.6)
   └─ Party/
      ├─ RimBank.Party.PartyReferenceDataDirectory.Tests/              # domain + application (no I/O)
      ├─ RimBank.Party.PartyReferenceDataDirectory.Api.Tests/          # HTTP + Problem Details + BIAN conformance
      ├─ RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests/          # MCP client → tools, Problem Details errors, parity with Api
      └─ RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/   # Testcontainers
```

**Test namespaces:** a test namespace follows the code under test from the outside in, adds the test folder, and always ends with `.Tests`: `<namespace of the project under test>.<folder>.Tests`. For example `RimBank.SharedKernel.MoneyAndCurrency.Tests` (folder `MoneyAndCurrency/` of `RimBank.SharedKernel.Tests`) or `RimBank.Party.PartyReferenceDataDirectory.Registration.Tests`; root files use the project name (`RimBank.SharedKernel.Tests`). Never `….Tests.<Folder>`. Shared test libraries such as `RimBank.SharedKernel.Testing` keep their own name.

### 3.4 Inside a core project: business names only

Folders follow aggregates, BIAN Behavior Qualifiers and business flows. Generic technical folders (`Services`, `Repositories`, `Helpers`, `Managers`, `Utils`, `Dtos`) are forbidden.

```
RimBank.Party.PartyReferenceDataDirectory/
├─ PartyReferenceDataDirectoryEntry/      # Control Record (aggregate root)
│  ├─ PartyReferenceDataDirectoryEntry.cs # state + evolve
│  ├─ PartyReferenceDataDirectoryEntryId.cs
│  ├─ Events.cs
│  └─ Decisions.cs                        # pure decide functions
├─ Registration/                          # business flow (command side): Register / Initiate
│  ├─ RegisterParty.cs                    # command
│  └─ RegisterPartyHandler.cs
├─ Retrieval/                             # business flow (query side): Retrieve
│  ├─ RetrievePartyReferenceDataDirectoryEntry.cs         # query
│  ├─ RetrievePartyReferenceDataDirectoryEntryHandler.cs
│  ├─ PartyReferenceDataDirectoryEntryView.cs             # read model (immutable record)
│  └─ PartyReferenceDataDirectoryEntryViewProjection.cs   # pure evolve: event → view
├─ Demographics/                          # Behavior Qualifier (its own commands, queries, views)
├─ Reference/
├─ Associations/
├─ BankRelations/
├─ PartyDirectory.cs                      # write port: load and append the entry's event stream
└─ PartyDirectoryEnquiry.cs               # read port: query the read models
```

Commands and queries sit next to each other inside the business flow or Behavior Qualifier they belong to. They are told apart by name and by the port they use, not by technical `Commands/` or `Queries/` folders.

## 4. Dependency rules (enforced by architecture tests)

| Project | May reference | Must not reference |
|---|---|---|
| `RimBank.SharedKernel` | BCL only | Everything else |
| Core (`RimBank.X.Y`) | SharedKernel, its own `.PublishedLanguage` | ASP.NET Core, Marten, Wolverine, Npgsql, **any other context's** core, adapters or `.PublishedLanguage` |
| `.PublishedLanguage` | BCL only | Everything else |
| `.Subscriptions` | Its own core, SharedKernel, other contexts' `.PublishedLanguage`, Wolverine | Other contexts' cores or adapters, its own `.Api`, `.Mcp`, `.Postgresql` |
| `.Api` | Its own core, SharedKernel, `RimBank.Problems`, `RimBank.Http`, ASP.NET Core, Wolverine (`IMessageBus`) | `.Postgresql`, other contexts' adapters |
| `.Mcp` | Its own core, SharedKernel, `RimBank.Problems`, MCP C# SDK, Wolverine (`IMessageBus`) | `.Api`, `.Postgresql`, other contexts' adapters |
| `.Postgresql` | Its own core, SharedKernel, `RimBank.Postgresql`, Marten, Wolverine.Marten | `.Api`, `.Mcp`, other contexts |
| `RimBank.Problems` | SharedKernel, ASP.NET Core | Cores, adapters, other composition projects ([ADR 0015](../docs/adr/0015-problem-details-mapping.md)) |
| `RimBank.Http` | SharedKernel, `RimBank.Problems`, ASP.NET Core; used only by `.Api` adapters and `RimBank.Host` | Cores, other adapters, Marten, Wolverine ([ADR 0018](../docs/adr/0018-idempotency-keys-for-commands.md)) |
| `RimBank.Postgresql` | SharedKernel, Marten (with JasperFx, Weasel, Npgsql), Wolverine (`Wolverine.Marten`, `Wolverine.Postgresql`), Microsoft.Extensions abstractions; used only by `.Postgresql` adapters and by `RimBank.Host` for its own message store. It also adds the idempotency policy to every command handler ([ADR 0018](../docs/adr/0018-idempotency-keys-for-commands.md)) | Cores, other adapters, other composition projects, ASP.NET Core ([ADR 0016](../docs/adr/0016-per-context-marten-stores.md), [ADR 0017](../docs/adr/0017-wolverine-wiring.md)) |
| `RimBank.Host` | All adapters and cores (composition only) | Business logic |

**Core and Wolverine:** handlers in the core are plain classes with `Handle` methods. They **return** events and messages (Wolverine's cascading convention) instead of taking framework services, so the core has no package reference to Wolverine or Marten. Persistence goes through ports in the core. The `.Postgresql` adapter implements those ports with Marten: the write port over event streams, and the read port over projected documents.

**CQRS rules (also enforced by architecture tests):**

| Type | May depend on | Must not depend on |
|---|---|---|
| Command handler | Write port, aggregate decisions, its own context's local reference read models used only as decision inputs (see §8.4) | The context's own query read models, query handlers |
| Query handler | Read port, views | Write port, aggregates, decisions |
| Projection (`…Projection`) | Domain events, views | Ports, handlers, `TimeProvider` (time comes from event data) |

The architecture tests tell these roles apart by SharedKernel markers ([ADR 0012](../docs/adr/0012-cqrs-role-markers.md)): commands implement `ICommand`, queries `IQuery<TView>`, write ports `IWritePort` and read ports `IReadPort`. A handler is a class with a `Handle` / `HandleAsync` method whose first parameter is a command or a query, and a view is a type returned by an `IProjection`. Until local reference read models exist (4.2), a command handler may use no read port or view at all.

## 5. API & error contract

- Routes, operation names and resource names follow BIAN, for example `POST /PartyReferenceDataDirectory/Register` and `GET /PartyReferenceDataDirectory/{id}/Retrieve`.
- Request and response models live in `.Api` and are mapped to and from the core. Domain types are never serialised directly.
- **Commands vs queries over HTTP:** BIAN `Retrieve` operations are `GET` endpoints that dispatch a query. All other BIAN operations (`Register`, `Initiate`, `Update`, `Control`, `Execute`, `Exchange`, `Request`, …) are `POST`/`PUT` endpoints that dispatch a command.
- **Command responses** come from the command's outcome (the identifier, stream version and the state produced by the decision). They never read a read model, so they do not depend on projection lag. Where BIAN's response schema expects the full Control Record, it is built from the aggregate state produced by the decision.
- **Query responses** come only from read models. A stream version is returned in an `ETag` header, and a command may send it back in `If-Match` for optimistic concurrency.
- **Every non-2xx response is `application/problem+json` (RFC 9457).**
  - `type`: a stable URN, for example `urn:rimbank:problem:party:party-not-found`
  - `title`, `status`, `detail`, `instance`
  - extensions: `errorCode`, `traceId`, and `errors` (field → messages) for validation failures
- Each core `Error` maps to a Problem Details response in one place for the whole solution, `RimBank.Problems`, which every `.Api` and `.Mcp` uses (`error.ToHttpResult()`); the host emits `shared.not-found` and `shared.unexpected` itself ([ADR 0015](../docs/adr/0015-problem-details-mapping.md)). Unhandled exceptions become a generic 500 Problem Details response with no internal details exposed.
- BIAN-declared statuses are honoured: 400, 401, 403, 404, 429 and 500, plus 409 for concurrency and idempotency conflicts.
- Every state-changing endpoint requires an `Idempotency-Key` header: a missing or invalid one is a 400, and a key reused with a different payload or still in use is a 409 (`RequireIdempotencyKey()` and `IdempotencyKeyHeader` in `RimBank.Http`, [ADR 0018](../docs/adr/0018-idempotency-keys-for-commands.md)).

## 6. MCP (Model Context Protocol) adapter

- **MCP is a driving adapter alongside `.Api`.** The `.Mcp` project sends the same core commands and queries through `IMessageBus`. It never calls the HTTP API and never contains business logic, so the HTTP and MCP channels cannot drift apart.
- **Hosting:** all contexts' MCP tools are registered in `RimBank.Host` and exposed at a single `/mcp` endpoint over Streamable HTTP. When a context is extracted, its `.Mcp` adapter moves with it.
- **Naming:** each tool maps 1:1 to a BIAN operation, named `<service_domain>_<operation>[_<behavior_qualifier>]` in snake_case (for example `party_reference_data_directory_register` and `current_account_retrieve_payment`). Descriptions come from the BIAN operation description, and input schemas reuse the `.Api` request models.
- **Tool annotations:** Retrieve tools dispatch queries and are marked `readOnly`; every other tool dispatches a command. Operations that move money or close facilities are marked `destructive` and are not `idempotent` unless an idempotency key is supplied.
- **Security:** the MCP endpoint follows the MCP authorization spec (OAuth 2.1, with Keycloak as the authorization server). Tool calls go through the same per-BIAN-operation authorisation policies as HTTP. Money-moving tools need a dedicated scope and are disabled by default, so each deployment must turn them on explicitly.
- **Errors:** a business or validation failure returns a tool result with `isError: true` whose content is the Problem Details JSON (same `type`, `errorCode` and `traceId` as HTTP). Stack traces and internal details are never exposed.
- **Audit:** every tool call records the MCP client identity and a correlation ID in event metadata, as HTTP calls do.

## 7. Domain modelling conventions

- **Aggregates:** event-sourced through Marten. Each aggregate is a `sealed record` state with `Evolve` functions, and decisions are pure static functions.
- **IDs:** strongly typed `readonly record struct` wrapping `Guid` v7, implementing `ITypedId<TSelf>` ([ADR 0008](../docs/adr/0008-typed-identifier-pattern.md)).
- **Money:** `Money(decimal Amount, Currency Currency)` using ISO 4217 minor units, with explicit rounding policy. `double` and `float` are never used for amounts.
- **Time:** always injected through `TimeProvider`. `DateTime.Now` is never used.
- **Failures:** `Result<T>` / `Error` for business rules. Exceptions are reserved for programmer errors and infrastructure faults.
- **Events:** past tense, named in BIAN language (`PartyRegistered`, `CurrentAccountFacilityInitiated`). Domain events are internal; integration events in `.PublishedLanguage` are versioned and backwards compatible.
- **Behavior Qualifiers:** modelled as an entity inside the Control Record aggregate by default. A BQ becomes its own aggregate only when it has an independent lifecycle or concurrency hotspot, and that choice needs an ADR.

## 8. Commands, queries & read models (CQRS)

CQRS is applied **logically everywhere** and **physically where needed**. Every context separates its write side (commands → decisions → events) from its read side (events → projections → queries). Separate document shapes, asynchronous projections and dedicated read stores are added only when a query needs them.

### 8.1 Write side

- A **command** is an immutable record named as a BIAN operation in the imperative (`RegisterParty`, `UpdateDemographics`, `ExecutePayment`). It carries an idempotency key (`ICommand.IdempotencyKey`) and, where it updates an existing record, an expected stream version. Each context handles a command once per key: the same key and payload return the first acknowledgement without running the handler again, and only successful outcomes are stored, in the same transaction as the events ([ADR 0018](../docs/adr/0018-idempotency-keys-for-commands.md)).
- A **command handler** loads the aggregate through the write port, calls a pure `Decide`, appends the resulting events and returns an acknowledgement (`id`, `version`, and the decided state when BIAN requires it). It returns `Result<T>`, never a read model.
- **Invariants that protect money** (funds availability, closure rules, holds) are checked against the authoritative write-side state in the same stream and transaction. They are never checked against an eventually consistent projection.
- Decision inputs from another context (for example "is this party known?", "is this product launched?") come from **local reference read models** fed by integration events (see §8.4). Their staleness is acceptable and documented in the context's ADR.

### 8.2 Read side

- A **query** is an immutable record named `Retrieve<Thing>` (or `List<Things>` for collections). A **query handler** reads views through the read port and returns `Result<TView>` (`NotFound` is an `Error`, which becomes a 404 Problem Details response).
- A **view** (read model) is an immutable record shaped for its consumers. It holds no behaviour and is never used to make decisions on the write side.
- A **projection** is a pure function `(view, event) → view`, implemented with Marten projections in `.Postgresql`. The pure part lives in the core so it can be unit tested without a database.

### 8.3 Projection lifecycle (when to use which)

| Need | Choice |
|---|---|
| Single-stream view that callers read right after writing (Retrieve a Control Record) | **Inline** projection, same transaction as the events |
| Cross-stream, list, search, history, reporting (product catalogue, transaction history, trial balance, audit) | **Async** projection run by Marten's async daemon |
| A query that needs another context's data | A local reference read model fed by that context's integration events (see §8.4); never a direct read of its tables or views |
| A query that combines several contexts (for example a customer overview) | A dedicated query-only context fed by integration events (for example BIAN *Customer Position*), or composition by the channel; never a cross-schema join |

- Async views expose their staleness (last processed event sequence) so that callers and tests can wait for or detect lag (`GetViewProgressAsync`, [ADR 0016](../docs/adr/0016-per-context-marten-stores.md)).
- A view is wired by lifecycle with `AddInlineView` / `AddAsyncView`, which run the core's pure projection through a generic Marten adapter that holds no logic, and is rebuilt with `RebuildViewAsync` ([ADR 0016](../docs/adr/0016-per-context-marten-stores.md)).
- Every projection is **rebuildable** from the event store. A projection change that alters its shape ships with a rebuild step, documented in the PR.
- Read models live in the context's own PostgreSQL schema, like its events, and each context runs its own async daemon, so a stuck projection in one context never blocks another.

### 8.4 Read models across contexts (loose coupling)

- **Ownership:** a view belongs to exactly one context and is reachable only through that context's read port, `.Api` and `.Mcp`. Views are never shared, never put in `.PublishedLanguage`, and never read by another context.
- **Producer side:** integration events are mapped from domain events (through the outbox), never from read models. They carry the state consumers need (event-carried state transfer), are versioned, and only change in backwards-compatible ways. A contract test pins each published event's schema.
- **Consumer side:** the `.Subscriptions` adapter receives the integration event (idempotently, through Wolverine's durable inbox), translates it into the consumer's own types, and records it as a local reference event in the consumer's own store. The local reference read model is projected from those local events, so it can be rebuilt **without the producer**.
- **Cross-context requests are events, not commands.** A context never sends another context's command, because commands are internal to the context that owns them and are never in `.PublishedLanguage`. When context A needs context B to act, A publishes a request event in A's `.PublishedLanguage` (for example `PaymentOrderExecutionRequested`). B's `.Subscriptions` adapter turns it into one of B's own commands, decided by B's rules. B then publishes the outcome (for example `CurrentAccountPaymentExecuted` or `CurrentAccountPaymentRejected`), and A's `.Subscriptions` adapter reacts to it. The requesting record's identifier (for example the payment order ID) is the correlation ID and the idempotency key of the translated command, so a redelivered request never moves money twice.
- **Minimal copies:** a local reference read model keeps only the fields its context needs (for example "party is known and active", not the party's demographics). This also limits the spread of PII.
- **Tolerant reader:** consumers ignore unknown fields and accept events out of order or more than once.
- **Enforcement:** architecture tests forbid cross-context references other than `.Subscriptions` → `.PublishedLanguage`. Database roles forbid cross-schema reads, and an integration test proves it.

## 9. Testing stack

| Kind | Tooling | Scope |
|---|---|---|
| Unit | xUnit v3, Shouldly | Pure domain decisions, projections (Given events → Then view) and handlers (no I/O) |
| Property-based | FsCheck | Money arithmetic, balance invariants, evolve/decide laws, projection/aggregate agreement |
| Integration | Testcontainers (PostgreSQL), Alba | Marten persistence and projections (inline and async daemon), Wolverine outbox, HTTP endpoints end to end |
| Architecture | ArchUnitNET | Dependency rules and CQRS rules in §4, naming rules in §3, cross-context isolation in §8.4 |
| Contract | OpenAPI/AsyncAPI conformance tests; published-event schema snapshots | Implemented routes and events match the BIAN v14 ext-DDD YAML; published integration events stay backwards compatible |
| MCP | MCP C# SDK client in-process against the test host | Tool list, input schemas, results, Problem Details errors, and parity with the HTTP endpoint for the same operation |
| Mutation (later) | Stryker.NET | Core projects of money-moving domains |

## 10. Security & supply chain

- Every endpoint requires authentication by default. Authorisation policies are defined per BIAN operation.
- PII is masked in logs, and sensitive fields are protected at rest.
- Secrets come from user-secrets locally and from a vault in deployed environments. They are never in source.
- GitHub: CodeQL, Dependabot, secret scanning, SBOM generation on release, and signed release tags.
