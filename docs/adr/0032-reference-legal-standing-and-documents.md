# 0032. Reference BQ: the party's declared legal standing and documents, never the bank's assessment of them

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.20

## Context

Roadmap 1.20 was the last stage of the Reference Behavior Qualifier ([ADR 0029](0029-reference-fields-in-stages.md)). Ten BIAN Reference fields were still refused:

- **The party's own details (six fields):**
  - `PartyLegalStructure` (`Partylegalstructuretypevalues`, the UK Open Banking `OBExternalLegalStructureType1Code`);
  - `PoliticalExposureType` (`Politicalexposuretypevalues`) and `PoliticalExposureDescriptionRecord` (a string);
  - `CustomerSinceDate` (`ISODateTime`);
  - `GovernmentIssuedDocumentDetails` (a `PartyDocument`, which holds a `Document`, which holds an `Identifier`, a `Datetimeperiod` and about ten more parts);
  - `DocumentDirectoryEntryInstanceReference` (a `DocumentDirectoryEntry`).
- **References to other parties (four fields):** `CorporateCustomerReference`, `CorporateCustomerLegalEntityReference` and `CompanyOfficerReference` (each an `Involvedparty`: `PartyReference`, `InvolvementReference`), and `CompanyOfficerRole` (an ISO 20022 `Party`).

What the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) leaves open:

- **The legal structure enum misses a value.** Its description lists the UK Open Banking code list, which includes `CharitableIncorporatedOrganisation`, but the enum leaves it out.
- **`Document.DocumentType` is the wrong code list.** It is `Financialdocumenttypevalues` (commercial invoice, credit note, …), which cannot describe a passport.
- **Nothing links a political exposure description to its type.**
- **The four references point at other parties.** That is what the Associations BQ (roadmap 1.11, its own aggregate in [ADR 0024](0024-party-reference-data-directory-mapping.md)) models.

**A question of ownership.** Political exposure and identity documents are KYC data. Two kinds of fact can be recorded about them:

- what the party **declares** ("I hold a domestic political position", "my passport number is …");
- what the bank **concludes** after screening or due diligence ("this party is a PEP", "this document is genuine", "high risk").

These change for different reasons and are owned by different people. BIAN models the conclusions in other service domains (Customer Due Diligence, Party Lifecycle Management, Regulatory Compliance). A directory that recorded both would grow into an "entity service" for KYC, which every later context would depend on (mission principle 7).

## Options considered

1. **All ten fields, with the references as opaque text.**
   - Pros: the Reference BQ is complete in one phase.
   - Cons: about two days of work (against principle 10), and party-to-party links duplicated outside Associations.
2. **All ten fields, with the references checked as directory entry ids.**
   - Pros: strong integrity.
   - Cons: the largest phase, and it rebuilds what Associations will do.
3. **The six own fields now, and the four references in a new row 1.21 after 1.11.**
   - Pros: each phase stays small, and the links land next to the BQ that models links.
   - Cons: callers still get a 400 for the references until 1.21.
4. **Leave political exposure and documents out of the directory and wait for a KYC context.**
   - Pros: the directory holds only identity and contact data.
   - Cons: BIAN fields left with no phase, and the declaration, which is reference data, would have no home.

## Decision

Option 3. **The directory records what the party declares, never the bank's assessment of it.** Option 4 is rejected: the declaration is reference data and BIAN puts it here. The test for any new Reference field is "does the party declare it, or does the bank conclude it?", and only declared values belong here. No verification status is added (unlike Demographics, [ADR 0028](0028-demographics-exchange-as-verification.md)). A future KYC context (Stage 8) will own screening, verification and risk outcomes. It will take the declared values as inputs through Party's published events (1.13), never by reading this view.

### Legal standing

