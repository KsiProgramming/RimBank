# Party Reference Data Directory: entry aggregate

Roadmap phase: 1.2
Branch: plan/1.2-party-entry-aggregate

## Context
Stage 0 and 1.1 are done. [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md) fixed the model: the Control Record `PartyReferenceDataDirectoryEntry` is the aggregate root, and Demographics, Reference and BankRelations are entities with one instance each per entry. Their ids are created at Register and recorded in `PartyRegistered`. This phase writes the first business code in `RimBank.Core`: the entry's state, its `Register` decision and the `PartyRegistered` event. It is pure domain code with no handler, port, adapter or view.

It builds on the decider contract and test helpers (ADR 0010: `IDecider`, `Decision`, `DeciderSpecification`, `DeciderLaws`), the typed id pattern (ADR 0008), `Result`/`Error` (ADR 0006) and the one core project (ADR 0023). It unblocks 1.3 (the `RegisterParty` command and handler), 1.4 (the Marten write adapter) and 1.6 (the entry view projected from `PartyRegistered`).

BIAN input: `RegisterPartyReferenceDataDirectoryEntry` in [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) has `PartyReference` (an ISO 20022 `Party`), `PartyType` (`Person` | `Organisation`) and `DirectoryEntryDateType`/`DirectoryEntryDate` (`OpenDate` | `RefreshDate`).

**CQRS split:** this phase covers the **write side** only: one decision (`Register`) and one event. No query, read model or projection is added. The entry view and its inline projection come in 1.6 (ADR 0024, tech-stack §8.3).

## Scope
### In scope
- Folder `src/RimBank.Core/Party/PartyReferenceDataDirectory/` (namespace `RimBank.Party.PartyReferenceDataDirectory`), laid out as in tech-stack §3.4.
- **Typed ids** (ADR 0008, `readonly record struct`, Guid v7):
  - `PartyReferenceDataDirectoryEntryId` in `PartyReferenceDataDirectoryEntry/`
  - `DemographicsId` in `Demographics/`
  - `PartyReferenceId` in `Reference/`
  - `BankRelationsId` in `BankRelations/`
- **`PartyType`** value (`Person`, `Organisation`, from the BIAN `Partytypevalues` enum) in `PartyReferenceDataDirectoryEntry/`.
- **Domain command** (the decider's input, *not* an `ICommand`): base `PartyReferenceDataDirectoryEntryCommand` and `RegisterPartyReferenceDataDirectoryEntry(EntryId, PartyType, OpenDate, DemographicsId, PartyReferenceId, BankRelationsId)`. 1.3's `RegisterParty : ICommand` handler creates the ids and the open date (through `TimeProvider`) and maps to it, so the idempotency key stays out of the domain.
- **Event**: base `PartyReferenceDataDirectoryEntryEvent` and `PartyRegistered` with the same fields. The open date is BIAN's `DirectoryEntryDate` of type `OpenDate`.
- **State** `PartyReferenceDataDirectoryEntry`: a `sealed record` implementing `IDecider<PartyReferenceDataDirectoryEntry, PartyReferenceDataDirectoryEntryCommand, PartyReferenceDataDirectoryEntryEvent>`. `Initial` means "not registered". After `PartyRegistered` it holds the entry id, party type, open date and the three BQ ids. `Evolve` throws `UnreachableException` for an unknown event.
- **Decisions** (`Decisions.cs`, a pure static `Decide` written as a switch expression):
  - From `Initial`: accept and return `PartyRegistered`.
  - From a registered entry: reject with `party.party-already-registered` (Conflict).
  - An undefined `PartyType` value: reject with `party.party-type-invalid` (Validation).
- **ADR 0024 amendment**: add the two new error codes to its error-code table.

### Out of scope
- `RegisterParty : ICommand`, its handler and the `PartyDirectory` write port (1.3); Marten (1.4); HTTP (1.5); the view, projection and query (1.6); MCP (1.16).
- `PartyReference` details (the ISO 20022 `Party` fields such as ContactPoint, Domicile and Residence). They come with 1.8 Update and the BQ phases 1.9–1.12. Leaving them out also keeps PII out of the stream until those phases (ADR 0024 "Personal data").
- `DirectoryEntryDateType` `RefreshDate`: it belongs to Update (1.8).
- Request-field validation and the 400 Problem Details response for it (1.5).
- Any `PartyAssociation` code (1.11).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Register payload | Entry id, `PartyType`, open date, the three singleton BQ ids | Chosen in the planning interview (minimal CR). ADR 0024 requires the BQ ids in `PartyRegistered`. Nothing is added speculatively (mission non-goals) |
| Decider command | Its own domain command `RegisterPartyReferenceDataDirectoryEntry`, separate from 1.3's `RegisterParty : ICommand` | Chosen in the planning interview. The pure decision does not see the idempotency key (ADR 0018), and ids and time come in the command (`IDecider` remarks, ADR 0008) |
| Open date | Passed in the command, set by the handler from `TimeProvider` | Decisions are pure (tech-stack §7) |
| Registering twice | `party.party-already-registered`, Conflict (409) | A stream is registered once. 1.3 always creates a new id, so this guards only against id reuse and bugs |
| Undefined `PartyType` | `party.party-type-invalid`, Validation (400) | C# enums can hold undefined values, and the decision must be total over its input |
| Folder and namespace | `PartyReferenceDataDirectoryEntry/` holds the type of the same name, as in tech-stack §3.4 | Follows the constitution. The resulting name clash for callers is an open question for 1.3 |
| Build order | Test first: ids → events + state/`Evolve` → `Decide` | Chosen in the planning interview |

## ADRs to write
- None. ADR 0024's error-code table is amended (it says a phase that changes the mapping updates it in the same PR).

## Open questions
- **Type/namespace name clash, for 1.3.** IDE0130 makes the namespace follow the folder, so the type `PartyReferenceDataDirectoryEntry` lives in the namespace `RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry`. Inside that namespace and its tests the name resolves to the type. From a sibling such as `Registration/` it resolves to the namespace (CS0118). 1.3 picks the fix: a `using` alias in each file that needs the type, or an ADR to rename the folder (for example `Entry/`) in tech-stack §3.4. 1.2 follows the current §3.4 tree and does not decide this.
