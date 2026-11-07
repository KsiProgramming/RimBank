# 0011. Namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>`

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.7

## Context

Mission principle 1 asks for business language everywhere, and `specs/tech-stack.md` §3.1 gives the namespace rule `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]`, with one BIAN service domain per bounded context. §3.2 lists the initial business domain mapping and says it "must be checked against the BIAN v14 Service Landscape and recorded in an ADR" before a service domain is created. Until now this was enforced only by review. Phase 0.7 makes it executable, and every other architecture rule depends on it: the dependency and cross-context rules find a type's context and adapter from its namespace.

## Options considered

1. **Review only.** Keep the rule as prose. Nothing stops a typo in a business domain or an `.Infrastructure` project, and the other rules cannot tell contexts apart reliably.
2. **Free-form context names checked by shape only** (`RimBank.X.Y`). Catches missing segments, but not a business domain that is not in the BIAN landscape.
3. **Shape plus a catalogue of service domains.** The architecture tests hold the §3.2 table as a list of (business domain, service domain) pairs; a namespace or project outside the list fails.

## Decision

Option 3.

- **Namespaces.** Every production type is in a composition project namespace (`RimBank.SharedKernel`, `RimBank.Host`, `RimBank.AppHost`, `RimBank.ServiceDefaults`, or a child of one) or in `RimBank.<BusinessDomain>.<ServiceDomain>[.<…>]` with a catalogued pair. BIAN names are PascalCase.
- **Parts of a context.** The fourth segment is an adapter when it is one of `PublishedLanguage`, `Subscriptions`, `Api`, `Mcp` or `Postgresql`. Any other fourth segment is a business folder of the core (an aggregate, Behavior Qualifier or flow, tech-stack §3.4).
- **Projects.** A `src/` project is named after its root namespace: a composition project name, or `RimBank.<BusinessDomain>.<ServiceDomain>` (the core) with at most one allowed adapter suffix. Every namespace in a project starts with the project name. *Amended by [ADR 0026](0026-flat-context-namespaces.md):* a context's namespace stops at the context or its adapter; folders are not namespace segments. *Amended by [ADR 0023](0023-one-core-project.md):* there is no per-context core project. Every context's core lives in `RimBank.Core`, whose namespaces are the core parts of catalogued contexts, and a context project always has an adapter suffix.
- **Catalogue.** `ServiceDomainCatalogue.Solution` in `tests/RimBank.Architecture.Tests` copies tech-stack §3.2. A new service domain adds its row there in the same PR as its BIAN mapping ADR (for example 1.1), so creating a context without that check fails the build.
- The rules are `namespace-rule` and `assembly-names` in the architecture tests (CONTRIBUTING, "Architecture rules").

## Consequences

- A typo in a business or service domain, a missing segment, or an unknown adapter suffix fails CI with the offending type or project named.
- §3.2 and the catalogue must change together; the catalogue is the executable copy.
- Test projects follow their own rule (tech-stack §3.3, `test-namespaces`); shared test libraries such as `RimBank.SharedKernel.Testing` and `RimBank.Architecture.Fixtures` keep their own names and are not production code.
- A future adapter kind (for example `.RabbitMq`) is added to the allowed suffixes and to tech-stack §3.1 in the PR that introduces it.
