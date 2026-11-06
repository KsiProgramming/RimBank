# Marten wiring

Roadmap phase: 0.10
Branch: plan/0.10-marten-wiring

## Context
Every context in Stage 1 and later stores its events and read models with Marten (tech-stack §1, §8). Before the first `.Postgresql` adapter is written (1.4), the convention it follows has to exist and be proven: one store per context, with its own schema, its own database role and its own async daemon (tech-stack §2, §8.3), inline and async projections built from the pure `IProjection` contract (ADR 0010), and every read model rebuildable (mission quality bar "Rebuildable reads").

This phase depends on 0.6 (`IDecider` / `IProjection`), 0.7 (architecture tests and the fixture stubs) and 0.8 (Host, AppHost with the `rimbank` database, ADR 0014). All are merged. It unblocks 0.11 (Wolverine's outbox needs a Marten store), 1.4 (the first write adapter) and 1.7 (the first inline projection).

Relevant constitution:
- [mission.md](../mission.md), principles 4 (event-sourced truth), 5 (CQRS), 7 (autonomous contexts: "read models are private", "failure stays local"), 9 (production-grade), and the non-goal "no speculative coverage". No real context exists yet, so the convention is proven with **two test-only fake contexts**, as 0.8 proved modules with fake modules.
- [tech-stack.md §2](../tech-stack.md#2-topology-modular-monolith-first): own schema **and database role** with no grants on other schemas, own Marten store, own async daemon.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): `.Postgresql` may reference its core, SharedKernel, Marten. This phase adds a row for the shared `RimBank.Postgresql` project.
- [tech-stack.md §8.2–8.4](../tech-stack.md#8-commands-queries--read-models-cqrs): the pure projection lives in the core, and its Marten implementation lives in `.Postgresql`. Inline is for single-stream read-your-write views, async is for everything else. Async views expose their staleness. Every projection can be rebuilt. Database roles forbid cross-schema reads, "and an integration test proves it".
- ADR 0010 (decider and projection contracts), ADR 0011 (namespace rule), ADR 0013 (packages and warnings), ADR 0014 (module entry point: adapters expose `Add…` extensions and the Host module only calls them).

**CQRS split:** this phase adds no BIAN command or query. It adds the plumbing for both sides:
- **write side:** appending to and loading an event stream in the context's store (what a write port will use in 1.4).
- **read side:** inline projections (same transaction as the append, for read-your-write views) and async projections (run by the context's own daemon), both adapted from a pure `IProjection<TView, TEvent>`.

The fake contexts use one inline view and one async view, to prove both lifecycles. There is no BIAN service domain in this phase, so there is no HTTP or MCP surface and no parity test.

## Scope
### In scope
- **`src/RimBank.Postgresql`**: a new shared library that every context's `.Postgresql` adapter uses. It references SharedKernel and Marten only. It contains:
  - **Store convention:** `builder.AddServiceDomainStore<TStore>(serviceDomain, configure)`, where `TStore : IDocumentStore` is a marker interface declared in the context's `.Postgresql` (for example `IPartyReferenceDataDirectoryStore`). It registers a Marten **ancillary store** (`AddMartenStore<TStore>`; there is no main `AddMarten` store, so no context owns a "default" store) with:
    - schema `<service_domain>` (snake_case of the service domain) for both events and documents;
    - the connection string `ConnectionStrings:<ServiceDomain>`, whose user is the context's own login role `rimbank_<service_domain>`;
    - its own async daemon (`DaemonMode.HotCold`);
    - `AutoCreate.CreateOrUpdate` + `ApplyAllDatabaseChangesOnStartup` outside Production and `AutoCreate.None` in Production;
    - a health check `postgresql-<service-domain>` tagged `ready`, which connects with the context's own role;
    - the `Marten` activity source and meter, picked up by the existing OpenTelemetry setup.
  - **Projection adapters:** `opts.AddInlineView<TView, TProjection, TEvent>()` and `opts.AddAsyncView<…>()`. They wrap a pure `IProjection<TView, TEvent>` as a Marten single-stream projection: the adapter only calls `TProjection.Project(view, event)`, and a `null` result deletes the view. No logic lives in the adapter.
  - **Staleness:** `store.GetViewProgressAsync(projectionName)` returns the last processed event sequence next to the store's high-water mark (tech-stack §8.3).
  - **Rebuild entry point:** `store.RebuildViewAsync(projectionName, timeout, ct)` as a library API, used by tests now and by the operations runbook in 7.4.
  - **Role and schema provisioner:** `ServiceDomainDatabase.ProvisionAsync(administrator, serviceDomain, contextConnectionString)`. It is idempotent and creates the login role (user and password taken from the context's connection string), creates the schema with that role as owner, and gives it no grants on any other schema. It runs at startup only when explicitly enabled (Development and tests). Production expects roles and schemas provisioned outside the application.
- **`tests/RimBank.Postgresql.Tests`**: unit tests for the projection adapters, plus Testcontainers smoke tests with two fake contexts (`FakeLedger` and `FakeRegistry`, both test-only).
- **Architecture rules** (each with a bad fixture):
  - `shared-postgresql-only-in-postgresql-adapters`: only a context's `.Postgresql` adapter depends on `RimBank.Postgresql`;
  - `shared-postgresql-dependencies`: `RimBank.Postgresql` depends on no core, adapter, composition project or ASP.NET Core;
  - Marten and Npgsql outside `.Postgresql` (and therefore a store marker anywhere else) are already caught by the existing `core-`, `api-`, `mcp-` and `subscriptions-dependencies` rules.
- **Constitution updates:** a `RimBank.Postgresql` row in tech-stack §4, entries in the §3.3 layout, and the `.Postgresql` row saying it may reference `RimBank.Postgresql`.
- **ADR 0016**: per-context Marten stores, roles and projection conventions.

### Out of scope
- Wolverine, `IMessageBus`, `IntegrateWithWolverine` and the outbox (0.11).
- Multi-stream (cross-stream) projection adapters. They are added with the first need, the product catalogue (2.5).
- A real context's store, write port or read port (1.4, 1.7). The fake contexts never ship in `src/`.
- A database health check on the Host itself. With no context registered, the Host opens no connection. Each store brings its own check.
- Per-context connection-string parameters in the AppHost (added with the first real context, 1.4). The AppHost is unchanged.
- An operations surface for rebuild (CLI verb or endpoint), async daemon health checks, projection-lag metrics and alerts (7.4).
- Production schema migrations or patch generation (decided in 7.5, with the container images).
- PII protection at rest (7.2).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Where the convention lives | New `src/RimBank.Postgresql` (SharedKernel + Marten), used by every `.Postgresql` | Adapters cannot reference the Host (ADR 0014). Without a shared library, each context would reinvent the wiring. This mirrors `RimBank.Problems` (ADR 0015). It is not named `RimBank.Marten`, because that namespace would hide `Marten` inside every `RimBank.*` file. |
| Store per context | Marten **ancillary** store `AddMartenStore<TStore>()` per context, with no main store | Tech-stack §2 "own Marten store, own async daemon". No context is privileged, and a stuck daemon stays local (principle 7). |
| Schema and role names | Schema `<service_domain>`, login role `rimbank_<service_domain>`, connection string `ConnectionStrings:<ServiceDomain>` | Lower-case snake_case is safe in PostgreSQL. One source for the user and password: the provisioner reads them from the connection string. |
| Role isolation | Each role owns only its schema and is given nothing on others | Tech-stack §8.4: "database roles forbid cross-schema reads". A test proves it. |
| Who provisions roles | Code provisioner using the admin connection, enabled only in Development and tests; Production provisions outside the app | Testable per context, and the role list is not duplicated in SQL scripts. The app never needs superuser rights in production (principle 9). |
| Projection adapter | Generic Marten single-stream projection that calls the pure `IProjection.Project` (null deletes) | The pure function stays in the core and is unit tested (§8.2). The adapter cannot hold logic, and the Given/Then helpers stay the reference. |
| Daemon mode | `HotCold` | Safe with several host instances: only one runs each store's daemon. |
| Schema changes | Auto-create outside Production, `None` in Production | Fast feedback locally. Production migrations are an explicit later decision (7.5). |
| Health | One `ready` check per store, using the context's role | Shows each context's database access on its own. No change for the Host until a context exists. |
| Rebuild | Library API now, ops surface in 7.4 | Meets "a projection rebuild entry point" without pulling 7.4 forward. |

## ADRs to write
- **0016 Per-context Marten stores and projection conventions**: why `RimBank.Postgresql` exists and what it may reference; ancillary stores with no main store; schema, role and connection-string naming; role isolation and who provisions it in each environment; the inline and async adapter over `IProjection`; daemon mode; auto-create policy; health checks; staleness and rebuild APIs.

## Open questions
Answered during implementation:
- **View identity:** a view has a `Guid Id` equal to its stream's id, and the adapter is a `SingleStreamProjection<TView, Guid>`. How a view exposes a typed ID (for example a mapped `PartyReferenceDataDirectoryEntryId` in the `.Api` response) is decided with the first real view in 1.6. ADR 0016 records this.
- **Marten API check:** the current Marten is **9.58.0**, not 8. `AddMartenStore<T>` + `AddAsyncDaemon(DaemonMode.HotCold)` + `ApplyAllDatabaseChangesOnStartup` on ancillary stores with **no main store** works. All Testcontainers tests pass with two such stores in one host, and `IDocumentStore` itself is not registered.
- **Event type naming:** explicit. Each context maps every event with `options.Events.MapEventType<TEvent>("snake_case_name")` (ADR 0016), and the fakes show the pattern. This is a review rule for now, not a test.
- **Additional finding:** `projections-implement-contract` (ADR 0010) caught the first name of the Marten adapter, `ViewProjection<…>`: the `…Projection` suffix is reserved for pure projections. The adapter is now `ViewProjector<…>`.
- **Additional finding:** pinning `Microsoft.Extensions.Hosting(.Abstractions)` centrally needs 10.0.14 (`Microsoft.AspNetCore.Mvc.Testing` 10.0.14 and `Microsoft.Extensions.Http.Resilience` 10.11.0 already need at least 10.0.13 or 10.0.14). With transitive pinning, that moves the Host's and AppHost's transitive `Microsoft.Extensions.*` packages from 10.0.12 to 10.0.14 (a patch update), in both the win-x64 and linux-x64 lock files.
