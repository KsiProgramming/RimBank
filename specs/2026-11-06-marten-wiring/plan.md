# Plan: Marten wiring

Test-first: each group adds its failing tests first, then the code, and the solution builds and every test passes after each group (mission principle 10).

## 1. Skeleton, packages, architecture rules, ADR
- [x] `docs/adr/0016-per-context-marten-stores.md`
- [x] `Directory.Packages.props`: an `ItemGroup Label="Persistence"` with `Marten` 9.58.0; `Testcontainers.PostgreSql` 4.16.0 under `Testing`; `Microsoft.Extensions.Hosting`, `.Hosting.Abstractions` and `.Diagnostics.HealthChecks` 10.0.14 under `Hosting`. NuGet audit raised nothing, so no `Npgsql` pin was needed (ADR 0013)
- [x] `src/RimBank.Postgresql/RimBank.Postgresql.csproj` (references `RimBank.SharedKernel`, `Marten` and the two `Microsoft.Extensions` abstractions), added to `RimBank.slnx`, with its lock file
- [x] `tests/RimBank.Postgresql.Tests/RimBank.Postgresql.Tests.csproj` (xUnit v3 on MTP, Shouldly, Testcontainers.PostgreSql, `RimBank.SharedKernel.Testing`), with its lock file
- [x] `RimBank.Architecture.Tests`: `RimBank.Postgresql` is a composition project, with two new rules: `shared-postgresql-only-in-postgresql-adapters` and `shared-postgresql-dependencies`. The two other rules in the original plan were dropped because the existing rules already cover them: `core-dependencies`, `api-dependencies`, `mcp-dependencies` and `subscriptions-dependencies` ban Marten and Npgsql outside `.Postgresql`, so a store marker can only live in a `.Postgresql`. A duplicate rule would break the "one bad fixture breaks one rule" check.
- [x] `RimBank.Architecture.Fixtures`: bad fixtures `CoreUsesSharedPostgresql`, `ApiUsesSharedPostgresql.Api` and `RimBank.Postgresql.FixtureViolations` (uses ASP.NET Core), plus a `RimBank.Postgresql` stub
- [x] `specs/tech-stack.md`: §2 store convention, §3.3 layout entries, §4 `RimBank.Postgresql` row and the `.Postgresql` row, §8.3 views and staleness; CONTRIBUTING rule rows and an "Event store and views" section
- [x] Linux lock files (`packages.linux-x64.lock.json` of AppHost, AppHost.Tests and Architecture.Tests) regenerated in a `mcr.microsoft.com/dotnet/sdk:10.0` container (SDK 10.0.403)

## 2. Store convention + write-side smoke test
- [x] `tests/RimBank.Postgresql.Tests/FakeContexts/`: `FakeLedger` (inline `FakeLedgerView`, async `FakeLedgerPostingsView`) and `FakeRegistry` (inline `FakeRegistryEntryView`), each with a store marker, events with explicit names, views and pure `IProjection`s
- [x] `PostgresqlServer` (one `postgres:17-alpine` Testcontainer per test assembly, one database per test) and `RunningHost`
- [x] Test `Appending_events_and_loading_the_stream_returns_them_in_order` (order, versions, explicit event type names, rows in `fake_ledger.mt_events`)
- [x] `ServiceDomainStoreRegistration.AddServiceDomainStore<TStore>(this IHostApplicationBuilder, string serviceDomain, Action<StoreOptions>? configure)`: ancillary store, keyed `NpgsqlDataSource`, schema, `DaemonMode.HotCold`, auto-create by environment, duplicate and missing-connection-string errors
- [x] `ServiceDomainName`: PascalCase → `snake_case` schema, `rimbank_<snake>` role, `postgresql-<kebab>` health check; letters only and a role name of at most 63 characters
- [x] Explicit event type names with Marten's own `options.Events.MapEventType<T>(alias)` (no helper needed)

## 3. Role and schema provisioner + isolation test
- [x] Test `A_context_role_cannot_read_or_write_another_contexts_schema` (events table, view table, `CREATE TABLE`) → SQL state `42501`; `Each_schema_is_owned_by_its_own_context_role`
- [x] Test `Provisioning_twice_changes_nothing`, and `Provisioning_refuses_a_connection_string_that_logs_in_as_another_role`
- [x] `ServiceDomainDatabase.ProvisionAsync(NpgsqlDataSource administrator, ServiceDomainName, string contextConnectionString, ct)`: creates the role if missing, sets `LOGIN` without elevated rights and the password (as a bind parameter through a transaction-local setting), creates the schema and sets its owner
- [x] `ServiceDomainRoleProvisioning` (`IHostedLifecycleService.StartingAsync`, registered before the store), on when `RimBank:Postgresql:ProvisionRoles` is true (default: Development only), with `ConnectionStrings:rimbank`

## 4. Projection adapters (inline + async)
- [x] Unit tests `ViewProjectorTests`: same view as `ProjectionSpecification`, `null` deletes, other events leave the view unchanged, name, progress lag
- [x] `ViewProjector<TView, TProjection, TEvent>` (a Marten `SingleStreamProjection<TView, Guid>` that only calls `TProjection.Project`). It is named `…Projector`, because `projections-implement-contract` (ADR 0010) reserves `…Projection` for pure projections.
- [x] `ViewRegistration.AddInlineView<…>()` / `AddAsyncView<…>()` / `ProjectionName<TProjection>()`
- [x] Testcontainers tests `An_inline_view_is_readable_right_after_saving` and `A_null_view_deletes_the_inline_document`
- [x] Testcontainers test `The_async_daemon_catches_up_and_reports_its_progress`
- [x] `ProjectionProgress` record + `store.GetViewProgressAsync(projectionName, ct)` (an extension on `IDocumentStore`)

## 5. Rebuild entry point
- [x] Testcontainers test `Rebuilding_a_view_yields_the_same_view_and_leaves_the_other_context_alone` (hard-deletes the view, checks it is gone, rebuilds, compares; the registry's view is unchanged)
- [x] `store.RebuildViewAsync(projectionName, timeout, ct)` through the store's own projection daemon

## 6. Health and observability, then wrap up
- [x] Tests `Each_store_registers_a_ready_health_check_named_after_its_context` (unit), `Each_context_is_healthy_when_its_role_reaches_its_database` and `A_context_that_cannot_log_in_is_unhealthy_and_the_other_is_not`
- [x] `ServiceDomainStoreHealthCheck` (`SELECT 1` as the context's role, generic description); the `Marten` activity source and meter in `RimBank.ServiceDefaults` (`MartenTelemetry`), with a Host test
- [x] ADR 0016 accepted, with the answers to the open questions
- [x] Tick 0.10 in `specs/roadmap.md`; update the AppHost XML comment to point to ADR 0016 and 1.4
- [x] `dotnet format RimBank.slnx --verify-no-changes`, locked restore, Release build with no warnings, 611 tests green
