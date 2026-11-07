# 0027. Optimistic concurrency for commands on existing records

- Status: Accepted
- Date: 2026-11-07
- Roadmap phase: 1.8

## Context

Roadmap 1.8 adds the first command on an existing record, BIAN `PartyReferenceDataDirectoryEntry/Update`. [tech-stack §5](../../specs/tech-stack.md#5-api--error-contract) says a query returns the record's stream version as `ETag`, and that a command may send it back in `If-Match` for optimistic concurrency. [tech-stack §8.1](../../specs/tech-stack.md#81-write-side) says such a command carries an expected stream version. [ADR 0024](0024-party-reference-data-directory-mapping.md) fixes a stale version as `party.version-conflict` (409). Several things were still open:

- whether `If-Match` is optional;
- what a missing or malformed `If-Match` answers;
- where the version is checked;
- what happens when two writers pass the check at the same time.

The Demographics, Reference and BankRelations Updates (1.9, 1.10, 1.12), the association updates (1.11) and every later context reuse the answer, so it is recorded once here.

Facts that shape it:

- BIAN declares 400, 401, 403, 404, 429 and 500, with no status for a missing precondition. RFC 6585 defines **428 Precondition Required** for exactly that case. RFC 9110 defines **412** for a failed `If-Match`, but the roadmap and ADR 0024 already fix 409 for a stale version, which matches ADR 0006's `Conflict` kind.
- A handler loads the stream, decides, and appends. The transactional middleware commits after the handler returns (ADR 0017). Marten checks an expected version at commit and throws `JasperFx.ConcurrencyException` (here `EventStreamUnexpectedMaxEventIdException`) when another writer committed first.
- Wolverine applies its `RetryTimes` error policies inside `IMessageBus.InvokeAsync`, not only to queued messages (Wolverine docs, "Error Handling Rules").
- The idempotency gate's lock is transaction-scoped, and it stores an outcome only on success (ADR 0018). A failed commit therefore leaves nothing behind.

## Options considered

1. **`If-Match` optional, last writer wins without it.** This is friendly to clients, but a core-banking record could then be silently overwritten by a client that never read it.
2. **`If-Match` required, with the version checked only by Marten at commit.** It is simple, but the common case becomes an exception, and each adapter (HTTP, MCP) would have to map that exception to 409 itself.
3. **`If-Match` required, checked by the handler, guarded by Marten, with one retry on a lost race.** The handler compares the expected version with the stream it loaded and returns the context's conflict as a `Result`. The write port appends with that expected version, so Marten refuses a commit that lost a race. A Wolverine policy runs such a command once more, and the handler's own check then answers with the same `Result`.

## Decision

Option 3.

- **`If-Match` is required** on every command that changes an existing record. Commands that create a record (Register, Initiate) take none.
  - **Missing:** 428 `shared.if-match-required`, a new `ErrorKind.PreconditionRequired` (amends ADR 0006 and ADR 0015).
  - **Malformed:** 400 `shared.if-match-invalid`, with `errors["If-Match"]`. This covers anything but exactly one strong tag holding a version of 1 or more, as `StreamVersionTag.Format` writes it. A weak tag, `*`, a list, an unquoted or signed number, `"0"`, two headers, or an empty value are all malformed. `*` is refused because a command must name the version it decided against.
  - **Stale:** 409, with the context's own `<context>.version-conflict` code (`party.version-conflict`).
  - `StreamVersionTag.ReadIfMatch` in `RimBank.Api` reads the header. The `If-Match` errors live beside it, as the `Idempotency-Key` errors do, because the header is HTTP's and is shared by every context.
- **The command carries `ExpectedVersion`.** The handler loads the stream and checks it in this order:
  1. An unknown record is `NotFound`.
  2. A version that differs from the loaded stream length is the version conflict.
  3. Only then does it decide.
  
  The check stays in the handler, not in the pure `Decide`, because the version is a fact of the stream, not of the aggregate's state.
- **The write port appends with the expected version** (`AppendToEntry(id, expectedVersion, events)`, which calls `session.Events.Append(id, expectedVersion + events.Count, events)` in Marten), so a commit that lost a race fails.
- **One retry on a lost race.** `AddServiceDomainStore` adds `CommandConcurrencyRetryPolicy` to every command chain of the context: `OnException<JasperFx.ConcurrencyException>().RetryTimes(1)`. The retry reloads the stream and returns the handler's own conflict, so neither HTTP nor MCP sees an exception. The failed attempt stored nothing: no events, no outbox message, no idempotency outcome, and its advisory lock is released. A second lost race in a row (possible only for a handler that appends at whatever version it loaded) still throws, which is a 500.
- **Idempotency is unchanged.** The expected version is part of the command, so it is part of its fingerprint (ADR 0018). The same key with the same `If-Match` replays the first answer. The same key with another `If-Match` is `shared.idempotency-key-reused` (409).
- **MCP** has no headers. A tool for such a command (1.17) takes `expectedVersion` as a required argument, and a missing one is `shared.tool-arguments-invalid`.

## Consequences

- Every Update-like command follows one recipe: an `ExpectedVersion` on the command, the check in the handler, an append with that version in the write port, and `ReadIfMatch` in the endpoint. 1.9–1.12 reuse `ReadIfMatch`, `AppendToEntry` and the retry policy as they are.
- 428 is a new status in RimBank's contract. It is not declared by BIAN, which is a documented interpretation like 409 and 422 (ADR 0015). Clients get a clear signal to send the `ETag` they read.
- Tests that enforce it:
  - `StreamVersionTagTests` cover the header rules;
  - `ConcurrencyRetryTests` in `RimBank.Postgresql.Tests` show that a lost race is retried once and answers the conflict as a value, and that the retry is bounded;
  - `UpdatePartyRoundTripTests` show that five concurrent Updates at one version give exactly one success and four `party.version-conflict` results;
  - the Party HTTP tests cover 428, 400 and 409.
