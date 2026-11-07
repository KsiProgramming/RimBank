# 0014. Host composition, module entry point and health probes

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.8

## Context

RimBank is a modular monolith: one deployable `RimBank.Host` composes every bounded context ([tech-stack §2](../../specs/tech-stack.md#2-topology-modular-monolith-first)). §2 says that "each context registers itself in the host through a single module entry point, so the host composes contexts without knowing their internals". §4 says the host references all adapters and cores "for composition only". Neither section says what that entry point looks like, or where it lives.

Roadmap 0.8 also adds the local orchestration (.NET Aspire AppHost with PostgreSQL) and the Aspire service defaults (OpenTelemetry, health checks, service discovery, resilience). The Aspire template maps the health endpoints only in Development. RimBank is meant to be production-grade from the first commit (mission principle 9), and orchestrators need liveness and readiness probes in every environment.

No context exists yet, and the mission's "no speculative coverage" rules out an empty one, so the convention has to be provable without a real context.

## Options considered

For the module entry point:

1. **Extension methods by naming convention.** Each context exposes `builder.Add<ServiceDomain>()` / `app.Map<ServiceDomain>()`, and `Program` calls them by name. This is the simplest option, but nothing types the convention, and the composition order is spread through `Program`.
2. **An `IServiceDomainModule` interface discovered by assembly scanning.** Adding a context needs no host edit, but the composition is invisible, its order depends on reflection, and every context would need a type that references the host.
3. **An `IServiceDomainModule` interface in `RimBank.Host`, one class per context inside the host, listed explicitly.** The contract is a type that tests and architecture rules can check, the order is deterministic and visible in one file, and contexts never reference the host.

For the health probes:

1. **Template behaviour** (Development only).
2. **Every environment, anonymous, status-only body.**

## Decision

**Module entry point: option 3.**

- `RimBank.Host.Modules.IServiceDomainModule` (namespace `RimBank.Host` since [ADR 0026](0026-flat-context-namespaces.md); the file stays in `Modules/`) has `ServiceDomain` (the BIAN service domain name), `AddServices(IHostApplicationBuilder)` and `MapEndpoints(IEndpointRouteBuilder)`.
- Each context has one module class at `src/RimBank.Host/Modules/<BusinessDomain>/<ServiceDomain>Module.cs`. It only calls the `Add…` / `Map…` extension methods that the context's own adapters expose (for example `.Postgresql`'s store registration, `.Api`'s endpoints, `.Mcp`'s tools). It holds no business logic and no wiring of its own.
- `ServiceDomainModules.All` is the explicit, ordered list of modules. There is no assembly scanning. `HostComposition.Build(builder, modules)` applies the service defaults, then each module's `AddServices` in list order, builds the application, maps the probes, then each module's `MapEndpoints` in list order. A service domain listed twice stops the host at startup, with its name in the message.
- The composition takes the module list as a parameter, so tests compose the host with fake modules (`tests/RimBank.Host.Tests/Modules`).

**Health probes: option 2.** `RimBank.ServiceDefaults` maps `/health` (every check) and `/alive` (checks tagged `live`) in every environment, with `AllowAnonymous()`, so roadmap 0.13's deny-by-default policy cannot lock out orchestrators. The body is the status only (`Healthy`, `Degraded` or `Unhealthy`, with 503 when unhealthy), never check names, exception text or versions. Probe requests are not traced, so they do not flood the telemetry.

**Composition projects.**

- `RimBank.ServiceDefaults` keeps the template's OpenTelemetry (OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` is set), the `self` liveness check, service discovery and the standard HttpClient resilience handler. Only `RimBank.Host` references it.
- `RimBank.AppHost` runs one PostgreSQL server with a named data volume and one `rimbank` database. `RimBank.Host` receives the `rimbank` connection string, waits for the database, and is health-checked on `/health`. The PostgreSQL password is an Aspire-generated parameter kept in user secrets. The per-context schemas and roles inside the database come in roadmap 0.10.
- The Host's first launch profile is HTTP only, so the AppHost starts without a development certificate (in CI as well). An HTTPS profile is kept for manual use.

**Aspire packaging.**

- The AppHost uses `Aspire.AppHost.Sdk` 13, pinned in the project's `Sdk` attribute, because Central Package Management cannot pin an MSBuild SDK. The other Aspire packages use `$(AspireVersion)` in `Directory.Packages.props`. Build guard **RB0003** (`Directory.Build.targets`) fails the build when the SDK version and `$(AspireVersion)` differ, so they are always raised together. The SDK adds `Aspire.Hosting.AppHost` itself; listing it in `Directory.Packages.props` is an error (NU1009).
- We keep the SDK default `AspireUseCliBundle=false`: DCP and the dashboard come from NuGet, pinned in the lock files and audited like every other package (ADR 0013), and neither CI nor contributors need the Aspire CLI.
- The SDK picks those packages for the build machine's runtime (`Aspire.Hosting.Orchestration.<rid>`, `Aspire.Dashboard.Sdk.<rid>`, from `NETCoreSdkRuntimeIdentifier`), so one lock file cannot be valid on both Windows and Linux. The projects that depend on the AppHost (`RimBank.AppHost`, `RimBank.AppHost.Tests`, `RimBank.Architecture.Tests`) therefore keep **one lock file per runtime**, `packages.<rid>.lock.json` (`NuGetLockFilePath`). We commit `win-x64` and `linux-x64` (CI); locked restore still applies to each. A contributor on another runtime (for example `osx-arm64`) restores once and commits the new file. All other projects keep a single `packages.lock.json`. With this default the SDK raises the MSBuild warning ASPIRE010. An `.editorconfig` cannot change an MSBuild warning, so `RimBank.AppHost.csproj` lowers ASPIRE010 to a message (`MSBuildWarningsAsMessages`), with the reason beside it. **This is the one exception to ADR 0013's "severity only in `.editorconfig`" rule**, and it covers this project and this diagnostic only.

**Architecture rules.** Three rules in `RimBank.Architecture.Tests`, each proven against a bad fixture:

- `service-defaults-only-in-host`: only `RimBank.Host` depends on `RimBank.ServiceDefaults`.
- `nothing-depends-on-apphost`: nothing depends on `RimBank.AppHost`.
- `host-is-the-composition-root`: nothing outside `RimBank.Host` depends on it. In particular, a context never implements `IServiceDomainModule` itself.

The rules govern hand-written code, so types a source generator emits with `[GeneratedCode]` (such as Aspire's `Projects.RimBank_Host` metadata in `RimBank.AppHost`) are skipped, like compiler-generated types.

## Consequences

- Adding a context to the host is one new file under `RimBank.Host/Modules/<BusinessDomain>/` and one line in `ServiceDomainModules.All`. The CONTRIBUTING checklist for a new context says so.
- Since [ADR 0021](0021-mcp-wiring-and-tool-conventions.md), `IServiceDomainModule` has a third member, `AddMcpTools(IMcpServerBuilder)`. The host calls it in list order, after every `AddServices`, so a context registers its MCP tools on the host's single `/mcp` server.
- The host project references every context's adapters, as §4 already allows. Contexts stay unaware of the host, and the architecture rules enforce it.
- The probes are public in production. They reveal only a status word, so this is acceptable. If a deployment needs more detail, it gets a separate, authenticated endpoint, decided in its own ADR.
- `RimBank.AppHost.Tests` starts real containers. In CI it always runs; locally it is skipped with a clear reason when Docker is not reachable. It removes the PostgreSQL data volume, so it runs on a throwaway database and never touches a developer's data.
- A Dependabot update to the Aspire packages that does not also raise the SDK attribute fails on RB0003. The fix is to raise the `Sdk` attribute in `RimBank.AppHost.csproj` in the same PR.
- Dependabot may not update the per-runtime lock files. When a package change touches the AppHost's dependencies, regenerate both files (see CONTRIBUTING, "Packages"); otherwise CI fails in locked mode, which is the intended signal.
