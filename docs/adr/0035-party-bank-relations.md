# 0035. BankRelations: what is recorded

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.12

## Context

BankRelations is the last Behavior Qualifier of Party Reference Data Directory. [ADR 0024](0024-party-reference-data-directory-mapping.md) made it a singleton entity of the entry aggregate, like Demographics and Reference: its id is created at Register, its section of the entry is empty until its first Update, its event is `BankRelationsUpdated`, and its Retrieve reads the entry's inline view.

The [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) gives `BankRelations/Update` and `BankRelations/Retrieve` the same body:

- `BankRelationType`: a free string, "the type of relationship (e.g. relationship manager)", with no code list;
- `BusinessUnitEmployeeReference` (`Involvedparty`: `PartyReference`, `InvolvementReference`): "the associated employee or unit";
- `BankRelationReference` (`PartyRelationship`): `RelationshipType`, `RelationshipValidityPeriod`, `RelationshipStartDate` and `RelationshipEndDate` (ISO `Datetime` objects), `RelationshipLifecycleStatus` (ISO 20022 `Status`) and `RelationshipIdentification`.

Three things are open in the spec:

- **What a type is.** Free text cannot drive a rule, and the mission asks for business values, not primitives (principle 3).
- **What the employee or unit is.** RimBank has no employee or organisation-unit context. [ADR 0033](0033-party-associations.md) and [ADR 0034](0034-reference-corporate-and-officer-references.md) decided that a link to another party in this directory is a registered entry id, checked on the write side.
- **How much of `PartyRelationship` to keep.** It has six parts, some of them overlapping (a validity period next to start and end dates).

## Options considered

1. **Free-text type, unchecked reference.** The smallest change, but a type nobody can rely on, and a party link with a weaker guarantee than every other one in the directory, which ADR 0033 rejected.
2. **Closed type, unchecked reference.** The type becomes a business value, but the reference still names nothing RimBank knows.
3. **Closed type, checked reference with a party-type rule.** The type says whether the bank's side is an employee (a person) or a unit (an organisation), and the reference is a registered entry of that type, loaded on the write side as in ADR 0034.

## Decision

Option 3, chosen in the planning interview. It keeps one guarantee for every party link in the directory, and it gives the type a meaning a rule can check.

### Recorded fields

