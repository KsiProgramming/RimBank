# Validation: SharedKernel: decider & projection abstractions

## Done when (from roadmap)
> **0.6 SharedKernel: decider & projection abstractions:** write side `Decide` / `Evolve` contracts and read side `Project` (view, event) → view contract, with test helpers (Given events / When command / Then events, and Given events / Then view).
> + Definition of done (roadmap.md)

How the Definition of done applies to this phase: "working code", "commands and queries kept separate" (the two contracts are the separation), "unit tests including a projection test (Given events → Then view)", "read model rebuildable" (shown by folding events into the sample view), "CI green", "ADR" and "roadmap tick" apply. There are no adapters, real read models, BIAN operations or HTTP/MCP endpoints, so the integration, Problem Details and MCP parity items do not apply. Architecture tests start in 0.7. As the user chose, the proof is **a sample aggregate and view tested through the helpers + FsCheck laws + helper self-tests + ADR 0010 with a compiling sample**.

## Automated checks
- [ ] `tests/RimBank.SharedKernel.Tests` is green (xUnit v3 + Shouldly + FsCheck on MTP), with, in `DecidersAndProjections/`:
  - [ ] **Sample aggregate through the helpers**: `Given()` / `When(Open EUR)` / `Then(Opened)`; `Given(Opened, Posted)` / `When(Post 10.00 EUR)` / `Then(Posted)`; posting to a closed ledger `ThenFails(Conflict, "sample.ledger-closed")`; posting `USD` to an `EUR` ledger `ThenFails(Validation, "shared.currency-mismatch")`; closing twice `Then()` (no events)
  - [ ] **Sample projection through the helpers** (Given events → Then view): `Opened` creates the view; `Opened, Posted, Posted` gives the summed balance and 2 postings; `Closed` sets `IsClosed`; an event before `Opened` gives `ThenNoView()`
  - [ ] **FsCheck laws** (at least 100 cases each) on the sample: decide determinism, fold determinism, every decided event evolves without throwing, and **projection/aggregate agreement** (projected balance and postings equal the aggregate's for any command sequence)
  - [ ] **Helper self-tests**: wrong events, missing events, unexpected success, unexpected failure, wrong error kind or code, wrong view, and an unexpected view each fail with `ShouldAssertException`, and the message names expected and actual
  - [ ] `DeciderUsageSampleTests` (the ADR 0010 sample) compiles and passes
- [ ] The helpers and laws live in `tests/RimBank.SharedKernel.Testing` (not in the test project), so context test projects can reuse them
- [ ] Test namespaces end with `.Tests` (`RimBank.SharedKernel.DecidersAndProjections.Tests`)
- [ ] CI workflow `ci`, job `build`, green on the PR; the `test-results` artifact contains the TRX file with the new tests
- [ ] `dotnet format RimBank.slnx --verify-no-changes` passes (switch expressions only, IDE0066)
- [ ] Release build with `TreatWarningsAsErrors=true` and `latest-recommended` analysers: 0 warnings
- [ ] CodeQL `analyze (csharp)` green with 0 error-level findings
- [ ] `RimBank.SharedKernel.csproj` still has no `PackageReference` or `ProjectReference`

## Manual / demo checks
- [ ] From the repo root, the CONTRIBUTING commands give the same result as CI:
  `dotnet restore RimBank.slnx && dotnet build RimBank.slnx -c Release --no-restore && dotnet test --solution RimBank.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults && dotnet format RimBank.slnx --verify-no-changes --no-restore` → exit 0, `failed: 0`
- [ ] Break the sample projection on purpose (for example, ignore `Posted`), run the tests, and check that both the projection spec and the agreement law fail, the law with a shrunk counter-example; then revert
- [ ] Break a `Then(...)` expectation on purpose and read the failure: it shows the given events, the command, and expected vs actual events in business terms
- [ ] Read ADR 0010: a reviewer can write a new aggregate, its projection and their tests from it without reading the helper code

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox for 0.6 ticked in the same PR, and the 0.7 rule ("aggregates implement `IDecider`, `…Projection` implements `IProjection`") added
- [ ] ADR 0010 (decider and projection contracts) written, status Accepted
- [ ] Both open questions in requirements.md answered (generic inference, events with collection fields)
- [ ] No new packages in `Directory.Packages.props` (FsCheck is already there from 0.5)
- [ ] No business-specific types in `src/RimBank.SharedKernel` (the sample ledger lives only in tests)
- [ ] `CONTRIBUTING.md` updated with the aggregates and projections note
