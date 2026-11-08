# Validation: Party Reference Data Directory: Reference BQ (party identifiers)

## Done when (from roadmap)
> **1.10 Reference BQ:** party identifiers (national ID, tax ID, LEI) as value objects; Update command and Retrieve query.
> + Definition of done (roadmap.md)

Some Definition-of-done items are covered elsewhere:
- The MCP tools and their HTTP parity tests come in 1.17, as for 1.8 and 1.9.
- The Reference integration channels stay deferred (ADR 0024).
- "Rebuildable from the event store" is proven here against a real PostgreSQL with `RebuildViewAsync`.

## Automated checks
- [x] **Value objects** (`RimBank.Core.Tests`, `Reference/`):
  - LEI: format, ISO 7064 MOD 97-10 checksum, and normalisation;
  - tax and national numbers: trimmed, with the Max35Text limit;
  - parsing changes: keep / clear, `party.reference-invalid` with per-field `errors`, and `party.reference-field-not-supported` naming each field and its phase.
- [x] **Decision** (Given / When / Then):
  - Update → `PartyReferenceUpdated` with the full details, and a no-op is still appended;
  - not registered → `party.party-not-found`;
  - a foreign BQ id → `party.behavior-qualifier-not-found`;
  - the entry laws (FsCheck) hold with Reference events in the generated histories.
- [x] **Handlers** (in-memory ports):
  - the check order is entry, then BQ id, then version, then decision;
  - a stale version → `party.version-conflict`, with nothing appended;
  - the acknowledgement is built from the decided state;
  - Retrieve checks the BQ id against the view.
- [x] **Projection:**
  - Given Register + `PartyReferenceUpdated` → Then the section and version 2;
  - Demographics and Reference sections are both kept;
  - Register only → no section.
- [x] **Marten** (Party `.Postgresql.Tests`, Testcontainers):
  - read-your-write of the section through `IMessageBus`;
  - the view version equals the stream version;
  - `RebuildViewAsync` recreates the section;
  - the stored event has the stable name `party_reference_updated`.
- [x] **HTTP** (`RimBank.Api.Tests/…/Reference`, Alba + Testcontainers):
  - Update → 200 with the `ETag` and a body built from the acknowledgement;
  - Retrieve → 200 from the view with the `ETag`, and 304 on a matching `If-None-Match`;
  - a partial Update keeps the omitted identifiers, and `""` clears one;
  - 400 for each of: the ids, `If-Match`, an invalid identifier, an unsupported field, `Idempotency-Key`;
  - 401, 404 (entry, BQ id), 409 and 428;
  - every error is `application/problem+json` with `type`, `errorCode` and `traceId`;
  - an idempotent replay returns the first answer;
  - the Register, Retrieve, Update and Demographics tests stay green.
- [x] **Conformance:**
  - `Reference/Update` is a `PUT` command and `Reference/Retrieve` a `GET` query;
  - both are in `_implemented` and match the BIAN YAML;
  - the ADR 0024 catalogue test is green.
- [x] **Architecture tests** green:
  - the command handler uses only `IPartyDirectory` and decisions;
  - `RetrievePartyReferenceHandler` uses only `IPartyDirectoryEnquiry`;
  - the projection is pure;
  - the endpoints dispatch only through `IMessageBus`;
  - the namespaces are flat.
- [x] `RimBank.Host.Tests` green.
- [ ] CI green: locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL.

## Manual / demo checks
- [x] The demo is the Alba suite: `dotnet test --project tests/RimBank.Api.Tests --filter "FullyQualifiedName~PartyReferenceDataDirectory.Api.Tests"`. Every Reference case above is green.
- [ ] Optional, with the AppHost running and a Keycloak token, each step with a fresh `Idempotency-Key`:
  1. `POST /PartyReferenceDataDirectory/Register` with `{"PartyType":"Organisation"}`. Note the id, the `PartyReferenceId` and `ETag: "1"`.
  2. `PUT …/{id}/Reference/{referenceid}/Update` with `If-Match: "1"` and `{"GovernmentIssuedIdentityReference":{"TaxIdentificationNumber":"FR12345678901"},"PartyLegalEntityReference":{"Identification":"5493001kjtiigc8y1r12"}}`. Expect 200, `ETag: "2"`, and the LEI upper-cased.
  3. `GET …/Retrieve`. Expect the same identifiers, with `ETag: "2"`.
  4. Repeat step 2 with `{"DateofBirth":"1990-01-01"}`. Expect 400 `party.reference-field-not-supported`, naming 1.18.
  5. Repeat step 2 with `If-Match: "1"` and a new key. Expect 409 `party.version-conflict`.
- [x] Reading check:
  - the command response is built from the acknowledgement, never from the view;
  - the route parameters are `partyreferencedatadirectoryid` and `referenceid`, as in BIAN;
  - no new code logs identifier values;
  - all 24 BIAN Reference fields are either supported or listed with a phase in `PartyReferenceFields` and ADR 0029.

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 1.10 ticked in the same PR, and rows 1.18–1.20 added
- [x] ADR 0029 written; ADR 0024 amended; tech-stack §3.3 and §3.4 updated
- [x] No identifier values in logs
- [x] `doc-0/` untouched
