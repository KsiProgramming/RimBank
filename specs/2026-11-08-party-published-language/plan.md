# Plan: Party Reference Data Directory: published language

The solution builds and every test passes after each group.

## 1. Published language project and events
- [ ] Create `src/Party/RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage/` (csproj with no package reference, `packages.lock.json`), add it to `RimBank.slnx`, and reference it from `RimBank.Core`.
- [ ] `PartyReferenceDataDirectoryEntryCreated` and `PartyReferenceDataDirectoryEntryUpdated` sealed records (fields in requirements.md), with XML docs naming the BIAN channel and the v1 identity.
- [ ] `PartyTypes` and `DirectoryEntryDateTypes` constants classes.
- [ ] Run the architecture tests: the existing `.PublishedLanguage` rules (BCL only, no views, only own core and `.Subscriptions` may use it) now apply to a real project and stay green.

## 2. Mapper in core
- [ ] `RimBank.Core/Party/PartyReferenceDataDirectory/Publication/PartyReferenceDataDirectoryPublication.cs`: `Created(PartyRegistered, version, eventId)` and `Updated(PartyReferenceDataDirectoryEntryUpdated, version, eventId)`. `PartyType` is mapped to its published string with a switch expression (IDE0066).
- [ ] `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Publication/PartyReferenceDataDirectoryPublicationTests.cs`:
  - Register maps to Created with `OpenDate`;
  - Update maps to Updated with `RefreshDate`;
  - every party type has a published value, and an unknown one is refused;
  - the `PartyType` string, the version and `OccurredAt` are carried.

## 3. Handler cascade and Wolverine wiring
- [ ] `RegisterPartyHandler` returns `(Result<RegisterPartyAcknowledged>, PartyReferenceDataDirectoryEntryCreated?)`, and `UpdatePartyReferenceDataDirectoryEntryHandler` returns `(…, PartyReferenceDataDirectoryEntryUpdated?)`. The event id is `Guid.CreateVersion7(time.GetUtcNow())`, and the event is `null` on every error path.
- [ ] Update the handler tests in `RimBank.Core.Tests`:
  - success yields exactly one event, with version 1 after Register and N+1 after Update;
  - not found, version conflict and decision errors yield `null`.
- [ ] Check the API and MCP callers that `InvokeAsync<Result<…>>` still gets the acknowledgement. `RimBank.Api` should need no change.
- [ ] `RimBank.Postgresql/PublishedLanguageMessageNaming.cs`: an `IMessageTypeNaming` that reads the `MessageIdentity` constant of `*.PublishedLanguage` types, registered once in `AddServiceDomainStore` (`WolverineMessageNaming.InsertFirst`), with unit tests in `RimBank.Postgresql.Tests`.
- [ ] Confirm the events are persisted in the party schema's `wolverine_incoming_envelopes` (durable local queue). With no handler, Wolverine discards them with a log entry. That is accepted until 4.1.

## 4. Outbox integration tests (Testcontainers)
- [ ] `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/Publication/PublishedLanguageOutboxTests.cs`, using a Wolverine tracked session and a test-only subscriber handler:
  - Register publishes exactly one Created with the right id, type, date and version 1;
  - Update publishes exactly one Updated with version 2;
  - a stale `ExpectedVersion` publishes nothing;
  - an unknown entry publishes nothing;
  - a replayed `Idempotency-Key` publishes nothing again;
  - a BQ Update (Demographics) publishes nothing;
  - the stored envelope carries the v1 identity and the expected JSON payload;
  - there is exactly one envelope per committed Control Record event, and none in the host's `wolverine` schema.

## 5. Schema contract
- [ ] `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/Publication/PartyPublishedLanguageSchemaTests.cs` (it references Wolverine, so it can check the real serializer): export each event's JSON schema with `JsonSchemaExporter` and Wolverine's wire settings. Compare it, with the event's channel and identity, to `Publication/Approved/<identity>.approved.json`. On a mismatch, the test writes `*.received.json` (git-ignored) and fails with a message pointing to ADR 0036.
- [ ] Also test: Wolverine writes the same property names, an event survives the wire round trip, a v1 reader tolerates an added field, and each type goes on the wire under its declared identity.

## 6. Catalogue, ADRs and docs
- [ ] `PartyReferenceDataDirectoryCatalogue`: give each CR channel its CLR type, and add a test that every channel catalogued for a done phase has a type and that its address exists in the AsyncAPI YAML.
- [ ] ADR 0036 (Party published language), ADR 0024 integration-channel table, tech-stack §3 tree and §8.4 link, and the CONTRIBUTING "changing a published event" note.
- [ ] Tick roadmap 1.13. Then run `dotnet format --verify-no-changes` and the full `dotnet test`.
