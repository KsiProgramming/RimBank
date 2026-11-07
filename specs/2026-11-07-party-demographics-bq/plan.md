# Plan: Party Reference Data Directory: Demographics BQ

Every group is written test first. After each group, the solution builds and its tests pass (one commit per group).
- Test namespaces end with `.Tests`, and context namespaces stay flat (tech-stack §3.3, ADR 0026).
- Use switch expressions only (IDE0066).
- The Testcontainers and Alba tests need Docker. If it is not available locally, they run in CI.

## 1. Core: Demographics model, decisions, handlers and projection
- [ ] Tests first in `RimBank.Core.Tests/Party/PartyReferenceDataDirectory/` (namespace `RimBank.Party.PartyReferenceDataDirectory.Tests`):
  - `Demographics/DemographicsChangesTests`, for parsing raw text into changes:
    - `null` → keep;
    - `""` or whitespace → clear;
    - text is trimmed;
    - one character over Max140Text or Max2048Text → `party.demographics-invalid`, with every offending field in `errors`.
  - `Demographics/PartyDemographicsTests`: `Apply(changes)` merges field by field and replaces `Employment` whole. `HasDetails` is false when every field is empty.
  - `Demographics/ExchangeActionTypesTests`:
    - `Verify` / `Reject` → the action;
    - missing → `party.exchange-action-type-required`;
    - another case or an unknown value → `party.exchange-action-type-invalid`.
  - `Demographics/UpdateDemographicsTests` (Given / When / Then on the entry decider):
    - the first Update → `DemographicsUpdated` with the full details and `Unverified`;
    - a changing Update after Verify → `Unverified`;
    - a no-op Update after Verify → still `Verified`, and the event is still appended;
    - not registered → `party.party-not-found`;
    - a foreign `DemographicsId` → `party.behavior-qualifier-not-found`.
  - `Demographics/ExchangeDemographicsTests`:
    - Verify / Reject on recorded details → `DemographicsExchanged` with the status and `VerificationDate`;
    - on details never recorded → `party.demographics-not-recorded`;
    - a foreign id → `party.behavior-qualifier-not-found`.
  - `PartyReferenceDataDirectoryEntryTests`: `Evolve` handles both events and keeps the CR fields. Extend `EntryExamples`.
  - `PartyReferenceDataDirectoryEntryLawsProperties`: the generators also produce Demographics Updates and Exchanges, and the laws still hold.
  - `Demographics/UpdateDemographicsHandlerTests` and `ExchangeDemographicsHandlerTests` (`InMemoryPartyDirectory`, `FakeTimeProvider`):
    - success → an acknowledgement with version n+1 and the decided section;
    - the checks run in order (entry, then BQ id, then version, then decision);
    - a failure appends nothing.
  - `Demographics/RetrieveDemographicsHandlerTests` (`InMemoryPartyDirectoryEnquiry`): the view when the id matches; `party.party-not-found`; `party.behavior-qualifier-not-found`.
  - `Retrieval/PartyReferenceDataDirectoryEntryViewProjectionTests`:
    - Given Register + `DemographicsUpdated` + `DemographicsExchanged` → Then the section, the status, the date and version 3;
    - the existing cases still pass, and the section is `null` after Register only.
- [ ] `Demographics/`:
  - `PartyDemographics.cs` (record, plus `Empty`, `Apply` and `HasDetails`) and `Employment.cs`;
  - `DemographicsChanges.cs` (the tri-state fields and `Parse(...) → Result<DemographicsChanges>`);
  - `DemographicsVerificationStatus.cs`, plus `ExchangeActionType.cs` and `ExchangeActionTypes.FromBianName`;
  - `DemographicsErrors.cs` (`DemographicsIdInvalid` (field `demographicsid`), `DemographicsInvalid`, `ExchangeActionTypeRequired` / `Invalid`, `DemographicsNotRecorded` (`Error.BusinessRule`));
  - `BehaviorQualifierNotFound` (`party.behavior-qualifier-not-found`) goes on `PartyReferenceDataDirectoryEntryErrors`, because Reference (1.10) and BankRelations (1.12) reuse it;
  - `Events.cs` (`DemographicsUpdated` and `DemographicsExchanged`, both `PartyReferenceDataDirectoryEntryEvent`).
- [ ] Entry state: add `Demographics` (`PartyDemographics`), `DemographicsVerificationStatus` and `DemographicsVerificationDate`, with the `Evolve` cases.
- [ ] `Decisions.cs`: the decider inputs `UpdateDemographicsDetails(DemographicsId, DemographicsChanges)` and `ExchangeDemographicsDetails(DemographicsId, ExchangeActionType, DateTimeOffset)`, with their `Decide` cases.
- [ ] `Demographics/`: the `UpdateDemographics` and `ExchangeDemographics` commands (`ICommand`: `EntryId`, `DemographicsId`, `ExpectedVersion`, payload, `IdempotencyKey`), their `…Acknowledged` records and handlers, following `UpdatePartyReferenceDataDirectoryEntryHandler` (load, then the checks, then `Decide`, then `AppendToEntry`, then acknowledge from the evolved state).
- [ ] `Demographics/RetrieveDemographics.cs` (`IQuery<PartyReferenceDataDirectoryEntryView>`: `EntryId`, `DemographicsId`) and its handler.
- [ ] View: add a `Demographics` section record (`PartyDemographics? Details`, `Status`, `VerificationDate?`) to `PartyReferenceDataDirectoryEntryView`, and handle both events in the projection.
- [ ] Run `RimBank.Core.Tests` and `RimBank.Architecture.Tests` (CQRS roles: the handlers use only the write port; Retrieve uses only the read port).

