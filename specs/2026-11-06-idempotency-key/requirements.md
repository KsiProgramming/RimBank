# Idempotency

Roadmap phase: 0.12
Branch: plan/0.12-idempotency-key

## Context
Every state-changing operation must accept an idempotency key ([mission.md](../mission.md), quality bar "Idempotency"; principle 9). Every state-changing endpoint must require an `Idempotency-Key` header ([tech-stack.md §5](../tech-stack.md#5-api--error-contract)), and every command carries an idempotency key ([tech-stack.md §8.1](../tech-stack.md#81-write-side)). Later phases depend on it: 1.5 (the first `POST`), 4.14 (MCP money-moving tools that require a key) and 5.4 (a redelivered payment request uses the payment order ID as its key and "moves money only once", §8.4).

This phase depends on 0.3 (`Result`/`Error` with `Conflict`), 0.9 (`Error` → Problem Details, ADR 0015), 0.10 (per-context Marten stores, ADR 0016) and 0.11 (Wolverine dispatch, per-context outbox and the "a failed result commits nothing" rule, ADR 0017). All are merged. The 0.11 spec already anticipated that "idempotency is a Wolverine policy".

Because the key is enforced on the **message bus** and not only at the HTTP edge, the same rule covers `.Api`, `.Mcp` (later) and `.Subscriptions` redeliveries. The record of a handled key is stored **in the context's own schema, in the same transaction as the events**, so a crash cannot leave the events committed without the key record, or the other way round (principle 7, ADR 0016 isolation).

As in 0.10 and 0.11, there is no real context yet, so the convention is proven with the **test-only fake contexts** (`FakeLedger`, `FakeRegistry`) (non-goal "no speculative coverage").

**CQRS split:**
- **Command side (all of this phase):** an idempotency policy wraps every command handler chain. It applies to `ICommand` only.
- **Query side:** none. Queries are naturally idempotent and carry no key (roadmap 0.12). The policy must not touch `IQuery<TView>` chains, and the HTTP binder is not applied to `GET`.
- **Read models:** none. The stored outcome is a write-side record that is never projected or queried by any read port, so it needs no projection, inline or async.

## Scope
### In scope
- **SharedKernel:** `IdempotencyKey`, a value object holding an opaque string (1–255 printable ASCII characters, as allowed by the IETF `Idempotency-Key` header draft), with `TryCreate` returning `Result`. `ICommand` gains `IdempotencyKey IdempotencyKey { get; }`, so every command carries one (§8.1).
- **`RimBank.Postgresql` (extended, no new project):** `AddServiceDomainStore<TStore>` adds a Wolverine middleware to every **command** chain of the context. It:
  1. computes a **fingerprint** (SHA-256 of the command's JSON, with the key left out);
  2. looks up the stored outcome for `(command type, key)` in the context's own store;
  3. **same key and same fingerprint:** returns the stored acknowledgement and does not call the handler (no new events, no new messages);
  4. **same key and a different fingerprint:** returns `Error.Conflict("shared.idempotency-key-reused", …)` as a value;
  5. **new key:** calls the handler. On success, it stores the outcome document in the same session, so it commits atomically with the events and the outbox.
- **Only successful outcomes are stored.** A failed `Result` commits nothing (ADR 0017), so retrying with the same key decides again.
- **Concurrency:** two in-flight calls with the same key commit at most once. The loser does not commit and returns `Error.Conflict("shared.idempotency-key-in-use", …)`.
- **HTTP binder (shared, new small project `RimBank.Http`):** reads the `Idempotency-Key` header into an `IdempotencyKey` for command endpoints. A missing or invalid header gives a 400 Problem Details response (`shared.idempotency-key-missing` / `shared.idempotency-key-invalid`). The two `Conflict` codes map to 409 through `RimBank.Problems`. It is proven on a test-only endpoint.
- **`RimBank.Problems`:** the new `shared.idempotency-*` codes and their documented Problem Details types.
- **Constitution and docs:** tech-stack §3.3 (new project), §4 (a `RimBank.Http` row; `RimBank.Postgresql` row notes the policy), §5 and §8.1 (link to ADR 0018); a CONTRIBUTING "Idempotency" section.
- **ADR 0018.**

### Out of scope
- The MCP `idempotencyKey` tool argument convention (0.15 MCP wiring / 1.16, made mandatory for money-moving tools in 4.14).
- Binding a key to the caller (key scoped per client or principal) after authentication exists (0.13, 1.15). Until then the scope is per context and command type.
- Expiry and clean-up of stored outcomes (7.x hardening; recorded in the ADR as a follow-up).
- Real endpoints (1.5) and `.Subscriptions` deriving keys from correlation IDs (4.1, 5.4).
- Replaying failed outcomes (see Decisions).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Where the key is enforced | A **Wolverine middleware on command chains**, added per context by `RimBank.Postgresql` | Covers HTTP, MCP and `.Subscriptions` with one rule. The core stays free of Wolverine (§4 "Core and Wolverine"). |
| How a command carries the key | A member on `ICommand` (`IdempotencyKey IdempotencyKey { get; }`) | Applies §8.1 ("a command carries an idempotency key") and makes it hard to forget. Subscriptions can derive it from a correlation ID (§8.4). |
| Where outcomes live | A Marten document in the **context's own schema**, written in the handler's session | Atomic with events and outbox. No shared table, so ADR 0016 isolation and "failure stays local" are kept. Rejected: a host-level response cache (shared table, HTTP only). |
| Key scope | `(context, command type, key)` | Two contexts or two operations never clash. Scoping per caller comes with 0.13/1.15. |
| Payload match | SHA-256 fingerprint of the command's JSON without the key | A reused key with a changed payload is detected deterministically, and only the hash is stored, not the payload (limits PII, §10). |
| Which outcomes are stored | **Successful acknowledgements only** | Consistent with "a failed result commits nothing" (ADR 0017). A rejected command (for example insufficient funds) can be retried with the same key once the cause is fixed. |
| Reused key, different payload | `Conflict` `shared.idempotency-key-reused` → 409 | Matches roadmap 0.12 and tech-stack §5 (409 for idempotency conflicts). |
| Concurrent same key | The loser does not commit and returns `Conflict` `shared.idempotency-key-in-use` → 409 | Money correctness: the command is never handled twice. The client retries and then gets the replay. |
| HTTP binder home | New `RimBank.Http` (SharedKernel, `RimBank.Problems`, ASP.NET Core), used only by `.Api` and the host | `RimBank.Problems` stays about errors only (ADR 0015). `.Api` cannot reference `RimBank.Postgresql`. |

## ADRs to write
- **0018 Idempotency keys for commands:** why the bus and not HTTP middleware; the key's format; `ICommand` carrying the key; per-context outcome storage in the same transaction; fingerprint; storing successes only; the reused / in-use conflict codes; the `RimBank.Http` project; follow-ups for MCP (0.15/4.14), caller scoping (0.13/1.15) and expiry (7.x).

## Open questions
Answered during implementation (ADR 0018):
- **In-flight detection:** a transaction-scoped advisory lock (`pg_try_advisory_xact_lock` on a hash of schema, command type and key), taken in the handler's own transaction before the lookup. The loser is refused before its handler runs, so no commit-time exception has to be turned back into a value. `Concurrent_calls_with_the_same_key_commit_once` proves it.
- **Replaying the acknowledgement:** a generated `IChainPolicy` (two JasperFx frames, the same technique as `AddWritePort` in ADR 0017). `Envelope.ResponseType` is internal to Wolverine, so the gate returns a replay or a refusal through `MessageContext.EnqueueCascadingAsync`, the same call Wolverine generates for a handler's return value, and then stops the chain. The acknowledgement is stored as System.Text.Json and read back as the handler's `TAck`.
- **`RimBank.Http`:** kept as a separate project, guarded by `http-only-in-api-and-host` and `http-dependencies`.
- **Additional finding:** the four `shared.idempotency-*` errors live on `IdempotencyKey` in SharedKernel, so the bus policy and the HTTP binder share them. `RimBank.Problems` needed no change, because `Conflict` already maps to 409.
- **Additional finding:** a command handler must return `Result<T>` (alone, in a `Task`, or in a tuple with cascaded messages). The policy fails at startup with a clear message otherwise.
