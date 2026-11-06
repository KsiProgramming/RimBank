# Plan: Party Reference Data Directory: Register flow (command side)

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). Core code goes in `src/RimBank.Core/Party/PartyReferenceDataDirectory/`, and tests go in the same folders under `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/`. Test namespaces end with `.Tests`, for example `RimBank.Party.PartyReferenceDataDirectory.Registration.Tests` (tech-stack §3.3). Use switch expressions only (IDE0066).

## 1. Write port and in-memory adapter
- [x] `IPartyDirectory.cs` (namespace `RimBank.Party.PartyReferenceDataDirectory`, `: IWritePort`): `LoadEntryAsync(entryId, cancellationToken)` and `StartEntry(entryId, events)`, with XML docs saying that `StartEntry` only stages the stream and the handler's transaction commits it.
- [x] Tests first (`InMemoryPartyDirectoryTests`, namespace `RimBank.Party.PartyReferenceDataDirectory.Tests`):
  - loading an unknown entry gives an empty history;
  - after `StartEntry`, loading gives the same events in order;
  - starting an existing stream throws `InvalidOperationException` (as Marten refuses a duplicate stream id at commit).
- [x] `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/InMemoryPartyDirectory.cs`: a `sealed class` over a dictionary of streams, with a `StreamOf(entryId)` accessor for assertions and a `Seed(entryId, events)` to preload a history.
- [x] Run `RimBank.Architecture.Tests`: `IPartyDirectory` is a write port in the core with no Marten or Wolverine reference.

## 2. RegisterParty command, acknowledgement and handler
- [x] `Registration/RegisterParty.cs` and `Registration/RegisterPartyAcknowledged.cs` as in requirements.md, with XML docs that name the BIAN operation `PartyReferenceDataDirectoryEntry/Register`.
- [x] Tests first (`RegisterPartyHandlerTests`, namespace `RimBank.Party.PartyReferenceDataDirectory.Registration.Tests`, with `FixedTimeProvider` from `RimBank.SharedKernel.Testing` and a fresh `InMemoryPartyDirectory`):
  - Register a `Person` and an `Organisation`: the result succeeds; the directory holds exactly one stream with one `PartyRegistered` whose fields equal the ack's; the ack's `Version` is 1; `OpenDate` is the fixed time; the four ids are distinct, valid Guid v7 values timestamped at the fixed time.
  - The ack equals the fold of the stored stream (`Evolve` from `Initial`), so it is built from the decided state.
  - An undefined `PartyType` (`(PartyType)99`) fails with `ErrorKind.Validation` and `party.party-type-invalid`, and no stream is started.
  - An entry that is already registered fails with `ErrorKind.Conflict` and `party.party-already-registered`, and nothing is appended. A small private stub `IPartyDirectory` returns a `PartyRegistered` history for every id, because the handler creates its own id.
  - The handler ignores the idempotency key: two commands that differ only in their key give equal results apart from the generated ids.
  - The ack survives a System.Text.Json round trip (ADR 0018 stores and replays it).
- [x] `Registration/RegisterPartyHandler.cs`: a static class with `HandleAsync(RegisterParty command, IPartyDirectory directory, TimeProvider time, CancellationToken cancellationToken)`. It creates the ids and the open date, loads and folds the history with `using Entry = …PartyReferenceDataDirectoryEntry.PartyReferenceDataDirectoryEntry;`, calls `Entry.Decide`, then on success calls `StartEntry` and returns the ack built from `Evolve` of the decided events. On failure it returns the `Error` unchanged.
- [x] Run `RimBank.Architecture.Tests`: `RegisterPartyHandler` is classified as a command handler (first parameter `ICommand`) and uses only `IPartyDirectory`, the aggregate and `TimeProvider`, with no read port, view, query or query handler.

## 3. Docs and roadmap
- [x] tech-stack §3.4 tree: `PartyDirectory.cs` → `IPartyDirectory.cs`, and add `RegisterPartyAcknowledged.cs` under `Registration/`.
- [x] Check that ADR 0024 needs no change (no new error code, event or operation row) and leave it untouched.
- [x] Roadmap: tick **1.3**.
- [x] Run `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally.
