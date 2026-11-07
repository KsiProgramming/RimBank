# Party Reference Data Directory: Update

Roadmap phase: 1.8
Branch: plan/1.8-party-entry-update

## Context
1.2–1.7 built Register (command) and Retrieve (query) for the Control Record end to end. Retrieve returns the entry's stream version as a strong `ETag`. This phase adds the CR's second command, BIAN `PartyReferenceDataDirectoryEntry/Update` (`PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Update`). It is also the first command on an **existing** stream, so it brings optimistic concurrency: the client sends the `ETag` back in `If-Match`, and a stale version is `409 party.version-conflict`. It unblocks the Demographics, Reference and BankRelations Updates (1.9, 1.10, 1.12), which reuse the same expected-version append and `If-Match` handling. It also unblocks the `…Entry/Updated` integration event (1.13) and the MCP Update tool (1.17).

Drivers:
- Mission principles 3 (pure decisions), 4 (event-sourced truth), 5 (a command returns an acknowledgement built from the decided state, never from a read model), 8 (Problem Details) and 9 (idempotency).
- Tech-stack §5: the route follows BIAN; Update is a command; every state-changing endpoint requires `Idempotency-Key`; `If-Match` carries the stream version; 409 is used for concurrency.
- Tech-stack §7: time comes from `TimeProvider`.
- Tech-stack §8.1: a command carries an expected stream version.
- Tech-stack §8.3: the view is inline.
- [ADR 0018](../../docs/adr/0018-idempotency-keys-for-commands.md) and [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md): the `UpdatePartyReferenceDataDirectoryEntry` command, the `PartyReferenceDataDirectoryEntryUpdated` event and `party.version-conflict`.

BIAN source: `PartyReferenceDataDirectoryEntry/Update` in [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml).
- Method `put`, responses 200/400/401/403/404/429/500.
- The body `UpdatePartyReferenceDataDirectoryEntry` has `PartyReference`, `DirectoryEntryDateType` (`OpenDate` | `RefreshDate`), `DirectoryEntryDate` and `PartyType`.
- The spec's 200 entry points at `BadRequest`, which is a spec defect. The intended `UpdatePartyReferenceDataDirectoryEntryResponse` returns the same schema as the request.

**CQRS split:**
- **Command side:** `UpdatePartyReferenceDataDirectoryEntry`. It goes through a pure `Decide`, appends `PartyReferenceDataDirectoryEntryUpdated` with an expected version, and acknowledges from the decided state.
- **Query side:** no new query. The existing `PartyReferenceDataDirectoryEntryView` stays **inline** and gains `RefreshDate`. Its projection handles the new event, so a Retrieve right after Update sees the change.

## Scope
### In scope
- **What Update changes** (chosen in the interview):
  - `PartyType` is optional. When omitted, the entry keeps its current type. When given, it is parsed the same way as in Register, so `Person` and `Organisation` are accepted and any other value is `party.party-type-invalid`.
  - Every accepted Update records the time of the update (from `TimeProvider`) as the entry's **RefreshDate** (BIAN `DirectoryEntryDateType` `RefreshDate`). An Update is therefore always a business fact and always appends an event, even when the party type is unchanged.
  - `PartyReference`, `DirectoryEntryDateType` and `DirectoryEntryDate` in the request are accepted and ignored, as in Register. `PartyReference` belongs to Demographics and Reference (1.9, 1.10), and the directory sets its own dates.
- **Core** (`RimBank.Party.PartyReferenceDataDirectory`):
  - `UpdatePartyReferenceDataDirectoryEntry` (`ICommand`): entry id, expected version, optional party type and idempotency key.
  - `UpdatePartyReferenceDataDirectoryEntryAcknowledged`, built from the decided state and the new version.
  - The handler:
    - an unknown entry → `party.party-not-found`;
    - an expected version that differs from the stored stream version → `party.version-conflict`;
    - nothing is appended on failure.
  - The decider input `UpdatePartyReferenceDataDirectoryEntryDetails`, the `Decide` and `Evolve` cases, the event `PartyReferenceDataDirectoryEntryUpdated(EntryId, PartyType, RefreshDate)`, and `RefreshDate` in the entry state. The event carries the resulting party type in full, ready for 1.13.
  - `PartyReferenceDataDirectoryEntryErrors.VersionConflict`.
  - The view gains `RefreshDate?`, and the projection handles the event.
  - The write port gains `AppendToEntry(entryId, expectedVersion, events)`.
- **`.Postgresql`:** `MartenPartyDirectory.AppendToEntry` appends with Marten's expected-version check.
- **`RimBank.Postgresql`:** a Wolverine policy on command chains retries once on Marten's `ConcurrencyException`. If a concurrent writer wins between the handler's load and the commit, the retry reloads the stream, and the handler's own check answers `party.version-conflict`. The race never becomes a 500.
- **`RimBank.Api`:** `StreamVersionTag.ReadIfMatch` reads the request's `If-Match`.
  - The header must hold exactly one strong tag (`"2"`).
  - A missing header → **428** `shared.if-match-required`.
  - A weak tag, `*`, a list or an unparsable value → **400** `shared.if-match-invalid`.
