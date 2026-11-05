# Validation: SharedKernel: typed identifiers

## Done when (from roadmap)
> **0.4 SharedKernel: typed identifiers:** base pattern for `Guid` v7 strongly typed IDs.
> + Definition of done (roadmap.md)

How the Definition of done applies to this phase: the "working code", "unit tests", "CI green", "ADR" and "roadmap tick" items apply. There are no adapters, read models, BIAN operations or HTTP/MCP endpoints yet, so the projection, integration, Problem Details and MCP parity items do not apply. The `Validation` error added here gets its Problem Details mapping in 0.9. Architecture tests start in 0.7. As the user chose, the proof is **unit tests + reusable contract tests + a green CI run + ADR 0008 with a compiling sample**.

## Automated checks
- [ ] `tests/RimBank.SharedKernel.Tests` is green (xUnit v3 + Shouldly on MTP), with tests for:
  - [ ] `New(TimeProvider)`: v7, non-empty, timestamp equals the fixed provider time (ms), two calls differ
  - [ ] validation: `Guid.Empty` and a v4 GUID are rejected by `From` (`ArgumentException`), `TryCreate` (`Validation` error `shared.invalid-identifier`) and `Parse` / `TryParse`
  - [ ] `default(TId)` is documented as invalid: `TryCreate(default(TId).Value)` fails
  - [ ] equality, hash code, `ToString()` lower-case `D`, `Parse(ToString())` round-trip, span overloads, `TryFormat`
  - [ ] ordering: IDs created at increasing times sort in creation order; `CreatedAt` returns the creation instant (ms)
  - [ ] JSON: plain string value, dictionary keys, nullable, invalid input throws `JsonException`
- [ ] `SampleIdContractTests : TypedIdContractTests<SampleId>` runs every inherited fact (the MTP summary shows them; none are "not found")
- [ ] The ADR 0008 usage sample exists as a test and compiles
- [ ] CI workflow `ci`, job `build`, green on the PR; the `test-results` artifact contains the TRX file
- [ ] `dotnet format RimBank.slnx --verify-no-changes` passes
- [ ] Release build with `TreatWarningsAsErrors=true` and `latest-recommended` analysers: 0 warnings
- [ ] CodeQL `analyze (csharp)` green with 0 error-level findings
- [ ] `RimBank.SharedKernel.csproj` still has no `PackageReference` or `ProjectReference` (code review; ArchUnitNET from 0.7)

## Manual / demo checks
- [ ] From the repo root, the CONTRIBUTING commands give the same result as CI:
  `dotnet restore RimBank.slnx && dotnet build RimBank.slnx -c Release --no-restore && dotnet test --solution RimBank.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults && dotnet format RimBank.slnx --verify-no-changes --no-restore` → exit 0, `failed: 0`
- [ ] Read the declaration template in ADR 0008: a reviewer can declare a new ID by copying it (about 15 lines) and inheriting `TypedIdContractTests<TId>`, with no other changes
- [ ] `SampleId.TryCreate("not-a-guid")` returns a failure whose `Error` is `Validation` with code `shared.invalid-identifier` and a business-language message (shown by a test; it will be a 400 Problem Details from 0.9)

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox for 0.4 ticked in the same PR, and the 0.7 follow-up rule added
- [ ] ADR 0008 (typed identifier pattern) written, status Accepted
- [ ] Both open questions in requirements.md answered (xUnit library package, static virtual defaults) and reflected in ADR 0008
- [ ] Only the xUnit extensibility package added to `Directory.Packages.props`; no versions in `.csproj` files
- [ ] No business-specific IDs in SharedKernel (`SampleId` lives only in the tests)
- [ ] `specs/tech-stack.md` §3.3 and `CONTRIBUTING.md` updated
