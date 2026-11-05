# Aspire AppHost + ServiceDefaults

Roadmap phase: 0.8
Branch: plan/0.8-aspire-apphost-servicedefaults

## Context
Up to now RimBank is a set of libraries (`RimBank.SharedKernel`) and tests. Nothing runs yet. This phase adds the deployable host and its local orchestration. These are the first runnable pieces: Problem Details (0.9), Marten (0.10), Wolverine (0.11), authentication (0.13) and MCP (0.15) all plug into `RimBank.Host`. Its **module entry point** is how Stage 1 adds the first context without the host knowing that context's internals.

It depends on 0.1 (solution, CPM, lock files) and 0.7 (architecture tests, ADR 0011, which already allows `RimBank.Host`, `RimBank.AppHost` and `RimBank.ServiceDefaults` by name). All of these are merged. No later Stage 0 phase is a prerequisite.

Relevant constitution:
- [mission.md](../mission.md), principle 9 (production-grade from the first commit: observability, security) and the quality bar "OpenTelemetry traces, metrics and structured logs in every bounded context". The mission's non-goal "no speculative coverage" means no empty context is added: the module convention is proven with **test-only fake modules**.
- [tech-stack.md §1](../tech-stack.md#1-platform): .NET Aspire (AppHost + ServiceDefaults), PostgreSQL as an Aspire resource, OpenTelemetry with the Aspire dashboard locally and OTLP elsewhere.
- [tech-stack.md §2](../tech-stack.md#2-topology-modular-monolith-first): one deployable `RimBank.Host`; "each context registers itself in the host through a single module entry point".
- [tech-stack.md §3.3](../tech-stack.md#33-repository-layout) layout (`src/RimBank.Host`, `src/RimBank.AppHost`, `src/RimBank.ServiceDefaults`) and test namespaces; [§4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): `RimBank.Host` may reference all adapters and cores for composition only.
- ADR 0011 (namespace rule, composition projects), ADR 0013 (warnings and package governance: CPM, lock files, NuGet audit).

**CQRS split:** this phase adds no command, query, handler or read model. The health endpoints are infrastructure probes, not BIAN operations, so they dispatch nothing and need no MCP tool or parity test. There is no read model, so there is no inline or async choice to make.

There is no BIAN service domain in this phase.

## Scope
### In scope
- **`src/RimBank.ServiceDefaults`** (from the `aspire-servicedefaults` template, adjusted to the build rules): `AddServiceDefaults()` adds OpenTelemetry (ASP.NET Core, HttpClient and runtime instrumentation; OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` is set), default health checks (a `self` check tagged `live`), service discovery and standard HttpClient resilience. `MapDefaultEndpoints()` maps the health endpoints.
- **Health endpoints, in every environment and anonymous**: `/alive` (only `live` checks) and `/health` (all checks). The response is the status only (`Healthy` / `Unhealthy`), with no check names, exceptions or versions. This differs from the template, which maps them only in Development, and ADR 0014 records why: orchestrators need probes in production, and status-only output exposes nothing. Both endpoints are marked `AllowAnonymous` now, so 0.13's deny-by-default policy does not break them.
- **`src/RimBank.Host`**: an empty ASP.NET Core Minimal API host. It calls `AddServiceDefaults()` and `MapDefaultEndpoints()`, composes the registered modules, and holds no business logic. Its `Program` is `public partial` so tests can use `WebApplicationFactory<Program>`.
- **Module entry-point convention** in `RimBank.Host`:
  - `IServiceDomainModule` with `string ServiceDomain { get; }` (the BIAN service domain name), `void AddServices(IHostApplicationBuilder builder)` and `void MapEndpoints(IEndpointRouteBuilder endpoints)`.
  - One module class per context, at `RimBank.Host/Modules/<BusinessDomain>/<ServiceDomain>Module.cs` (none exist yet). It only calls the `Add…` / `Map…` extension methods that the context's own adapters expose (for example `.Postgresql`'s `AddPartyReferenceDataDirectoryPostgresql`).
  - `ServiceDomainModules`: an **explicit, ordered list** (no reflection scanning), empty in this phase. The composition (`AddServiceDomainModules` / `MapServiceDomainModules`) takes the list as a parameter, so tests can pass fake modules. A duplicate `ServiceDomain` name fails at startup with a clear message.
- **`src/RimBank.AppHost`** (Aspire 13, `Aspire.AppHost.Sdk`): a `postgres` server resource with a named data volume, one `rimbank` database, and the `rimbank-host` project with `WithReference(rimbank)` and `WaitFor(rimbank)`. The PostgreSQL password is an Aspire-generated parameter kept in user secrets, never in source. The Host does not open a database connection yet (0.10).
- **Tests**: `tests/RimBank.Host.Tests` (in-process) and `tests/RimBank.AppHost.Tests` (Aspire.Hosting.Testing, Docker), both xUnit v3 on MTP with Shouldly.
- **Architecture rules** (in `RimBank.Architecture.Tests`, each with a bad fixture): only `RimBank.Host` depends on `RimBank.ServiceDefaults`; no `src/` assembly depends on `RimBank.AppHost`; no core, adapter or SharedKernel depends on `RimBank.Host`.
- **ADR 0014**: the host composition, the module entry-point convention and the health endpoint exposure.

### Out of scope
- Problem Details handler and the `Error` → Problem Details mapping (0.9).
- Marten stores, per-context schemas and database roles, and a PostgreSQL health check (0.10). The `rimbank` database is only provisioned and referenced here.
- Wolverine and `IMessageBus` (0.11), the Idempotency-Key policy (0.12), Keycloak and JWT bearer (0.13), MCP endpoint (0.15).
- Any real service domain module (Stage 1, through 1.5 and later).
- Container images and publish settings (7.5), projection-lag metrics (7.4).
- RabbitMQ resource (only when a context is extracted).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Module entry point | `IServiceDomainModule` interface in `RimBank.Host`, one class per context under `Modules/<BusinessDomain>/`, explicit ordered `ServiceDomainModules` list | Satisfies tech-stack §2 ("single module entry point") with a type that tests can check. Explicit order is deterministic and does not rely on reflection. Keeping it in Host means cores and adapters never reference the host (§4). |
| Where registration code lives | Adapters expose their own `Add…`/`Map…` extensions; the Host module class only calls them | Each adapter owns its own wiring, and the host stays "composition only" (§4). |
| Health endpoints | `/health` + `/alive` in all environments, anonymous, status-only body | Production probes are needed (principle 9). Status-only output avoids leaking internals. 0.13 must not lock out the probes. |
| ServiceDefaults contents | Keep the template's OTel, health, service discovery and HttpClient resilience | Standard Aspire defaults (tech-stack §1). Resilience and discovery cost nothing now and are needed once a context calls out. |
| Aspire version | Aspire 13 (latest stable); SDK version in the AppHost `Sdk="Aspire.AppHost.Sdk/<version>"` attribute, packages in `Directory.Packages.props` with lock files | CPM cannot pin an MSBuild SDK. ADR 0013 package governance applies to the rest. |
| PostgreSQL resource | One server, one `rimbank` database, named data volume | Tech-stack §1 "one schema per service domain": the schemas and roles inside this database come in 0.10. |
| Host database access now | None (reference + `WaitFor` only) | Keeps 0.8 small. 0.10 adds the connection, the Marten stores and the database health check together. |

## ADRs to write
- **0014 Host composition and module entry point**: the `IServiceDomainModule` contract and its file location, the explicit registry and why there is no scanning, what a module may call (adapter extensions only), duplicate detection, the health endpoint exposure and status-only output, and the Aspire SDK pinning.

## Open questions
- Does Aspire 13's `Aspire.AppHost.Sdk` still need an explicit `Aspire.Hosting.AppHost` package reference, and does DCP restore cleanly in `--locked-mode` on Windows and Linux? (Check in group 3; record the answer here.)
- Does any template ServiceDefaults code trip `latest-recommended` analysers with warnings as errors (for example CA1062 or the `IDE` rules)? Fix it in code, or scope the change in `.editorconfig` per ADR 0013, never with `NoWarn`.
