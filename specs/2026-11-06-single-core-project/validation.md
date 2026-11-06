# Validation: Single core project

## Done when (from roadmap)
> **0.17 Single core project:** one `RimBank.Core` project holds the domain and application logic of every service domain, one folder per context (namespaces `RimBank.<BusinessDomain>.<ServiceDomain>`), with one `RimBank.Core.Tests`; adapters and `.PublishedLanguage` stay per context and reference it. ADR on the layout (ADR 0023); architecture rules updated so core code can live only in `RimBank.Core` and contexts stay isolated inside it.
> + Definition of done (roadmap.md)

## Automated checks
- [x] `RimBank.Architecture.Tests/SolutionOnlyRulesTests` (the arch-test unit cases chosen in the interview):
  - `RimBank.Core` with catalogued core namespaces passes `assembly-names`;
  - `RimBank.Core` holding an adapter namespace (`….Api`), a composition namespace (`RimBank.SharedKernel`), `RimBank.Core.*` or an uncatalogued context fails, with the namespace named in the violation;
  - a bare `RimBank.<BusinessDomain>.<ServiceDomain>` project name fails;
  - `cores-live-in-rimbank-core`: a core-namespace type outside `RimBank.Core` fails, and adapter and composition types are ignored.
- [x] `RimBank.Architecture.Tests/SolutionRulesTests`: every rule (including `cores-live-in-rimbank-core`) holds for the solution, and the assembly lists include `RimBank.Core` and `RimBank.Core.Tests`.
- [x] `RimBank.Architecture.Tests/FixtureRulesTests` unchanged and green (cross-context isolation still bites with all fixture contexts in one assembly).
- [x] `RimBank.Core.Tests`: `The_core_references_only_the_shared_kernel_and_the_bcl`.
- [x] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes`, CodeQL: green in CI.

## Manual / demo checks
- [x] `dotnet sln RimBank.slnx list` (or opening the solution) shows `src/RimBank.Core` and `tests/RimBank.Core.Tests`.
- [x] A scratch type `namespace RimBank.Party.PartyReferenceDataDirectory.Api; public sealed record Probe;` added to `RimBank.Core` makes `assembly-names` fail with a message naming the namespace. Revert it.
- [x] Reading check: tech-stack §2–§4, CONTRIBUTING and ADR 0023 agree; no remaining text describes a per-context core project (`grep -n "core project has no suffix" specs/`).

## Merge criteria
- [x] All checks above pass in CI
- [x] Roadmap checkbox 0.17 ticked in the same PR
- [x] ADR 0023 written; ADR 0011 amended; ADR 0004 and 0017 notes added
- [x] `doc-0/` untouched
