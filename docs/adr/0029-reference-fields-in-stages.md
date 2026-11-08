# 0029. Reference BQ: the party identifiers first, the other fields in stages and refused until then

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.10 (amended in 1.18, 1.19 and 1.20)

## Context

Roadmap 1.10 builds the Reference Behavior Qualifier of the Party Reference Data Directory: `Reference/Update` and `Reference/Retrieve` ([ADR 0024](0024-party-reference-data-directory-mapping.md)). Its roadmap row asks for "party identifiers (national ID, tax ID, LEI) as value objects".

What the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) gives:

- `UpdateReference` and `RetrieveReference` have **24 top-level fields**, most of them nested ISO 20022 types:
  - identifiers;
  - names, birth date, nationality and residency;
  - postal addresses and contact points;
  - legal structure, political exposure, corporate officers and documents.
- `GovernmentIssuedIdentityReference` is a `PartyIdentificationInformation`, with `TaxIdentificationNumber` and `NationalRegistrationNumber` (both Max35Text) among about fifteen parts. `RegistrationAuthorityIdentification` is Max8Text: a registry code, not an LEI.
- `PartyLegalEntityReference` is a `Party`, "the reference to the legal entity". Its `Identification` is a string.
- There is no field named for the LEI.

A roadmap phase is about one focused day ([mission](../../specs/mission.md) principle 10). Modelling all 24 fields at once would take several. No other roadmap phase covered the Reference BQ, and Register's policy for fields it does not use is "accept and ignore" (ADR 0024). Applied here, that policy would drop most of the BQ from the plan for good. It would also answer `200` to a caller whose address or birth date was never stored.

## Options considered

1. **All 24 fields in 1.10.**
   - Pros: the BQ is complete at once.
   - Cons: several days of work and a large PR, against principle 10.
2. **Identifiers only, with the other fields accepted and ignored.**
   - Pros: the smallest change, and it matches Register.
   - Cons: the other fields are never scheduled, and a `200` hides data that was not stored.
3. **Identifiers only, with the other fields refused, and no roadmap rows for them.**
   - Pros: honest answers.
   - Cons: the fields still have no plan.
4. **Identifiers in 1.10, the other fields in new roadmap rows, and refused until each row lands.**
   - Pros: honest answers, every field has a phase, and each phase stays small.
   - Cons: callers that send the other fields get a 400 until their phase lands.

## Decision

Option 4, chosen when 1.10 was planned.

### The identifiers (1.10)

| BIAN field | Value object | Rule |
|---|---|---|
| `GovernmentIssuedIdentityReference.TaxIdentificationNumber` | `TaxIdentificationNumber` | Trimmed, not blank, at most 35 characters (Max35Text). No country format check |
| `GovernmentIssuedIdentityReference.NationalRegistrationNumber` | `NationalRegistrationNumber` | Same as the tax identification number |
| `PartyLegalEntityReference.Identification` | `LegalEntityIdentifier` | ISO 17442: trimmed and upper-cased, 18 letters or digits followed by 2 check digits, and the ISO 7064 MOD 97-10 check holds |

- **Where the LEI goes.** The LEI identifies the legal entity, so it goes in `PartyLegalEntityReference.Identification`. That needs no extension field. `RegistrationAuthorityIdentification` is too short for it.
- **Partial update.** As for Demographics (1.9), an identifier left out keeps its value, an empty one clears it, and every accepted Update appends `PartyReferenceUpdated` with the full resulting Reference.
- **Validation.** Any invalid identifier is `party.reference-invalid` (400), with every offending BIAN field named in `errors`.
- **JSON.** Each identifier is stored and sent as a plain string, like `Currency` ([ADR 0009](0009-money-and-currency.md)).
- **No verification status.** BIAN gives Reference no Exchange, so unlike Demographics ([ADR 0028](0028-demographics-exchange-as-verification.md)) it has no status.

### Names and residency (recorded from 1.18)

