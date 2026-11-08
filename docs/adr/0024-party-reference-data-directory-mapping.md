# 0024. Party Reference Data Directory: BIAN → DDD mapping

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 1.1

## Context

Party Reference Data Directory is the first service domain RimBank builds (roadmap Stage 1). Its core is `RimBank.Party.PartyReferenceDataDirectory`, the folder `Party/PartyReferenceDataDirectory/` of `RimBank.Core` ([ADR 0023](0023-one-core-project.md)). Its BIAN v14 ext-DDD specifications are the source of truth ([mission](../../specs/mission.md#source-of-truth), [ADR 0002](0002-bian-specs-location.md)):

- [OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml): Control Record (CR) *PartyReferenceDataDirectoryEntry*, Behavior Qualifiers (BQs) *Demographics*, *Reference*, *Associations* and *BankRelations*, and 17 operations.
- [AsyncAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/asyncapi-3.x/yamls/PartyReferenceDataDirectory.yaml): 10 channels, a `Created` and an `Updated` channel for the CR and for each BQ.

The mission says a CR is an aggregate root, and leaves it to a per-domain ADR whether each BQ is an entity inside it or its own aggregate. [tech-stack §7](../../specs/tech-stack.md#7-domain-modelling-conventions) makes "entity" the default and requires an ADR for an exception. Roadmap 1.1 asks this ADR to fix the aggregate boundaries, the BQ modelling, the command/query split of the operations, and the read models with their projection lifecycle, before 1.2 writes the first aggregate.

What the spec shows about each BQ:

- **Demographics**, **Reference** and **BankRelations** have `Update` and `Retrieve`, and Demographics also has `Exchange`. None of them has a `Register` or `Initiate`, yet their routes carry a BQ id (`/{partyreferencedatadirectoryid}/Demographics/{demographicsid}/Update`).
- **Associations** has its own `Register` (`/{partyreferencedatadirectoryid}/Associations/Register`) as well as `Update` and `Retrieve` by `{associationsid}`. An association links the entry to another party (`AssociateReference`, `AssociateType`) for a validity period (`AssociationValidFromToDate`). A party can have many associations.

## Options considered

1. **One aggregate, all four BQs as entities.** This is the tech-stack default and the simplest option. But every association of a party is appended to that party's stream, so busy parties (companies with many employees, signatories or beneficiaries) become a concurrency hotspot on one stream. An association also links two entries, yet it would live in the stream of only one of them, and its own `Register` and lifecycle would be hidden inside the entry.
2. **The entry aggregate with Demographics, Reference and BankRelations as entities, and Associations as its own aggregate `PartyAssociation`.** The singleton BQs stay with the entry. Each association gets its own stream, version and lifecycle, matching its own `Register` operation and id in the spec.
3. **Every BQ as its own aggregate.** The singleton BQs have no lifecycle of their own (BIAN gives them no `Register`) and change at the pace of the entry. Making them aggregates only adds streams, ids and cross-stream checks.

## Decision

Option 2. It keeps the tech-stack default for the three singleton BQs and takes the documented exception only for Associations, the one BQ with an independent lifecycle and many instances per party.

### Aggregates and identity

| Aggregate | Stream id | Holds | Folder |
|---|---|---|---|
| `PartyReferenceDataDirectoryEntry` (CR, aggregate root) | `PartyReferenceDataDirectoryEntryId` | The CR (party reference, party type, directory entry dates), and the Demographics, Reference and BankRelations entities | `PartyReferenceDataDirectoryEntry/`, `Demographics/`, `Reference/`, `BankRelations/` |
| `PartyAssociation` | `PartyAssociationId` | The owning entry id, the associate entry id, the associate type, the validity period and the other association details | `Associations/` |

- Both ids are Guid v7 typed ids ([ADR 0008](0008-typed-identifier-pattern.md)). Each one is the Marten stream id of its aggregate and the `Id` of its view.
- **Singleton BQ ids.** Demographics, Reference and BankRelations each have exactly one instance per entry. When the entry is registered, the `RegisterParty` handler creates their ids (`DemographicsId`, `PartyReferenceId`, `BankRelationsId`, typed ids) and passes them in the command. `PartyRegistered` records them, and the Register acknowledgement returns them. They are the `{demographicsid}`, `{referenceid}` and `{bankrelationsid}` route values. A BQ id that does not belong to the entry is `party.behavior-qualifier-not-found`. Until a BQ's first `Update`, its section of the entry is empty.
- **Write port.** `PartyDirectory` (`IWritePort`) loads and appends both stream types. One write port per context is enough here: both aggregates live in the same store and schema.

### Operations: commands and queries

Every `Retrieve` is a query, and every other operation is a command (mission principle 5). MCP tool names follow [ADR 0021](0021-mcp-wiring-and-tool-conventions.md). A tool is registered only when its phase lands.

| BIAN operationId | Kind | Command / query | Phase | MCP tool |
|---|---|---|---|---|
| `PartyReferenceDataDirectoryEntry/Register` | Command | `RegisterParty` | 1.2–1.5 | `party_reference_data_directory_register` |
| `PartyReferenceDataDirectoryEntry/Retrieve` | Query | `RetrievePartyReferenceDataDirectoryEntry` | 1.6–1.7 | `party_reference_data_directory_retrieve` |
| `PartyReferenceDataDirectoryEntry/Update` | Command | `UpdatePartyReferenceDataDirectoryEntry` | 1.8 | `party_reference_data_directory_update` |
| `Demographics/Update` | Command | `UpdateDemographics` | 1.9 | `party_reference_data_directory_update_demographics` |
| `Demographics/Exchange` | Command | `ExchangeDemographics` | 1.9 | `party_reference_data_directory_exchange_demographics` |
| `Demographics/Retrieve` | Query | `RetrieveDemographics` | 1.9 | `party_reference_data_directory_retrieve_demographics` |
| `Reference/Update` | Command | `UpdatePartyReference` | 1.10 | `party_reference_data_directory_update_reference` |
| `Reference/Retrieve` | Query | `RetrievePartyReference` | 1.10 | `party_reference_data_directory_retrieve_reference` |
| `Associations/Register` | Command | `RegisterPartyAssociation` | 1.11 | `party_reference_data_directory_register_associations` |
| `Associations/Update` | Command | `UpdatePartyAssociation` | 1.11 | `party_reference_data_directory_update_associations` |
| `Associations/Retrieve` | Query | `RetrievePartyAssociation` | 1.11 | `party_reference_data_directory_retrieve_associations` |
| `BankRelations/Update` | Command | `UpdateBankRelations` | 1.12 | `party_reference_data_directory_update_bank_relations` |
| `BankRelations/Retrieve` | Query | `RetrieveBankRelations` | 1.12 | `party_reference_data_directory_retrieve_bank_relations` |
| `PartyReferenceDataDirectoryEntry/Control` | Command | — | deferred | — |
| `PartyReferenceDataDirectoryEntry/Request` | Command | — | deferred | — |
| `PartyReferenceDataDirectoryEntry/Execute` | Command | — | deferred | — |
| `PartyReferenceDataDirectoryEntry/Exchange` | Command | — | deferred | — |

**Register request (1.5).** BIAN's Register body has `PartyReference`, `DirectoryEntryDateType`, `DirectoryEntryDate` and `PartyType`. Only `PartyType` is used. The other three are accepted and ignored: the directory records the time of registration as the entry's `OpenDate` (`TimeProvider`, tech-stack §7), and the party's details are Demographics and Reference data, recorded by their own updates (1.9, 1.10). BIAN's generated enum wrappers (`{"Partytypevalues": "Person"}`) are flattened to plain strings (`"Person"`), in the request and the response. Register answers `200` (BIAN's declared status) with the BIAN Register fields and the ids of the entry and its three singleton BQs, and the entry's version as `ETag`.

**Retrieve request (1.7).** `GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve` answers `200` from the entry's view only, with the BIAN Retrieve fields (`PartyType`, `DirectoryEntryDateType` = `OpenDate`, `DirectoryEntryDate`) and the ids of the entry and its three singleton BQs, in the same shape as the Register response. BIAN's `PartyReference` is not returned: the party's details are served by the Demographics and Reference Retrieves (1.9, 1.10). The entry's version is the strong `ETag` (`"1"`), and an `If-None-Match` that names it (weak comparison, a list or `*`) answers `304 Not Modified` with the `ETag` and no body. A route value that is not an identifier RimBank issues (not a GUID, empty, or not a version 7 GUID) is `400` with the shared `shared.invalid-identifier` code and `errors["partyreferencedatadirectoryid"]`; an unknown entry is `404` `party.party-not-found`.

**Update request (1.8).** `PUT /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Update` takes BIAN's `UpdatePartyReferenceDataDirectoryEntry` body.
- **What changes.** Only `PartyType` is used, and it is optional. When it is omitted, the entry keeps its type. When it is given, it is read like Register's, except that a blank value is `party.party-type-invalid`.
- **RefreshDate.** Every accepted Update records the time of the update (`TimeProvider`) as the entry's `RefreshDate`, BIAN's second `DirectoryEntryDateType`. An Update with an unchanged type is therefore still a business fact and appends `PartyReferenceDataDirectoryEntryUpdated`.
- **Ignored fields.** `PartyReference`, `DirectoryEntryDateType` and `DirectoryEntryDate` are accepted and ignored, as in Register.
- **Headers.** The request requires `Idempotency-Key` and `If-Match` ([ADR 0027](0027-optimistic-concurrency-for-commands.md)): missing → `428` `shared.if-match-required`, malformed → `400` `shared.if-match-invalid`, stale → `409` `party.version-conflict`.
- **Response.** It answers `200`, built from the acknowledgement: the Register response's shape plus `RefreshDate`, with the new version as `ETag`. From 1.8, Retrieve also returns `RefreshDate`, and omits it until the first Update.
- **Spec defect.** The spec's `200` for this operation points at `BadRequest`. RimBank answers with the intended `UpdatePartyReferenceDataDirectoryEntryResponse` schema instead.

**Demographics requests (1.9).** The Demographics record lives in the entry (an entity, as decided above), so its three operations use the entry's stream, version and inline view.
- **Ids.** Every route carries `{partyreferencedatadirectoryid}` and `{demographicsid}`. A malformed one is `400` `shared.invalid-identifier`, naming that parameter in `errors`. The checks run in order: an unknown entry is `party.party-not-found`, a `demographicsid` that is not the entry's is `party.behavior-qualifier-not-found`, and then (for commands) a stale `If-Match` is `party.version-conflict`.
- **Update** (`PUT …/Demographics/{demographicsid}/Update`) is a partial update of BIAN's `UpdateDemographics` fields. A field left out keeps its value, an empty one clears it, and text is trimmed. A given `Employment` (`PartyReference`, `InvolvementReference`, free text never checked against the directory) replaces the whole employment. `Socio-EconomicClassification` and the `Employment` parts are at most 140 characters (ISO 20022 Max140Text), and the histories and `ServicingConstraints` at most 2048 (Max2048Text). A longer one is `party.demographics-invalid` with the BIAN field names in `errors`. Every accepted Update appends `DemographicsUpdated`, even when nothing changes.
- **Exchange** (`PUT …/Demographics/{demographicsid}/Exchange`) verifies or rejects the recorded details, through RimBank's extension field `ExchangeActionType`. The status lifecycle, the ignored body fields and the not-recorded rule are in [ADR 0028](0028-demographics-exchange-as-verification.md).
- **Retrieve** (`GET …/Demographics/{demographicsid}/Retrieve`) answers from the entry's view, with the entry's version as `ETag` and `304` on a matching `If-None-Match`. Before the first Update, it answers `200` with the ids and `VerificationStatus` `Unverified` only.
- **Headers and responses.** Update and Exchange require `Idempotency-Key` and `If-Match`, as in 1.8. All three answer `200` with the same body: BIAN's Demographics fields without `EthnicityReligion`, `VerificationStatus`, `VerificationDate` (left out while unverified), and the entry and Demographics ids. Update and Exchange build it from their acknowledgement.

**Reference requests (1.10).** The Reference record lives in the entry (an entity, as decided above), so both operations use the entry's stream, version and inline view. [ADR 0029](0029-reference-fields-in-stages.md) decides which BIAN fields are recorded when.
- **Ids.** Every route carries `{partyreferencedatadirectoryid}` and `{referenceid}`, checked in the same order as Demographics. A malformed `referenceid` is `400` `shared.invalid-identifier` with `errors["referenceid"]`.
- **Update** (`PUT …/Reference/{referenceid}/Update`) is a partial update of the party identifiers:
  - `GovernmentIssuedIdentityReference.TaxIdentificationNumber` and `.NationalRegistrationNumber` (Max35Text);
  - the LEI (ISO 17442, check digits verified) in `PartyLegalEntityReference.Identification`.
  - From 1.18, it also updates:
    - `PartyFullLegalName` (Max140Text) and `PartyNameSalutation` (Max35Text);
    - `DateofBirth` (a calendar date, from 1900-01-01 to today);
    - `Nationality.Code` and `PartyCountryOfResidence.Code` (ISO 3166-1 alpha-2, [ADR 0030](0030-iso-3166-country-codes.md));
    - `ResidencyStatus` (`StatusDescription` `Resident` or `NonResident`, and an optional `StatusDateTime`).
  - A value left out keeps its value, and an empty one (or an object whose parts are all empty) clears it. An invalid one is `party.reference-invalid` with the BIAN field names in `errors`.
  - Every accepted Update appends `PartyReferenceUpdated`, even when nothing changes.
  - The other BIAN Reference fields are refused with `party.reference-field-not-supported` until the phase that records them (1.19–1.20, ADR 0029). So are the parts of the objects above that RimBank does not record.
- **Retrieve** (`GET …/Reference/{referenceid}/Retrieve`) answers from the entry's view, with the entry's version as `ETag` and `304` on a matching `If-None-Match`. Before the first Update, it answers `200` with the ids only.
- **Headers and responses.** Update requires `Idempotency-Key` and `If-Match`, as in 1.8. Both answer `200` with the same body:
  - `GovernmentIssuedIdentityReference` and `PartyLegalEntityReference`, each left out when it holds no identifier;
  - from 1.18: the names, `DateofBirth` as `YYYY-MM-DD`, `Nationality` and `PartyCountryOfResidence` as `{ "Code": … }`, and `ResidencyStatus`, each left out when unknown;
  - the entry and Reference ids.

  Update builds it from its acknowledgement.

**Deferred** means not in Stage 1 and not scheduled (no speculative coverage, mission non-goals). Control (terminate an entry) is the likeliest to come next. It needs its own decisions on what a terminated entry means for its associations and for consumers, and those are recorded when it is scheduled. Demographics/Exchange (accept, verify, reject) is a command on the entry. Its semantics are decided in [ADR 0028](0028-demographics-exchange-as-verification.md) (1.9).

### Domain events

| Aggregate | Event | Raised by |
|---|---|---|
| Entry | `PartyRegistered` | `RegisterParty` (carries the singleton BQ ids) |
| Entry | `PartyReferenceDataDirectoryEntryUpdated` | `UpdatePartyReferenceDataDirectoryEntry` (carries the resulting party type and the `RefreshDate`, 1.8) |
| Entry | `DemographicsUpdated` | `UpdateDemographics` (carries the full resulting details and their verification status and date, 1.9) |
| Entry | `DemographicsExchanged` | `ExchangeDemographics` (carries the verification status and date, 1.9, [ADR 0028](0028-demographics-exchange-as-verification.md)) |
| Entry | `PartyReferenceUpdated` | `UpdatePartyReference` (carries the full resulting Reference: the party identifiers from 1.10, and the names, date of birth, nationality and residency from 1.18, [ADR 0029](0029-reference-fields-in-stages.md)) |
| Entry | `BankRelationsUpdated` | `UpdateBankRelations` |
| PartyAssociation | `PartyAssociationRegistered` | `RegisterPartyAssociation` |
| PartyAssociation | `PartyAssociationUpdated` | `UpdatePartyAssociation` |

The exact names are fixed by the phase that adds each event, within this list. Each phase may add events (for example a no-op guard needs none), and any addition updates this table.

### Integration channels

Integration events are mapped from domain events, never from views (tech-stack §8.4). Each one carries the state its consumers need.

| AsyncAPI channel | From | Phase |
|---|---|---|
| `PartyReferenceDataDirectoryEntry/Created` | `PartyRegistered` | 1.13 |
| `PartyReferenceDataDirectoryEntry/Updated` | `PartyReferenceDataDirectoryEntryUpdated` | 1.13 |
| `Demographics/Created`, `Demographics/Updated` | The first / a later `DemographicsUpdated` or `DemographicsExchanged` of an entry | deferred |
| `Reference/Created`, `Reference/Updated` | The first / a later `PartyReferenceUpdated` of an entry | deferred |
| `Associations/Created`, `Associations/Updated` | `PartyAssociationRegistered` / `PartyAssociationUpdated` | deferred |
| `BankRelations/Created`, `BankRelations/Updated` | The first / a later `BankRelationsUpdated` of an entry | deferred |

The BQ channels are deferred until a consumer needs them. Current Account (4.1) needs only "the party is known", which the CR channels carry. They also carry PII, which is better not published before 7.2.

### Cross-aggregate rule

`RegisterPartyAssociation` requires the owning entry (the path's `{partyreferencedatadirectoryid}`) and the associate entry (`AssociateReference`) to exist in this directory, and the two must differ. The handler checks this by loading both entry streams through `PartyDirectory`, the **write side**, and never through a view (mission principle 5, tech-stack §8.1). The associate must be an entry of this directory: a reference to a party RimBank does not know is refused. An association's owner and associate never change after Register.

### Read models

Both views are inline single-stream projections, so a Retrieve right after a command sees its result ([tech-stack §8.3](../../specs/tech-stack.md#83-projection-lifecycle-when-to-use-which)). Each is wired with `AddInlineView` and can be rebuilt with `RebuildViewAsync` ([ADR 0016](0016-per-context-marten-stores.md)).

| View | Stream | Lifecycle | Queries served |
|---|---|---|---|
| `PartyReferenceDataDirectoryEntryView` | Entry | Inline | `RetrievePartyReferenceDataDirectoryEntry` (the CR fields), and `RetrieveDemographics`, `RetrievePartyReference` and `RetrieveBankRelations` (each returns its BQ section, after checking the BQ id) |
| `PartyAssociationView` | PartyAssociation | Inline | `RetrievePartyAssociation`. A path whose entry id is not the association's owner is `party.association-not-found` |

Stage 1 has no async view. Listing a party's associations is not a BIAN operation of this domain. If a later phase needs it, it gets an async cross-stream view and its own decision.

`PartyDirectoryEnquiry` (`IReadPort`) reads both views.

### Concurrency

The `ETag` returned by a Retrieve, and the `If-Match` a command sends back, is the **stream version** of the aggregate ([tech-stack §5](../../specs/tech-stack.md#5-api--error-contract)): the entry's version for the CR and its three BQs, and the association's own version for an association. A stale version is `party.version-conflict` (409). Updating one singleton BQ changes the entry version, so a client holding an older `ETag` for another BQ of the same entry gets a 409 and retries.

### Personal data

- Demographics `EthnicityReligion` is **not modelled**: it is special-category personal data, and RimBank keeps none of it. Its field is ignored in requests and never returned. Retrieve returns the BIAN schema without it.
- The other Demographics fields, party names, identifiers (Reference) and contact details are PII. They live only in this context's streams and views, and are listed for 7.2 (masking in logs, protection at rest).

### Error codes

| Code | Kind | When |
|---|---|---|
| `party.party-already-registered` | Conflict (409) | Register on an entry that is already registered (1.2) |
| `party.party-type-invalid` | Validation (400) | A `PartyType` that is neither `Person` nor `Organisation` (1.2); over HTTP, any other string, including another case (1.5) |
| `party.party-type-required` | Validation (400) | The Register request has no `PartyType`, or an empty one (1.5) |
| `party.party-not-found` | NotFound (404) | The path's entry does not exist (any command or query on an entry, including `Associations/Register`) |
| `party.behavior-qualifier-not-found` | NotFound (404) | The path's Demographics, Reference or BankRelations id does not belong to the entry |
| `party.association-not-found` | NotFound (404) | The association does not exist, or the path's entry is not its owner |
| `party.associate-not-registered` | BusinessRule (422) | `AssociateReference` is not an entry of this directory |
| `party.self-association` | BusinessRule (422) | The associate is the owning entry |
| `party.demographics-invalid` | Validation (400) | A Demographics Update text is longer than its ISO 20022 type allows; `errors` names each BIAN field (1.9) |
| `party.exchange-action-type-required` | Validation (400) | A Demographics Exchange has no `ExchangeActionType`, or a blank one (1.9, ADR 0028) |
| `party.exchange-action-type-invalid` | Validation (400) | An `ExchangeActionType` other than `Verify` or `Reject` (1.9, ADR 0028) |
| `party.demographics-not-recorded` | BusinessRule (422) | A Demographics Exchange on details that were never recorded, or were all cleared (1.9, ADR 0028) |
| `party.reference-invalid` | Validation (400) | A Reference Update value is not valid: an identifier (length, or LEI format and check digits); from 1.18, a name too long, a date that is not ISO 8601 or a birth date before 1900-01-01 or after today, a country that is not ISO 3166-1 alpha-2, or a residency status other than `Resident`/`NonResident`. `errors` names each BIAN field (1.10, 1.18, ADR 0029) |
| `party.reference-field-not-supported` | Validation (400) | A Reference Update sends a BIAN field or object part RimBank does not record yet; `errors` names each field and its phase, if one is planned (1.10, ADR 0029) |
| `party.version-conflict` | Conflict (409) | `If-Match` is not the current stream version (1.8, [ADR 0027](0027-optimistic-concurrency-for-commands.md)) |
| `shared.if-match-required` | PreconditionRequired (428) | A command on an existing entry or association has no `If-Match` (1.8, ADR 0027) |
| `shared.if-match-invalid` | Validation (400) | `If-Match` is not exactly one strong version tag (1.8, ADR 0027) |

Validation codes (400) for each request's fields are added by the phase that adds the request.

## Consequences

- 1.2 builds the entry aggregate with `PartyRegistered` carrying the singleton BQ ids. 1.11 builds a second aggregate, `PartyAssociation`, with its own stream, view and typed id, instead of adding an entity to the entry.
- The context's store has two stream types and two inline views. `PartyDirectory` and `PartyDirectoryEnquiry` cover both.
- Associations scale and version independently of the entry. The price is a cross-aggregate existence check, done on the write side. It is not transactional across streams, but entries are never deleted, so a successful check stays true. Control (terminate) is deferred, and will decide what a terminated entry means for its associations.
- BQ updates of one entry contend on one stream version. This is acceptable because they are rare, human-paced changes.
- The CR Control, Request, Execute and Exchange operations and the 8 BQ channels are not implemented in Stage 1. The 1.14 conformance tests cover only the implemented subset, and 1.17's tool-list test compares against the implemented rows of the operation table.
- **Enforcement:** `tests/RimBank.Conformance.Tests/Catalogues/PartyReferenceDataDirectoryCatalogue.cs` holds the operation and channel tables above as data. `PartyReferenceDataDirectoryCatalogueTests` fails if a spec operation or channel is missing from them or listed twice, if a row's command/query kind differs from the spec's, or if a phase is neither a Stage 1 phase nor `deferred`. A phase that changes this mapping updates this ADR and that table in the same PR.
- tech-stack §3.4 shows the `PartyAssociation` aggregate in `Associations/`, and the roadmap 1.11 row and Stage 1 header point here.