## 2. PostgreSQL round trip
- [ ] `PartyReferenceDataDirectoryPostgresql.Configure`: `MapEventType<DemographicsUpdated>("demographics_updated")` and `MapEventType<DemographicsExchanged>("demographics_exchanged")`.
- [ ] Tests first: `DemographicsRoundTripTests` in the Party `.Postgresql.Tests` (Testcontainers, through `IMessageBus`):
  - Register → Update Demographics (version 1) → the acknowledgement has version 2, and `RetrieveDemographics` right after returns the section (inline read-your-write);
  - Exchange Verify (version 2) → `Verified`, with the date;
  - the view version equals the stream version;
  - after deleting the view document, `RebuildViewAsync` recreates an equal view, section included;
  - a stale version → `party.version-conflict`;
  - a foreign `demographicsid` → `party.behavior-qualifier-not-found`;
  - `EthnicityReligion` is not in the stored events or the view (it is not even in the core types, but check the stored JSON).
- [ ] Run the Party `.Postgresql.Tests` and `RimBank.Host.Tests`.

## 3. HTTP Demographics endpoints
- [ ] Tests first in `RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Demographics/`:
  - `UpdateDemographicsRequestTests` and `ExchangeDemographicsRequestTests`: BIAN JSON names (`Socio-EconomicClassification`, `Employment.PartyReference`, …); `EthnicityReligion` is accepted and dropped; the Exchange detail fields are ignored; `ExchangeActionType` is a plain string.
  - `DemographicsResponseTests`: mapping from each acknowledgement and from the view; `VerificationDate` is omitted when `null`; no `EthnicityReligion` property.
  - `UpdateDemographicsEndpointTests`, `ExchangeDemographicsEndpointTests` and `RetrieveDemographicsEndpointTests` (Alba, Testcontainers, test-signed JWTs). Each case starts with Register and uses the returned `DemographicsId`:
    - Update with `If-Match: "1"` → 200, `ETag` `"2"`, and the body shows the details and `Unverified`. GET Retrieve then shows the same section with `ETag` `"2"`, and `If-None-Match: "2"` → 304.
    - Exchange `{"ExchangeActionType":"Verify"}` → 200 `Verified`, with `VerificationDate`. A following changing Update → `Unverified`.
    - Retrieve before any Update → 200, `Unverified` and no detail fields.
    - Errors: 409 stale `If-Match`; 428 no `If-Match`; 400 `W/"1"`; 400 a field too long (`party.demographics-invalid` with `errors`); 400 a missing or invalid action; 422 Exchange before any details; 404 an unknown entry; 404 a foreign `demographicsid`; 400 `not-a-guid` for either id; 400 no `Idempotency-Key`; 401 no token.
    - An idempotent replay returns the same 200 and `ETag`.
    - Every error is `application/problem+json` with `type`, `errorCode` and `traceId`.
- [ ] `src/RimBank.Api/Party/PartyReferenceDataDirectory/Demographics/`:
  - `UpdateDemographicsRequest.cs`, `ExchangeDemographicsRequest.cs` and `EmploymentModel.cs` (or the request's nested record);
  - `DemographicsResponse.cs`, one shape with `From(…Acknowledged)` and `From(view)`;
  - `UpdateDemographicsEndpoint.cs`, `ExchangeDemographicsEndpoint.cs` and `RetrieveDemographicsEndpoint.cs`. Each has the BIAN route and `OperationId` (`Demographics/Update`, `Demographics/Exchange`, `Demographics/Retrieve`), the tag `Demographics`, the BIAN summary, a description (`If-Match`, the ignored fields, the `ExchangeActionType` extension) and `Produces…` for every status. Each parses both ids, then `ReadIfMatch` (commands), then the request's `ToCommand`, then `InvokeAsync`, then `ETag` and 200, or `error.ToHttpResult()`.
- [ ] `PartyReferenceDataDirectoryApi` maps the three endpoints. Add the operations to `PartyReferenceDataDirectoryConformanceTests._implemented`. Check `OpenApiDocumentTests` still passes, and extend it if it lists the operations.
- [ ] Run `RimBank.Api.Tests`, `RimBank.Conformance.Tests`, `RimBank.Architecture.Tests` (`adapters-dispatch-through-the-message-bus`, `http-dependencies`) and `RimBank.Host.Tests`.

## 4. Docs and roadmap
- [ ] New `docs/adr/0028-demographics-exchange-as-verification.md` (from `docs/adr/template.md`).
- [ ] ADR 0024:
  - a "Demographics requests (1.9)" paragraph;
  - the event payloads;
  - the new error codes in the table;
  - link Exchange to 0028.
  - Check that `tests/RimBank.Conformance.Tests/Catalogues/PartyReferenceDataDirectoryCatalogue.cs` still says 1.9 for the three rows.
- [ ] Tech-stack §3.4 tree: the `Demographics/` contents (model, the three flows).
- [ ] XML docs: the `IPartyDirectory` / `IPartyDirectoryEnquiry` remarks still hold, and the view and entry docs mention the Demographics section.
- [ ] Roadmap: tick **1.9**.
- [ ] Run a locked restore, `dotnet build -c Release`, `dotnet test --solution RimBank.slnx` and `dotnet format --verify-no-changes`.
