# 0018. Idempotency keys for commands

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.12

## Context

The mission's quality bar says that every state-changing operation accepts an idempotency key. [tech-stack §5](../../specs/tech-stack.md#5-api--error-contract) requires an `Idempotency-Key` header on every state-changing endpoint, with 409 for idempotency conflicts. §8.1 says that every command carries a key. Roadmap 0.12 asks for a middleware or policy where the same key gives the same result and a different payload gives 409. Queries are naturally idempotent and need no key.

Commands do not only come over HTTP. `.Mcp` tools dispatch the same commands (§6; 4.14 makes a key mandatory for money-moving tools). A `.Subscriptions` adapter turns a redelivered request event into a command whose key is the requesting record's identifier, so that "a redelivered request never moves money twice" (§8.4, 5.4). Every one of those paths reaches the core through `IMessageBus.InvokeAsync<Result<T>>`, inside a handler chain that commits through the context's own store ([ADR 0017](0017-wolverine-wiring.md)).

Two constraints come from earlier decisions:

- Each context owns its schema and role, with no grants elsewhere ([ADR 0016](0016-per-context-marten-stores.md)). A shared table is not available.
- A failed decision commits nothing ([ADR 0017](0017-wolverine-wiring.md)).

## Options considered

Where the key is enforced:

1. **ASP.NET Core middleware that caches whole HTTP responses** per key in a host table. It is simple, but it covers neither MCP nor `.Subscriptions`. It needs a table shared by every context, and the cached response is not committed with the events, so a crash in between replays the wrong thing or nothing.
2. **A check inside each core handler.** It is explicit, but every handler repeats it, the core would need a port for "seen keys", and forgetting it is silent.
3. **A Wolverine chain policy on every command handler of a context, added by `RimBank.Postgresql`.** It uses one rule for every channel and stores the outcome in the handler's own session, so it commits atomically with the events and the outbox. The core stays unaware of it.

How a second call with the same key is detected while the first is still running:

1. **A unique insert:** the second commit fails on the primary key. That failure arrives as an exception from `SaveChangesAsync`, after the handler has run, which would have to be turned back into a value.
2. **A transaction-scoped PostgreSQL advisory lock** (`pg_try_advisory_xact_lock`), taken before the lookup in the handler's transaction. The loser knows at once, before the handler runs, and the lock is released by the commit or rollback.

Where the HTTP header is read:

1. **In `RimBank.Problems`.** It already references ASP.NET Core, but it is about errors only (ADR 0015).
2. **A new small `RimBank.Http` project** for the HTTP conventions every `.Api` shares.

## Decision

- **`IdempotencyKey`** (SharedKernel) is a `readonly record struct` holding an opaque string of 1–255 visible ASCII characters (`!` to `~`, no spaces), with `TryCreate` returning `Result` and a plain-string JSON converter. Its errors are `shared.idempotency-key-missing` and `shared.idempotency-key-invalid` (Validation, 400), and `shared.idempotency-key-reused` and `shared.idempotency-key-in-use` (Conflict, 409).
- **`ICommand` carries the key:** `IdempotencyKey IdempotencyKey { get; }`. A command handler never reads it.
- **Option 3 for enforcement.** `AddServiceDomainStore<TStore, TCore>` adds `CommandIdempotencyPolicy` for the context's namespace. For every handler chain whose message is an `ICommand`, it makes the chain transactional on the context's session and generates:
  1. `IdempotencyGate.EnterAsync<TAck>`: refuse a missing key; begin the session's transaction; take `pg_try_advisory_xact_lock` on a hash of (schema, command type, key), or return `in-use`; load the `IdempotentOutcome` with id `<command type>:<key>`. If one exists with the same fingerprint, return its acknowledgement; if its fingerprint differs, return `reused`. In all of these cases the handler does not run.
  2. The handler.
  3. `IdempotencyGate.Remember<TAck>`: on success only, insert the outcome into the session, before the transactional middleware saves it.
- **A replay or refusal is returned the way a handler returns its result** (`MessageContext.EnqueueCascadingAsync`), so `InvokeAsync<Result<TAck>>` receives it as a value. A replay never cascades the handler's messages again.
- **Fingerprint:** the hexadecimal SHA-256 of the command's System.Text.Json serialization without its `IdempotencyKey` property. Only the fingerprint and the acknowledgement are stored, never the payload (this limits the spread of PII, §10).
- **Only successful outcomes are stored.** A rejected command (for example insufficient funds) commits nothing, so the same key can be retried once its cause is fixed. The caller sees the decision again, not a stale rejection.
- **Scope:** the key is scoped to its context (the outcome lives in its schema) and its command type (part of the id). It is not yet scoped to the caller.
- **`RimBank.Http`** (SharedKernel, `RimBank.Problems`, ASP.NET Core) holds `IdempotencyKeyHeader`, an endpoint parameter bound from the header, and `RequireIdempotencyKey()`, an endpoint or group filter that answers 400 Problem Details before the endpoint runs and marks the endpoint with `IdempotencyKeyRequirement` metadata. One header with one value is accepted; several values are `invalid`. Only `.Api` and `RimBank.Host` may use it.

## Consequences

- Every command in every context is idempotent per key, whichever channel sends it. A context gets this from `AddServiceDomainStore`, and its core does nothing beyond declaring the key on its commands.
- A command handler must return `Result<T>` (alone, in a `Task`, or in a tuple with cascaded messages). Startup fails with a clear message otherwise.
- The outcome table `mt_doc_idempotentoutcome` lives in each context's schema. Marten's `mt_last_modified` records when each outcome was written.
- New architecture rules: `http-only-in-api-and-host` and `http-dependencies`, each with bad fixtures. `RimBank.Http` is a composition project.
- The first `.Api` (1.5) adds `RequireIdempotencyKey()` to its command endpoints (or their group) and passes `idempotency.Value` into the command.
- Deferred:
  - The MCP `idempotencyKey` tool argument (0.15/1.16, mandatory for money-moving tools in 4.14).
  - Scoping keys to the authenticated caller, so that two clients cannot collide or probe each other's keys (0.13/1.15).
  - Expiry and clean-up of stored outcomes, using `mt_last_modified` (7.x hardening).
  - `.Subscriptions` deriving keys from correlation IDs (4.1, 5.4).
- Proven by `tests/RimBank.Postgresql.Tests/Idempotency` (Testcontainers: replay with one event and one outgoing message, reused key, concurrent calls, failed result retried, key in two contexts and on two command types, missing key, queries untouched, no payload stored), `tests/RimBank.Http.Tests` (400 and 409 Problem Details, queries need no key, endpoint metadata) and `tests/RimBank.SharedKernel.Tests/Idempotency`.
