# Plan: Party Reference Data Directory: entry aggregate

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). Core code goes in `src/RimBank.Core/Party/PartyReferenceDataDirectory/` and tests in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/`, in the same folders. Test namespaces end with `.Tests`, for example `RimBank.Party.PartyReferenceDataDirectory.PartyReferenceDataDirectoryEntry.Tests` (tech-stack §3.3).

## 1. Typed ids
- [ ] `RimBank.Core.Tests.csproj`: add `FsCheck.Xunit.v3` (already in `Directory.Packages.props`), then refresh the lock file.
- [ ] Contract tests first, one class each inheriting `TypedIdContractTests<TId>`: `PartyReferenceDataDirectoryEntryIdContractTests`, `DemographicsIdContractTests`, `PartyReferenceIdContractTests`, `BankRelationsIdContractTests`.
- [ ] `PartyReferenceDataDirectoryEntry/PartyReferenceDataDirectoryEntryId.cs`, `Demographics/DemographicsId.cs`, `Reference/PartyReferenceId.cs`, `BankRelations/BankRelationsId.cs`: `readonly record struct`s implementing `ITypedId<TSelf>` by forwarding to `TypedId` (ADR 0008).
- [ ] Run `RimBank.Architecture.Tests`: the typed id rule, namespace rule and cross-context isolation pass on the first real context folder.

## 2. Events and state
- [ ] `PartyReferenceDataDirectoryEntry/PartyType.cs`: `Person`, `Organisation`.
- [ ] `PartyReferenceDataDirectoryEntry/Events.cs`: `abstract record PartyReferenceDataDirectoryEntryEvent`, and `sealed record PartyRegistered(PartyReferenceDataDirectoryEntryId EntryId, PartyType PartyType, DateTimeOffset OpenDate, DemographicsId DemographicsId, PartyReferenceId PartyReferenceId, BankRelationsId BankRelationsId)`.
- [ ] `PartyReferenceDataDirectoryEntry/Decisions.cs`: `abstract record PartyReferenceDataDirectoryEntryCommand` and `sealed record RegisterPartyReferenceDataDirectoryEntry(...)` with the same fields. `Decide` is temporarily a stub (`partial`) so the state compiles.
- [ ] Tests first (`PartyReferenceDataDirectoryEntryTests`): `Initial` is not registered; evolving `PartyRegistered` gives a registered state holding all six values; evolving an unknown event subtype (a private test record) throws `UnreachableException`.
- [ ] `PartyReferenceDataDirectoryEntry/PartyReferenceDataDirectoryEntry.cs`: `sealed partial record` implementing `IDecider<…>`, with `Initial` and a switch-expression `Evolve` (no switch statements, IDE0066).

## 3. Register decision
- [ ] Tests first (`RegisterTests`), with `using EntrySpecification = DeciderSpecification<PartyReferenceDataDirectoryEntry, PartyReferenceDataDirectoryEntryCommand, PartyReferenceDataDirectoryEntryEvent>;`:
  - `Given()` `When(register)` `Then(new PartyRegistered(...))`, once for `Person` and once for `Organisation`.
  - `Given(partyRegistered)` `When(register)` `ThenFails(ErrorKind.Conflict, "party.party-already-registered")`.
  - `Given()` `When(register with (PartyType)99)` `ThenFails(ErrorKind.Validation, "party.party-type-invalid")`.
- [ ] `Decisions.cs`: the pure static `Decide` as a switch expression over (command, state), returning `Decision.Accept<PartyReferenceDataDirectoryEntryEvent>(…)` or an `Error`. The error factories live in one place in the aggregate folder, for example `PartyReferenceDataDirectoryEntryErrors`.
- [ ] Property tests (`PartyReferenceDataDirectoryEntryLawsTests`): a `Gen<PartyReferenceDataDirectoryEntryCommand>` that draws from a small fixed pool of ids (so repeat registrations happen), both `PartyType` values, an occasional undefined `PartyType`, and fixed open dates. Then `[Property]` `DeciderLaws<…>.DecisionsAreDeterministic` and `DeciderLaws<…>.DecidedEventsEvolve`.

## 4. Docs and roadmap
- [ ] ADR 0024 "Error codes" table: add `party.party-already-registered` (Conflict 409, Register on a registered entry) and `party.party-type-invalid` (Validation 400, a `PartyType` that is not `Person` or `Organisation`).
- [ ] tech-stack §3.4 tree: add `PartyType.cs` under `PartyReferenceDataDirectoryEntry/`.
- [ ] Roadmap: tick **1.2**.
- [ ] `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally.
