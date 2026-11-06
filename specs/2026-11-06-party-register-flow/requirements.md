# Party Reference Data Directory: Register flow (command side)

Roadmap phase: 1.3
Branch: plan/1.3-party-register-flow

## Context
1.2 delivered the entry aggregate: the state `PartyReferenceDataDirectoryEntry`, its pure `Decide`/`Evolve`, the domain command `RegisterPartyReferenceDataDirectoryEntry` and the event `PartyRegistered` ([ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md)). This phase adds the application layer around that decision. That means the first real `ICommand` (`RegisterParty`), its handler and acknowledgement, and the context's write port `IPartyDirectory`, proven against an in-memory test adapter. It unblocks 1.4 (the Marten implementation of `IPartyDirectory`) and 1.5 (`POST /PartyReferenceDataDirectory/Register`, whose response is built from this acknowledgement).

It follows tech-stack §8.1 (a handler loads through the write port, calls a pure `Decide`, appends, and returns an acknowledgement, never a read model), §4 (the core handler is a plain class whose `Handle` returns `Result<T>`, with no Wolverine or Marten reference), §7 (time and new ids come through `TimeProvider`), ADR 0012 (role markers; ports are interfaces named with an `I`, so `IPartyDirectory`) and ADR 0018 (the command carries an `IdempotencyKey` that the handler never reads, and the acknowledgement is stored and replayed, so it must be JSON round-trippable).

Wolverine discovers handlers in `RimBank.Core` only when a context calls `AddServiceDomainStore` (ADR 0017). Party gets that call in 1.4, so the new handler is not yet part of the host and cannot break its startup.

**CQRS split:** **write side only**. There is one command (`RegisterParty`), one handler and one write port (`IPartyDirectory`). No query, read port, view or projection is added. The entry view (inline) comes in 1.6.

## Scope
### In scope
- `Registration/RegisterParty.cs`: `sealed record RegisterParty(PartyType PartyType, IdempotencyKey IdempotencyKey) : ICommand`.
- `Registration/RegisterPartyAcknowledged.cs`: `sealed record RegisterPartyAcknowledged(PartyReferenceDataDirectoryEntryId EntryId, long Version, PartyType PartyType, DateTimeOffset OpenDate, DemographicsId DemographicsId, PartyReferenceId PartyReferenceId, BankRelationsId BankRelationsId)`, built from the state the decision produced (tech-stack §5: BIAN's Register response returns the Control Record).
- `Registration/RegisterPartyHandler.cs`: a static class with `HandleAsync(RegisterParty, IPartyDirectory, TimeProvider, CancellationToken)` returning `Task<Result<RegisterPartyAcknowledged>>`. It:
  1. creates the entry id and the three singleton BQ ids (`New(time)`, Guid v7) and the open date (`time.GetUtcNow()`);
  2. loads the entry's history through the port and folds it from `Initial` with `Evolve`;
  3. calls `Decide` with `RegisterPartyReferenceDataDirectoryEntry`;
  4. on success, starts the entry stream with the decided events and returns the acknowledgement (version = the number of events in the history plus the number of decided events). On failure it returns the `Error` and appends nothing.
- `IPartyDirectory.cs` at the context root (`RimBank.Party.PartyReferenceDataDirectory`, `: IWritePort`). It has only what Register needs:
  - `Task<IReadOnlyList<PartyReferenceDataDirectoryEntryEvent>> LoadEntryAsync(PartyReferenceDataDirectoryEntryId entryId, CancellationToken cancellationToken)`: empty for an unknown entry;
  - `void StartEntry(PartyReferenceDataDirectoryEntryId entryId, IReadOnlyList<PartyReferenceDataDirectoryEntryEvent> events)`: stages a new stream, which the handler's transaction commits (as Marten's `StartStream` does).
- **Name clash (open question from 1.2):** files outside the aggregate folder that name the state type use the alias `using Entry = RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntry;`. The folder and namespace stay as in tech-stack §3.4, and no ADR is needed.
- In-memory test adapter `InMemoryPartyDirectory` in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/`, with its own small tests.
- tech-stack §3.4 tree: `PartyDirectory.cs` → `IPartyDirectory.cs` (ADR 0012 naming), and add `RegisterPartyAcknowledged.cs` under `Registration/`.

### Out of scope
- The Marten implementation of `IPartyDirectory`, `AddWritePort`, the `MapEventType` names, the Party `.Postgresql` and its host module, and registering `TimeProvider.System` in the host: **1.4**.
- The HTTP endpoint, request validation and the `Idempotency-Key` header: **1.5**.
- `AppendToEntry` with an expected version: **1.8** (Update). Association streams: **1.11**.
- The view, projection, query and read port: **1.6**.
- Integration events and the outbox: **1.13**. MCP tool: **1.16**.
- `PartyReference` (ISO 20022 `Party`) details and other PII fields: 1.8–1.12, as in 1.2.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Command payload | `PartyType` and `IdempotencyKey` only | Chosen in the planning interview (minimal). This matches the 1.2 domain command. Ids and time are created by the handler (§7, `IDecider` remarks) |
| Acknowledgement | Entry id, stream version, party type, open date, the three BQ ids | Chosen in the planning interview. ADR 0024 says the Register ack returns the BQ ids, and tech-stack §5 builds the BIAN Register response (the CR) from the decided state, never from a read model |
| Version in the ack | `history.Count + events.Count` (1 for a new entry) | Matches Marten's stream version without asking the port, so the core stays port-agnostic. 1.4's integration test checks that the two agree |
| Write port surface | `LoadEntryAsync` + `StartEntry` only | Chosen in the planning interview. No speculative methods (mission non-goals). 1.8 adds the append with an expected version |
| Load before deciding | Yes, even though a new Guid v7 almost never collides | It keeps `Decide` authoritative over the real state (the `party.party-already-registered` path is reachable), at the cost of one read per Register |
| Port name | `IPartyDirectory` in `IPartyDirectory.cs` | `.editorconfig` requires an `I` prefix, and ADR 0012 names it `IPartyDirectory`. The tech-stack tree is corrected |
| Type/namespace clash | `using Entry = …` alias where needed | Chosen in the planning interview. This avoids renaming 1.2's folder and needs no ADR |
| Handler shape | Static class, `HandleAsync` returning `Task<Result<T>>`, with no cascaded messages | Wolverine convention (§4, ADR 0018: the handler must return `Result<T>`). There is nothing to publish until 1.13 |
| In-memory adapter location | `RimBank.Core.Tests` (Party only) | Chosen in the planning interview. A generic shared adapter would be speculative until a second context needs one |
| Build order | Test first: port + in-memory adapter → command, ack and handler → docs | Chosen in the planning interview |

## ADRs to write
- None. ADR 0024 is unchanged: no new error code or event. The ack's BQ ids are already in it.

## Open questions
- None.
