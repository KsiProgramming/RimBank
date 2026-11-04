# RimBank — Tech Stack & Architecture Rules

> Part of the RimBank constitution. See also [mission.md](mission.md) and [roadmap.md](roadmap.md).
> Adding, replacing or removing a technology requires an ADR in `docs/adr/`.

## 1. Platform

| Concern | Choice | Notes |
|---|---|---|
| Runtime / language | **.NET (latest GA)**: currently .NET 10 LTS / C# 14 | Pinned via `global.json`. Move to each new GA release after the dependencies support it. |
| Solution format | `RimBank.slnx` | |
| Build settings | `Directory.Build.props` | `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `ImplicitUsings=enable` |
| Package versions | Central Package Management (`Directory.Packages.props`) | No versions in individual `.csproj` files |
| Database | **PostgreSQL** | One schema per service domain |
| Event store & documents | **Marten** | Event-sourced aggregates, projections for read models |
| Messaging / application pipeline | **Wolverine** | Command handling, transactional outbox/inbox, integration events, scheduled messages, sagas. Transport is local (in-process, durable) in the modular monolith and RabbitMQ when a context is extracted. |
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
- Each service domain is a fully isolated bounded context with its own PostgreSQL schema, its own Marten event streams, no shared tables, and no project references to another context's core.
- Contexts communicate **asynchronously through integration events** (Wolverine durable local queues with an outbox). Synchronous cross-context calls are allowed only through an explicit port and need an ADR.
- Extracting a context into its own service changes only adapters and host wiring, **never the core**.

## 3. Naming & solution layout

### 3.1 Namespace rule

```
RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]
```

- `<BusinessDomain>` and `<ServiceDomain>` use BIAN Service Landscape names in PascalCase.
- **One BIAN service domain = one bounded context.**
- The **core project has no suffix**: `RimBank.Party.PartyReferenceDataDirectory`.
- Adapter projects add a suffix: `.Api`, `.Mcp`, `.Postgresql`, and later `.RabbitMq` and so on.
- Integration events that other contexts consume live in `.PublishedLanguage` (a DDD term, and the only shared contract).

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
├─ Directory.Build.props
├─ Directory.Packages.props
├─ specs/                         # constitution (this folder)
├─ docs/adr/                      # architecture decision records
├─ release14.0.0/                 # BIAN v14 specs (read-only input)
├─ src/
│  ├─ RimBank.SharedKernel/       # Money, Currency, Result, typed ids, decider abstractions (no infra)
│  ├─ RimBank.Host/               # composition root: wires all contexts, ProblemDetails, auth, OTel
│  ├─ RimBank.AppHost/            # .NET Aspire orchestration
│  ├─ RimBank.ServiceDefaults/    # Aspire service defaults (OTel, health, resilience)
│  └─ Party/
│     ├─ RimBank.Party.PartyReferenceDataDirectory/                    # core
│     ├─ RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage/
│     ├─ RimBank.Party.PartyReferenceDataDirectory.Api/
│     ├─ RimBank.Party.PartyReferenceDataDirectory.Mcp/
│     └─ RimBank.Party.PartyReferenceDataDirectory.Postgresql/
└─ tests/
   ├─ RimBank.Architecture.Tests/
   └─ Party/
      ├─ RimBank.Party.PartyReferenceDataDirectory.Tests/              # domain + application (no I/O)
      ├─ RimBank.Party.PartyReferenceDataDirectory.Api.Tests/          # HTTP + Problem Details + BIAN conformance
      ├─ RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests/          # MCP client → tools, Problem Details errors, parity with Api
      └─ RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/   # Testcontainers
```

### 3.4 Inside a core project: business names only

Folders follow aggregates, BIAN Behavior Qualifiers and business flows. Generic technical folders (`Services`, `Repositories`, `Helpers`, `Managers`, `Utils`, `Dtos`) are forbidden.

```
RimBank.Party.PartyReferenceDataDirectory/
├─ PartyReferenceDataDirectoryEntry/      # Control Record (aggregate root)
│  ├─ PartyReferenceDataDirectoryEntry.cs # state + evolve
│  ├─ PartyReferenceDataDirectoryEntryId.cs
│  ├─ Events.cs
│  └─ Decisions.cs                        # pure decide functions
├─ Registration/                          # business flow: Register / Initiate
│  ├─ RegisterParty.cs                    # command
│  └─ RegisterPartyHandler.cs
├─ Demographics/                          # Behavior Qualifier
├─ Reference/
├─ Associations/
├─ BankRelations/
└─ PartyDirectory.cs                      # port, named by business role
```

## 4. Dependency rules (enforced by architecture tests)

| Project | May reference | Must not reference |
|---|---|---|
| `RimBank.SharedKernel` | BCL only | Everything else |
| Core (`RimBank.X.Y`) | SharedKernel, its own `.PublishedLanguage`, other contexts' `.PublishedLanguage` | ASP.NET Core, Marten, Wolverine, Npgsql, any other context's core or adapters |
| `.PublishedLanguage` | BCL only | Everything else |
| `.Api` | Its own core, SharedKernel, ASP.NET Core, Wolverine (`IMessageBus`) | `.Postgresql`, other contexts' adapters |
| `.Mcp` | Its own core, SharedKernel, MCP C# SDK, Wolverine (`IMessageBus`) | `.Api`, `.Postgresql`, other contexts' adapters |
| `.Postgresql` | Its own core, SharedKernel, Marten, Wolverine.Marten | `.Api`, `.Mcp`, other contexts |
| `RimBank.Host` | All adapters and cores (composition only) | Business logic |

