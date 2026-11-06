# Validation: Marten wiring

## Done when (from roadmap)
> **0.10 Marten wiring:** one Marten store per context (own schema, own database role with no grants on other schemas, own async daemon); an inline projection and an async projection wired by convention; a projection rebuild entry point; Testcontainers smoke tests for append, inline read-your-write, async catch-up, and a test that one context's role cannot read another context's schema.
> + Definition of done (roadmap.md)

Phase description → checks:
- *One Marten store per context, own schema / role / daemon* → `ServiceDomainStoreRegistrationTests`, `A_context_role_cannot_read_another_contexts_schema`, `Rebuilding_one_context_does_not_touch_another`
- *Inline and async projection wired by convention* → `ViewProjectionAdapterTests`, `An_inline_view_is_readable_right_after_saving`, `The_async_daemon_catches_up_and_reports_its_progress`
- *A projection rebuild entry point* → `Rebuilding_a_projection_yields_the_same_view`
- *Smoke tests* → the Testcontainers suite below

Definition of done items that do not apply: there is no BIAN operation (no MCP parity), no HTTP endpoint and no business error path (no new Problem Details). The CQRS, projection-test, rebuild and cross-context items **do** apply and are covered below.

## Automated checks
- [ ] `tests/RimBank.Postgresql.Tests`, unit (no I/O):
  - [ ] `ViewProjectionAdapterTests`: the adapter's view equals the `ProjectionSpecification` Given/Then result for the same events; a `null` from `Project` deletes the view
  - [ ] `ServiceDomainNameTests`: `PartyReferenceDataDirectory` → schema `party_reference_data_directory`, role `rimbank_party_reference_data_directory`; invalid names are rejected
  - [ ] Registration: each fake store resolves as its own `IDocumentStore` with its own schema and daemon; Production uses `AutoCreate.None`
- [ ] `tests/RimBank.Postgresql.Tests`, Testcontainers (PostgreSQL, two fake contexts):
  - [ ] Append + load a stream: events come back in order, with the expected version
  - [ ] Inline read-your-write: the view can be read in the same session right after `SaveChangesAsync`
  - [ ] Async catch-up: `GetProjectionProgressAsync` reaches the high-water mark, then the async view is correct
  - [ ] Rebuild: after the view documents are deleted, `RebuildProjectionAsync` restores the same view; the other context's views are unchanged
  - [ ] Isolation: `rimbank_fake_ledger` reading a `fake_registry` table fails with SQL state `42501`; provisioning twice is idempotent
  - [ ] Health: `postgresql-fake-ledger` is Healthy against the container, and Unhealthy with a wrong password, with a status-only body
- [ ] `tests/RimBank.Architecture.Tests`:
  - [ ] New rules pass on the solution; each fails on its own bad fixture and only on that one (violation-isolation theory)
  - [ ] Existing rules (namespace, `.Tests` suffix, forbidden folders, `double`/`float`) pass on `RimBank.Postgresql` and `RimBank.Postgresql.Tests`
- [ ] Existing suites unchanged and green: `RimBank.Host.Tests` (still no database needed), `RimBank.AppHost.Tests`, `RimBank.Problems.Tests`, `RimBank.SharedKernel.Tests`
- [ ] `dotnet restore RimBank.slnx --locked-mode` (win-x64 and linux-x64 lock files where needed), Release build with warnings as errors, `dotnet format RimBank.slnx --verify-no-changes`
- [ ] NuGet audit: no known vulnerability in Marten, Npgsql or Testcontainers (ADR 0013)
- [ ] GitHub Actions `ci` (Testcontainers run on `ubuntu-24.04`) and `codeql` green

## Manual / demo checks
Optional, not required to merge:
- [ ] `dotnet test --project tests/RimBank.Postgresql.Tests` with Docker running: all smoke tests pass locally in under a minute
- [ ] While a test is paused, `psql` into the container shows schemas `fake_ledger` and `fake_registry`, owned by `rimbank_fake_ledger` and `rimbank_fake_registry`, and `\dn+` shows no cross grants

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 0.10 ticked in the same PR
- [ ] ADR 0016 written and accepted; tech-stack §3.3 and §4 updated for `RimBank.Postgresql`
- [ ] No passwords or connection strings in logs, test output or source (provisioner passes the password as a parameter; tests use generated passwords)
- [ ] Fake contexts live only under `tests/` (no speculative context in `src/`)