| BIAN field | Recorded as | Rule |
|---|---|---|
| `PartyLegalStructure` | `PartyLegalStructure` enum | The 11 BIAN values plus `CharitableIncorporatedOrganisation`. Read without regard to case, spaces or hyphens, and answered in BIAN spelling (`Co-Operative`, `Sole(SoleTrader)`). Stored by name |
| `PoliticalExposureType` | `PoliticalExposureType` enum | `Politicalexposureforeign`, `Politicalexposuredomestic` or `Nopoliticalexposure`, in any case. Stored by name (`Foreign`, `Domestic`, `NoExposure`) |
| `PoliticalExposureDescriptionRecord` | `PoliticalExposureDescription` | Trimmed, not blank, at most 350 characters (Max350Text, as for `Status.StatusDescription`) |
| `CustomerSinceDate` | `DateOnly` | Read like `DateofBirth` ([ADR 0029](0029-reference-fields-in-stages.md)), answered as `YYYY-MM-DD`. From 1900-01-01 (checked when read), not after today (checked by the decision) |

- **BIAN enum wrappers** (`{"Partylegalstructuretypevalues": "Charity"}`) are flattened to plain strings, as for `PartyType` (ADR 0024).
- **Two rules span several fields.** The decision checks them on the resulting Reference, so a partial update cannot leave a contradiction behind:
  - a recorded description needs a recorded type of foreign or domestic exposure. Declaring `Nopoliticalexposure` while an old description is kept is refused, unless the same request clears the description;
  - when both are recorded, `CustomerSinceDate` is not before `DateofBirth`. It is not checked against the entry's `OpenDate`, because a relationship can be older than the directory entry (for example, after a migration).

  Either one is `party.reference-invalid` naming the field.
- **No party-type rule.** As in 1.18, every value is accepted for a person and for an organisation.

### Documents

| BIAN path | Recorded as | Rule |
|---|---|---|
| `GovernmentIssuedDocumentDetails.PartyDocumentType` | `PartyDocumentType` enum | `WorkDocument`, `CertificationDocument` or `PersonalDocument`, any case, **required** |
| `….DocumentReference.DocumentIdentification.IdentifierValue` | `GovernmentIssuedDocumentNumber` | Trimmed, Max35Text, **required** |
| `….DocumentReference.DocumentName` | `GovernmentIssuedDocumentName` | Trimmed, Max140Text, optional (for example `Passport`) |
| `….DocumentReference.DocumentValidityPeriod.FromDateTime.DateTimeContent` | `DateOnly` | Optional, read like `DateofBirth` |
| `….DocumentReference.DocumentValidityPeriod.ToDateTime.DateTimeContent` | `DateOnly` | Optional, not before `From` |
| `DocumentDirectoryEntryInstanceReference.DocumentDirectoryEntryIdentification` | `DocumentDirectoryEntryReference` | Trimmed, Max35Text, required. Opaque: BIAN Document Directory is not a RimBank context, so it is never resolved (that would be a cross-context call) |

- **One document**, because BIAN gives a single object. A given document replaces the whole document, and one whose recorded parts are all blank clears it.
- **An expired document can be recorded.** Acting on expiry is a KYC concern.
- **Every other part is refused**, at any depth, with `party.reference-field-not-supported` and no phase. That includes `PartyReference`, `DocumentType`, `DocumentStatus`, `DocumentVersion`, `IdentifierIssuerReference`, `TimeZoneCode`, `DirectoryEntryStatus`, and so on.

## Consequences

- **Roadmap.** Row 1.20 is reworded to the six fields. A new row, **1.21 Reference BQ: corporate and officer references**, is added after 1.11, and the four references are tagged `1.21` in `PartyReferenceFields.Staged`. 1.21 records them as checked directory entries on organisations only ([ADR 0034](0034-reference-corporate-and-officer-references.md)).
- **Events.** `PartyReferenceUpdated` keeps its name. `PartyReferenceDetails` gains the nullable `LegalStanding` and `Documents` members, so events and views stored before 1.20 still load, fold and rebuild (proved by the PostgreSQL tests).
- **Tests.**
  - The cross-field rules are Given / When / Then decision tests, and they are part of the entry's decider laws.
  - An FsCheck property proves the partial-update law over every recorded field.
  - A log-capture test proves that no submitted Reference value is logged.
- **1.13 published language.** The integration events must not carry the declared political exposure or document values to every subscriber. They should carry what consumers need (for example "a political exposure is declared"). A future KYC context that needs the values asks for them under its own ADR.
- **PII.** Political exposure and identity documents are sensitive personal data. They live only in this context's stream and view, and are listed for 7.2.
