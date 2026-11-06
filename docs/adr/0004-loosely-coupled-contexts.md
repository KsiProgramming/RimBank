# 0004. Loosely coupled bounded contexts

- Status: Accepted
- Date: 2026-11-04
- Roadmap phase: n/a (constitution change, applies from 0.7 onwards)

## Context

RimBank starts as a modular monolith: one host, one PostgreSQL server, many service domains (`specs/tech-stack.md` §2). In a single process and database it is easy for contexts to couple quietly: a query joins another context's tables, a core references another context's event types, one context sends another context's commands, or a shared view becomes an unofficial contract. Each of these makes contexts change together and breaks the promise that a context can be extracted by changing only adapters.

Read models are the most likely place for this coupling, because the data another context needs is usually already projected somewhere. ADR 0003 introduced read models in every context, so this risk grows from the first slice.

Changing `specs/mission.md` requires an ADR (ADR 0001).

## Options considered

1. **Shared read models.** Contexts read each other's views directly. Easy, but every view becomes a public contract and contexts can no longer evolve or be extracted independently.
2. **Synchronous queries between contexts.** A context calls another's API to read data at decision time. Contracts are explicit, but contexts become dependent on each other's availability and latency.
3. **Published language with consumer-owned copies.** Contexts share only versioned integration events. A consumer translates them at its edge and keeps its own minimal local read model. Cross-context requests are themselves events.

## Decision

Option 3.

- **Read models are private** to the context that owns them, never put in `.PublishedLanguage` and never read by another context.
- **Integration events** in `.PublishedLanguage` are mapped from domain events, carry the state consumers need, are versioned, and are pinned by schema contract tests.
- **The `.Subscriptions` adapter** is the only project allowed to reference another context's `.PublishedLanguage` (since [ADR 0023](0023-one-core-project.md), the only part of a context allowed to use it: `RimBank.Core` references every context's `.PublishedLanguage`, and each core uses only its own). It receives events through Wolverine's durable inbox, translates them into the context's own language, and records them as local events, so local reference read models are rebuildable without the producer. The core no longer references other contexts' `.PublishedLanguage`.
- **Cross-context requests are events, not commands.** A context publishes a request event; the receiving context's `.Subscriptions` turns it into its own command, decided by its own rules, and publishes the outcome. The requesting record's identifier is the idempotency key.
- **Isolation:** each context has its own Marten store, async daemon, PostgreSQL schema and database role with no grants on other schemas.
- **Cross-context queries** go to a dedicated query-only context fed by events (for example BIAN *Customer Position*), or are composed by the channel.

The suffix `.Subscriptions` was chosen over `.AntiCorruption` (unclear to readers outside DDD) and `.Integration` (confusing next to integration test projects). It implements DDD's anti-corruption layer pattern.

The details are in `specs/mission.md` principle 7 and `specs/tech-stack.md` §2, §3, §4 and §8.4.

## Consequences

- Architecture tests forbid cross-context references other than `.Subscriptions` → `.PublishedLanguage` (roadmap 0.7), and an integration test proves database roles forbid cross-schema reads (roadmap 0.10). Since [ADR 0023](0023-one-core-project.md) every context's core shares the `RimBank.Core` project, so isolation between cores rests on these namespace-based rules alone.
- Consumers accept eventual consistency in their local reference read models; each consuming context documents that staleness in its own ADR (roadmap 4.1, 4.2).
- Multi-context flows such as the internal book transfer are sagas over request and outcome events (roadmap 5.3, 5.4), with compensation instead of distributed transactions.
- Data is duplicated across contexts on purpose, kept minimal to limit PII spread.
