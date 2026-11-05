# Architecture tests

Roadmap phase: 0.7
Branch: plan/0.7-architecture-tests

## Context
Up to now the constitution's structural rules have been prose: the dependency table, the CQRS rules, cross-context isolation, business-only folder names, `readonly record struct` IDs and "no `double` for money". Stage 1 creates the first real context (six projects). The rules have to be executable before then, so the first context is shaped by tests and not only by review.

It depends on 0.4 (`ITypedId<TSelf>`, ADR 0008), 0.5 (`Money`, ADR 0009) and 0.6 (`IDecider` / `IProjection`, ADR 0010), which are all merged. It unblocks every Stage 1 phase: the Definition of done item "Architecture tests green" means something only after this phase.

The main difficulty is that `src/` holds only `RimBank.SharedKernel`, so most rules would pass vacuously on the real solution. The user chose to prove every rule against a **fixture assembly**: a fake "good" context, plus one "bad" context per rule that breaks exactly that rule.

Relevant constitution:
- [mission.md](../mission.md), principles 1 (business language, no `Manager`/`Helper`/`Service`/`Util`/`Repository` folders), 2 (hexagonal, "Architecture tests enforce this"), 5 (CQRS) and 7 (loosely coupled contexts); quality bar "architecture tests for dependency rules".
- [tech-stack.md §3.1](../tech-stack.md#31-namespace-rule) namespace rule, §3.2 business domain mapping, §3.3 test namespaces, §3.4 forbidden folders.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests) dependency table and CQRS rules table.
- [tech-stack.md §8.4](../tech-stack.md#84-read-models-across-contexts-loose-coupling) "architecture tests forbid cross-context references other than `.Subscriptions` → `.PublishedLanguage`".
- [tech-stack.md §9](../tech-stack.md#9-testing-stack): ArchUnitNET.
- ADR 0003 (CQRS), 0004 (loosely coupled contexts), 0008 (typed IDs), 0009 (money), 0010 (decider and projection contracts, "0.7 can check by type…").

**CQRS split:** this phase adds no command, query, handler or read model. It **adds the markers** the rules use to tell the two sides apart (user choice): `ICommand` and `IWritePort` on the write side, `IQuery<TView>` and `IReadPort` on the read side. It also enforces the split: command handlers do not touch read ports or views, query handlers do not touch write ports or aggregates, and projections are pure. No read model is added, so there is no inline/async choice to make.

There is no BIAN service domain in this phase.

## Scope
### In scope
- **CQRS role markers** in `RimBank.SharedKernel` (BCL only, empty marker interfaces with XML docs):
  - `ICommand`: a command record (write side).
  - `IQuery<TView>`: a query record whose handler returns `Result<TView>` (read side).
  - `IWritePort`: a port that loads and appends an aggregate's event stream (for example `PartyDirectory`).
  - `IReadPort`: a port that reads views (for example `PartyDirectoryEnquiry`).
  - A **command handler** is a class with a `Handle` / `HandleAsync` method whose first parameter implements `ICommand`. A **query handler** is the same for `IQuery<>`. A **view** is any `TView` of an `IProjection<TView, TEvent>`.
- **New projects**:
  - `tests/RimBank.Architecture.Tests` (xUnit v3 on MTP, Shouldly, ArchUnitNET): the rules as reusable functions, run against the **real solution** (they must pass) and against the **fixtures** (each must fail on its bad context).
  - `tests/RimBank.Architecture.Fixtures` (class library, not a test project, never referenced by `src/`): a good sample context and one bad context per rule. Stub `Marten`, `Wolverine`, `Npgsql`, `Microsoft.AspNetCore` and `ModelContextProtocol` namespaces stand in for the real packages, so no infrastructure packages are needed.
- **Rules** (each rule names its source in its `Because(...)` text):
  1. **Dependency table** (§4): SharedKernel and `.PublishedLanguage` BCL only; a core does not depend on ASP.NET Core, Marten, Wolverine, Npgsql or another context; `.Api` does not depend on `.Postgresql`; `.Mcp` does not depend on `.Api` or `.Postgresql`; `.Postgresql` does not depend on `.Api` or `.Mcp`; `.Subscriptions` does not depend on another context's core or adapters, or on its own `.Api`, `.Mcp` or `.Postgresql`; no adapter depends on another context's adapters.
  2. **Cross-context isolation** (§8.4): only `.Subscriptions` may depend on another context's `.PublishedLanguage`; `.PublishedLanguage` contains no views (no `IProjection` `TView`) and no core types.
  3. **CQRS** (§4): command handlers do not depend on `IReadPort`, views, `IQuery<>` types or query handlers; query handlers do not depend on `IWritePort`, `IDecider` types or `ICommand` types; a `…Projection` implements `IProjection`, is sealed, has no instance fields and does not depend on ports, handlers or `TimeProvider`.
  4. **Aggregates** (ADR 0010): every type with a static `Evolve` implements `IDecider`, and every `IDecider` implementation is a `sealed record` in a core namespace.
  5. **Naming**: the namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]` (ADR 0011), with business and service domains checked against a `ServiceDomainCatalogue` copied from §3.2 and adapters limited to `.Api`, `.Mcp`, `.Postgresql`, `.Subscriptions`, `.PublishedLanguage`; the composition projects (`RimBank.SharedKernel`, `.Host`, `.AppHost`, `.ServiceDefaults`) are allowed by name; an assembly's name equals its root namespace.
  6. **Forbidden folder names** (§3.4): no namespace segment `Services`, `Repositories`, `Helpers`, `Managers`, `Utils`, `Dtos`, `Commands` or `Queries` in `src/` assemblies.
  7. **Typed IDs** (ADR 0008): every `ITypedId<T>` implementation is a `readonly record struct`.
  8. **No binary floating point** (ADR 0009): no `double` or `float` (including `double?` and generic arguments such as `List<double>`) in fields, properties, parameters or return types in cores, SharedKernel or `.PublishedLanguage`.
  9. **Test namespaces** (§3.3): every type in a `*.Tests` assembly is in a namespace ending with `.Tests`.
- **ADR 0011**: the namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>` (roadmap requirement).
- **ADR 0012**: the CQRS role markers and how the rules classify types.
- **CONTRIBUTING rule catalogue**: a table of every rule, its source (tech-stack section or ADR), and how to run only the architecture tests.

### Out of scope
- The database-role rule (a context cannot read another schema): an integration test in 0.10.
- **Local reference read ports** that command handlers may use (§8.4): not modelled yet. Until 4.2 the rule forbids command handlers from using any `IReadPort` or view. 4.2 adds a reference-port marker and relaxes the rule in the same PR (see Open questions).
- A guard that fails when a new `src/` or `tests/` project is not loaded by the architecture tests: not chosen. A Stage 1 PR adds the project reference itself, and the CONTRIBUTING catalogue says so.
- Rules about HTTP routes, MCP tool names and BIAN operations: conformance in 0.14 and MCP in 0.15.
- Package-level enforcement (for example a banned-package analyzer): the type-dependency rules cover it.
- Idempotency keys or expected versions on `ICommand`: 0.12 and 1.8.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| How rules classify types | Marker interfaces `ICommand`, `IQuery<TView>`, `IWritePort`, `IReadPort` in SharedKernel; handlers classified by the message they handle; views are `IProjection` `TView`s | User choice. Classifying by type means a rule cannot be dodged by renaming, and the compiler checks the markers. Same "contract by interface" style as `IDecider` / `ITypedId` (ADR 0008, 0010) |
| How rules are proven | `RimBank.Architecture.Fixtures`: one good context, one bad context per rule, stub framework namespaces | User choice. Without fixtures every context rule passes vacuously until Stage 1. Stubs avoid pulling Marten or ASP.NET Core into a test library |
| Rule granularity | Rules are namespace/type based, not assembly based | The fixture holds several fake contexts and adapters in one assembly. The "assembly name = root namespace" rule, run on the real solution, then links namespaces to projects |
| Rule shape | Each rule is a function `(Architecture, ServiceDomainCatalogue) → IArchRule`, run by a `[Fact]` on the solution and a `[Theory]` on the fixtures | One definition per rule; the same code is checked in both directions |
| Violation isolation | Every bad fixture context fails **its** rule and passes all the others | Proves the rules do not overlap or fire by accident, and keeps the failure message pointing at one cause |
| Loading the solution | `RimBank.Architecture.Tests` references every `src/` and `tests/` project; the fixtures assembly is loaded separately and never mixed in | Explicit and fast. See open question on referencing test executables |
| Business domain list | `ServiceDomainCatalogue` in the test project, copied from tech-stack §3.2; a new context adds its row | §3.2 requires an ADR before a service domain is created; the catalogue makes that a failing test instead of a review comment |
| Test framework glue | `TngTech.ArchUnitNET` plus its xUnit v3 integration package, if one is published for the pinned xUnit version; otherwise the core package with `Check` wrapped in a Shouldly assertion | tech-stack §9 names ArchUnitNET; ADR 0007 says projects add it in their own phase |
| Build order | Test-first, 5 groups, one commit each | Same rhythm as 0.3–0.6 |

## ADRs to write
- **0011: Namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>`**: BIAN names in PascalCase, one service domain = one context, core without suffix, allowed adapter suffixes, composition projects, assembly name = root namespace, and how the `ServiceDomainCatalogue` test enforces §3.2.
- **0012: CQRS role markers**: the four markers, how handlers and views are classified, why markers instead of names, the temporary "no read port in command handlers" rule until 4.2, and the fixture approach for proving rules.

## Open questions
- ~~**Referencing test executables**~~ Answered: a `ProjectReference` from `RimBank.Architecture.Tests` to `RimBank.SharedKernel.Tests` (both xUnit v3 executables) builds and runs fine. The solution under test is every `RimBank.*` assembly in the test project's output, except the fixtures, so adding the project reference is all a new project needs.
- ~~**ArchUnitNET and xUnit v3 package**~~ Answered: `TngTech.ArchUnitNET` 0.13.4 (netstandard2.0) works on .NET 10 and MTP. The `TngTech.ArchUnitNET.xUnitV3` glue package is not used: it pulls in legacy `System.Net.Http` / `System.Text.RegularExpressions` packages, and the rules raise a `ShouldAssertException` themselves. 0.13.5 was 10 days old at the time; Dependabot can move the pin. Recorded in ADR 0012.
- ~~**Detecting `readonly record struct`**~~ Answered: ArchUnitNET exposes `IsRecord` on classes but not on structs, and its `IsCompilerGenerated` only looks at names. The rule reads `[IsReadOnly]` on the type and a `PrintMembers` method carrying `[CompilerGenerated]`. Reflection was not needed.
- **Reference read ports (4.2):** proposed shape is an `IReferenceReadPort` marker that command handlers may depend on, added with the first local reference read model. To be confirmed in 4.2's ADR.
- **Found during implementation:**
  - The rules are small checks over ArchUnitNET's loaded types and dependency graph (signatures, fields, generic arguments and method bodies), not its fluent DSL. "Another context than mine" and "a view is what a projection returns" do not fit the DSL, and the checks give one plain message per offending type with the rule's source.
  - To keep each bad context breaking exactly one rule, the dependency-table rules look only inside a context and at frameworks; every reference to another context is judged by `cross-context-isolation` alone.
  - Compiler-generated types (lambdas, async state machines) are folded into the type that owns them, so a dependency in an `async` handler body still counts against the handler.
  - The rule set is 20 rules: the seven dependency-table rows are separate rules, `assembly-names` is its own rule, and the projection contract and purity are two rules.
  - The markers were added with the fixtures in the first commit, not in group 4, because the good fixture context needs them.
