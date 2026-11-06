# Plan: Single core project

Order chosen in the planning interview: ADR → specs → rules → project. The solution builds and every test passes at the end of each group.

## 1. ADR
- [ ] `docs/adr/0023-one-core-project.md` from `template.md`:
  - **Context:** quote `doc-0/ideas.MD` lines 8–9, the Stage 0 reading (one core per context), the WalletSystem pattern, and the fact that no context exists yet.
  - **Options:** (1) one core per service domain (status quo: compile-time isolation, more projects); (2) one core per business domain (a middle ground with two isolation mechanisms); (3) one `RimBank.Core` with namespace isolation enforced by architecture tests.
  - **Decision:** option 3, with `RootNamespace = RimBank`, one test project, and adapters plus `.PublishedLanguage` per context.
  - **Consequences:** extraction path, Wolverine discovery (shared assembly, bound by namespace), `public` visibility, Stryker `--mutate "Party/PartyReferenceDataDirectory/**"`, the new `cores-live-in-rimbank-core` rule, and the `assembly-names` change.
- [ ] Amend `docs/adr/0011-namespace-rule.md` ("Projects" bullet → see ADR 0023) and add one-line notes to ADR 0004 (enforcement) and ADR 0016 (`TCore` lives in `RimBank.Core`).

## 2. Specs and docs
- [ ] `specs/tech-stack.md`: §2 isolation and extraction bullets; §3.1 core-project bullet; §3.3 layout tree (`src/RimBank.Core/Party/PartyReferenceDataDirectory/`, `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/`, per-context adapter projects unchanged); §3.4 heading and tree root (`RimBank.Core/Party/PartyReferenceDataDirectory/`); §4 Core row and the "Core and Wolverine" paragraph; §8.4 enforcement line.
- [ ] `src/RimBank.Postgresql/ServiceDomainStoreRegistration.cs`: the `TCore` XML doc says the assembly is `RimBank.Core` and the namespace is what binds the store (doc only, no behaviour change).
- [ ] `CONTRIBUTING.md`: the "Architecture rules" table (`assembly-names` wording, new `cores-live-in-rimbank-core` row), and where a new context's core code and tests go.
- [ ] `specs/roadmap.md`: the 0.17 row (added during planning) is ticked in the implementation PR.

## 3. Architecture rules (test-first)
- [ ] Failing cases in `tests/RimBank.Architecture.Tests/SolutionOnlyRulesTests.cs`:
  - `RimBank.Core` with `RimBank.Party.PartyReferenceDataDirectory` and `…Registration` namespaces passes `AssemblyNameProblems`;
  - `RimBank.Core` holding `RimBank.Party.PartyReferenceDataDirectory.Api`, `RimBank.SharedKernel`, `RimBank.Core.Party` or `RimBank.Customers.Directory` fails;
  - the bare `RimBank.Party.PartyReferenceDataDirectory` project name now fails (move it from the allowed theory to the failing one);
  - `CoreTypeProblems` (new pure function over (type, namespace, assembly)): a core-namespace type in `RimBank.Core` passes, while one in `RimBank.Party.PartyReferenceDataDirectory.Api` or in `RimBank.Party.PartyReferenceDataDirectory` fails; adapter and composition types are ignored.
- [ ] `ArchitectureRules.Naming.cs`: `AssemblyNameProblems` gets the `RimBank.Core` case (`CoreProject` constant), and allowed context project names need an adapter suffix.
- [ ] New `CoresLiveInRimBankCore` rule (`cores-live-in-rimbank-core`, `SolutionOnly: true`) plus `CoreTypeProblems`; add it to `ArchitectureRules.All` after `AssemblyNames` and update the rule list in the `ArchitectureRules` summary comment.

## 4. Projects
- [ ] `src/RimBank.Core/RimBank.Core.csproj` (`RootNamespace` `RimBank`, a reference to `RimBank.SharedKernel`, and a header comment pointing to ADR 0023), plus its lock file.
- [ ] `tests/RimBank.Core.Tests/RimBank.Core.Tests.csproj` (xunit.v3, Shouldly, references to `RimBank.Core` and `RimBank.SharedKernel.Testing`), plus its lock file. Add `CoreReferencesTests.cs` (namespace `RimBank.Core.Tests`) with `The_core_references_only_the_shared_kernel_and_the_bcl`, which checks `typeof(<marker>).Assembly.GetReferencedAssemblies()`. Without a type, load the assembly by name `RimBank.Core`.
- [ ] Add both projects to `RimBank.slnx` (`/src/` and `/tests/` folders).
- [ ] `RimBank.Architecture.Tests.csproj`: add references to both projects, then refresh `packages.win-x64.lock.json` and `packages.linux-x64.lock.json` (ADR 0014).
- [ ] `SolutionRulesTests`: add `RimBank.Core` and `RimBank.Core.Tests` to `The_solution_loads_every_project_and_never_the_fixtures`, and `RimBank.Core.Tests` to the test-assembly list.
- [ ] `dotnet build`, `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes`.
