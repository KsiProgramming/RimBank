# Party Reference Data Directory: Reference BQ (legal standing, political exposure and documents)

Roadmap phase: 1.20
Branch: plan/1.20-reference-legal-corporate-details

## Context
1.10 built BIAN `Reference/Update` and `Reference/Retrieve`. 1.18 and 1.19 then added the names, residency, addresses and contact points. Ten BIAN Reference fields are still refused with `party.reference-field-not-supported`, tagged `1.20` ([ADR 0029](../../docs/adr/0029-reference-fields-in-stages.md)).

They fall into two groups. Both are typed in the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) (`UpdateReference`, `RetrieveReference`):
- **The party's own details (6 fields):**
  - `PartyLegalStructure` (`Partylegalstructuretypevalues`, the UK Open Banking `OBExternalLegalStructureType1Code`);
  - `PoliticalExposureType` (`Politicalexposuretypevalues`) and `PoliticalExposureDescriptionRecord` (string);
  - `CustomerSinceDate` (`ISODateTime`);
  - `GovernmentIssuedDocumentDetails` (`PartyDocument` → `Document` → `Identifier`, `Datetimeperiod`);
  - `DocumentDirectoryEntryInstanceReference` (`DocumentDirectoryEntry`).
- **References to other parties (4 fields):** `CorporateCustomerReference`, `CorporateCustomerLegalEntityReference` and `CompanyOfficerReference` (`Involvedparty`: `PartyReference`, `InvolvementReference`), and `CompanyOfficerRole` (an ISO 20022 `Party`).
  - These are party-to-party links, so they overlap with the Associations BQ (1.11, its own aggregate, ADR 0024).
  - **Chosen in the interview:** they move to a new roadmap row, **1.21**, placed after 1.11. They stay refused until then, tagged `1.21`.

This phase records the six own fields. As in 1.18 and 1.19, the Reference record stays in the entry aggregate (ADR 0024). The phase reuses the existing routes, handlers, check order, stream version (`ETag` / `If-Match`), `PartyReferenceUpdated` event and inline view. It adds **no new operation, port, route or view**.

It depends on 1.10, 1.18 and 1.19, all done. It reuses 1.18's date reading (`PartyReferenceDates`) and its rule that the decision checks dates against today. It unblocks 1.21 and the Reference MCP tools (1.17).

Drivers:
- Mission principles:
  - 3: value objects and enums, no primitive obsession;
  - 4: event-sourced truth;
  - 5: commands acknowledge from the decided state, and queries read views;
  - 8: Problem Details;
  - 10: small steps, which is why the party references are split out.
- Quality bar "Security": political exposure and identity documents are sensitive personal data.
- Tech-stack:
  - §3.4: context value objects live in `Reference/`, with a flat namespace (ADR 0026);
  - §5: routes, `If-Match`, `ETag` and `Idempotency-Key`;
  - §8.1–8.3: inline view, rebuildable.

**CQRS split:**
- **Command side:** `UpdatePartyReference` gains the new changes. The flow does not change: load, check the entry, the BQ id and the version, run a pure `Decide`, append with the expected version, and acknowledge from the decided state. Two new cross-field invariants are checked in `Decide` on the resulting Reference (see below).
- **Query side:** `RetrievePartyReference` reads the same **inline** `PartyReferenceDataDirectoryEntryView`. Its Reference section gains the new fields. There is no async projection.

## Scope
### In scope
- **`PartyLegalStructure` (enum, core `Reference/`).**
  - The 11 BIAN enum values are accepted, plus `CharitableIncorporatedOrganisation`. BIAN lists that value in its description of the UK Open Banking code list it cites, but leaves it out of the enum.
  - Values are read in any case and answered in BIAN spelling (`Co-Operative`, `Sole(SoleTrader)`).
  - BIAN's enum wrapper is flattened to a plain string, as for `PartyType` (ADR 0024).
- **Political exposure (core).**
  - `PoliticalExposureType` (enum): `Politicalexposureforeign`, `Politicalexposuredomestic` or `Nopoliticalexposure`, read in any case and stored by name.
  - `PoliticalExposureDescriptionRecord` (`PoliticalExposureDescription`): trimmed, not blank, at most 350 characters (Max350Text, as for `Status.StatusDescription`).
  - **Invariant in `Decide`:** a recorded description needs a recorded type of foreign or domestic exposure. Otherwise the result is `party.reference-invalid` naming `PoliticalExposureDescriptionRecord`. The check runs on the resulting Reference, so setting `Nopoliticalexposure` while an old description is kept is refused unless the same request clears the description.
