# 0012. CQRS role markers and how the architecture rules are proven

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.7

## Context

`specs/tech-stack.md` §4 has a CQRS rules table: command handlers must not use the context's query read models or query handlers, query handlers must not use the write port, aggregates or decisions, and projections must not use ports, handlers or `TimeProvider`. ADR 0003 asks the architecture tests (roadmap 0.7) to enforce it. To do that a rule has to know which types are commands, queries, handlers, ports and views.

There was a second problem. When 0.7 starts, `src/` holds only `RimBank.SharedKernel`, so a rule about contexts, adapters or handlers would pass on the solution only because there is nothing to check yet.

## Options considered

For classifying types:

1. **Naming conventions** (`Retrieve*` / `List*` is a query, `*Handler` a handler, `*Enquiry` a read port). No new types, but a misnamed type escapes the rule without anyone noticing.
2. **Marker interfaces in SharedKernel.** The compiler checks them, a rename cannot dodge a rule, and they follow the "contract by interface" style of `ITypedId` and `IDecider` (ADR 0008, ADR 0010).
3. **Attributes.** Like markers, but easier to forget and invisible to generic constraints.

For proving the rules:

1. **Rules only.** They start to bite in Stage 1.
2. **Violations inside the test assembly.** One project, but project-level dependency rules cannot be shown failing.
3. **A fixture assembly** with good and bad fake contexts.

## Decision

Markers (option 2) and a fixture assembly (option 3).

### Markers

Four empty, BCL-only interfaces in `RimBank.SharedKernel`:

| Marker | Side | Put it on |
|---|---|---|
| `ICommand` | write | a command record (`RegisterParty`) |
| `IWritePort` | write | the port interface that loads and appends an aggregate's stream |
| `IQuery<TView>` | read | a query record (`RetrievePartyReferenceDataDirectoryEntry`) |
| `IReadPort` | read | the port interface that reads views |

The rules derive the rest:

- A **command handler** is a class with a `Handle` / `HandleAsync` method whose first parameter is an `ICommand`; a **query handler** is the same for an `IQuery<>`.
- A **view** is a type returned by the static `Project` of an `IProjection<TView, TEvent>`.
- An **aggregate** is a type implementing `IDecider<TSelf, TCommand, TEvent>`.
- A type "uses" another when ArchUnitNET records a dependency on it anywhere: signature, field, generic argument or method body. Compiler-generated types (lambdas, async state machines) count as part of the type that owns them.

Ports are interfaces, so `.editorconfig` names them with an `I` (`IPartyDirectory`, `IPartyDirectoryEnquiry`).

**Reference read models (tech-stack §8.4).** A command handler may use a local reference read model as a decision input. None exists yet, so for now the rule forbids every `IReadPort` and view in a command handler. Phase 4.2 adds a reference-port marker (proposed: `IReferenceReadPort`) and relaxes the rule in the same PR.

### Proving the rules

- `tests/RimBank.Architecture.Fixtures` is a class library, never referenced by `src/`. It holds two good contexts (`RimBank.SampleBanking.SampleLedger` with all its adapters, and `SampleStatement` with a `.Subscriptions` adapter) and one bad context per rule. Empty types in `Marten`, `Wolverine`, `Npgsql`, `Microsoft.AspNetCore` and `ModelContextProtocol` namespaces stand in for the real packages.
- Every rule is checked in both directions: it holds for the solution and for the good contexts, and it fails on its bad context. Each bad context breaks **exactly one** rule (violation isolation). To keep rules from overlapping, the dependency-table rules look only inside a context and at frameworks; every reference to another context is judged by `cross-context-isolation` alone.
- Two rules read assembly names or test assemblies (`assembly-names`, `test-namespaces`), which a single fixture assembly cannot show. Their checks are pure functions, and their negative cases are tested on plain names.

### Tooling

- `TngTech.ArchUnitNET` 0.13.4 loads the assemblies and provides the dependency graph. The rules are small checks written over that graph, not the fluent DSL, because rules such as "another context than mine" and "a view, defined as a projection's return type" do not fit the DSL. They also give one failure message per offending type with the rule's source. The xUnit v3 glue package is not used: it pulls in legacy `System.Net.Http` packages and adds nothing the `ShouldAssertException`-based assertion does not.
- The solution under test is every `RimBank.*` assembly in the test project's output, which means every project `RimBank.Architecture.Tests.csproj` references, test executables included (a reference from one xUnit v3 test executable to another builds and runs fine).
- ArchUnitNET does not expose `readonly` or `record` on structs. The typed-ID rule reads what the compiler emits instead: `[IsReadOnly]` on the type, and a `[CompilerGenerated]` `PrintMembers` method.

## Consequences

- Stage 1 types carry markers from day one: commands `ICommand`, queries `IQuery<TView>`, ports `IWritePort` / `IReadPort`. A forgotten marker means a type goes unchecked, so reviewers check markers like they check names.
- A new project must be referenced from `RimBank.Architecture.Tests.csproj`, or the rules never see it. There is no guard test for this yet; the CONTRIBUTING new-context checklist covers it.
- Adding a rule means adding a bad fixture context for it; the `Rule_has_a_bad_context_and_fires_nowhere_else` test fails until there is one.
- SharedKernel grows by four empty interfaces; it still has no package or project reference.
