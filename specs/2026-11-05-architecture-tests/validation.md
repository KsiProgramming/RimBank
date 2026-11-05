# Validation: Architecture tests

## Done when (from roadmap)
> **0.7 Architecture tests:** ArchUnitNET project enforcing the dependency table, the CQRS rules (command handlers do not use query read models; query handlers do not use write ports or aggregates; projections are pure), cross-context isolation (only `.Subscriptions` may reference another context's `.PublishedLanguage`; `.PublishedLanguage` contains no views) forbidden folder names, every `ITypedId<T>` implementation being a `readonly record struct` (ADR 0008), and no `double` or `float` fields, properties or parameters in cores, SharedKernel or `.PublishedLanguage` (ADR 0009), and every test namespace ending with `.Tests` (tech-stack §3.3), and every aggregate state implementing `IDecider` and every `…Projection` implementing `IProjection` (ADR 0010). ADR on the namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>`.
> + Definition of done (roadmap.md)

How the Definition of done applies to this phase: "working code", "commands and queries kept separate" (the markers and CQRS rules), "architecture tests green", "CI green", "ADR" and "roadmap tick" apply. There are no adapters, read models, BIAN operations or HTTP/MCP endpoints, so the integration, rebuild, Problem Details and MCP parity items do not apply. As the user chose, the proof is that **every rule fails on its bad fixture**, plus **ADR 0011, ADR 0012 and a CONTRIBUTING rule catalogue**.

## Automated checks
- [ ] `tests/RimBank.Architecture.Tests` is green (xUnit v3 + Shouldly + ArchUnitNET on MTP), and for **every rule** below there is:
  - a test that it **passes on the real solution**,
  - a test that it **passes on the good fixture contexts** (`SampleLedger`, `SampleStatement`),
  - a test that it **fails on its bad fixture**, with a message naming the offending type and the rule's reason (tech-stack section or ADR).
- [ ] Rules covered:
  - [ ] Dependency table, one rule per row of tech-stack §4 (SharedKernel, core, `.PublishedLanguage`, `.Subscriptions`, `.Api`, `.Mcp`, `.Postgresql`)
  - [ ] Only `.Subscriptions` references another context's `.PublishedLanguage`; `.PublishedLanguage` has no views and no core types
  - [ ] Command handlers do not use `IReadPort`, views, queries or query handlers
  - [ ] Query handlers do not use `IWritePort`, aggregates or commands
  - [ ] `…Projection` implements `IProjection`, is sealed, has no instance fields and does not use ports, handlers or `TimeProvider`
  - [ ] Every type with a static `Evolve` implements `IDecider`; every `IDecider` is a `sealed record` in a core
  - [ ] Namespace rule (ADR 0011) against the `ServiceDomainCatalogue`; assembly name = root namespace
  - [ ] No `Services`, `Repositories`, `Helpers`, `Managers`, `Utils`, `Dtos`, `Commands`, `Queries` namespace segments in `src/`
  - [ ] Every `ITypedId<T>` is a `readonly record struct`
  - [ ] No `double` / `float` (including nullable and generic arguments) in fields, properties, parameters or returns of cores, SharedKernel or `.PublishedLanguage`
  - [ ] Every type in a `*.Tests` assembly is in a namespace ending with `.Tests`
- [ ] **Violation isolation** theory: each bad fixture context fails exactly one rule and passes all the others
- [ ] The real-solution run includes `RimBank.SharedKernel`, `RimBank.SharedKernel.Testing` and `RimBank.SharedKernel.Tests`, and never the fixtures assembly
- [ ] `RimBank.SharedKernel.csproj` still has no `PackageReference` or `ProjectReference`; the four markers are in SharedKernel with XML docs
- [ ] No project in `src/` references `RimBank.Architecture.Fixtures`
- [ ] Test namespaces in the new project end with `.Tests` (the rule checks itself)
- [ ] CI workflow `ci`, job `build`, green on the PR; the `test-results` artifact contains the TRX with the architecture tests
- [ ] `dotnet format RimBank.slnx --verify-no-changes` passes (switch expressions only, IDE0066)
- [ ] Release build with `TreatWarningsAsErrors=true`: 0 warnings
- [ ] CodeQL `analyze (csharp)` green with 0 error-level findings

## Manual / demo checks
- [ ] From the repo root, the CONTRIBUTING commands give the same result as CI:
  `dotnet restore RimBank.slnx && dotnet build RimBank.slnx -c Release --no-restore && dotnet test --solution RimBank.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults && dotnet format RimBank.slnx --verify-no-changes --no-restore` → exit 0, `failed: 0`
- [ ] Running only the architecture tests with the command in the CONTRIBUTING catalogue works and lists every rule
- [ ] Read one failing fixture message (for example `CoreUsesMarten`): it names the type, the forbidden dependency and "tech-stack §4" in plain words
- [ ] Read ADR 0012 and the CONTRIBUTING catalogue: a reviewer can tell, for a new Stage 1 type, which marker it needs and which rule will check it

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox for 0.7 ticked in the same PR
- [ ] ADR 0011 (namespace rule) and ADR 0012 (CQRS role markers) written, status Accepted
- [ ] CONTRIBUTING has the rule catalogue and the new-context checklist
- [ ] tech-stack §3.3, §4 and §8 updated for the markers and the fixtures project
- [ ] Only ArchUnitNET packages added to `Directory.Packages.props` (already named in tech-stack §9, so no technology ADR)
- [ ] All open questions in requirements.md answered or moved to a named later phase
