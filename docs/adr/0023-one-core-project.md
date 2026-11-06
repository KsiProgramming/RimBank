# 0023. One core project for all service domains

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.17

## Context

The project brief ([doc-0/ideas.MD](../../doc-0/ideas.MD), lines 8–9) says:

> for the project cs project we should have Core and other depends on it, and core should only define application and domain logic like following the next name space RimBank.Domain1 the implementation can be in an other preoject fi in api RimBank.Domain1.Api or postgresql RimBank.Domain1.postgresql

Stage 0 read this as **one core project per service domain**, named after its context (`RimBank.Party.PartyReferenceDataDirectory`), next to its adapter projects. That reading went into `specs/tech-stack.md` §3 and [ADR 0011](0011-namespace-rule.md) ("a `src/` project is named after its root namespace"). The intended meaning is **one `Core` project** that holds the domain and application logic of every service domain, with one folder per context, while the namespaces still name the context (`RimBank.Domain1`) and the adapters stay per context. It is the layout of the author's `WalletSystem` repository: project `WalletSystem.Core`, namespaces `WalletSystem.Expenses…`, and no `Core` in any namespace.

No service domain exists in `src/` yet (Stage 1 creates the first), so the layout can change now without moving any code.

The constitution's isolation promises do not depend on the project layout:

- Every dependency and isolation rule in `tests/RimBank.Architecture.Tests` already finds a type's context and part from its **namespace** (ADR 0011), not its assembly. All the fixture contexts that prove those rules share one assembly, `RimBank.Architecture.Fixtures`.
- `AddServiceDomainStore<TStore, TCore>` ([ADR 0017](0017-wolverine-wiring.md), [ADR 0018](0018-idempotency-keys-for-commands.md)) binds handlers, the store, idempotency and the local queue to the core's **namespace**. The fake Ledger and Registry contexts in `tests/RimBank.Postgresql.Tests` already share one assembly, and each stays on its own store.

## Options considered

1. **One core project per service domain** (the Stage 0 reading). Cross-context code fails to compile because the project reference is missing, and extracting a context moves a whole project. But it does not match the brief, and the solution gains one project per context on top of its adapters.
2. **One core project per business domain** (`RimBank.Party`, `RimBank.Payments`, …). A middle ground that still needs the architecture tests between service domains of the same business domain. That means two isolation mechanisms, and neither the brief nor the BIAN model asks for it.
3. **One `RimBank.Core` project for every service domain**, with one folder per context. Namespaces keep naming the context, and the architecture tests (already namespace-based) keep contexts apart inside it. Adapters and `.PublishedLanguage` stay per context.

## Decision

Option 3.

- **`src/RimBank.Core`** holds the domain and application logic (aggregates, value objects, decisions, commands, queries, handlers, ports, views and pure projections) of every service domain. Its `RootNamespace` is `RimBank`, so the folder `Party/PartyReferenceDataDirectory/` is the namespace `RimBank.Party.PartyReferenceDataDirectory`. The word `Core` never appears in a namespace. It references `RimBank.SharedKernel` and, once a context publishes integration events, that context's own `.PublishedLanguage`. It references nothing technical.
- **`tests/RimBank.Core.Tests`** is the one test project for every core, with the same folders (`Party/PartyReferenceDataDirectory/Registration/` → `RimBank.Party.PartyReferenceDataDirectory.Registration.Tests`, tech-stack §3.3).
- **Adapters stay per context:** `RimBank.<BusinessDomain>.<ServiceDomain>.{Api,Mcp,Postgresql,Subscriptions}`, each referencing `RimBank.Core`. **`.PublishedLanguage` stays per context**, because it is a BCL-only contract that consumers of an extracted context must be able to take on its own ([ADR 0004](0004-loosely-coupled-contexts.md)).
- **Isolation inside `RimBank.Core`** is enforced by the existing namespace-based rules: `cross-context-isolation` (a context's core never uses another context's core, adapters or `.PublishedLanguage`) and `core-dependencies` (no framework, no own adapters). Core types are `public` because their own adapters use them. `internal` cannot mean "visible to my own adapters only" across assemblies without `InternalsVisibleTo` for every adapter of every context.
- **Project names** (amends ADR 0011): a `src/` project is a composition project, `RimBank.Core`, or a context **adapter** `RimBank.<BusinessDomain>.<ServiceDomain>.<Adapter>`. A bare `RimBank.<BusinessDomain>.<ServiceDomain>` project is no longer allowed. Every namespace in `RimBank.Core` must be the core part of a catalogued context: no adapter suffix, no composition namespace, and no `RimBank.Core.*` (`assembly-names`).
- **Core code lives only in `RimBank.Core`:** a type in a context's core namespace in any other assembly fails the new solution-only rule `cores-live-in-rimbank-core`.

## Consequences

- One place for all business logic. A new context adds a folder under `RimBank.Core` and `RimBank.Core.Tests` plus the adapter projects it needs, but no new core project.
- Mission principle 7 ("never reference each other's core") is kept by the architecture tests and no longer by missing project references. A cross-context use still compiles, and CI fails on `cross-context-isolation` with the offending type named.
- **Extracting a context** into its own service moves its folder out of `RimBank.Core` into the new service's own core project. The namespaces stay the same, so the core's **code** does not change. Adapters and host wiring change as before (tech-stack §2).
- **Wolverine discovery:** every context's `AddServiceDomainStore` includes the same `RimBank.Core` assembly, so Wolverine discovers all core handlers once. Each handler is still bound to its own context's store, idempotency policy and local queue by namespace. In the modular monolith every context is registered, so no handler is left without its store.
- **Building and testing:** a change in any context rebuilds `RimBank.Core` and runs all of `RimBank.Core.Tests`. Mutation testing (tech-stack §9, Stryker) on a money-moving context is scoped by folder, for example `--mutate "LoansAndDeposits/CurrentAccount/**"`.
- tech-stack §2, §3.1, §3.3, §3.4, §4 and §8.4 and CONTRIBUTING are updated. ADR 0004 and ADR 0017 get notes that point here.
- Proven by `SolutionOnlyRulesTests` (the `assembly-names` and `cores-live-in-rimbank-core` cases), `SolutionRulesTests` (every rule holds with `RimBank.Core` loaded), the unchanged `FixtureRulesTests` (isolation still bites inside one assembly), and `RimBank.Core.Tests` (`RimBank.Core` references only `RimBank.SharedKernel` and the BCL).
