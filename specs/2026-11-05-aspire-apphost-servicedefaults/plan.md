# Plan: Aspire AppHost + ServiceDefaults

Inside-out, four groups, one commit each. The solution builds (Release, warnings as errors), `dotnet test` passes and `dotnet format --verify-no-changes` is clean after each group. Package versions go only in `Directory.Packages.props`, and every new project commits its `packages.lock.json` (ADR 0013).

## 1. ServiceDefaults and an empty Host with health and OpenTelemetry
- [ ] Add packages to `Directory.Packages.props` (new "Hosting" and "Observability" sections): `OpenTelemetry.Exporter.OpenTelemetryProtocol`, `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `OpenTelemetry.Instrumentation.Http`, `OpenTelemetry.Instrumentation.Runtime`, `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.ServiceDiscovery`; under "Testing": `Microsoft.AspNetCore.Mvc.Testing`
- [ ] `src/RimBank.ServiceDefaults/RimBank.ServiceDefaults.csproj` (`IsAspireSharedProject=true`, `FrameworkReference Microsoft.AspNetCore.App`) and `Extensions.cs` (namespace `RimBank.ServiceDefaults`), adapted from `dotnet new aspire-servicedefaults`: `AddServiceDefaults`, `ConfigureOpenTelemetry`, `AddDefaultHealthChecks` (`self` tagged `live`), `MapDefaultEndpoints`
- [ ] `MapDefaultEndpoints`: map `/health` and `/alive` in every environment, `.AllowAnonymous()`, with a status-only response writer (no entries, no exception text)
- [ ] `src/RimBank.Host/RimBank.Host.csproj` (`Microsoft.NET.Sdk.Web`, references ServiceDefaults), `Program.cs`: `AddServiceDefaults()`, build, `MapDefaultEndpoints()`, run; `public partial class Program;` for tests; `appsettings.json` with logging levels only (no secrets)
- [ ] `src/RimBank.Host/RimBank.Host.http`: `GET {{host}}/health` and `GET {{host}}/alive`
- [ ] `tests/RimBank.Host.Tests` (`OutputType=Exe`, xUnit v3, Shouldly, `Microsoft.AspNetCore.Mvc.Testing`); `Health/HealthEndpointTests.cs` (namespace `RimBank.Host.Health.Tests`): `/health` and `/alive` return 200 with body `Healthy` and no auth, in both `Development` and `Production` environments; `Observability/OpenTelemetryRegistrationTests.cs`: `TracerProvider` and `MeterProvider` are resolvable
- [ ] Add the three projects to `RimBank.slnx` (`/src/`, `/tests/`); add `RimBank.Host.Tests` to the architecture tests' test-namespace rule (project reference, as for `RimBank.SharedKernel.Tests` in 0.7)
- [ ] Check locally: Release build, test, format

## 2. Module entry-point convention
- [ ] `src/RimBank.Host/Modules/IServiceDomainModule.cs`: `ServiceDomain`, `AddServices(IHostApplicationBuilder)`, `MapEndpoints(IEndpointRouteBuilder)`, with XML docs (one per context, it only calls adapter extensions; see ADR 0014)
- [ ] `src/RimBank.Host/Modules/ServiceDomainModules.cs`: `public static IReadOnlyList<IServiceDomainModule> All { get; } = [];` (with a comment that Stage 1 adds the first entry)
- [ ] `src/RimBank.Host/Modules/ServiceDomainModuleComposition.cs`: `AddServiceDomainModules(this IHostApplicationBuilder, IReadOnlyList<IServiceDomainModule>)` (throws `InvalidOperationException` that names a duplicate `ServiceDomain`) and `MapServiceDomainModules(this IEndpointRouteBuilder, …)`, both in list order
- [ ] `src/RimBank.Host/HostComposition.cs`: `public static WebApplication Build(WebApplicationBuilder builder, IReadOnlyList<IServiceDomainModule> modules)` (service defaults → modules' `AddServices` → build → `MapDefaultEndpoints` → modules' `MapEndpoints`); `Program.cs` becomes `HostComposition.Build(WebApplication.CreateBuilder(args), ServiceDomainModules.All).Run();`
- [ ] Tests `Modules/ServiceDomainModuleCompositionTests.cs` (namespace `RimBank.Host.Modules.Tests`) build the host through `HostComposition.Build` with `builder.WebHost.UseTestServer()` and `app.GetTestClient()`, using two fake modules defined in the test project: services registered by both, endpoints of both reachable (`GET /fake-a/ping` → 200), `AddServices` and `MapEndpoints` called in list order, duplicate name → startup fails with the name in the message, empty list → host starts and `/health` is 200
- [ ] Check locally: Release build, test, format

## 3. AppHost with PostgreSQL and the Aspire smoke test
- [ ] `src/RimBank.AppHost/RimBank.AppHost.csproj` (`Sdk="Aspire.AppHost.Sdk/<13.x>"`, `UserSecretsId`, references `RimBank.Host`); add `Aspire.Hosting.PostgreSQL` (and `Aspire.Hosting.AppHost` if still needed) to CPM
- [ ] `AppHost.cs`: `var postgres = builder.AddPostgres("postgres").WithDataVolume("rimbank-postgres-data"); var rimbank = postgres.AddDatabase("rimbank"); builder.AddProject<Projects.RimBank_Host>("rimbank-host").WithReference(rimbank).WaitFor(rimbank).WithHttpHealthCheck("/health");`
- [ ] `tests/RimBank.AppHost.Tests` (xUnit v3, Shouldly, `Aspire.Hosting.Testing`); `Startup/AppHostStartupTests.cs` (namespace `RimBank.AppHost.Startup.Tests`): `DistributedApplicationTestingBuilder.CreateAsync<Projects.RimBank_AppHost>()`, wait until `postgres` and `rimbank-host` are healthy (with a timeout), `GET /health` → 200 `Healthy`, and `rimbank-host` received a `ConnectionStrings__rimbank` value
- [ ] Add both projects to `RimBank.slnx`; add `RimBank.AppHost.Tests` to the test-namespace rule
- [ ] Confirm the tests run in the existing CI `build` job on `ubuntu-24.04` (Docker is available) within the 20-minute timeout; raise the timeout only if needed and say why in the PR
- [ ] Record the answers to the open questions (AppHost package, locked-mode restore of DCP) in requirements.md
- [ ] Check locally: Release build, test, format

## 4. Architecture rules, ADR, docs and roadmap
- [ ] `ServiceDomainCatalogue` / composition-project handling: confirm `RimBank.Host`, `RimBank.AppHost`, `RimBank.ServiceDefaults` and the namespace `RimBank.Host.Modules.<BusinessDomain>` pass the namespace rule (ADR 0011); add the real assemblies to `SolutionArchitecture`
- [ ] Rule **only Host uses ServiceDefaults**; bad fixture: a sample core referencing a stub `RimBank.ServiceDefaults`-like type
- [ ] Rule **nothing in `src/` depends on AppHost**; bad fixture: a sample adapter referencing a stub `Projects`/AppHost type
- [ ] Rule **no core, adapter or SharedKernel depends on Host** (including `IServiceDomainModule`); bad fixture: a sample `.Api` implementing a stub `IServiceDomainModule`
- [ ] Add the new bad contexts to the violation-isolation theory (each fails only its own rule)
- [ ] `docs/adr/0014-host-composition-and-module-entry-point.md` (MADR, Accepted), per requirements.md "ADRs to write"
- [ ] `CONTRIBUTING.md`: "Running RimBank locally" (`dotnet run --project src/RimBank.AppHost`, Docker required, dashboard URL from the console), the "new context" checklist gains "add a `<ServiceDomain>Module` under `RimBank.Host/Modules/<BusinessDomain>/` and append it to `ServiceDomainModules.All`", and the three new rules in the "Architecture rules" catalogue
- [ ] `specs/tech-stack.md` §2: name `IServiceDomainModule` and link ADR 0014; §3.3: add `tests/RimBank.Host.Tests` and `tests/RimBank.AppHost.Tests` to the layout
- [ ] `README.md`: one "Run it" line pointing to CONTRIBUTING
- [ ] `specs/roadmap.md`: tick **0.8**
