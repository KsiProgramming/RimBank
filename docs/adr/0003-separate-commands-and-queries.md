# 0003. Separate commands and queries (CQRS)

- Status: Accepted
- Date: 2026-11-04
- Roadmap phase: n/a (constitution change, applies from 0.3 onwards)

## Context

RimBank's aggregates are event-sourced with Marten (`specs/tech-stack.md` §1, §7). BIAN v14 operations fall into two groups: `Retrieve` operations read a Control Record or a Behavior Qualifier, and all other operations (`Register`, `Initiate`, `Update`, `Control`, `Execute`, `Exchange`, `Request`, …) change state (see for example `doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml`).

Event streams are a good write model but a poor query model: replaying a stream to answer every read is slow, cannot serve lists or cross-stream views, and mixes read concerns into the aggregate. At the same time, money-protecting rules (funds availability, closure, holds) must never be decided on stale data.

Changing `specs/mission.md` requires an ADR (ADR 0001).

## Options considered

1. **No separation.** Handlers load the aggregate for both reads and writes. Simple, but reads replay streams, lists and history are impossible, and the aggregate grows read-only fields.
2. **Full physical CQRS everywhere.** Separate read stores and asynchronous projections for every context. Scales well, but adds lag and operational cost where it is not needed.
3. **Logical CQRS everywhere, physical where needed.** Every context separates commands (decisions → events) from queries (projections → views). Views are inline projections by default; asynchronous projections or dedicated stores are added only when a query needs a different shape, cross-stream data or independent scaling.

## Decision

Option 3.

- BIAN `Retrieve` operations are **queries**: `GET` endpoints and `readOnly` MCP tools that read views through a read port.
- All other BIAN operations are **commands**: they go through a pure decision, append events, and return an acknowledgement built from the decision's outcome, never from a read model.
- Money-protecting invariants are checked against write-side state in the same stream and transaction, never against a projection.
- Inline projections serve single-record reads that callers need right after writing; async projections serve lists, history, reporting and audit.
- Every read model can be rebuilt from the event store.

The details are in `specs/tech-stack.md` §4 (CQRS dependency rules), §5, §6 and §8.

## Consequences

- SharedKernel gains a projection contract and Given/Then-view test helpers (roadmap 0.6).
- Architecture tests enforce that command handlers do not use query read models, query handlers do not use write ports or aggregates, and projections are pure (roadmap 0.7).
- Marten wiring covers inline and async projections and a rebuild entry point (roadmap 0.10).
- Each service domain's slice gets separate command-side and query-side phases (for example roadmap 1.3–1.7).
- Async views have visible lag; tests and callers must be able to wait for or detect it, and operations must monitor it (roadmap 7.4).
