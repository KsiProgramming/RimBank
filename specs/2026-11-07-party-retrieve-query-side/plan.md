# Plan: Party Reference Data Directory: Retrieve flow (query side)

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). Core code goes in `src/RimBank.Core/Party/PartyReferenceDataDirectory/`, and tests go in the same folders under `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/`. Test namespaces end with `.Tests`, for example `RimBank.Party.PartyReferenceDataDirectory.Retrieval.Tests` (tech-stack §3.3). Use switch expressions only (IDE0066). Files outside the aggregate folder that name the state type use the `using Entry = …;` alias (1.3).

## 1. View and pure projection
- [x] Tests first (`PartyReferenceDataDirectoryEntryViewProjectionTests`, namespace `RimBank.Party.PartyReferenceDataDirectory.Retrieval.Tests`, with `using EntryViewSpecification = RimBank.SharedKernel.Testing.ProjectionSpecification<…ViewProjection, …View, PartyReferenceDataDirectoryEntryEvent>;` and the `EntryExamples` from 1.2):
  - Given `PartyRegistered` for a `Person` and for an `Organisation` → Then the view with `Id` = the entry's Guid, `Version` 1, and the party type, open date and three BQ ids of the event;
  - Given no events → `ThenNoView`;
  - the view's `EntryId` equals the event's `EntryId`;
  - every later entry event bumps `Version` (a test-only `LaterEntryEvent` stands in until 1.8), and an entry event before registration leaves no view;
  - the view survives a System.Text.Json round trip with the SharedKernel converters (Marten stores it as JSON), and `EntryId` is not serialised.
- [x] `Retrieval/PartyReferenceDataDirectoryEntryView.cs` as in requirements.md, with XML docs naming BIAN `PartyReferenceDataDirectoryEntry/Retrieve` and saying that `Version` is the entry's stream version (the `ETag` in 1.7).
- [x] `Retrieval/PartyReferenceDataDirectoryEntryViewProjection.cs`: `PartyRegistered` → new view at version 1; any other entry event → `view is null ? null : view with { Version = view.Version + 1 }`. XML docs explain why every entry event bumps the version.
- [x] Run `RimBank.Architecture.Tests`: the projection is classified as a projection (`IProjection`, name ends with `Projection`), is pure, and uses no port, handler or `TimeProvider`.

## 2. Read port, in-memory adapter, query and handler
- [x] `IPartyDirectoryEnquiry.cs` (namespace `RimBank.Party.PartyReferenceDataDirectory`, `: IReadPort`): `FindEntryAsync(entryId, cancellationToken)` returning `null` for an unknown entry.
- [x] Tests first (`InMemoryPartyDirectoryEnquiryTests`, namespace `RimBank.Party.PartyReferenceDataDirectory.Tests`):
  - an unknown entry gives `null`;
  - after `Seed(entryId, events)`, `FindEntryAsync` gives the view the projection folds from those events.
- [x] `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/InMemoryPartyDirectoryEnquiry.cs`: a `sealed class` over a dictionary of views, whose `Seed` folds events from `null` with `PartyReferenceDataDirectoryEntryViewProjection.Project`.
- [x] `PartyReferenceDataDirectoryEntryErrors.PartyNotFound`: `Error.NotFound("party.party-not-found", "This party is not registered in the directory.")`. Its code and kind are asserted by the handler's not-found test, as the entry's other errors are tested through their callers.
- [x] `Retrieval/RetrievePartyReferenceDataDirectoryEntry.cs` (query) with XML docs naming the BIAN operation.
- [x] Tests first (`RetrievePartyReferenceDataDirectoryEntryHandlerTests`, namespace `RimBank.Party.PartyReferenceDataDirectory.Retrieval.Tests`):
  - a registered entry → success with the projected view (`Version` 1);
  - an unknown entry → `ErrorKind.NotFound`, `party.party-not-found`;
  - two seeded entries → each query returns its own view.
- [x] `Retrieval/RetrievePartyReferenceDataDirectoryEntryHandler.cs`: a static class with `HandleAsync(RetrievePartyReferenceDataDirectoryEntry query, IPartyDirectoryEnquiry enquiry, CancellationToken cancellationToken)`, which returns the view or `PartyNotFound`.
- [x] `SolutionRulesTests.The_party_retrieve_flow_is_classified_as_read_side` (next to the 1.3 write-side pin): `RetrievePartyReferenceDataDirectoryEntryHandler` is a query handler; it depends on `IPartyDirectoryEnquiry` and the view only, never on `IPartyDirectory`, the aggregate, a command or a command handler; `IPartyDirectoryEnquiry` is an `IReadPort`; `PartyReferenceDataDirectoryEntryViewProjection` is a projection.
- [x] Run `RimBank.Architecture.Tests` and `RimBank.Host.Tests` (unchanged: the host still starts and Register still works with the handler discovered). The Party `.Api.Tests` need Docker and run in CI.

## 3. Docs and roadmap
- [ ] tech-stack §3.4 tree: `PartyDirectoryEnquiry.cs` → `IPartyDirectoryEnquiry.cs`, and its comment "read port: query the entry and association views".
- [ ] Check that ADR 0024 needs no change (no new error code, event, view or operation row) and leave it untouched.
- [ ] Roadmap: tick **1.6**.
- [ ] Run `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally.
