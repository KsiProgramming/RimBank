# Party Reference Data Directory: Associations BQ

Roadmap phase: 1.11
Branch: plan/1.11-party-associations-bq

## Context
1.1 decided that Associations is the one Behavior Qualifier of this domain with its own lifecycle and many instances per party, so it is **its own aggregate, `PartyAssociation`**, with its own stream, typed id, version and inline view ([ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md), options and cross-aggregate rule). The catalogue already tags `Associations/Register`, `Associations/Update` and `Associations/Retrieve` with `1.11`, and `IPartyDirectory` / `IPartyDirectoryEnquiry` say they grow here.

The [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) gives the three operations the same body (`RegisterAssociations`, `UpdateAssociations`, `RetrieveAssociations`):
- `AssociateReference` (`Involvedparty`: `PartyReference`, `InvolvementReference`);
- `AssociateType` (`Party_Partyrelationshiptypevalues`). The enum lists 7 values, and its description lists 2 more: `PartyIsThePowerOfAttorneyForParty` and `PartyReceivesBenefitsFromAParty`;
- `AssociationValidFromToDate` (ISO 20022 `DateTimePeriod`: `FromDateTime`, `ToDateTime` and unrelated payment parts);
- `EmployeeReference`, `AssociateObligationorDependencyDescription`, `ProductInstanceReference`, `PreferredBeneficiary`, `ProxyRepresentativePowerofAttorneyReference` and `ProductInstanseInvolvementReference`.

**Chosen in the interview:** the core slice. 1.11 records the associate, the relationship type and the validity period. Every other field is refused, as ADR 0029 does for Reference.

It depends on 1.2–1.8 (entry aggregate, write and read ports, Marten store, `If-Match`), all done. It unblocks **1.21** (corporate and officer references, ADR 0032) and the Associations MCP tools (**1.17**).

Drivers:
- Mission principles:
  - 3: value objects and enums, no primitive obsession;
  - 4: event-sourced truth;
  - 5: commands acknowledge from the decided state, and the cross-aggregate checks read the **write side**, never a view;
  - 8: Problem Details;
  - 10: small steps, so only three BIAN fields.
- Tech-stack:
  - §3.4: the `Associations/` folder, with a flat namespace (ADR 0026);
  - §5: routes, `Idempotency-Key`, `If-Match` and `ETag`;
  - §7: a BQ as its own aggregate needs an ADR (ADR 0024 records it);
  - §8.1–8.3: an inline single-stream view that can be rebuilt.

**CQRS split:**
- **Command side:**
  - `RegisterPartyAssociation`: the handler creates the `PartyAssociationId` (`TimeProvider`), loads the owner's and the associate's **entry streams** through `IPartyDirectory`, runs the pure `Decide`, and starts the association stream;
  - `UpdatePartyAssociation`: load the association stream, check the owner and `ExpectedVersion`, decide, and append with the expected version.
  - Both acknowledge from the decided state, never from the view.
- **Query side:** `RetrievePartyAssociation` reads the new **inline** `PartyAssociationView` through `IPartyDirectoryEnquiry`. There is no async projection, and no list of a party's associations (not a BIAN operation, ADR 0024).

## Scope
### In scope
- **Aggregate `PartyAssociation` (core `Associations/`):**
  - `PartyAssociationId` (Guid v7 typed id, ADR 0008);
  - state: id, owner `PartyReferenceDataDirectoryEntryId`, associate `PartyReferenceDataDirectoryEntryId`, `PartyRelationshipType`, optional `AssociationValidityPeriod`;
  - `Evolve` and a pure `Decide` for Register and Update, with `IDecider` (ADR 0010);
  - events (`PartyAssociationEvent` base): `PartyAssociationRegistered` (the full state) and `PartyAssociationUpdated` (the full resulting type and period).
- **`PartyRelationshipType` (enum) + `PartyRelationshipTypes` (BIAN spelling):**
  - the 7 enum values plus the 2 that only BIAN's description lists (ADR 0033);
  - read in any case, answered in BIAN spelling;
  - BIAN's enum wrapper is flattened to a plain string, as for `PartyType` (ADR 0024);
  - **direction:** the record reads "the owner (path entry) *type* the associate". For example, `PartyIsParentOfParty` means the owner is the associate's parent.
