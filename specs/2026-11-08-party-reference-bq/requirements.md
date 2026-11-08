# Party Reference Data Directory: Reference BQ (party identifiers)

Roadmap phase: 1.10
Branch: plan/1.10-party-reference-bq

## Context
1.9 gave the first singleton Behavior Qualifier, Demographics, real behaviour on the entry's stream, version and inline view. Since 1.2, every entry has had a `PartyReferenceId`, but its Reference section is still empty. This phase adds BIAN `Reference/Update` and `Reference/Retrieve` for the **party identifiers**: tax identification number, national registration number and LEI. ADR 0024 models Reference as an **entity inside the entry aggregate**, like Demographics.

BIAN's `UpdateReference` / `RetrieveReference` schemas have **24 fields**: identifiers, names, birth date, nationality, residency, addresses, contact points, political exposure, legal structure, corporate officers and documents. One phase can't do all of that in a focused day (mission principle 10). No other roadmap phase covered them, so leaving them "accepted and ignored" would have dropped them for good, and clients would get a `200` for data that was never stored. **In the interview, the user chose to split the BQ.** 1.10 does the identifiers end to end and adds three explicit roadmap rows for the rest (1.18–1.20, listed right after 1.10). Until each row lands, its fields are **refused with a 400**, never silently dropped.

1.10 reuses 1.8–1.9's `AppendToEntry`, `StreamVersionTag.ReadIfMatch`/`SetOn`, the retry on a lost race, `party.behavior-qualifier-not-found` and the BQ-id check order. It adds no new port. It unblocks 1.18–1.20 and the Reference MCP tools (1.17).

Drivers:
- Mission principles 3 (value objects, no primitive obsession), 4 (event-sourced truth), 5 (commands acknowledge from the decided state, queries read views), 8 (Problem Details), 9 (idempotency) and 10 (small steps).
- Quality bar "Security": identifiers are PII.
- Tech-stack §3.4 (`Reference/` folder, flat namespace, ADR 0026), §5 (routes, `If-Match`, `ETag`, `Idempotency-Key`), §7 (BQ as an entity), §8.1–8.3 (inline view).
- [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md): `UpdatePartyReference`, `RetrievePartyReference` and the event `PartyReferenceUpdated`.
- BIAN source: [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml):
  - `PUT …/{partyreferencedatadirectoryid}/Reference/{referenceid}/Update` and `GET …/Retrieve`;
  - the schemas `UpdateReference` and `RetrieveReference`;
  - `PartyIdentificationInformation` (`TaxIdentificationNumber` and `NationalRegistrationNumber`, both Max35Text);
  - `Party.Identification`.

**CQRS split:**
- **Command side:** `UpdatePartyReference` loads the entry, checks it in order (entry, BQ id, version), calls a pure `Decide` on the entry, appends to the entry stream with the expected version, and acknowledges from the decided state.
- **Query side:** `RetrievePartyReference` reads the existing **inline** `PartyReferenceDataDirectoryEntryView`, which gains a Reference section, through `IPartyDirectoryEnquiry`, after checking the BQ id. There is no new view and no async projection.

## Scope
### In scope
- **Identifier value objects (core, `Reference/`):**
  - `TaxIdentificationNumber` and `NationalRegistrationNumber`: trimmed, non-empty and at most 35 characters (ISO 20022 Max35Text). There is no country-specific format check.
  - `LegalEntityIdentifier`: ISO 17442, trimmed and upper-cased. It must be 18 alphanumerics followed by 2 check digits, and pass the ISO 7064 MOD 97-10 checksum.
  - `PartyIdentifiers`: the three identifiers, each optional, with `Empty`, `Apply(changes)` and `HasAny`.
  - `PartyReferenceDetails`: the Reference record in the entry state and its view. For now it holds only `Identifiers`; 1.18–1.20 add sibling fields to it.
