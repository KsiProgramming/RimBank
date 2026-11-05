# Plan: Architecture tests

Each group is one commit. The solution builds and the tests pass after each one. Every rule is written test-first: first the bad fixture context and a failing "fails on its bad context" test, then the rule, then the "passes on the solution" and "passes on the good context" tests. No business code is added to `src/` except the four CQRS markers in SharedKernel.

## 1. Projects, harness and SharedKernel-facing rules
- [ ] Check ArchUnitNET support for xUnit v3 / MTP and .NET 10; add `TngTech.ArchUnitNET` (and the xUnit v3 integration package if it fits) to `Directory.Packages.props` under "Testing"
- [ ] `tests/RimBank.Architecture.Tests/RimBank.Architecture.Tests.csproj` (`OutputType=Exe`, xunit.v3, Shouldly, ArchUnitNET; references `src/RimBank.SharedKernel` and `tests/RimBank.Architecture.Fixtures`); add both projects to `RimBank.slnx` under `/tests/`
- [ ] `tests/RimBank.Architecture.Fixtures/RimBank.Architecture.Fixtures.csproj` (class library, `IsPackable=false`); `Stubs/` with empty types in `Marten`, `Wolverine`, `Npgsql`, `Microsoft.AspNetCore.Http`, `ModelContextProtocol.Server` namespaces (local `NoWarn` only if analysers require it, with a comment)
- [ ] `SolutionArchitecture.cs` (namespace `RimBank.Architecture.Tests`): loads the real assemblies once; `FixtureArchitecture.cs`: loads only the fixtures assembly
- [ ] `ServiceDomainCatalogue.cs`: tech-stack §3.2 rows (business domain, service domain), plus a fixture catalogue for the sample contexts
- [ ] `RuleHarness.cs`: `PassesOn(rule, architecture)`, `FailsOnlyOn(rule, badNamespace)` with a readable failure message (rule, reason, offending types)
- [ ] Rule **SharedKernel is BCL only**: bad fixture type in a `RimBank.SharedKernel`-like stand-in namespace depending on `Marten`
- [ ] Rule **no binary floating point** (fields, properties, parameters, returns, `double?`, generic arguments) for cores, SharedKernel and `.PublishedLanguage`; bad fixture `RimBank.SampleBanking.FloatingLedger` with a `double` amount
- [ ] Rule **typed IDs are `readonly record struct`**; bad fixtures: a `record class` and a non-readonly `record struct` implementing `ITypedId<T>`; answer the "detecting readonly record struct" open question
- [ ] Check locally: Release build (warnings as errors), test, `dotnet format --verify-no-changes`

## 2. Naming rules
- [ ] Good fixture context `RimBank.SampleBanking.SampleLedger` (core, `.Api`, `.Mcp`, `.Postgresql`, `.PublishedLanguage`) and a second good context `RimBank.SampleBanking.SampleStatement` with a `.Subscriptions` adapter, used again in groups 3–4
- [ ] Rule **namespace rule** (ADR 0011): `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]` against the catalogue, allowed adapter suffixes, composition projects by name; bad fixtures: an unknown business domain and an unknown adapter suffix (`.Infrastructure`)
- [ ] Rule **assembly name = root namespace** (solution only; it cannot fail on the single-assembly fixture, so its negative test uses an in-memory type list)
- [ ] Rule **forbidden folder names** (`Services`, `Repositories`, `Helpers`, `Managers`, `Utils`, `Dtos`, `Commands`, `Queries`) for `src/` assemblies; bad fixture `…SampleLedger.Services`
- [ ] Rule **test namespaces end with `.Tests`**: add project references to `RimBank.SharedKernel.Tests` (answer the "referencing test executables" open question; fall back to loading by path); `RimBank.SharedKernel.Testing` and the fixtures are excluded by name; negative test on an in-memory type list

## 3. Dependency table and cross-context isolation
- [ ] Rule set **dependency table** (§4), one rule per row: SharedKernel, core, `.PublishedLanguage`, `.Subscriptions`, `.Api`, `.Mcp`, `.Postgresql`; bad fixtures such as `CoreUsesMarten`, `ApiUsesPostgresql`, `McpUsesApi`, `PostgresqlUsesMcp`, `SubscriptionsUsesOwnApi`, `PublishedLanguageUsesWolverine`
- [ ] Rule **only `.Subscriptions` → other context's `.PublishedLanguage`**; bad fixtures: a core and an `.Api` that reference `SampleLedger.PublishedLanguage` from `SampleStatement`, and a `.Subscriptions` that references another context's core
- [ ] Rule **`.PublishedLanguage` contains no views or core types** (needs the view classification from group 4, so it starts as "no dependency on the own core" and is extended in group 4)
- [ ] `[Theory]` over all bad contexts: each one fails exactly its rule and passes every other rule (violation isolation)

## 4. CQRS markers and CQRS, aggregate and projection rules
- [ ] `src/RimBank.SharedKernel/ICommand.cs`, `IQuery.cs` (`IQuery<TView>`), `IWritePort.cs`, `IReadPort.cs`: empty marker interfaces with XML docs (write side vs read side, the handler classification, link to ADR 0012); `RimBank.SharedKernel.csproj` still has no package or project reference
- [ ] `Classification.cs` in the tests: command handlers, query handlers, views (`TView` of `IProjection`), aggregates (`IDecider` implementers), ports
- [ ] Good fixture context gains a command and handler, a query and handler, a view and its `…Projection`, an aggregate, a write port and a read port
- [ ] Rule **command handler** does not depend on `IReadPort`, views, `IQuery<>` or query handlers; bad fixture `CommandHandlerReadsView`
- [ ] Rule **query handler** does not depend on `IWritePort`, `IDecider` types or `ICommand`; bad fixture `QueryHandlerUsesWritePort`
- [ ] Rule **projection is pure**: `…Projection` implements `IProjection`, is sealed, has no instance fields, and does not depend on ports, handlers or `TimeProvider`; bad fixtures `ProjectionUsesTimeProvider`, `ProjectionNotImplementingContract`
- [ ] Rule **aggregates**: every type with a static `Evolve` implements `IDecider`; every `IDecider` implementation is a `sealed record` in a core namespace; bad fixtures `AggregateWithoutDecider`, `DeciderAsClass`
- [ ] Extend **`.PublishedLanguage` contains no views**; bad fixture `PublishedLanguageExposesView`
- [ ] Re-run the violation-isolation theory with the new bad contexts

## 5. ADRs, docs and roadmap
- [ ] `docs/adr/0011-namespace-rule.md` (MADR, Accepted): the rule, adapter suffixes, composition projects, assembly = root namespace, the `ServiceDomainCatalogue`
- [ ] `docs/adr/0012-cqrs-role-markers.md` (MADR, Accepted): the four markers, handler and view classification, markers vs names, the temporary rule until 4.2, the fixture approach, the ArchUnitNET package choice and the answered open questions
- [ ] `CONTRIBUTING.md`: "Architecture rules" catalogue (rule → source → test class), how to run only them (`dotnet test --project tests/RimBank.Architecture.Tests`), and the checklist for a new context (add the catalogue row, add the project references, mark commands, queries and ports)
- [ ] `specs/tech-stack.md` §4 and §8: name the markers in the CQRS rules table; §3.3: add `RimBank.Architecture.Fixtures` to the layout
- [ ] `specs/roadmap.md`: tick **0.7** (mention the markers in its text)
- [ ] Record the answers to the open questions in requirements.md
