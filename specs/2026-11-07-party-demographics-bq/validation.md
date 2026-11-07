# Validation: Party Reference Data Directory: Demographics BQ

## Done when (from roadmap)
> **1.9 Demographics BQ:** model, decisions, Update/Exchange commands, view fields and Retrieve query.
> + Definition of done (roadmap.md)

Some Definition-of-done items are covered elsewhere:
- The MCP tools and their HTTP parity tests come in 1.17, as for 1.8.
- The Demographics integration channels stay deferred (ADR 0024).
- "Rebuildable from the event store" is proven here against a real PostgreSQL with `RebuildViewAsync`.

## Automated checks
- [ ] **Model** (`RimBank.Core.Tests`, `Demographics/`):
  - parsing changes: keep / clear / trim, and the Max140Text and Max2048Text limits → `party.demographics-invalid` with per-field `errors`;
  - `Apply` merge semantics, with `Employment` replaced whole;
  - `ExchangeActionType` parsing → `…-required` / `…-invalid`.
- [ ] **Decisions** (Given / When / Then):
  - Update → `DemographicsUpdated` with the full details. The status is reset only on a change, and a no-op is still appended.
  - Exchange Verify / Reject → `DemographicsExchanged` with the date. With no details → `party.demographics-not-recorded`.
  - Not registered → `party.party-not-found`; a foreign BQ id → `party.behavior-qualifier-not-found`.
  - The entry laws (FsCheck) hold with Demographics events in the generated histories.
- [ ] **Handlers** (in-memory ports):
  - the check order is entry, then BQ id, then version, then decision;
  - a stale or future version → `party.version-conflict`, with nothing appended;
  - acknowledgements are built from the decided state;
  - Retrieve checks the BQ id against the view.
- [ ] **Projection** (`PartyReferenceDataDirectoryEntryViewProjectionTests`): Given Register + Update + Exchange → Then the section, the status, `VerificationDate` and version 3. Register only → no section.
- [ ] **Marten** (Party `.Postgresql.Tests`, Testcontainers):
  - read-your-write of the section through `IMessageBus`;
  - the view version equals the stream version;
  - `RebuildViewAsync` recreates the section;
  - the stored events have stable names (`demographics_updated`, `demographics_exchanged`);
  - no `EthnicityReligion` in the stored JSON.
- [ ] **HTTP** (`RimBank.Api.Tests/…/Demographics`, Alba + Testcontainers):
  - Update and Exchange → 200 with `ETag` and the body built from the acknowledgement; Retrieve → 200 from the view, with the `ETag`, and 304 on a matching `If-None-Match`;
  - 400 (ids, `If-Match`, field length, action, `Idempotency-Key`), 401, 404 (entry, BQ id), 409, 422 and 428, all `application/problem+json` with `type`, `errorCode` and `traceId`;
  - an idempotent replay returns the first answer;
  - `EthnicityReligion` is never returned;
  - the existing Register, Retrieve and Update tests stay green.
- [ ] **Conformance:** `Demographics/Update` and `Demographics/Exchange` are `PUT` commands, `Demographics/Retrieve` is a `GET` query, and all three are in `_implemented` and match the BIAN YAML. The ADR 0024 catalogue test is green.
- [ ] **Architecture tests** green:
  - the command handlers use only `IPartyDirectory` and decisions;
  - `RetrieveDemographicsHandler` uses only `IPartyDirectoryEnquiry`;
  - the projection is pure;
  - the endpoints dispatch only through `IMessageBus`;
  - the namespaces are flat.
- [ ] `RimBank.Host.Tests` green.
- [ ] CI green: locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL.

## Manual / demo checks
- [ ] The demo is the Alba suite: `dotnet test --project tests/RimBank.Api.Tests --filter "FullyQualifiedName~PartyReferenceDataDirectory.Api.Tests"`. Every Demographics case above is green.
- [ ] Optional, with the AppHost running and a Keycloak token, each step with a fresh `Idempotency-Key`:
  1. `POST /PartyReferenceDataDirectory/Register` `{"PartyType":"Person"}` → note the id, the `DemographicsId` and `ETag: "1"`.
  2. `PUT …/{id}/Demographics/{demographicsid}/Update` with `If-Match: "1"` and `{"Socio-EconomicClassification":"B","EthnicityReligion":"x"}` → 200, `ETag: "2"`, `Unverified`, and no `EthnicityReligion`.
  3. `PUT …/Exchange` with `If-Match: "2"` and `{"ExchangeActionType":"Verify"}` → 200, `Verified`, `ETag: "3"`.
  4. `GET …/Retrieve` → the same section, with `ETag: "3"`.
  5. Repeat step 3 with `If-Match: "2"` and a new key → 409 `party.version-conflict`.
- [ ] Reading check:
  - the command responses are built from acknowledgements, never from the view;
  - the route parameters are `partyreferencedatadirectoryid` and `demographicsid`, as in BIAN;
  - no new code logs demographics values.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.9 ticked in the same PR
- [ ] ADR 0028 (Demographics Exchange as verification) written; ADR 0024 amended; tech-stack §3.4 updated
- [ ] `EthnicityReligion` never stored or returned, and no demographics values in logs
- [ ] `doc-0/` untouched