- **SharedKernel / `RimBank.Problems`:** a new `ErrorKind.PreconditionRequired`, mapped to 428.
- **HTTP endpoint (`RimBank.Api`):** `PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Update` (`Update/` folder).
  - It requires `Idempotency-Key` and `If-Match`. A malformed id → 400 `shared.invalid-identifier`.
  - It answers **200** with the BIAN Update fields plus the entry and BQ ids, in the same shape as the Register response plus `RefreshDate`. The new version is returned as `ETag`.
  - The response is built from the acknowledgement, never from the view.
- **Retrieve response:** gains `RefreshDate`, omitted until the first Update.
- **Conformance:** `_implemented` gains the Update operation.
- **Docs:**
  - New ADR 0027.
  - ADR 0024: an "Update request (1.8)" paragraph, the error codes and the spec's 200 defect.
  - Amend ADR 0006 and ADR 0015 for the new kind, and ADR 0017 for the retry policy.
  - Tech-stack §5: the `If-Match` rules.

### Out of scope
- `PartyReference` (names, identifiers): **1.9 / 1.10**.
- The BQ Updates, which will reuse `AppendToEntry` and `ReadIfMatch`: **1.9, 1.10, 1.12**.
- The `PartyReferenceDataDirectoryEntry/Updated` integration event: **1.13**.
- Full request and response shape conformance: **1.14**.
- Per-operation scopes: **1.15**. Until then, any authenticated caller may update.
- The MCP Update tool and its parity test: **1.17**. MCP has no headers, so the tool will take `expectedVersion` as a required argument.
- Client-supplied dates, a no-op guard, an optional `If-Match` (last writer wins) and an FsCheck version property: not chosen in the interview.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| What Update changes | Optional `PartyType` plus a directory-set `RefreshDate`; `PartyReference` and client dates are ignored | Chosen in the interview. It fits BIAN's `RefreshDate` date type, ADR 0024 (dates come from `TimeProvider`, `PartyReference` belongs to the BQs), and leaves no no-op Updates |
| `If-Match` | Required on every Update. Exactly one strong tag. Missing → 428, malformed → 400, stale → 409 | Chosen in the interview. Required, because a core-banking record must not be silently overwritten. 428 is RFC 6585's status for a missing precondition. 409, not RFC 9110's 412, for a stale tag, as the roadmap and ADR 0024 fix |
| Version check | The handler compares `ExpectedVersion` with the loaded stream length before `Decide`, and the write port appends with Marten's expected version | The common case is a deterministic `Result`, testable in-memory. Marten guards the race at commit |
| Commit-time race | Wolverine `OnException<ConcurrencyException>().RetryTimes(1)` on command chains, in `RimBank.Postgresql` | Wolverine applies retry policies inside `InvokeAsync` (Wolverine docs, error handling). The retry reloads the stream and returns the same `party.version-conflict` over HTTP, and later over MCP, with no exception mapping in the adapters. The failed attempt stored nothing, so idempotency is unaffected |
| Response | 200, Register response shape + `RefreshDate`, from the acknowledgement; new `ETag` | tech-stack §5: command responses come from the decided state. One shape for clients |
| Retrieve | Also returns `RefreshDate`, omitted when `null` | The view now shows the refresh |
| Idempotency | Unchanged convention (ADR 0018). The expected version is part of the command, so a replay with the same key and `If-Match` returns the first answer, and the same key with another `If-Match` is 409 | No new mechanism |
| Error codes | `party.version-conflict` (Conflict, 409, already in ADR 0024); new `shared.if-match-required` (PreconditionRequired, 428) and `shared.if-match-invalid` (Validation, 400) | `If-Match` is an HTTP header shared by every context, so its errors are `shared.*` and live in `RimBank.Api`, next to the `Idempotency-Key` errors |
| Folder | `Update/` in `RimBank.Core` and `RimBank.Api` (namespaces stay flat, ADR 0026) | Matches `Registration/` and `Retrieval/` |
| Existing view documents | No migration. `RefreshDate` is nullable, so documents from before 1.8 read as "never refreshed" | The projection's shape grows only by an optional field |
| Work order | Inside-out, test first: core → Postgres → HTTP → docs | Chosen in the interview |

## ADRs to write
- **ADR 0027 (new): optimistic concurrency for commands.**
  - `If-Match` is required on commands to existing records.
  - Missing → 428, malformed → 400, stale → 409.
  - The handler pre-checks the expected version, and the write port appends with an expected version.
  - One Wolverine retry turns a commit-time race into the same conflict.
  - MCP passes the version as an argument.
- **Amend ADR 0024:**
  - an "Update request (1.8)" paragraph;
  - the exact event name;
  - `shared.if-match-*` in the error table;
  - the spec's 200 → `BadRequest` defect.
- **Amend ADR 0006** (`ErrorKind.PreconditionRequired`) and **ADR 0015** (428 mapping).
- **Amend ADR 0017** (the retry policy on command chains).

## Open questions
- None.
