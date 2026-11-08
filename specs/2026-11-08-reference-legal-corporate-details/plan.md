# Plan: Party Reference Data Directory: Reference BQ (legal standing, political exposure and documents)

Every group is written test first. After each group, the solution builds and its tests pass (one commit per group).
- Test namespaces end with `.Tests`, and context namespaces stay flat (tech-stack §3.3, ADR 0026).
- Use switch expressions only (IDE0066).
- Business rules live in `RimBank.Core`. The adapters only translate the transport (memory: business logic in core).
- Do not log any submitted Reference value (7.2). Group 5 proves it.
- The Testcontainers and Alba tests need Docker. If it is not available locally, they run in CI.

## 1. Core: value objects
- [ ] Tests first in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Reference/`:
  - `PartyLegalStructureTests`:
    - all 12 values parse in any case (`co-operative`, `SOLE(SOLETRADER)`);
    - they are answered in BIAN spelling;
    - an unknown value is invalid.
  - `PoliticalExposureTests`:
    - the three types parse in any case;
    - the description is trimmed and accepted at 350 characters;
    - 351 characters is invalid, and so is a blank description.
  - `GovernmentIssuedDocumentTests`:
    - a full document parses;
    - the number is required and at most 35 characters, and the name at most 140;
    - `PartyDocumentType` is required and must be known;
    - a validity `To` before `From` is invalid;
    - each date reads both as a date and as a date-time;
    - all parts blank → clear.
  - `DocumentDirectoryEntryReferenceTests`:
    - the identification is trimmed and accepted at 35 characters;
    - 36 characters or blank is invalid.
- [ ] Production code in `src/RimBank.Core/Party/PartyReferenceDataDirectory/Reference/`:
  - enums `PartyLegalStructure`, `PoliticalExposureType` and `PartyDocumentType`, with BIAN-name mapping, following the `ResidencyStatusCode` pattern;
  - `PoliticalExposureDescription`, `GovernmentIssuedDocumentNumber`, `GovernmentIssuedDocumentName` and `DocumentDirectoryEntryReference`, on the `IPartyReferenceText<T>` / `PartyReferenceTextJsonConverter<T>` pattern;
  - `PoliticalExposure` (`Type`, `Description?`);
  - `GovernmentIssuedDocument` (`Type`, `Number`, `Name?`, `ValidFrom?`, `ValidTo?`, `TryCreate`);
  - `PartyLegalStanding` (`LegalStructure?`, `PoliticalExposure?`, `CustomerSince?`; `Empty`, `Apply`, `HasAny`);
  - `PartyDocuments` (`GovernmentIssuedDocument?`, `DocumentDirectoryEntry?`; `Empty`, `Apply`, `HasAny`).

## 2. Core changes, decision and projection, with the HTTP adapter (one commit)
Once the six fields leave `Staged`, an adapter that did not map them would drop them silently. So the core and HTTP changes go in together, as in 1.18 and 1.19.
- [ ] Core tests first:
  - `PartyReferenceChangesTests` (extend):
    - each new field: `null` keeps, and blank or all-blank clears;
    - several invalid values → one `party.reference-invalid` naming every path;
    - refused `PartyDocument`, `Document`, `Identifier`, `Datetime` and `DocumentDirectoryEntry` parts → `party.reference-field-not-supported`, with no phase;
    - `CustomerSinceDate` before 1900-01-01 is invalid;
    - update the pin to **4** staged fields, all tagged **1.21**;
    - move the staged-field examples from `PartyLegalStructure` to `CompanyOfficerRole`.
  - `PartyReferenceDetailsTests`: Apply merges, replaces and clears `LegalStanding` and `Documents`.
  - `UpdatePartyReferenceTests` (Given / When / Then):
    - all six fields → `PartyReferenceUpdated` with the full details, and the 1.10–1.19 values kept;
    - `CustomerSinceDate` tomorrow → `party.reference-invalid`;
    - `CustomerSinceDate` before the recorded `DateofBirth` → invalid, in the same request and across two requests;
    - a description with `Nopoliticalexposure`, or with no type → invalid;
    - switching to `Nopoliticalexposure` while clearing the description → accepted.
  - `Retrieval/PartyReferenceDataDirectoryEntryViewProjectionTests`:
    - the new fields reach the Reference section;
    - a 1.19-shaped `PartyReferenceUpdated` still projects, with the new parts empty.
  - `EntryExamples`: add the 1.20 sample values (`FullestReferenceDetails`). Keep the 1.18 and 1.19 examples unchanged.
- [ ] Core production code:
  - `PartyReferenceInput` gains `string? PartyLegalStructure`, `PoliticalExposureType`, `PoliticalExposureDescription` and `CustomerSinceDate`, plus `PartyDocumentInput? GovernmentIssuedDocument` and `string? DocumentDirectoryEntryIdentification`;
  - `PartyReferenceFields`:
    - the new path constants;
    - the allowed part names of `PartyDocument`, `Document`, `Identifier`, `Datetimeperiod`, `Datetime` and `DocumentDirectoryEntry`;
    - remove the six entries from `Staged`, and re-tag the four references `1.21`;
  - `PartyReferenceChanges` gains one `PartyReferenceChange<T>` per field, parsed after the unsupported-field check;
  - `PartyReferenceDetails` gains `LegalStanding`, `Documents` and their `Current…` accessors, merged in `Apply`;
  - `Decide` (Reference) adds the PEP-description and customer-since invariants on the resulting details, next to the date-of-birth future check;
  - the view projection copies the new parts.
- [ ] HTTP tests first in `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Reference/`:
  - `PartyReferenceRequestTests`:
    - BIAN JSON (with the enum wrappers flattened) → raw values;
    - unknown nested parts are reported as unsupported paths, for example `GovernmentIssuedDocumentDetails.DocumentReference.DocumentStatus`;
  - `PartyReferenceResponseTests`:
    - empty values and parts are left out;
    - the legal structure is answered in BIAN spelling;
    - dates are answered as `YYYY-MM-DD`;
  - `UpdatePartyReferenceEndpointTests`:
    - 200 with the new `ETag` and the full body;
    - 400 `party.reference-invalid` (an invalid enum, a description without exposure, a future `CustomerSinceDate`, a missing document number);
    - 400 `party.reference-field-not-supported` for `CompanyOfficerRole` (1.21) and for `GovernmentIssuedDocumentDetails.PartyReference` (no phase);
    - 409 on a stale `If-Match`;
  - `RetrievePartyReferenceEndpointTests`: GET returns the 1.20 fields, and a matching `If-None-Match` gets 304;
  - `ReferenceScenarios`: add the 1.20 sample payload.
- [ ] HTTP production code in `src/RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`:
  - `PartyDocumentModel`, `DocumentModel`, `IdentifierModel`, `DateTimePeriodModel`, `DateTimeModel` and `DocumentDirectoryEntryModel`;
  - `UpdatePartyReferenceRequest` and `PartyReferenceResponse` gain the six fields;
  - the endpoint collects the unknown nested parts from the extension data, as in 1.19.
- [ ] Conformance: there is no new route. `PartyReferenceDataDirectorySpecificationTests` must still pass.

## 3. PostgreSQL: event and view round-trip
- [ ] Extend `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/PartyReferenceRoundTripTests.cs`:
  - an Update with all six fields → the stream folds back to the same `PartyReferenceDetails`, and the inline view holds the same values, with the enums stored by name;
  - a stored 1.19 `PartyReferenceUpdated` JSON (without the new members) still loads and folds;
  - a projection rebuild gives the same view.
- [ ] Register no new converter if the attributes cover the new types.

## 4. FsCheck partial-update law
- [ ] `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Reference/ReferenceArbitraries.cs`: valid `PartyReferenceDetails` across every recorded field (1.10–1.20), using `RimBank.SharedKernel.Testing` helpers where they fit.
- [ ] `PartyReferencePartialUpdateProperties`:
  - empty changes leave any details unchanged;
  - applying the changes built from details `d` to any details gives `d` on every changed field and keeps every omitted field;
  - applying the same changes twice gives the same result as applying them once.

## 5. Log-capture PII test
- [ ] `tests/RimBank.Host.Testing/`: a capturing `ILoggerProvider` (`CapturedLogs`) that records the formatted messages, the state values, the scopes and the exceptions. A test host can opt into it.
- [ ] `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Reference/ReferenceLogPrivacyTests`:
  - send a successful Update with every Reference field (1.10–1.20);
  - then send a 400 and a 409 Update;
  - then send a Retrieve;
  - assert that no captured entry contains any submitted value (identifiers, names, dates, addresses, contacts, PEP description, document number and name, directory reference).

## 6. Demo, docs and roadmap
- [ ] `src/RimBank.Host/RimBank.Host.http`: add a "Reference BQ" section with:
  - the Update with the 1.20 payload, using `If-Match` from the previous answer;
  - a Retrieve, and a 304 with `If-None-Match`;
  - an error example with the expected Problem Details as a comment.
- [ ] `docs/adr/0032-reference-legal-standing-and-documents.md`, from `template.md`. It opens with the ownership rule (declared, not assessed; assessments go to a future KYC context), then gives the field rules.
- [ ] Amend ADR 0029 (the 1.20 table, the staged table moved to 1.21, the pin count, the PII list) and ADR 0024 (the Reference paragraph).
- [ ] `specs/tech-stack.md` §3.4: add the new files to the `Reference/` tree.
- [ ] `specs/roadmap.md`:
  - reword 1.20 to the six fields and tick it;
  - add **1.21 Reference BQ: corporate and officer references** after 1.11;
  - create the 1.21 issue and board card through the roadmap sync script, after merge as usual.
- [ ] Add an "As built" section to this plan for any differences.
- [ ] Run `dotnet format --verify-no-changes`, then `dotnet build` and `dotnet test` (with Docker).