| BIAN field | Recorded as | Rule |
|---|---|---|
| `PartyFullLegalName` | `PartyFullLegalName` | Trimmed, not blank, at most 140 characters (Max140Text) |
| `PartyNameSalutation` | `PartyNameSalutation` | Trimmed, not blank, at most 35 characters (Max35Text), free text |
| `DateofBirth` | `DateOnly` | An ISO 8601 date, or the date as written in an ISO 8601 date-time (no time-zone conversion). From 1900-01-01 to today (UTC, from the handler's `TimeProvider`), both included. Answered as `YYYY-MM-DD` |
| `Nationality.Code` | `CountryCode` ([ADR 0030](0030-iso-3166-country-codes.md)) | Trimmed and upper-cased, then an officially assigned ISO 3166-1 alpha-2 code |
| `PartyCountryOfResidence.Code` | `CountryCode` | Same as `Nationality.Code` |
| `ResidencyStatus.StatusDescription` | `ResidencyStatusCode` | `Resident` or `NonResident`, in any case, stored by name |
| `ResidencyStatus.StatusDateTime` | `DateTimeOffset?` | An ISO 8601 date-time with its offset as written (UTC when none). Only allowed with a code |

- **Date only.**
  - BIAN types `DateofBirth` as `ISODateTime`, but a birth date is a calendar date: its time and offset carry no business meaning, and converting them to UTC could move the day.
  - RimBank keeps the date as written and answers with an ISO date. This is the one place where the response differs from the BIAN format.
- **The future check is the decision's.**
  - Parsing has no clock, so the Reference handler stamps today's UTC date on the decision input, and `Decide` refuses a later birth date with `party.reference-invalid` naming `DateofBirth`.
  - A birth date already recorded is not checked again when an Update keeps it.
- **A lower bound of 1900-01-01.**
  - An earlier date of birth is almost always a typo (for example `0990` for `1990`), and no living party was born before it.
  - It is refused with `party.reference-invalid` naming `DateofBirth`.
  - The bound is fixed and needs no clock, so `PartyReferenceChanges.Create` checks it (`EarliestDateOfBirth`), with the format.
- **BIAN objects, partly recorded.**
  - `Nationality` and `PartyCountryOfResidence` are BIAN `Country` objects, of which only `Code` is recorded. `ResidencyStatus` is an ISO 20022 `Status`, of which only `StatusDescription` and `StatusDateTime` are recorded.
  - Their other parts (`Citizen`, `Tax`, `StatusReason`, …) are refused like `OtherIdentification`, with no phase named.
  - A given object replaces the whole value. One whose recorded parts are all empty clears it.
- **Current values only.** BIAN describes nationality and residency "and history as appropriate". RimBank records the current value. The history is in the entry's events.
- **No party-type rule.** Every field is accepted for a person and for an organisation (for an organisation, the country of residence is where its affairs are directed).

### Addresses and contact points (recorded from 1.19)

[ADR 0031](0031-reference-contact-points-and-addresses.md) gives the full rules.

| BIAN field | Recorded as | Rule |
|---|---|---|
| `ResidentialAddress`, `PartyAddress`, `CorporateAddress` | `PostalAddress` | `Department`, `SubDepartment` (Max70Text), `StreetName`, `StreetBuildingIdentification`, `BuildingName` (Max35Text), `Floor`, `PostOfficeBox`, `PostCodeIdentification` (Max16Text), `TownName` (Max35Text, required) and `Country.Code` (`CountryCode`, required) |
| `eMailAddress.EmailAddress` | `EmailAddress` | Trimmed, at most 256 characters, `local@domain.tld` syntax, kept as written |
| `CellPhoneNumber.MobileNumber` | `MobileNumber` | E.164 after removing spaces, hyphens and dots, 7 to 15 digits, stored normalised |
| `SocialNetworkContacts.URLAddress` | `SocialNetworkAddress` | An absolute `https` URL, at most 2048 characters |

- **BIAN objects, partly recorded.** As in 1.18, the other `PostalAddress` parts (`AddressType`, `Location`, `ValidityPeriod`, …) and the other `ContactPoint` parts are refused, with no phase named. A given address replaces the whole address, and an object whose recorded parts are all blank clears it.

### Legal standing and documents (recorded from 1.20)

[ADR 0032](0032-reference-legal-standing-and-documents.md) gives the full rules. These are what the party declares; the bank's assessment of them (screening, verification) is not recorded here.

| BIAN field | Recorded as | Rule |
|---|---|---|
| `PartyLegalStructure` | `PartyLegalStructure` | The BIAN code list plus `CharitableIncorporatedOrganisation`, answered in BIAN spelling |
| `PoliticalExposureType` | `PoliticalExposureType` | `Politicalexposureforeign`, `Politicalexposuredomestic` or `Nopoliticalexposure` |
| `PoliticalExposureDescriptionRecord` | `PoliticalExposureDescription` | Max350Text, only with a foreign or domestic exposure (checked on the resulting Reference) |
| `CustomerSinceDate` | `DateOnly` | From 1900-01-01 to today, and not before `DateofBirth` |
| `GovernmentIssuedDocumentDetails` | `GovernmentIssuedDocument` | `PartyDocumentType` and the number (required), the name and the validity period |
| `DocumentDirectoryEntryInstanceReference` | `DocumentDirectoryEntryReference` | `DocumentDirectoryEntryIdentification`, Max35Text, opaque |

### Corporate and officer references (recorded from 1.21)

The four fields were planned for 1.20. They reference other parties, so they moved to 1.21, after the Associations BQ (1.11, ADR 0032). [ADR 0034](0034-reference-corporate-and-officer-references.md) gives the full rules.

| BIAN field | Recorded as | Rule |
|---|---|---|
| `CorporateCustomerReference` | `PartyReferenceDataDirectoryEntryId` | Its `PartyReference`: another registered entry, an organisation |
| `CorporateCustomerLegalEntityReference` | `PartyReferenceDataDirectoryEntryId` | Its `PartyReference`: another registered entry, an organisation |
| `CompanyOfficerReference` | `PartyReferenceDataDirectoryEntryId` | Its `PartyReference`: another registered entry, a person |
| `CompanyOfficerRole` | `CompanyOfficerRole` | Max35Text, only with an officer |

They are recorded only on an organisation's entry. `InvolvementReference` is refused, with no phase named.

### The fields in stages

Since 1.21, every BIAN Reference field is recorded and `PartyReferenceFields.Staged` is empty. A field a later BIAN release adds would be staged there with its phase.

- **Refused until their phase lands.** A request that sends any of these fields with a value (not `null`) is `party.reference-field-not-supported` (400). `errors` names each field and the phase that will add it, and nothing is stored. The same applies to any other part of `GovernmentIssuedIdentityReference` or `PartyLegalEntityReference`, such as `OtherIdentification`. No phase is planned for those parts, so their message names none.
- **The check comes first.** It runs before the identifiers are checked, so a request is never applied partly.
- **Other properties are ignored.** Properties that are not BIAN Reference fields are ignored, as elsewhere. Staged field names are matched without regard to case.
- **Where the rule lives.** The field-to-phase table and the error are in the core (`PartyReferenceFields`, `PartyReferenceChanges.Create`). Adapters only report which properties were sent, so HTTP and MCP (1.17) refuse the same fields.
- **Register is unchanged.** It keeps accepting and ignoring the CR fields it does not use (ADR 0024). Those fields are recorded by the BQs, so nothing is lost.

## Consequences

- **Roadmap.** Rows 1.18, 1.19 and 1.20 are listed right after 1.10, and 1.21 right after 1.11. Each one removes its fields from `PartyReferenceFields.Staged`, adds them to `PartyReferenceDetails`, and updates this ADR's table.
- **Tests.** `PartyReferenceChangesTests` pins the staged fields and their phases, so a phase cannot add a field without updating the table: 22 across three phases at 1.10, 16 across 1.19 and 1.20 after 1.18, 10, all for 1.20, after 1.19, 4, all for 1.21, after 1.20, and none since 1.21.
- **Clients.** A client that sends a staged field gets a 400 that says when the field will be supported, never a silent drop.
- **Uniqueness.** Identifiers are not unique across entries. A uniqueness rule (for example one LEI per entry) needs a cross-stream check and its own ADR.
- **PII.** The identifiers, from 1.18 the names, birth date, nationality and residency, from 1.19 the addresses and contact points, from 1.20 the declared legal standing and documents, and from 1.21 the corporate customer's references and its officer's role, are personal data; political exposure and identity documents are sensitive. They live only in this context's stream and view, and are listed for 7.2. No new code logs them.
