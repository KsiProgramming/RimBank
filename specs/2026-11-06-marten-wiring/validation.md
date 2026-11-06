# Validation: Marten wiring

## Done when (from roadmap)
> **0.10 Marten wiring:** one Marten store per context (own schema, own database role with no grants on other schemas, own async daemon); an inline projection and an async projection wired by convention; a projection rebuild entry point; Testcontainers smoke tests for append, inline read-your-write, async catch-up, and a test that one context's role cannot read another context's schema.
> + Definition of done (roadmap.md)

Phase description → checks:
- *One Marten store per context, own schema / role / daemon* → `ServiceDomainStoreRegistrationTests`, `RoleIsolationTests`, `Rebuilding_a_view_yields_the_same_view_and_leaves_the_other_context_alone`
- *Inline and async projection wired by convention* → `ViewProjectorTests`, `An_inline_view_is_readable_right_after_saving`, `The_async_daemon_catches_up_and_reports_its_progress`
- *A projection rebuild entry point* → `Rebuilding_a_view_yields_the_same_view_and_leaves_the_other_context_alone`
- *Smoke tests* → the Testcontainers suite below

Definition of done items that do not apply: there is no BIAN operation (no MCP parity), no HTTP endpoint and no business error path (no new Problem Details). The CQRS, projection-test, rebuild and cross-context items **do** apply and are covered below.

## Automated checks
- [x] `tests/RimBank.Postgresql.Tests`, unit (no I/O):
  - [x] `ViewProjectorTests`: the adapter's view equals the `ProjectionSpecification` Given/Then result for the same events; a `null` from `Project` deletes the view; other events leave it unchanged
  - [x] `ServiceDomainNameTests`: `PartyReferenceDataDirectory` → schema `party_reference_data_directory`, role `rimbank_party_reference_data_directory`; invalid and over-long names are rejected
  - [x] `ServiceDomainStoreRegistrationTests`: each fake store resolves as its own `IDocumentStore` with its own schema; no main store; Production uses `AutoCreate.None`; one `ready` check per store; a duplicate store and a missing connection string fail clearly
- [x] `tests/RimBank.Postgresql.Tests`, Testcontainers (PostgreSQL 17, two fake contexts):
  - [x] Append + load a stream: events come back in order, with the expected version and explicit event names
  - [x] Inline read-your-write: the view can be read right after `SaveChangesAsync`; a `null` view deletes the document
  - [x] Async catch-up: `GetViewProgressAsync` reaches the appended sequence, then the async view is correct
  - [x] Rebuild: after the view document is deleted, `RebuildViewAsync` restores the same view; the other context's view is unchanged
  - [x] Isolation: `rimbank_fake_ledger` reading or writing `fake_registry` fails with SQL state `42501`; each schema is owned by its own role; provisioning twice is idempotent; a connection string for another role is refused
  - [x] Health: both contexts are Healthy against the container; a context with a wrong password is Unhealthy with a generic description, and the other stays Healthy
- [x] `tests/RimBank.Architecture.Tests`:
  - [x] New rules pass on the solution; each fails on its own bad fixtures and only there (violation-isolation theory)
  - [x] Existing rules (namespace, assembly names, `.Tests` suffix, forbidden folders, `double`/`float`, projection naming) pass on `RimBank.Postgresql` and `RimBank.Postgresql.Tests`
- [x] Existing suites green: `RimBank.Host.Tests` (still no database; new test for the Marten activity source), `RimBank.AppHost.Tests`, `RimBank.Problems.Tests`, `RimBank.SharedKernel.Tests`
- [x] `dotnet restore RimBank.slnx --locked-mode` (win-x64 and linux-x64 lock files), Release build with warnings as errors, `dotnet format RimBank.slnx --verify-no-changes`
- [x] NuGet audit: no known vulnerability in Marten, Npgsql or Testcontainers (ADR 0013)
- [ ] GitHub Actions `ci` (Testcontainers run on `ubuntu-24.04`) and `codeql` green

## Manual / demo checks
Optional, not required to merge:
- [x] `dotnet test --project tests/RimBank.Postgresql.Tests` with Docker running: all tests pass locally in under a minute
- [ ] While a test is paused, `psql` into the container shows schemas `fake_ledger` and `fake_registry`, owned by `rimbank_fake_ledger` and `rimbank_fake_registry`, and `\dn+` shows no cross grants (also asserted by `Each_schema_is_owned_by_its_own_context_role`)

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 0.10 ticked in the same PR
- [x] ADR 0016 written and accepted; tech-stack §2, §3.3, §4 and §8.3 updated for `RimBank.Postgresql`
- [x] No passwords or connection strings in logs, test output or source (the provisioner passes the password as a bind parameter; the tests generate one per run)
- [x] Fake contexts live only under `tests/` (no speculative context in `src/`)