**Core and Wolverine:** handlers in the core are plain classes with `Handle` methods. They **return** events and messages (Wolverine's cascading convention) instead of taking framework services, so the core has no package reference to Wolverine or Marten. Persistence goes through ports in the core. The `.Postgresql` adapter implements those ports with Marten.

## 5. API & error contract

- Routes, operation names and resource names follow BIAN, for example `POST /PartyReferenceDataDirectory/Register` and `GET /PartyReferenceDataDirectory/{id}/Retrieve`.
- Request and response models live in `.Api` and are mapped to and from the core. Domain types are never serialised directly.
- **Every non-2xx response is `application/problem+json` (RFC 9457).**
  - `type`: a stable URN, for example `urn:rimbank:problem:party:party-not-found`
  - `title`, `status`, `detail`, `instance`
  - extensions: `errorCode`, `traceId`, and `errors` (field → messages) for validation failures
- Each core `Error` maps to a Problem Details response in one place per context (`.Api`). Unhandled exceptions become a generic 500 Problem Details response with no internal details exposed.
- BIAN-declared statuses are honoured: 400, 401, 403, 404, 429 and 500, plus 409 for concurrency and idempotency conflicts.
- Every state-changing endpoint requires an `Idempotency-Key` header.

## 6. MCP (Model Context Protocol) adapter

- **MCP is a driving adapter alongside `.Api`.** The `.Mcp` project sends the same core commands and queries through `IMessageBus`. It never calls the HTTP API and never contains business logic, so the HTTP and MCP channels cannot drift apart.
- **Hosting:** all contexts' MCP tools are registered in `RimBank.Host` and exposed at a single `/mcp` endpoint over Streamable HTTP. When a context is extracted, its `.Mcp` adapter moves with it.
- **Naming:** each tool maps 1:1 to a BIAN operation, named `<service_domain>_<operation>[_<behavior_qualifier>]` in snake_case (for example `party_reference_data_directory_register` and `current_account_retrieve_payment`). Descriptions come from the BIAN operation description, and input schemas reuse the `.Api` request models.
- **Tool annotations:** Retrieve operations are marked `readOnly`. Operations that move money or close facilities are marked `destructive` and are not `idempotent` unless an idempotency key is supplied.
- **Security:** the MCP endpoint follows the MCP authorization spec (OAuth 2.1, with Keycloak as the authorization server). Tool calls go through the same per-BIAN-operation authorisation policies as HTTP. Money-moving tools need a dedicated scope and are disabled by default, so each deployment must turn them on explicitly.
- **Errors:** a business or validation failure returns a tool result with `isError: true` whose content is the Problem Details JSON (same `type`, `errorCode` and `traceId` as HTTP). Stack traces and internal details are never exposed.
- **Audit:** every tool call records the MCP client identity and a correlation ID in event metadata, as HTTP calls do.

## 7. Domain modelling conventions

- **Aggregates:** event-sourced through Marten. Each aggregate is a `sealed record` state with `Evolve` functions, and decisions are pure static functions.
- **IDs:** strongly typed `readonly record struct` wrapping `Guid` v7.
- **Money:** `Money(decimal Amount, Currency Currency)` using ISO 4217 minor units, with explicit rounding policy. `double` and `float` are never used for amounts.
- **Time:** always injected through `TimeProvider`. `DateTime.Now` is never used.
- **Failures:** `Result<T>` / `Error` for business rules. Exceptions are reserved for programmer errors and infrastructure faults.
- **Events:** past tense, named in BIAN language (`PartyRegistered`, `CurrentAccountFacilityInitiated`). Domain events are internal; integration events in `.PublishedLanguage` are versioned and backwards compatible.
- **Behavior Qualifiers:** modelled as an entity inside the Control Record aggregate by default. A BQ becomes its own aggregate only when it has an independent lifecycle or concurrency hotspot, and that choice needs an ADR.

## 8. Testing stack

| Kind | Tooling | Scope |
|---|---|---|
| Unit | xUnit v3, Shouldly | Pure domain decisions and application handlers (no I/O) |
| Property-based | FsCheck | Money arithmetic, balance invariants, evolve/decide laws |
| Integration | Testcontainers (PostgreSQL), Alba | Marten persistence, Wolverine outbox, HTTP endpoints end to end |
| Architecture | ArchUnitNET | Dependency rules in §4, naming rules in §3 |
| Contract | OpenAPI/AsyncAPI conformance tests | Implemented routes and events match the BIAN v14 ext-DDD YAML |
| MCP | MCP C# SDK client in-process against the test host | Tool list, input schemas, results, Problem Details errors, and parity with the HTTP endpoint for the same operation |
| Mutation (later) | Stryker.NET | Core projects of money-moving domains |

## 9. Security & supply chain

- Every endpoint requires authentication by default. Authorisation policies are defined per BIAN operation.
- PII is masked in logs, and sensitive fields are protected at rest.
- Secrets come from user-secrets locally and from a vault in deployed environments. They are never in source.
- GitHub: CodeQL, Dependabot, secret scanning, SBOM generation on release, and signed release tags.
