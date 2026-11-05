# Validation: SharedKernel: Result & Error

## Done when (from roadmap)
> **0.3 SharedKernel: Result & Error:** `Result<T>`, `Error` (code, message, kind, including `NotFound` and `Conflict`), with unit tests.
> + Definition of done (roadmap.md)

How the Definition of done applies to this phase: the "working code", "unit tests", "CI green", "ADR" and "roadmap tick" items apply. There are no adapters, read models, BIAN operations or HTTP/MCP endpoints yet, so the projection, integration, Problem Details and MCP parity items do not apply. Architecture tests start in 0.7. As the user chose, the proof is **unit tests + a green CI run**.

## Automated checks
- [ ] `tests/RimBank.SharedKernel.Tests` (xUnit v3 + Shouldly, on Microsoft.Testing.Platform) is green, with tests for:
  - [ ] every `ErrorKind` factory, `NotFound` and `Conflict` included
  - [ ] error code check: valid and invalid codes, from the list in plan.md group 2
  - [ ] `FieldErrors`: empty by default, kept when given, immutable, part of equality
  - [ ] `Result<T>` and `Result`: both states, wrong-state access throws, `Success(null)` throws, implicit conversions
  - [ ] `Match`, `Map`, `Bind`, `MapError`, `Tap`: the success path and failure short-circuiting (the failure delegate is never called)
  - [ ] `MapAsync`, `BindAsync`, `Match` on `Task<Result<T>>`: the same, including that a failure does not call the async delegate
  - [ ] LINQ `Select`/`SelectMany`: query syntax succeeds and returns the first failure
- [ ] CI workflow `ci`, job `build`, green on the PR, and the test step log shows the SharedKernel tests **ran** (not "no tests found")
- [ ] The `test-results` artifact contains a TRX file
- [ ] `dotnet format RimBank.slnx --verify-no-changes` passes (in CI)
- [ ] Release build with `TreatWarningsAsErrors=true` and `latest-recommended` analysers: 0 warnings
- [ ] CodeQL `codeql / analyze (csharp)` actually **analyses** code (its steps are no longer skipped) and reports 0 error-level findings; this is the 0.2 follow-up
- [ ] `RimBank.SharedKernel.csproj` has no `PackageReference` or `ProjectReference` (code review; ArchUnitNET enforces it from 0.7)

## Manual / demo checks
- [ ] From the repo root, the commands in CONTRIBUTING give the same result as CI:
  `dotnet restore RimBank.slnx && dotnet build RimBank.slnx -c Release --no-restore && dotnet test --solution RimBank.slnx -c Release --no-build && dotnet format RimBank.slnx --verify-no-changes --no-restore` → exit 0, with a test summary of `failed: 0` (use the exact flags from ADR 0007)
- [ ] Read the public API: a handler-style sample in the ADR 0006 example section compiles in a test, for example
  `Result<PartyId> r = Error.NotFound("party.party-not-found", "No party with this id.");` and a `from … select` chain
- [ ] `Error.NotFound("Party.NotFound", "…")` throws `ArgumentException` naming the convention (shown by a test)

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox for 0.3 ticked in the same PR
- [ ] ADR 0006 (Result & Error model and error code convention) and ADR 0007 (test platform) written, with status Accepted
- [ ] The open question in requirements.md (MTP `dotnet test` arguments) is answered and reflected in `ci.yml`, CONTRIBUTING and ADR 0007
- [ ] Only `xunit.v3` and `Shouldly` added to `Directory.Packages.props`; no versions in `.csproj` files
- [ ] No business-specific types in SharedKernel (no context names except as examples in docs and tests)
