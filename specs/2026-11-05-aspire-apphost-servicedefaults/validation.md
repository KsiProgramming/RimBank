# Validation: Aspire AppHost + ServiceDefaults

## Done when (from roadmap)
> **0.8 Aspire AppHost + ServiceDefaults:** empty `RimBank.Host` with a health endpoint, OpenTelemetry, a PostgreSQL resource, and the module entry-point convention each context uses to register itself.
> + Definition of done (roadmap.md)

Phase description → checks:
- *Empty `RimBank.Host` with a health endpoint* → `HealthEndpointTests`, `AppHostStartupTests`
- *OpenTelemetry* → `OpenTelemetryRegistrationTests`
- *A PostgreSQL resource* → `AppHostStartupTests` (postgres healthy, connection string passed to the host)
- *Module entry-point convention* → `ServiceDomainModuleCompositionTests`, ADR 0014, architecture rules

Definition of done items that do not apply: there are no commands, queries or read models (CQRS split, projection tests, rebuild), no BIAN operation (MCP parity), and no business error path (Problem Details arrives in 0.9; the health endpoints have no error responses besides `Unhealthy`/503).

## Automated checks
- [ ] `tests/RimBank.Host.Tests`, run in-process with `WebApplicationFactory<Program>`:
  - [ ] `GET /health` and `GET /alive` → 200, body `Healthy`, no `Authorization` header, in both the `Development` and `Production` environments
  - [ ] The health response body contains no check names, exception text or version
  - [ ] `TracerProvider` and `MeterProvider` are registered
  - [ ] Two fake modules are added and mapped in list order, and their endpoints respond 200
  - [ ] A duplicate `ServiceDomain` makes startup throw `InvalidOperationException` that names the duplicate
  - [ ] An empty module list still starts the host
- [ ] `tests/RimBank.AppHost.Tests` (Aspire.Hosting.Testing, Docker):
  - [ ] `postgres` and `rimbank-host` reach `Healthy` within the timeout
  - [ ] `GET /health` on `rimbank-host` → 200 `Healthy`
  - [ ] `rimbank-host` receives the `rimbank` connection string
- [ ] `tests/RimBank.Architecture.Tests`:
  - [ ] New rules pass on the solution: only Host uses ServiceDefaults; nothing in `src/` depends on AppHost; no core, adapter or SharedKernel depends on Host
  - [ ] Each new rule fails on its own bad fixture and only on that one (violation-isolation theory)
  - [ ] Existing rules (namespace rule, test namespaces ending with `.Tests`, forbidden folders) pass on the three new `src/` projects and the two new test projects
- [ ] `dotnet restore RimBank.slnx --locked-mode`, the Release build (warnings as errors) and `dotnet format RimBank.slnx --verify-no-changes` all pass, and each new project has a committed `packages.lock.json`
- [ ] NuGet audit reports no known vulnerability in the new packages (ADR 0013)
- [ ] GitHub Actions `ci` (including the AppHost tests on `ubuntu-24.04`) and `codeql` are green

## Manual / demo checks
Optional, not required to merge:
- [ ] `dotnet run --project src/RimBank.AppHost` (Docker running): the dashboard lists `postgres`, `rimbank` and `rimbank-host` as healthy, and a `GET /health` trace from `RimBank.Host.http` shows up in the dashboard

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox **0.8** ticked in the same PR
- [ ] ADR 0014 written (module entry point, registry, health exposure, Aspire SDK pinning)
- [ ] No secrets in source: the PostgreSQL password is an Aspire parameter in user secrets, and `appsettings*.json` holds no connection strings
- [ ] Health responses and logs expose no internal details
- [ ] `CONTRIBUTING.md`, `README.md` and `specs/tech-stack.md` (§2, §3.3) updated
- [ ] Open questions in requirements.md answered or moved to a follow-up issue