- **`CustomerSinceDate` (`DateOnly`).** Read like `DateofBirth` (an ISO date, or the date as written in a date-time) and answered as `YYYY-MM-DD`.
  - From 1900-01-01, checked in `Create`.
  - Not after today (UTC, from the handler's `TimeProvider`), checked in `Decide`.
  - **Invariant in `Decide`:** when both are recorded, `CustomerSinceDate` is not before `DateofBirth`.
  - It is not checked against the entry's `OpenDate`, because a relationship can be older than the directory entry (for example, after a migration).
- **`GovernmentIssuedDocumentDetails` (`PartyDocument`, core).** One document, because BIAN gives a single object. A lean subset is recorded:

  | BIAN path | Recorded as | Rule |
  |---|---|---|
  | `PartyDocumentType` | `PartyDocumentType` enum | `WorkDocument`, `CertificationDocument` or `PersonalDocument`, any case, **required** |
  | `DocumentReference.DocumentIdentification.IdentifierValue` | `GovernmentIssuedDocumentNumber` | Trimmed, Max35Text, **required** |
  | `DocumentReference.DocumentName` | `GovernmentIssuedDocumentName` | Trimmed, Max140Text, optional (for example `Passport`) |
  | `DocumentReference.DocumentValidityPeriod.FromDateTime.DateTimeContent` | `DateOnly` | Optional, read like `DateofBirth` |
  | `DocumentReference.DocumentValidityPeriod.ToDateTime.DateTimeContent` | `DateOnly` | Optional, and not before `From` |

  - An expired document can be recorded. Acting on expiry is a later KYC concern (Stage 8).
  - Every other part is refused with `party.reference-field-not-supported`, with no phase named. That includes `PartyReference`, `DocumentType` (an invoice code list), `DocumentStatus`, `DocumentVersion`, `IdentifierIssuerReference`, `TimeZoneCode` and the rest.
- **`DocumentDirectoryEntryInstanceReference` (`DocumentDirectoryEntryReference`).**
  - `DocumentDirectoryEntryIdentification`: trimmed, Max35Text, required.
  - It is an opaque reference to an entry in BIAN Document Directory, a service domain RimBank does not have yet, so it is not resolved.
  - Its other parts (`DirectoryEntryDateType`, `DirectoryEntryDate`, `DirectoryEntryStatus`) are refused, with no phase named.
- **Partial update (as in 1.18 and 1.19):**
  - an omitted field keeps its value;
  - a given value or object replaces it;
  - a blank string or an object whose recorded parts are all blank clears it;
  - every accepted Update appends `PartyReferenceUpdated` with the full resulting Reference.
- **Errors:**
  - every invalid value goes into one `party.reference-invalid` (400), and `errors` names each path. Examples: `PartyLegalStructure`, `PoliticalExposureType`, `PoliticalExposureDescriptionRecord`, `CustomerSinceDate`, `GovernmentIssuedDocumentDetails.DocumentReference.DocumentIdentification.IdentifierValue`, `GovernmentIssuedDocumentDetails.DocumentReference.DocumentValidityPeriod.ToDateTime.DateTimeContent`, `DocumentDirectoryEntryInstanceReference.DocumentDirectoryEntryIdentification`;
  - the unsupported-field check still runs first.
- **Staged table:** the six fields leave `PartyReferenceFields.Staged`. The four party references are re-tagged `1.21`.
- **Model:**
  - `PartyReferenceDetails` gains `LegalStanding` (`PartyLegalStanding`: legal structure, political exposure and customer-since date) and `Documents` (`PartyDocuments`: the government-issued document and the directory reference), with `CurrentLegalStanding` and `CurrentDocuments` accessors;
  - both new members are nullable and `[JsonIgnore]` computed, as in 1.18 and 1.19, so events and view documents from 1.10 to 1.19 still load and fold.
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`):**
  - new models `PartyDocumentModel` (with its nested `DocumentModel`, `IdentifierModel`, `DateTimePeriodModel` and `DateTimeModel`) and `DocumentDirectoryEntryModel`;
  - the Update request and the shared Update/Retrieve response gain the six fields;
  - empty values are left out of the response;
  - the adapter only reports the values and the unknown parts it was sent.
- **Roadmap:**
  - the 1.20 row is reworded to the six fields and ticked;
  - a new row **1.21 Reference BQ: corporate and officer references** is added after 1.11.
- **Extra checks (chosen in the interview):**
  - an FsCheck partial-update law;
  - a log-capture PII test;
  - Reference demo requests in `src/RimBank.Host/RimBank.Host.http`.
- **Docs:**
  - new ADR 0032;
  - ADR 0029 amended;
  - ADR 0024 amended (the Reference paragraph);
  - tech-stack §3.4 updated (the `Reference/` tree).

### Out of scope
- `CorporateCustomerReference`, `CorporateCustomerLegalEntityReference`, `CompanyOfficerReference` and `CompanyOfficerRole`: **1.21**. They stay refused.
- More than one document, document history, issuer and issuing country, document images, and resolving the Document Directory reference. No phase is planned for these.
- Rules tied to `PartyType`, such as `Individual` only for persons. As in 1.18, every field is accepted for both party types.
- PEP screening, PEP-driven due diligence and expiry alerts. These belong to later KYC and compliance work (Stage 8).
- The `Reference/Created` and `Reference/Updated` channels (after 7.2), the MCP tools (**1.17**), per-operation scopes (**1.15**), and masking and encryption at rest (**7.2**). The log-capture test only proves that no new code logs these values.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Phase split | Six own fields now. The four `Involvedparty`/`Party` references go to a new 1.21 after 1.11 | Chosen in the interview. They are party-to-party links that overlap with Associations, and principle 10 keeps the phase to about a day |
| Legal structure values | The 11 BIAN enum values plus `CharitableIncorporatedOrganisation`, in any case, answered in BIAN spelling | The BIAN description cites the full OB code list, and the enum misses one of its values (mission "Source of truth" 3, ADR 0032) |
| Enum JSON shape | Plain string, with BIAN's wrapper object flattened | Same as `PartyType` (ADR 0024) |
| PEP description | Max350Text, and only with an exposed type, checked in `Decide` on the result | A description of "no exposure" makes no sense. Checking the result covers partial updates |
| `CustomerSinceDate` | Date only, 1900-01-01 to today, not before `DateofBirth` | Same reading as `DateofBirth` (ADR 0029). The relationship cannot start before the party is born |
| Document subset | Type, number (both required), name, and validity from/to (from ≤ to) | Enough to identify and age-check an ID document. BIAN's `DocumentType` is an invoice code list, so it is refused |
| Directory reference | Opaque Max35Text, not resolved | Document Directory is not a RimBank context, and resolving it would be a cross-context call (principle 7) |
| Update semantics | Partial: omitted keeps, blank clears, given replaces | Same as 1.9, 1.10, 1.18 and 1.19 |
| Event | `PartyReferenceUpdated`, with the same name and a larger `Details` | The new members are nullable, so earlier events still fold |
| Error codes | None new: `party.reference-invalid` and `party.reference-field-not-supported` | Same contract as 1.10 to 1.19 |
| Log-capture helper | A capturing `ILoggerProvider` in `RimBank.Host.Testing` | No new package, so no ADR 0013 change |
| Work order | Inside-out, test first: value objects → changes, decision, projection and HTTP (one commit) → PostgreSQL → extra checks and docs | Chosen in the interview. Same as 1.19, so no commit drops fields silently |

## ADRs to write
- **ADR 0032 (new): Reference legal standing, political exposure and documents.** It records:
  - the legal structure values (including `CharitableIncorporatedOrganisation`) and the BIAN spelling;
  - the PEP description invariant;
  - the `CustomerSinceDate` bounds and the date-of-birth invariant;
  - the recorded `PartyDocument` subset and the refused parts;
  - the opaque Document Directory reference;
  - the split of the four party references into 1.21.

  Options considered:
  - all ten fields with opaque references;
  - all ten fields with the references checked as directory entry ids;
  - the full `Document` tree.
- **Amend ADR 0029:**
  - a "Recorded (1.20)" table;
  - the staged table now holds 4 fields, all tagged 1.21;
  - the pin count in Consequences;
  - the PII list, adding political exposure and identity documents.
- **Amend ADR 0024:** the Reference paragraph gains the 1.20 fields and the 1.21 note.
- **Tech-stack §3.4:** the `Reference/` tree gains the new files.

## Open questions
- None.