- **`AssociationValidityPeriod` (value object):**
  - `FromDateTime` and `ToDateTime`, both optional, at least one given;
  - read as an ISO 8601 date-time (a date alone, or a value without an offset, is read as UTC) and stored as UTC `DateTimeOffset`;
  - `From` ≤ `To` when both are given;
  - no check against today: an association can be recorded after it ended or before it starts. A set `ToDateTime` is how an association is ended. No "active" flag is derived, because a view must not depend on the clock.
- **Register (`POST /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Associations/Register`):**
  - `AssociateReference.PartyReference` is required: an entry id RimBank issues (Guid v7);
  - `AssociateType` is required; `AssociationValidFromToDate` is optional;
  - checks in order: request values (400) → owner entry exists (404 `party.party-not-found`) → associate ≠ owner (422 `party.self-association`) → associate entry exists (422 `party.associate-not-registered`);
  - both checks load entry streams through `IPartyDirectory` (ADR 0024 cross-aggregate rule);
  - no uniqueness check: the same pair and type can be recorded twice (for example, for two periods), because uniqueness would need a cross-stream index (ADR 0033);
  - requires `Idempotency-Key`; answers `200` with `ETag` `"1"`.
- **Update (`PUT …/Associations/{associationsid}/Update`):**
  - a partial update, as in 1.9–1.20: an omitted field keeps its value, and a given one replaces it;
  - `AssociateType` cannot be cleared (blank → `party.association-invalid`);
  - an empty `AssociationValidFromToDate` object clears the period;
  - `AssociateReference`: the same associate is accepted; another one is 422 `party.associate-cannot-change` (owner and associate never change, ADR 0024);
  - checks in order: route ids (400 `shared.invalid-identifier`) → body values (400) → association exists and its owner is the path entry (404 `party.association-not-found`) → `If-Match` (409 `party.version-conflict`) → associate unchanged (422);
  - requires `Idempotency-Key` and `If-Match` (428 / 400 / 409, ADR 0027);
  - every accepted Update appends `PartyAssociationUpdated`, even when nothing changes.
- **Retrieve (`GET …/Associations/{associationsid}/Retrieve`):**
  - answers from `PartyAssociationView`, with the association's version as `ETag` and `304` on a matching `If-None-Match`;
  - an unknown association, or one whose owner is not the path entry, is 404 `party.association-not-found`.
- **Response (all three, `200`):** `AssociationsId`, `PartyReferenceDataDirectoryEntryId`, `AssociateReference` `{ "PartyReference": … }`, `AssociateType` in BIAN spelling, and `AssociationValidFromToDate` (each bound as ISO 8601 UTC, left out when unknown). Register and Update build it from their acknowledgement.
- **Refused fields:** every other BIAN field, `AssociateReference.InvolvementReference` and the non-date parts of `DateTimePeriod` are refused with 400 `party.association-field-not-supported`, with `errors` naming each path (and `1.21` where it applies, otherwise no phase). The check runs before the value checks, as for Reference.
- **Ports:**
  - `IPartyDirectory` gains `LoadAssociationAsync`, `StartAssociation` and `AppendToAssociation`;
  - `IPartyDirectoryEnquiry` gains `FindAssociationAsync`;
  - in-memory test adapters in `RimBank.Core.Tests` and Marten implementations in `.Postgresql`.
