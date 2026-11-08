# Plan: Party Reference Data Directory: published language

The solution builds and every test passes after each group.

## 1. Published language project and events
- [ ] Create `src/Party/RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage/` (csproj with no package reference, `packages.lock.json`), add it to `RimBank.slnx`, and reference it from `RimBank.Core`.
- [ ] `PartyReferenceDataDirectoryEntryCreated` and `PartyReferenceDataDirectoryEntryUpdated` sealed records (fields in requirements.md), with XML docs naming the BIAN channel and the v1 identity.
- [ ] `PartyTypes` and `DirectoryEntryDateTypes` constants classes.
- [ ] Run the architecture tests: the existing `.PublishedLanguage` rules (BCL only, no views, only own core and `.Subscriptions` may use it) now apply to a real project and stay green.

## 2. Mapper in core
- [ ] `RimBank.Core/Party/PartyReferenceDataDirectory/Publication/PartyReferenceDataDirectoryPublication.cs`: `Publish(PartyReferenceDataDirectoryEntryEvent, long versionAfter, Guid eventId)` returns an integration event or `null`. It is a switch expression over every entry event (IDE0066), and BQ events map to `null`.
- [ ] `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Publication/PartyReferenceDataDirectoryPublicationTests.cs`:
  - Register maps to Created with `OpenDate`;
  - Update maps to Updated with `RefreshDate`;
  - each of the 4 BQ event types maps to `null`;
  - the `PartyType` string, the version and `OccurredAt` are carried.

## 3. Handler cascade and Wolverine wiring
- [ ] `RegisterPartyHandler` returns `(Result<RegisterPartyAcknowledged>, PartyReferenceDataDirectoryEntryCreated?)`, and `UpdatePartyReferenceDataDirectoryEntryHandler` returns `(…, PartyReferenceDataDirectoryEntryUpdated?)`. The event id is `Guid.CreateVersion7(time.GetUtcNow())`, and the event is `null` on every error path.
- [ ] Update the handler tests in `RimBank.Core.Tests`:
  - success yields exactly one event, with version 1 after Register and N+1 after Update;
  - not found, version conflict and decision errors yield `null`.
- [ ] Check the API and MCP callers that `InvokeAsync<Result<…>>` still gets the acknowledgement. `RimBank.Api` should need no change.
- [ ] Check the Wolverine message-identity API with ctx7, then assign `party-reference-data-directory-entry.created.v1` / `.updated.v1` in `PartyReferenceDataDirectoryPostgresql` (or record the fallback from the open question).
- [ ] Confirm the events are persisted in the party schema's `wolverine_outgoing_envelopes` and are routed through the context's durable local queue. With no route, Wolverine discards them with a log entry. That is accepted until 4.1.

## 4. Outbox integration tests (Testcontainers)
- [ ] `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/Publication/PublishedLanguageOutboxTests.cs`, using a Wolverine tracked session and a test-only subscriber handler:
  - Register publishes exactly one Created with the right id, type, date and version 1;
  - Update publishes exactly one Updated with version 2;
  - a stale `ExpectedVersion` publishes nothing;
  - an unknown entry publishes nothing;
  - a replayed `Idempotency-Key` publishes nothing again;
  - a BQ Update (Demographics) publishes nothing;
  - the envelope is written in the same transaction as the events (a rolled-back commit leaves no envelope).

## 5. Schema contract
- [ ] `tests/RimBank.Conformance.Tests/PublishedLanguage/PartyPublishedLanguageSchemaTests.cs`: export each event's JSON schema with `JsonSchemaExporter` and Wolverine's serializer options. Compare it to `…/PublishedLanguage/Approved/party-reference-data-directory-entry.created.v1.schema.json` (and `.updated.v1`). On a mismatch, the test writes `*.received.json` and fails with a message pointing to ADR 0036.
- [ ] One serialisation round-trip test per event (JSON → record → JSON is stable), plus a test that a payload with an extra unknown field still deserialises.

## 6. Catalogue, ADRs and docs
- [ ] `PartyReferenceDataDirectoryCatalogue`: give each CR channel its CLR type, and add a test that every channel catalogued for a done phase has a type and that its address exists in the AsyncAPI YAML.
- [ ] ADR 0036 (Party published language), ADR 0024 integration-channel table, tech-stack §3 tree and §8.4 link, and the CONTRIBUTING "changing a published event" note.
- [ ] Tick roadmap 1.13. Then run `dotnet format --verify-no-changes` and the full `dotnet test`.
