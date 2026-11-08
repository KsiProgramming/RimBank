# 0033. Associations BQ: what a party association records

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.11

## Context

[ADR 0024](0024-party-reference-data-directory-mapping.md) made the Associations Behavior Qualifier its own aggregate, `PartyAssociation`, with its own stream, typed id, version and inline view. It also fixed the cross-aggregate rule: Register needs the owning entry and the associate entry to exist in this directory, checked on the write side, and the two must differ. It left open which BIAN fields an association records.

The [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) gives `Associations/Register`, `Associations/Update` and `Associations/Retrieve` the same body, with nine fields:

- `AssociateReference` (`Involvedparty`: `PartyReference`, `InvolvementReference`);
- `AssociateType` (`Party_Partyrelationshiptypevalues`). Its enum lists 7 values, but its description lists 9: it adds `PartyIsThePowerOfAttorneyForParty` and `PartyReceivesBenefitsFromAParty`;
- `AssociationValidFromToDate` (ISO 20022 `DateTimePeriod`: `FromDateTime`, `ToDateTime`, and payment parts such as `RelatedStandingOrder` and `NumberOfDays`);
- `EmployeeReference`, `AssociateObligationorDependencyDescription`, `PreferredBeneficiary`, `ProxyRepresentativePowerofAttorneyReference`, `ProductInstanceReference` and `ProductInstanseInvolvementReference`.

Roadmap 1.11 is one small phase (mission principle 10), and the interview chose the core slice.

## Options considered

1. **The core slice: associate, type and validity period, the rest refused.** It is the minimum that makes an association meaningful: who, how, and when.
2. **The core slice plus the free-text party references** (`EmployeeReference`, `PreferredBeneficiary`, `ProxyRepresentativePowerofAttorneyReference`) **as opaque, unchecked references.** This adds three party links that the directory cannot verify, next to an associate it does verify. Two kinds of party reference with different guarantees would confuse consumers.
3. **The 7 enum values only.** This follows the enum strictly, and drops two relationships that BIAN's own description lists, including power of attorney, a common banking relationship.
4. **Checking the period against today** (for example, refusing a past end). Banks record associations late, and record future ones ahead of time. A rule tied to the clock would refuse both.

## Decision

Option 1.

### Recorded fields

| BIAN path | Recorded as | Rule |
|---|---|---|
| `AssociateReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | Required at Register. A directory entry id (Guid v7), registered and other than the owner (ADR 0024) |
| `AssociateType` | `PartyRelationshipType` | Required at Register. One of 9 values, read trimmed and in any case, answered in BIAN spelling. BIAN's enum wrapper is flattened to a plain string, as for `PartyType` |
| `AssociationValidFromToDate.FromDateTime`, `.ToDateTime` | `AssociationValidityPeriod` | Optional. ISO 8601 date-time (one without an offset is UTC) or date (00:00 UTC), stored in UTC; at least one bound; `From` ≤ `To`; never compared with today |

- **Relationship types.** The 7 enum values plus `PartyIsThePowerOfAttorneyForParty` and `PartyReceivesBenefitsFromAParty`, because BIAN's description lists them (mission "Source of truth" 3, as ADR 0032 did for `CharitableIncorporatedOrganisation`). The member names are the BIAN names, so the store keeps BIAN spelling.
- **Direction.** A record reads "the owner (the path's entry) *type* the associate": `PartyIsParentOfParty` means the owner is the associate's parent. The inverse is not recorded: the associate's side would be another association, registered by its own owner.
- **Ending an association.** A known `ToDateTime` is how an association ends. No "active" flag is derived, because a view must not depend on the clock (tech-stack §8.2). A consumer compares the period with its own time.
- **Owner and associate never change.** An Update that names the registered associate is accepted. One that names another is `party.associate-cannot-change` (422): a different associate is a different association, so the client registers a new one.
- **No uniqueness.** The same owner, associate and type can be registered twice, for example for two periods. Checking it would need a cross-stream index on the write side, and a duplicate does no harm.
- **Partial update.** Like the singleton BQs: an omitted value keeps its value, a given one replaces it, and an empty `AssociationValidFromToDate` clears the period. The type cannot be cleared. Every accepted Update appends `PartyAssociationUpdated`, even when nothing changes.

### Refused fields

A request that sends one of these with a value is refused with `party.association-field-not-supported` (400), `errors` naming each BIAN path. The check runs before the values are read, so nothing is recorded partly. Properties that are not BIAN's are ignored.

- `EmployeeReference`, `AssociateObligationorDependencyDescription`, `PreferredBeneficiary`, `ProxyRepresentativePowerofAttorneyReference`: no phase is planned. The power-of-attorney link itself is recordable as the type `PartyIsThePowerOfAttorneyForParty`.
- `ProductInstanceReference`, `ProductInstanseInvolvementReference`: product agreements belong to later contexts (Stage 2+). No phase is planned.
- `AssociateReference.InvolvementReference`: the role is carried by `AssociateType`.
- Every part of `AssociationValidFromToDate` other than `FromDateTime` and `ToDateTime`.

### Error codes

The codes ADR 0024 lists for associations stay (`party.party-not-found`, `party.self-association`, `party.associate-not-registered`, `party.association-not-found`, `party.version-conflict`). This decision adds:

| Code | Kind | When |
|---|---|---|
| `party.association-invalid` | Validation (400) | A missing associate or type at Register, a blank one at Update, an associate that is not an entry id, an unknown type, or an invalid period; `errors` names each BIAN path |
| `party.association-field-not-supported` | Validation (400) | A BIAN field or part RimBank does not record |
| `party.associate-cannot-change` | BusinessRule (422) | An Update names another associate |
| `party.association-already-registered` | Conflict (409) | Register on an association stream that already exists. The handler creates a new id, so only a replayed decision meets it |

## Consequences

- `RimBank.Core/Party/PartyReferenceDataDirectory/Associations/` holds the aggregate (`PartyAssociation`, its events and decisions), the value objects (`PartyRelationshipType`, `AssociationValidityPeriod`), the input readers (`PartyAssociationTerms`, `PartyAssociationChanges`), the flows and the view. `PartyAssociationFields.NotRecorded` lists the refused fields.
- `IPartyDirectory` and `IPartyDirectoryEnquiry` cover both stream types. Loading one type of stream as the other gives no events, so an association id used as an associate is `party.associate-not-registered`, never a cast failure.
- The HTTP adapter has one request model for Register and Update (`PartyAssociationRequest`), because BIAN's two bodies are the same.
- The view and its events carry PII: who is related to whom. They are listed for 7.2 with the other Party data.
- 1.21 (the corporate and officer Reference fields) can now decide whether those references are associations, plain references, or both.
- **Enforcement:** `PartyAssociationInputTests`, `PartyAssociationDecisionTests` and `PartyAssociationLawsProperties` (core), `PartyAssociationRoundTripTests` (PostgreSQL), and the Alba Associations endpoint tests pin these rules.
