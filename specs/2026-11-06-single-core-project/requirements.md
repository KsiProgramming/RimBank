# Single core project

Roadmap phase: 0.17
Branch: plan/0.17-single-core-project

## Context
The project brief says: "for the project cs project we should have Core and other depends on it, and core should only define application and domain logic" ([doc-0/ideas.MD](../../doc-0/ideas.MD), lines 8–9). Stage 0 read this as **one core project per service domain**, named after the context (`RimBank.Party.PartyReferenceDataDirectory`), and wrote that into [tech-stack §3](../tech-stack.md#3-naming--solution-layout) and [ADR 0011](../../docs/adr/0011-namespace-rule.md). The intended meaning is **one `RimBank.Core` project** that holds the domain and application logic of every service domain, one folder per context. This is the pattern of the author's `WalletSystem` repository (project `WalletSystem.Core`, namespaces `WalletSystem.Expenses…`). Adapters stay per context and depend on `RimBank.Core`.

This phase corrects the layout before Stage 1 creates the first context. No context exists in `src/` yet, so nothing has to move. It depends on 0.7 (architecture tests, ADR 0011/0012) and 0.10–0.11 (store and Wolverine wiring, ADR 0016/0017). It unblocks 1.2, the first real type, which goes into `RimBank.Core`.

What stays the same:
- **Namespaces.** Core code keeps `RimBank.<BusinessDomain>.<ServiceDomain>[.<BusinessFolder>]`. "Core" never appears in a namespace (mission principle 1).
- **Isolation.** Every dependency and isolation rule in `RimBank.Architecture.Tests` is already namespace-based. `cross-context-isolation` therefore bites inside one assembly, as the fixtures already show: all fixture contexts share one assembly. Mission principle 7 ("never reference each other's core") still holds, but it is enforced by architecture tests instead of missing project references.
- **Runtime wiring.** `AddServiceDomainStore<TStore, TCore>` binds handlers, the store, idempotency and queues by core namespace ([ServiceDomainStoreRegistration.cs](../../src/RimBank.Postgresql/ServiceDomainStoreRegistration.cs)). The fake Ledger and Registry contexts in `RimBank.Postgresql.Tests` already share one assembly and stay on their own stores.

**CQRS split:** none. There are no commands, queries or read models in this phase. It changes only the project layout and rules that commands, queries and views will follow from 1.2.

## Scope
### In scope
- **ADR 0023 "One core project for all service domains".** It covers the decision, the extraction path (a context's folder moves out of `RimBank.Core` into its own core project with unchanged namespaces and code), the consequences for Wolverine discovery (all core handlers are discovered once, and each is bound to its context's store by namespace), visibility (core types stay `public` for their adapters, and isolation is the architecture tests' job), and Stryker (scoped by folder with `--mutate`). It amends ADR 0011 (project names) and adds notes to ADR 0004 (enforcement now by namespace, not by project references) and ADR 0017 (the `TCore` assembly is `RimBank.Core`).
- **New project `src/RimBank.Core/RimBank.Core.csproj`.** `RootNamespace` is `RimBank`, so `Party/PartyReferenceDataDirectory/` maps to `RimBank.Party.PartyReferenceDataDirectory`. It references `RimBank.SharedKernel` only. It has no types yet (no service domain shells, per the "no speculative coverage" non-goal) and gets a `.PublishedLanguage` reference per context when that context is added.
- **New project `tests/RimBank.Core.Tests/`.** It is the one test project for every core, with one folder per context (`Party/PartyReferenceDataDirectory/Registration/` → `RimBank.Party.PartyReferenceDataDirectory.Registration.Tests`). It starts with one real test, because Microsoft Testing Platform fails a run with zero tests: `RimBank.Core` references no assembly other than `RimBank.SharedKernel` and the BCL.
- **Architecture tests:**
  - `assembly-names`: `RimBank.Core` is an allowed project name. Every namespace in it must be the **core part** of a catalogued context (no adapter suffix, no composition namespace, no `RimBank.Core.*`). A bare `RimBank.<BusinessDomain>.<ServiceDomain>` project name is no longer allowed. Context projects are adapters only: `.PublishedLanguage`, `.Subscriptions`, `.Api`, `.Mcp` and `.Postgresql`.
  - New solution-only rule `cores-live-in-rimbank-core`: a type in a context's core namespace lives in the `RimBank.Core` assembly. This catches a core type placed in an adapter project under the core namespace, or in a stray project. Its logic is a pure function, so the negative cases are unit tests (like `assembly-names`).
  - `RimBank.Core` and `RimBank.Core.Tests` are added to `RimBank.Architecture.Tests.csproj` and to `SolutionRulesTests` (assembly lists).
- **Docs:** tech-stack §2 (isolation by namespace and rules; "extraction never changes the core's *code*"), §3.1 (the core is `RimBank.Core`; context projects are adapters), §3.3 (layout tree: `src/RimBank.Core/<BusinessDomain>/<ServiceDomain>/`, `tests/RimBank.Core.Tests/`), §3.4 ("Inside `RimBank.Core`": one folder per context, then business folders), §4 (the Core row: `RimBank.Core`; inside it a context references only its own namespace, SharedKernel and its own `.PublishedLanguage`; adapters reference `RimBank.Core`), and the §8.4 enforcement line. CONTRIBUTING ("Architecture rules" table, plus where new core code goes). Roadmap: the 0.17 row, ticked in the implementation PR.

### Out of scope
- Any service domain code (1.2 onwards).
- Merging adapters (`.Api`, `.Mcp`, `.Postgresql`, `.Subscriptions`) or `.PublishedLanguage` into shared projects. They stay per context: `.PublishedLanguage` must stay BCL-only and shippable on its own to consumers of an extracted context.
- Changing `doc-0/ideas.MD`. It is a read-only project input. ADR 0023 quotes it and records the interpretation.
- Mission changes. Principles 2 and 7 still read correctly ("the core of each service domain" is its folder and namespace), so no constitution ADR beyond 0023 is needed.
- An explicit Testcontainers test for two contexts sharing one core assembly. The existing Ledger and Registry fakes already cover it.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Core layout | One `RimBank.Core` project with all service domains' domain and application logic, one folder per context | The brief's intent (ideas.MD) and the WalletSystem pattern; fewer projects; one place for business logic |
| Namespaces in Core | `RimBank.<BusinessDomain>.<ServiceDomain>[.<BusinessFolder>]`, `RootNamespace = RimBank` | Keeps ADR 0011 namespaces and every namespace-based rule unchanged; no technical word "Core" in business namespaces |
| Core tests | One `RimBank.Core.Tests`, folders per context | Mirrors the production layout; chosen in the planning interview |
| Adapters and `.PublishedLanguage` | Stay per context and reference `RimBank.Core` | Adapters move with the context on extraction, and `.PublishedLanguage` is a standalone BCL-only contract (ADR 0004) |
| Cross-context isolation inside Core | Architecture tests (`cross-context-isolation`, `core-dependencies`), unchanged | Already namespace-based and proven by fixtures that share one assembly |
| New guard rule | `cores-live-in-rimbank-core`, solution-only, pure-function negative cases | Core code cannot drift back into per-context projects or adapters |
| Empty Core project | Created now, no types | Infrastructure, not a service-domain shell; lets 1.2 add the first type without a layout change |
| Visibility | Core types `public` (adapters need commands, queries, ports and views) | `internal` cannot express "visible to my own adapters only" across assemblies without `InternalsVisibleTo` sprawl |

## ADRs to write
- **0023 One core project for all service domains** (amends ADR 0011; notes on ADR 0004 and ADR 0017).

## Open questions
- None.