- **PostgreSQL:** stable stored event names `party_association_registered` and `party_association_updated`, and `AddInlineView<PartyAssociationView, PartyAssociationViewProjection, PartyAssociationEvent>`.
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/Associations/`):** three endpoints, the request/response models and route values, mapped in `PartyReferenceDataDirectoryApi`.
- **Demo requests** in `src/RimBank.Host/RimBank.Host.http` (approach group 4).
- **Docs:**
  - new ADR 0033;
  - ADR 0024 amended;
  - tech-stack §3.4 updated;
  - the roadmap row 1.11 ticked.

### Out of scope
- `EmployeeReference`, `AssociateObligationorDependencyDescription`, `PreferredBeneficiary` and `ProxyRepresentativePowerofAttorneyReference`: refused, no phase planned. (The power-of-attorney link itself is recordable as `PartyIsThePowerOfAttorneyForParty`.)
- `ProductInstanceReference` and `ProductInstanseInvolvementReference`: product agreements belong to Stage 2+ contexts. Refused, no phase planned.
- The corporate and officer Reference fields: **1.21**.
- Listing a party's associations, or the inverse view (associations where a party is the associate): not a BIAN operation. A later phase would need an async cross-stream view and an ADR (ADR 0024).
- Rules tied to `PartyType` (for example, only organisations can be `PartyIsSubsidiaryOfParty`). Every type is accepted for both party types.
- Ending an association by a Control operation, or terminating an entry (deferred, ADR 0024).
- The `Associations/Created` and `Associations/Updated` channels (deferred, ADR 0024), the MCP tools (**1.17**, as for every BQ so far), per-operation scopes (**1.15**), and masking and encryption at rest (**7.2**).
- A log-capture PII test and a run of the manual demo (not chosen in the interview).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Recorded fields | The associate, `AssociateType` and the validity period; everything else refused | Chosen in the interview. It is the minimum that makes an association meaningful, and principle 10 keeps the phase to about a day |
| Relationship types | The 7 enum values plus `PartyIsThePowerOfAttorneyForParty` and `PartyReceivesBenefitsFromAParty` | The BIAN description lists 9 values and the enum misses 2. RimBank follows the intent (mission "Source of truth" 3), as ADR 0032 did for `CharitableIncorporatedOrganisation` |
| Direction | "Owner *type* associate" | BIAN's value names read "Party … Party". Fixing the order once avoids two readings of the same record |
| Enum JSON shape | Plain string, with BIAN's wrapper flattened, in any case on input | Same as `PartyType` (ADR 0024) |
| Associate identity | `AssociateReference.PartyReference` must be an entry id of this directory; `InvolvementReference` refused | ADR 0024: the associate must be an entry RimBank knows. The role is carried by `AssociateType` |
| Validity period | Optional `From`/`To`, UTC `DateTimeOffset`, `From` ≤ `To`, no check against today, no derived "active" flag | BIAN types it `ISODateTime`. A projection must not depend on the clock (tech-stack §8.2) |
| Owner and associate | Fixed at Register; changing the associate is 422 `party.associate-cannot-change` | ADR 0024. A different associate is a different association, so the client registers a new one |
| Uniqueness | None | Checking it would need a cross-stream index on the write side. Duplicates are harmless, and two periods of the same link are legitimate |
| Cross-aggregate checks | Owner and associate loaded from their entry streams through `IPartyDirectory` | ADR 0024 and mission principle 5. Entries are never deleted, so a passed check stays true |
| Update semantics | Partial: omitted keeps, given replaces, an empty period clears; the type cannot be cleared | Same as 1.9–1.20 |
| New error codes | `party.association-invalid` (400), `party.association-field-not-supported` (400), `party.associate-cannot-change` (422) | One code per contract, as for Reference. The other codes are already in ADR 0024 |
| Version and `ETag` | The association's own stream version | ADR 0024 "Concurrency" |
| Work order | Domain-out, test first, one PR, one commit per group | Chosen in the interview |

## ADRs to write
- **ADR 0033 (new): Party associations: what is recorded.** It records:
  - the recorded fields and the refused ones (with phases);
  - the 9 relationship types, their BIAN spelling and the direction rule;
  - the validity period rules, and why there is no "active" flag;
  - immutable owner and associate, and `party.associate-cannot-change`;
  - no uniqueness;
  - the three new error codes.

  Options considered:
  - recording the free-text party references as opaque, unchecked references;
  - the 7 enum values only;
  - checking the period against today.
- **Amend ADR 0024:**
  - an "Associations requests (1.11)" paragraph with the routes, check order, headers and response;
  - the domain events table (final names);
  - the error codes table (the three new codes, and the 1.11 tags).
- **Tech-stack §3.4:** the `Associations/` tree with the real files, and the `RimBank.Api` folder list gains `Associations/`.

## Open questions
- None.