| BIAN path | Recorded as | Rule |
|---|---|---|
| `BankRelationType` | `BankRelationType` | `RelationshipManager` or `AccountOfficer` (an employee's role), `ServicingBranch` or `ServicingBusinessUnit` (a unit's role). Read in any case, answered as written here |
| `BusinessUnitEmployeeReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | A registered entry, not the party itself, of type `Person` for an employee's role and `Organisation` for a unit's. Only with a type |
| `BankRelationReference.RelationshipStartDate.DateTimeContent` | UTC `DateTimeOffset` | An ISO 8601 date-time (UTC when it has no offset) or a date (00:00 UTC) |
| `BankRelationReference.RelationshipEndDate.DateTimeContent` | UTC `DateTimeOffset` | Same reading, and not before the start |
| `BankRelationReference.RelationshipLifecycleStatus.StatusDescription` | `BankRelationLifecycleStatus` | `Active`, `Suspended` or `Ended`, read in any case |

- **One relationship per entry.** BIAN gives one object, and ADR 0024 makes the BQ a singleton. A party with several bank contacts (a relationship manager and a servicing branch) would need its own aggregate, as Associations did; that is left to a later phase and its own ADR.
- **The type list is closed and small.** A new value is an amendment of this ADR, with the party type it references.
- **Partial update, as for the other BQs.** An omitted value keeps its value. A blank value or an empty object clears it, and an empty `BankRelationReference` clears the dates and the status.
- **Refused parts** (`party.bank-relations-field-not-supported`, no phase planned): `BusinessUnitEmployeeReference.InvolvementReference` (the role is the type), `RelationshipType`, `RelationshipValidityPeriod` (it duplicates the start and end dates), `RelationshipIdentification`, the other `Datetime` parts (`TimeZoneCode`, `DaylightSavingIndicator`, `DateTimeType`) and the other `Status` parts (`StatusReason`, `StatusDateTime`, `ValidityTime`). They are checked before the values, so nothing is updated partly. Properties that are not BIAN's are ignored.

### Rules

The rules are checked on the **resulting** BankRelations, so a partial update cannot leave it inconsistent with values it keeps:

1. A reference without a type is `party.bank-relations-invalid` naming `BankRelationType`. Clearing the type while the reference is kept is refused unless the same request clears the reference.
2. An end before the start is `party.bank-relations-invalid` naming `BankRelationReference.RelationshipEndDate.DateTimeContent`. The same day is accepted.
3. The reference names the party itself: `party.self-reference`.
4. The reference names no registered entry: `party.referenced-party-not-registered`. An id of another stream type, such as an association, loads no entry events, so it is not registered (as in ADR 0033).
5. The reference's party type does not fit the type: `party.referenced-party-type-mismatch`.

- **Who checks what.** The handler loads the referenced entry through `IPartyDirectory`, never a view, when the Update sets the reference or changes the type. A reference kept with its type is not loaded again: it was checked when it was recorded. The pure `Decide` checks the loaded party type.
- **The owner's party type does not matter.** Both persons and organisations have bank relations, so, unlike ADR 0034, the entry's own Update needs no guard.
- **Accepted drift.** The referenced entry may later change its own party type. It is not re-checked, for the reason ADR 0034 gives: that would need a cross-stream index on the write side.
- **No clock.** The dates are never compared with today, and no "active" flag is derived from them: a view must not depend on the clock (tech-stack §8.2). The lifecycle status is what the party declares, and `Ended` does not require an end date.

### Error codes

| Code | Kind | When |
|---|---|---|
| `party.bank-relations-invalid` | Validation (400) | A value is not valid, a reference has no type, or the relationship ends before it starts; `errors` names each BIAN path |
| `party.bank-relations-field-not-supported` | Validation (400) | The request sends a BIAN part RimBank does not record; `errors` names each path |

Rules 3 to 5 reuse the ADR 0034 codes (`party.self-reference`, `party.referenced-party-not-registered`, `party.referenced-party-type-mismatch`), because they already mean "a reference to another party of this directory". As there, a business-rule error carries no field errors, so the `detail` starts with the BIAN path (`BusinessUnitEmployeeReference.PartyReference must name a party of type Person.`).

## Consequences

- **The Stage 1 BQs are complete over HTTP.** 1.13 (published language) and 1.17 (MCP tools for Update and the BQs) can follow. The `BankRelations/Created` and `Updated` channels stay deferred (ADR 0024).
- **The entry carries a nullable `BankRelations` section** in its state, its event and its view, so streams and views written before 1.12 still load, fold and rebuild.
- **The command handler reads another stream.** `UpdateBankRelationsHandler` loads at most one more entry through `IPartyDirectory`, as the Reference and Associations handlers do. It never uses a read port, so the CQRS architecture rules are unchanged.
- **PII.** Who manages a party at the bank, and since when, is personal data. The values live only in this context's stream and view, are listed for 7.2, and a log-capture test proves that no new code logs them.
- **Enforcement.** These tests pin the rules:
  - `BankRelationsDecisionTests`, `BankRelationsChangesTests`, `BankRelationTypesTests`, `BankRelationsPartialUpdateProperties` and `UpdateBankRelationsHandlerTests` (core);
  - `UpdateBankRelationsEndpointTests`, `RetrieveBankRelationsEndpointTests` and `BankRelationsLogPrivacyTests` (Alba);
  - `BankRelationsRoundTripTests` (PostgreSQL, with a view rebuild);
  - `PartyReferenceDataDirectoryConformanceTests` (the two BIAN operations).
