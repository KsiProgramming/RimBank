# Plan: Marten wiring

Test-first: each group adds its failing tests first, then the code, and the solution builds and every test passes after each group (mission principle 10).

## 1. Skeleton, packages, architecture rules, ADR
- [ ] `docs/adr/0016-per-context-marten-stores.md` (draft, accepted at the end of group 6)
- [ ] `Directory.Packages.props`: an `ItemGroup Label="Persistence"` with `Marten` (latest stable 8.x); `Testcontainers.PostgreSql` under `Testing`. The `Npgsql` transitive pin is only added if NuGet audit needs it (ADR 0013)
- [ ] `src/RimBank.Postgresql/RimBank.Postgresql.csproj` (references `RimBank.SharedKernel` and `Marten`), added to `RimBank.slnx`, with its lock file
- [ ] `tests/RimBank.Postgresql.Tests/RimBank.Postgresql.Tests.csproj` (xUnit v3 on MTP, Shouldly, Testcontainers.PostgreSql, `RimBank.SharedKernel.Testing`), with its lock file
- [ ] `RimBank.Architecture.Tests`: new rules `marten-only-in-persistence-adapters`, `shared-postgresql-only-in-adapters`, `shared-postgresql-references-no-context` and `document-store-marker-in-own-postgresql`
- [ ] `RimBank.Architecture.Fixtures`: one bad fixture per new rule (core using the Marten stub; `.Api` referencing `RimBank.Postgresql`; store marker declared in a core), included in the violation-isolation theory
- [ ] `specs/tech-stack.md`: a §4 row for `RimBank.Postgresql`, the `.Postgresql` row may reference `RimBank.Postgresql`, and §3.3 layout entries for `src/RimBank.Postgresql` and `tests/RimBank.Postgresql.Tests`

## 2. Store convention + write-side smoke test
- [ ] `tests/RimBank.Postgresql.Tests/FakeContexts/`: `FakeLedger` and `FakeRegistry`, each with a marker store (`IFakeLedgerStore : IDocumentStore`), one or two events, a view and a pure `IProjection`
- [ ] `PostgresqlFixture` (one Testcontainer per test assembly) that builds a host with both fake stores
- [ ] Test `Appending_events_and_loading_the_stream_returns_them_in_order` (events in schema `fake_ledger`, stream version as expected)
- [ ] `ServiceDomainStoreRegistration.AddServiceDomainStore<TStore>(this IHostApplicationBuilder, string serviceDomain, Action<StoreOptions>? configure)`: schema and connection-string naming, `AddMartenStore<TStore>`, `DaemonMode.HotCold`, auto-create by environment, `ApplyAllDatabaseChangesOnStartup` outside Production
- [ ] `ServiceDomainName` value: PascalCase → `snake_case` schema, `rimbank_<snake>` role, validated (non-empty, letters only)
- [ ] Explicit event type aliases (if ADR 0016 decides so) through a `opts.Events.AddEventType<T>(alias)` helper

## 3. Role and schema provisioner + isolation test
- [ ] Test `A_context_role_cannot_read_another_contexts_schema`: as `rimbank_fake_ledger`, `SELECT` from a `fake_registry` table → `PostgresException` with SQL state `42501` (insufficient_privilege)
- [ ] Test that provisioning is idempotent (run twice, no error)
- [ ] `ServiceDomainDatabase.ProvisionAsync(NpgsqlDataSource admin, string contextConnectionString, ct)`: `CREATE ROLE … LOGIN PASSWORD …` if missing, `CREATE SCHEMA IF NOT EXISTS … AUTHORIZATION …`, no other grants, identifiers quoted, and password passed only as a parameter (never logged)
- [ ] An opt-in startup hook (`ProvisionOnStartup`, used in Development and tests) that runs before Marten applies schema changes, with the admin connection string `ConnectionStrings:rimbank`

## 4. Projection adapters (inline + async)
- [ ] Unit tests (no I/O) `ViewProjectionAdapterTests`: for the same events, the adapter gives the same view as `ProjectionSpecification` Given/Then (SharedKernel.Testing), and a `null` result deletes the view
- [ ] `ViewProjection<TView, TProjection, TEvent>`: a Marten `SingleStreamProjection` that only calls `TProjection.Project`
- [ ] `StoreOptionsExtensions.AddInlineView<…>()` / `AddAsyncView<…>()`
- [ ] Testcontainers test `An_inline_view_is_readable_right_after_saving` (same session, no wait)
- [ ] Testcontainers test `The_async_daemon_catches_up_and_reports_its_progress` (waits on `GetProjectionProgressAsync` reaching the high-water mark, then reads the view)
- [ ] `ProjectionProgress` record + `GetProjectionProgressAsync<TStore>(projectionName, ct)`

## 5. Rebuild entry point
- [ ] Testcontainers test `Rebuilding_a_projection_yields_the_same_view` (delete or corrupt the view documents, rebuild, compare)
- [ ] `ProjectionRebuild.RebuildProjectionAsync<TStore>(IServiceProvider, string projectionName, TimeSpan timeout, ct)` through the store's projection daemon
- [ ] Test that rebuilding one fake context's projection does not touch the other context's views or daemon

## 6. Health and observability, then wrap up
- [ ] Test that each registered store adds a `postgresql-<service-domain>` check tagged `ready`. It is Healthy against the container and Unhealthy (status only) with a wrong password
- [ ] Health check implementation using the store's own connection; add the `Marten` activity source and meter to `RimBank.ServiceDefaults` (names only)
- [ ] ADR 0016 accepted, with the answers to the open questions
- [ ] Tick 0.10 in `specs/roadmap.md`; update the AppHost XML comment ("schemas and roles … in 0.10") to point to ADR 0016 and 1.4
- [ ] `dotnet format RimBank.slnx --verify-no-changes`, locked restore, Release build with no warnings