- **Update (partial, as in 1.9):**
  - An omitted identifier keeps its value, and `""` clears it.
  - Every accepted Update appends `PartyReferenceUpdated`, which carries the full resulting identifiers, even when nothing changes.
  - `PartyReferenceChanges.Create(...)` returns `Result`. Any invalid identifier gives `party.reference-invalid` (400), with every offending BIAN field named in `errors`.
- **Fields not supported yet:**
  - These are the other 22 top-level BIAN Reference fields, plus every sub-field of `GovernmentIssuedIdentityReference` and `PartyLegalEntityReference` other than the three identifiers.
  - Any of them that is present and not `null` gives `party.reference-field-not-supported` (400). `errors` names each one and says which roadmap phase will add it.
  - The error and its field-to-phase table live in the core. The `.Api` folder only reports which BIAN properties were present.
- **Handlers:** the same check order as 1.9:
  1. the entry exists, or `party.party-not-found`;
  2. the `referenceid` is the entry's `PartyReferenceId`, or `party.behavior-qualifier-not-found`;
  3. commands only: the expected version is the current one, or `party.version-conflict`;
  4. then the decision.
- **Projection:** the entry view gains a nullable `Reference` section, which is `null` until the first Update (and in documents from before 1.10).
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`):**
  - Update (`PUT`) requires `Idempotency-Key` and `If-Match`, and answers 200 with the new `ETag`, built from the acknowledgement.
  - Retrieve (`GET`) answers 200 from the view, with the entry's `ETag`, and 304 when `If-None-Match` matches.
  - Both answer with the same body: `GovernmentIssuedIdentityReference { TaxIdentificationNumber, NationalRegistrationNumber }`, `PartyLegalEntityReference { Identification }` (the LEI), and the entry and Reference ids. An identifier with no value is left out.
  - A malformed `referenceid` is 400 `shared.invalid-identifier` with `errors["referenceid"]`.
- **Roadmap:** add rows 1.18, 1.19 and 1.20 right after 1.10 (see Decisions), and tick 1.10.
- **Conformance:** add `Reference/Update` and `Reference/Retrieve` to `_implemented`.
- **Docs:** the new ADR 0029, ADR 0024 amendments, and the tech-stack §3.4 tree.

### Out of scope
- **Names, birth date, nationality and residency:** new roadmap row **1.18**:
  - `PartyFullLegalName`, `PartyNameSalutation`, `DateofBirth`, `Nationality`, `PartyCountryOfResidence` and `ResidencyStatus`.
- **Addresses and contact points:** new roadmap row **1.19**:
  - `ResidentialAddress`, `PartyAddress`, `CorporateAddress`, `eMailAddress`, `CellPhoneNumber` and `SocialNetworkContacts`.
- **Legal structure, political exposure, corporate details and documents:** new roadmap row **1.20**:
  - `PartyLegalStructure`, `PoliticalExposureType`, `PoliticalExposureDescriptionRecord`, `CustomerSinceDate`;
  - `CorporateCustomerReference`, `CorporateCustomerLegalEntityReference`, `CompanyOfficerReference`, `CompanyOfficerRole`;
  - `GovernmentIssuedDocumentDetails` and `DocumentDirectoryEntryInstanceReference`.
- **Other identifier types** (passport, driving licence, SSN, …, BIAN `Partyidentificationtypevalues`) and identifier validity dates: not chosen. A later row can add them.
- **Uniqueness of a tax ID or LEI across entries:** not chosen. It needs a cross-stream check and its own ADR.
- **The CR Retrieve's BIAN `PartyReference`:** it stays unreturned (ADR 0024, 1.7). The Reference Retrieve serves the identifiers.
- **The `Reference/Created` and `/Updated` channels:** deferred by ADR 0024, until a consumer needs them and PII protection (7.2) lands.
- **MCP tools** (`…_update_reference`, `…_retrieve_reference`) and their parity tests: **1.17**.
- **Per-operation scopes:** **1.15**.
- **PII masking and encryption at rest:** **7.2**. New code must not log identifier values.
- A captured-log PII test, LEI FsCheck properties and `.http` demo files: not chosen in the interview.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Reference scope | Identifiers only in 1.10. The other 22 BIAN fields move to new rows 1.18–1.20 | Chosen in the interview. Keeps the phase to about a day (principle 10) without losing any field from the roadmap |
| Fields not yet supported | Refused with 400 `party.reference-field-not-supported`, naming each field and its phase | Chosen in the interview over "accept and ignore": a 200 must never hide data that was not stored. This departs from Register's ignore policy, so ADR 0029 records it |
| Identifier model | Three typed fields: `TaxIdentificationNumber`, `NationalRegistrationNumber`, `LegalEntityIdentifier` | Chosen in the interview. Value objects, no primitive obsession (principle 3) |
| Where the LEI goes in BIAN JSON | `PartyLegalEntityReference.Identification` | BIAN describes it as the "reference to the legal entity", and `Party.Identification` is a string. `RegistrationAuthorityIdentification` is Max8Text (a registry code, not an LEI). No extension field is needed. Recorded in ADR 0029 |
| LEI validation | Trim and upper-case, then 20 characters `[A-Z0-9]{18}[0-9]{2}` and ISO 7064 MOD 97-10 | ISO 17442. Catches typos at the edge. The rule lives in the core (memory: business logic in core) |
| Tax and national numbers | Trimmed, Max35Text, no country format check | ISO 20022 types. Country rules are a later concern |
| Update semantics | Partial: omitted keeps, `""` clears; a no-op Update still appends `PartyReferenceUpdated` | Same as 1.8 and 1.9. The version always moves, so `If-Match` stays simple |
| Event payload | `PartyReferenceUpdated(EntryId, PartyReferenceId, PartyReferenceDetails Details)` with the full resulting state | Event-carried state, ready for the deferred channels (tech-stack §8.4). The name comes from ADR 0024 |
| No verification status | Reference has no Exchange operation in BIAN, so there is no status | Spec. Unlike Demographics (ADR 0028) |
| Retrieve query | `RetrievePartyReference : IQuery<PartyReferenceDataDirectoryEntryView>`; the endpoint maps the section | As `RetrieveDemographics` (ADR 0024, ADR 0012) |
| Retrieve before the first Update | 200 with the ids only | ADR 0024: the BQ exists from Register, and its section is empty until the first Update |
| Command flow | A Reference handler written on the model of `DemographicsCommandFlow`, with no shared extraction in this phase | Chosen in the interview ("mirror 1.9"). Extraction can come when BankRelations (1.12) adds a third copy |
| Concurrency | The entry's stream version is the `ETag` and `If-Match` | ADR 0024 Concurrency; ADR 0027 |
| New error codes | `party.reference-invalid` (400); `party.reference-field-not-supported` (400); `shared.invalid-identifier` on `referenceid` | Stable codes, documented in ADR 0024 |
| Existing view documents | No migration. The section is nullable and `null` reads as empty | The shape only grows. Rebuild is still proven |
| New roadmap rows | 1.18, 1.19 and 1.20, listed right after 1.10 | Reference is finished before Associations. The board sync script picks the rows up |
| Work order | Inside-out, test first: core, then Postgres, then HTTP, then docs and roadmap | Chosen in the interview |

## ADRs to write
- **ADR 0029 (new): Reference BQ fields in stages, with unsupported fields refused.**
  - The split into 1.10 and 1.18–1.20, and the field-to-phase table.
  - The refuse-with-400 policy, and why it differs from Register's ignore policy.
  - The LEI's place in `PartyLegalEntityReference.Identification`, and the identifier validation rules.
  - Options considered: all fields now; identifiers only with the rest ignored; identifiers only with the rest refused and no roadmap rows.
- **Amend ADR 0024:**
  - a "Reference requests (1.10)" paragraph;
  - the `PartyReferenceUpdated` payload;
  - the new error codes;
  - the 1.7 note on the CR's `PartyReference`;
  - a link to 0029.
- **Tech-stack §3.4 tree:** the `Reference/` contents.

## Open questions
- None. Placing 1.18–1.20 before 1.11 is a default. Move them after 1.12 if Associations should come first.
