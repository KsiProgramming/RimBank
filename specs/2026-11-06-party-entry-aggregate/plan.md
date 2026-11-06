# Plan: Party Reference Data Directory: entry aggregate

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). Core code goes in `src/RimBank.Core/Party/PartyReferenceDataDirectory/` and tests in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/`, in the same folders. Test namespaces end with `.Tests`, for example `RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests` (tech-stack §3.3).

## 1. Typed ids
- [x] `RimBank.Core.Tests.csproj`: add `FsCheck.Xunit.v3` (already in `Directory.Packages.props`), then refresh the lock file.
- [x] Contract tests first, one class each inheriting `TypedIdContractTests<TId>`: `PartyReferenceDataDirectoryEntryIdContractTests`, `DemographicsIdContractTests`, `PartyReferenceIdContractTests`, `BankRelationsIdContractTests`.
- [x] `PartyReferenceDataDirectoryEntry/PartyReferenceDataDirectoryEntryId.cs`, `Demographics/DemographicsId.cs`, `Reference/PartyReferenceId.cs`, `BankRelations/BankRelationsId.cs`: `readonly record struct`s implementing `ITypedId<TSelf>` by forwarding to `TypedId` (ADR 0008).
- [x] Run `RimBank.Architecture.Tests`: the typed id rule, namespace rule and cross-context isolation pass on the first real context folder.

## 2. Events and state
- [x] `PartyReferenceDataDirectoryEntry/PartyType.cs`: `Person`, `Organisation`.
- [x] `PartyReferenceDataDirectoryEntry/Events.cs`: `abstract record PartyReferenceDataDirectoryEntryEvent`, and `sealed record PartyRegistered(PartyReferenceDataDirectoryEntryId EntryId, PartyType PartyType, DateTimeOffset OpenDate, DemographicsId DemographicsId, PartyReferenceId PartyReferenceId, BankRelationsId BankRelationsId)`.
- [x] `PartyReferenceDataDirectoryEntry/Decisions.cs`: `abstract record PartyReferenceDataDirectoryEntryCommand` and `sealed record RegisterPartyReferenceDataDirectoryEntry(...)` with the same fields. Groups 2 and 3 landed as one commit, so no stub `Decide` was needed.
- [x] Tests first (`PartyReferenceDataDirectoryEntryTests`): `Initial` is not registered; evolving `PartyRegistered` gives a registered state holding all six values; evolving an unknown event subtype (a private test record) throws `UnreachableException`.
- [x] `PartyReferenceDataDirectoryEntry/PartyReferenceDataDirectoryEntry.cs`: `sealed partial record` implementing `IDecider<…>`, with `Initial` and a switch-expression `Evolve` (no switch statements, IDE0066).

## 3. Register decision
- [x] Tests first (`RegisterTests`), with `using EntrySpecification = DeciderSpecification<PartyReferenceDataDirectoryEntry, PartyReferenceDataDirectoryEntryCommand, PartyReferenceDataDirectoryEntryEvent>;`:
  - `Given()` `When(register)` `Then(new PartyRegistered(...))`, once for `Person` and once for `Organisation`.
  - `Given(partyRegistered)` `When(register)` `ThenFails(ErrorKind.Conflict, "party.party-already-registered")`.
  - `Given()` `When(register with (PartyType)99)` `ThenFails(ErrorKind.Validation, "party.party-type-invalid")`.
- [x] `Decisions.cs`: the pure static `Decide` as a switch expression over (command, state), returning `Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(…)` or an `Error`. The error factories live in one place in the aggregate folder, `PartyReferenceDataDirectoryEntryErrors`.
- [x] Property tests (`PartyReferenceDataDirectoryEntryLawsProperties`, the name the SharedKernel laws tests use): a `Gen<PartyReferenceDataDirectoryEntryCommand>` that draws from a small fixed pool of ids (so repeat registrations happen), both `PartyType` values, an occasional undefined `PartyType`, and fixed open dates. Then `[Property]` `DeciderLaws<…>.DecisionsAreDeterministic` and `DeciderLaws<…>.DecidedEventsEvolve`.

## 4. Docs and roadmap
- [x] ADR 0024 "Error codes" table: add `party.party-already-registered` (Conflict 409, Register on a registered entry) and `party.party-type-invalid` (Validation 400, a `PartyType` that is not `Person` or `Organisation`).
- [x] tech-stack §3.4 tree: add `PartyType.cs` under `PartyReferenceDataDirectoryEntry/`.
- [x] Roadmap: tick **1.2**.
- [x] `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally.
