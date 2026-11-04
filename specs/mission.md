# RimBank — Mission

> Part of the RimBank constitution. See also [tech-stack.md](tech-stack.md) and [roadmap.md](roadmap.md).
> Changes to this file require an ADR in `docs/adr/`.

## Mission

**RimBank is an open-source, production-grade core banking platform built in .NET that implements the BIAN (Banking Industry Architecture Network) Service Landscape v14 using Domain-Driven Design and hexagonal architecture.**

We want to show that a bank's core can be built openly, so that its code reads like the business it serves. Every BIAN service domain becomes a bounded context. Its names, flows and capabilities come from BIAN and banking, not from frameworks.

## Why RimBank exists

- **BIAN describes *what* a bank does, but not *how* to build it.** RimBank turns the BIAN v14 specifications into running, tested, deployable software.
- **Core banking is mostly closed and legacy.** RimBank is a modern, transparent, MIT-licensed option that institutions, fintechs and engineers can study, adopt and extend.
- **DDD and BIAN fit together naturally.** A BIAN service domain is a bounded context, a Control Record is an aggregate root, Behavior Qualifiers are parts of the model, and BIAN operations are domain services and commands. RimBank makes that mapping concrete.

## Source of truth

1. **BIAN v14 ISO 20022 extended DDD specifications** (`doc-0/release14.0.0/apis-iso20022_ext-ddd/`) are the authoritative input for:
   - bounded contexts (one per service domain),
   - aggregates (Control Record = aggregate root; each Behavior Qualifier is modelled as an entity inside it or as its own aggregate, decided per domain in an ADR),
   - operations (Initiate, Update, Control, Exchange, Execute, Request, Retrieve, Notify, …). `Retrieve` operations are **queries**; all other operations are **commands** (see principle 5),
   - integration events (AsyncAPI 3 channels such as `PartyReferenceDataDirectoryEntry/Created`),
   - ISO 20022-aligned data types.
2. The **semantic APIs** (`doc-0/release14.0.0/semantic-apis/`) are secondary reference material only.
3. When BIAN is ambiguous or underspecified, the decision is recorded in an ADR, and RimBank's model stays faithful to the *intent* of the service domain.

## Scope

**Near term: a coherent retail core, end to end.**
Party → Product → Account → Ledger positions → Payments → General ledger. This chain must work before we widen the scope.

**Long term: grow across the BIAN landscape.** We add service domains one at a time, in order of business value and dependency, never as empty shells.

## Guiding principles

1. **Business language first.** Namespaces, projects, folders, types and methods use BIAN and banking vocabulary. Domain code has no `Manager`, `Helper`, `Service`, `Util` or `Repository` folders. We organise by aggregate, business capability and flow.
2. **Hexagonal by construction.** The core of each service domain (domain and application logic) depends on nothing technical. Adapters (API, PostgreSQL, messaging) depend on the core, never the other way round. Architecture tests enforce this.
3. **DDD and SOLID, with functional discipline.**
   - Immutable value objects and events; no primitive obsession (strongly typed IDs, `Money`, `Currency`, ISO 20022 types).
   - Decisions are pure functions (`decide(command, state) → events`, `evolve(state, event) → state`).
   - Expected business failures are values (`Result`), not exceptions.
   - Composition over inheritance; types are sealed by default.
4. **Event-sourced truth.** Aggregate state is the result of business events. The audit trail is a built-in part of the model, not an afterthought.
5. **Commands and queries are separate (CQRS).** A command changes state through an aggregate's decision and returns only an acknowledgement. A query reads a read model (a projection of business events) and never changes state. Business decisions never depend on an eventually consistent read model when money or an invariant is at stake. We separate logically everywhere, and add separate or asynchronous read models only where a query needs a different shape, cross-stream data or independent scaling.
6. **Usable by AI agents as well as by applications.** Every service domain's capabilities are available both as BIAN HTTP APIs and as **MCP (Model Context Protocol) tools**. Both are thin adapters over the same core, so an AI agent gets the same business rules, authorisation, audit trail and errors as any other consumer.
7. **Autonomous, loosely coupled bounded contexts.** Service domains never share tables or read models, and never reference each other's core. They integrate through published integration events and, where unavoidable, explicit API calls. In particular:
   - **Read models are private.** A context's views are an internal detail. No other context queries, joins or subscribes to them, and they are never part of a contract.
   - **Consumers own their copy.** When a context needs another context's data, it keeps its own minimal local read model, fed by integration events and translated at its edge by its own subscriptions adapter, so a producer can change its model without breaking consumers.
   - **No callbacks to read.** Integration events carry the data consumers need, so a consumer does not query the producer to complete them.
   - **Failure stays local.** One context's slow or broken projection, or its outage, does not stop another context from accepting commands or answering queries.
8. **Uniform, standard errors.** Every error is returned as RFC 9457 Problem Details with a stable, documented error code. For HTTP this is an `application/problem+json` response; for MCP it is an error tool result whose content is the same Problem Details JSON.
9. **Production-grade from the first commit.** Security, correctness of money, idempotency, observability, and test coverage are part of every phase's definition of done. They are not a later hardening step.
10. **Small, verifiable steps.** Each roadmap phase is small, ships working tested code, and records significant decisions as ADRs.

## Quality bar (non-negotiable for production banking)

- **Money correctness:** `decimal` only, with ISO 4217 currency and minor-unit rules. Rounding rules are explicit, and no money is created or lost (balances can always be reconciled).
- **Idempotency:** every state-changing operation accepts an idempotency key.
- **Auditability:** every state change is traceable to an actor, a time, a correlation ID and a business event.
- **Security:** authenticated by default, authorised per BIAN operation, PII protected at rest and in logs, secrets never in source.
- **Observability:** OpenTelemetry traces, metrics and structured logs in every bounded context.
- **Rebuildable reads:** every read model can be rebuilt from the event streams at any time, without data loss.
- **Tests:** unit tests for domain decisions and projections, integration tests against a real PostgreSQL instance, architecture tests for dependency rules, and conformance tests against the BIAN specs.

## Non-goals

- RimBank is **not a licensed bank** and does not provide regulatory certification. Adopters are responsible for their own compliance.
- **No end-user UI** in scope. RimBank exposes HTTP APIs, MCP tools and events, and channels (including AI agents) are built on top.
- **No AI inside the core.** RimBank provides MCP tools for agents to call; it does not embed models or let model output make business decisions.
- **No real payment-scheme or card-network connectivity** at first. External rails sit behind ports, and adapters can be contributed later.
- **No speculative coverage.** A service domain is added only with real behaviour, tests and an owner, never as generated empty scaffolding.

## License and community

- **License:** MIT.
- Contributions follow the constitution (`specs/`), the ADR process (`docs/adr/`), and the definition of done in [roadmap.md](roadmap.md).
