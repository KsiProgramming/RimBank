# 0016. Per-context Marten stores and projection conventions

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.10

## Context

Every context keeps its events and read models in Marten ([tech-stack §1](../../specs/tech-stack.md#1-platform)). [tech-stack §2](../../specs/tech-stack.md#2-topology-modular-monolith-first) asks for an isolated context: its own PostgreSQL schema **and database role** with no grants on other schemas, its own Marten store, and its own async daemon. [§8](../../specs/tech-stack.md#8-commands-queries--read-models-cqrs) adds that the pure projection lives in the core and its Marten implementation in `.Postgresql`. Inline is for single-stream read-your-write views and async for the rest; async views expose their staleness; every view can be rebuilt; and "database roles forbid cross-schema reads, and an integration test proves it".

The first `.Postgresql` adapter arrives in roadmap 1.4. If every adapter wired Marten by hand, each context would repeat the same schema, role, daemon and projection plumbing, and the rules above would only be as strong as each copy. Adapters cannot reference `RimBank.Host` ([ADR 0014](0014-host-composition-and-module-entry-point.md)), so the convention cannot live there. No real context exists yet, and the mission's "no speculative coverage" rules out an empty one, so the convention has to be proven with test-only contexts.

## Options considered

Where the convention lives:

1. **In each `.Postgresql` adapter.** Nothing is shared, but every context repeats the same wiring, and the role isolation, daemon mode and auto-create policy can drift.
2. **In `RimBank.Host`.** Adapters cannot reference the host (ADR 0014), so the host would have to know each context's event types and views.
3. **A small shared library, `RimBank.Postgresql`, used by every `.Postgresql` adapter.** This mirrors `RimBank.Problems` ([ADR 0015](0015-problem-details-mapping.md)).

How a context gets its store:

1. **One main `AddMarten()` store with a schema per context.** There is one daemon for all contexts, so a stuck projection in one context stalls the others (mission principle 7).
2. **A Marten ancillary store per context (`AddMartenStore<TStore>()`), with no main store.**

Who creates the roles and schemas:

1. **Marten, using one login for every context.** There is no per-context role, so nothing stops one context reading another's schema.
2. **SQL scripts in the AppHost and in the test containers.** The role list is duplicated between SQL and C#, and cannot be tested per context.
3. **A provisioner in code that runs with an administrator connection in Development and tests only.** Production provisions roles outside the application.

## Decision

**A shared library, a store per context, and a provisioner in code (option 3, option 2, option 3).**

**`RimBank.Postgresql`** (`src/RimBank.Postgresql`) references SharedKernel, Marten (which brings JasperFx, Weasel and Npgsql) and the `Microsoft.Extensions` hosting and health-check abstractions, and holds no business logic. Only a context's `.Postgresql` adapter uses it (`shared-postgresql-only-in-postgresql-adapters`), and it references nothing else (`shared-postgresql-dependencies`). A core reaches its store only through its ports. The host reaches it only through the adapter's own extension methods. The library is not named `RimBank.Marten`, because inside any `RimBank.*` namespace that name would hide Marten's own `Marten` namespace.

**One store per context.** A context's `.Postgresql` declares a marker, `public interface I<ServiceDomain>Store : IDocumentStore;`, and calls `builder.AddServiceDomainStore<I<ServiceDomain>Store>("<ServiceDomain>", options => …)`. That call:

- registers a Marten **ancillary store**. There is no main `AddMarten()` store, so `IDocumentStore` itself is not registered, and no context owns a "default" store;
- puts the context's events and documents in schema `<service_domain>` (the PascalCase service domain name in snake_case, for example `party_reference_data_directory`);
- connects with the connection string `ConnectionStrings:<ServiceDomain>`, which logs in as the context's own role `rimbank_<service_domain>`. One `NpgsqlDataSource` per context is shared by the store and its health check;
- runs the context's **own async daemon** in `DaemonMode.HotCold`, so only one host instance runs each context's daemon, and a stuck projection stays inside its context;
- uses `AutoCreate.CreateOrUpdate` and applies schema changes at startup in every environment except Production, where it uses `AutoCreate.None`. Production schema migrations are decided with the container images (roadmap 7.5);
- adds a health check `postgresql-<service-domain>`, tagged `ready`, that runs `SELECT 1` as the context's role. A failure says only "The context cannot reach its database.", and the health endpoints print the status only (ADR 0014);
- refuses a second store for the same service domain, and names the missing connection string and role when the connection string is absent.

Service domain names are validated (PascalCase ASCII letters, with a role name no longer than PostgreSQL's 63-character limit), so every derived identifier is safe.

**Role isolation.** `ServiceDomainDatabase.ProvisionAsync` creates the context's role (`LOGIN`, with no superuser, database-creation, role-creation, replication or row-security-bypass rights), sets its password, and creates the context's schema with that role as its owner. Nothing else is granted to it, so reading or writing another context's schema fails with SQL state `42501`, which a Testcontainers test proves. It is idempotent: running it again only resets the password. The user and password come from the context's own connection string, so there is a single source for them, and the provisioner refuses a connection string that logs in as another role. The password travels only as a bind parameter, through a transaction-local setting, and never appears in a statement's text.

A hosted service runs the provisioner before Marten creates its schema objects. It runs only when `RimBank:Postgresql:ProvisionRoles` is on, which by default is the case in Development only, and it then needs the administrator connection string `ConnectionStrings:rimbank` (the Aspire database). In production the roles and schemas are created outside the application, and RimBank never connects with administrator rights. Roles are server-wide, so two hosts must not provision the same role at the same moment. The test suite starts its hosts one at a time for this reason.

**Views.** `options.AddInlineView<TView, TProjection, TEvent>()` and `options.AddAsyncView<…>()` register `ViewProjector<TView, TProjection, TEvent>`, a Marten single-stream projection that only calls the core's pure `TProjection.Project(view, event)` ([ADR 0010](0010-decider-and-projection-contracts.md)). A `null` view deletes the document, and an event that is not a `TEvent` leaves the view unchanged. The adapter holds no logic, and a unit test proves that it gives the same view as `ProjectionSpecification` for the same events. It is named `…Projector`, not `…Projection`, because ADR 0010 reserves that suffix for pure projections. A view's identity is its stream's `Guid` (a `Guid Id` property). How a view exposes a typed ID is decided with the first real view (roadmap 1.6). Marten knows each projection by the core projection's type name (`ViewRegistration.ProjectionName<TProjection>()`).

**Staleness and rebuild.** `store.GetViewProgressAsync(projectionName)` returns a `ProjectionProgress`: the last processed sequence, the daemon's high water mark, the lag, and whether the view has caught up. `store.RebuildViewAsync(projectionName, timeout)` rebuilds one view from the context's own event store, through that store's projection daemon, so other contexts are not touched. Both are library APIs; an operations surface (a command or an endpoint), daemon health checks and lag metrics come in roadmap 7.4.

**Event type names.** Each context maps every event to an explicit, stable name with `options.Events.MapEventType<TEvent>("snake_case_name")`, so renaming or moving a C# type never breaks a stored stream. The fake contexts show the pattern, and a reviewer checks it in each `.Postgresql`.

**Serialization (amended in roadmap 1.4).** Every store uses System.Text.Json with `EnumStorage.AsString` and `TypedIdJsonConverterFactory` ([ADR 0008](0008-typed-identifier-pattern.md)), so a typed id is stored as a plain GUID string and an enum as its name, never as its position. The idempotency gate stores and replays acknowledgements with the same rules (`StoredJson.Options`, [ADR 0018](0018-idempotency-keys-for-commands.md)), so a replayed acknowledgement keeps its typed ids. A Testcontainers test proves both on a stored document.

**Telemetry.** `RimBank.ServiceDefaults` subscribes to Marten's `Marten` activity source and meter (by name, with no package reference), so every context's store is traced and measured.

## Consequences

- A context's `.Postgresql` adapter needs a marker interface, one `AddServiceDomainStore` call with its event names and views, and its ports. Everything else is the same for every context.
- Each context needs its own connection string and role password: user secrets locally and a vault elsewhere. The AppHost gives the host one connection string per role (`Program.RoleConnectionStrings`), each with a generated password kept in user secrets; the first real context added its own in roadmap 1.4.
- Production needs its roles and schemas provisioned, and its schema objects migrated, outside the application. The script and the migration step are part of roadmap 7.5.
- `RimBank.Postgresql` is a new composition project root in the architecture tests (namespace and assembly-name rules), with two new rules and their bad fixtures. tech-stack §2, §3.3, §4 and §8.3 list it.
- The convention is proven by `tests/RimBank.Postgresql.Tests`: unit tests, and Testcontainers smoke tests with two test-only fake contexts (append, inline read-your-write, async catch-up with progress, rebuild, role isolation and health). The fakes never ship in `src/`.
- Adding `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Diagnostics.HealthChecks` to `Directory.Packages.props` pins them centrally, which moves the transitive `Microsoft.Extensions.*` packages of the Host and AppHost from 10.0.12 to 10.0.14 (a patch update).
- Wolverine's outbox (`IntegrateWithWolverine` on each store) came in roadmap 0.11 ([ADR 0017](0017-wolverine-wiring.md), which also lets the host use `RimBank.Postgresql` for its own message store), and cross-stream (multi-stream) view adapters come with their first use (2.5).
