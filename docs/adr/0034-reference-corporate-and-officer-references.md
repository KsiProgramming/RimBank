# 0034. Reference BQ: corporate and officer references as checked directory entries, on organisations only

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.21

## Context

[ADR 0032](0032-reference-legal-standing-and-documents.md) moved the last four BIAN Reference fields to roadmap 1.21, after the Associations BQ (1.11). They are references to other parties:

- `CorporateCustomerReference` ("the corporate entity, Company for company customers");
- `CorporateCustomerLegalEntityReference` ("the legal entity reference, for company customers");
- `CompanyOfficerReference` ("one or more identified company officers for a corporate customer").

  Each of these three is an `Involvedparty` (`PartyReference` and `InvolvementReference`, both strings).
- `CompanyOfficerRole` ("the specific role played by the identified officer", ISO 20022 `Party/Role/PartyRole`).

What the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) leaves open:

- **What a `PartyReference` points at.** It is a free string.
- **The role's shape.** `CompanyOfficerRole` is typed as BIAN's `Party` schema, which has no role property (`ContactPoint`, `Identification`, `MoneyLaunderingCheck`, …).
- **How many officers.** The description says "one or more", but the schema is one object.
- **Whether the fields fit any party.** All four describe a company customer.

[ADR 0033](0033-party-associations.md) set the rule for a link between parties in this directory: the other party is a directory entry id, checked on the write side. It rejected unchecked party links next to checked ones, because two guarantees for the same kind of link would confuse consumers.

## Options considered

1. **Checked entry ids, any party type.** Each `PartyReference` is a registered entry other than the party itself, and every value is allowed for a person and for an organisation, as in 1.18 and 1.20. Small, but it lets a person hold a "corporate customer", which BIAN's descriptions rule out.
2. **Checked entry ids with party-type rules.** As option 1, plus:
   - only an organisation's entry holds the references;
   - the corporate customer and the legal entity are organisations;
   - the officer is a person.

   This is closest to BIAN's intent. It is the first party-type rule in the Reference BQ, and it needs a guard on the entry's own Update.
3. **Retire the fields to Associations.** Refuse them for good and tell clients to register associations instead. The smallest change, but BIAN Reference fields would never be recorded.
4. **Opaque text references.** Cheap, but ADR 0033 rejected unchecked party links.

## Decision

Option 2, chosen in the planning interview.

### Recorded fields

| BIAN path | Recorded as | Rule |
|---|---|---|
| `CorporateCustomerReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | A registered entry, not the party itself, of type `Organisation` |
| `CorporateCustomerLegalEntityReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | Same as above. It may be the same entry as the corporate customer |
| `CompanyOfficerReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | A registered entry, not the party itself, of type `Person`. One officer, because BIAN gives one object |
| `CompanyOfficerRole` | `CompanyOfficerRole` | A plain string, trimmed, not blank, at most 35 characters (Max35Text), for example `Director`. Only with an officer |

- **The four live in `PartyCorporateReferences`.** It is a nullable member of `PartyReferenceDetails`, so events and views stored before 1.21 still load, fold and rebuild.
- **Partial update, as for the other Reference fields.** An omitted value keeps its value. A blank `PartyReference`, an empty object, a blank role or an empty role object clears the value.
- **Refused parts.** `InvolvementReference` is refused with `party.reference-field-not-supported` and no phase, because the role is in `CompanyOfficerRole`. So is every part of a `CompanyOfficerRole` sent as an object (BIAN's `Party` shape).

### Rules

- **Who checks what.** The handler loads each entry the Update **sets** through the write port (`IPartyDirectory`), never a view. The pure `Decide` then checks those loaded facts.
  - References the Update keeps are not loaded again, because they were checked when they were recorded.
  - An id of another stream type, such as an association, loads no entry events. So it is "not registered", never a cast failure (as in ADR 0033).
- **The order of the checks**, on the resulting Reference:
  1. Corporate references on an entry that is not an organisation are `party.corporate-references-need-organisation`.
  2. A reference to the party itself is `party.self-reference`.
  3. A reference that is not registered is `party.referenced-party-not-registered`.
  4. A reference of the wrong party type is `party.referenced-party-type-mismatch`.
  5. A role without an officer is `party.reference-invalid` naming `CompanyOfficerRole`. Clearing the officer while keeping the role is refused unless the same request clears the role, as for the political exposure description (ADR 0032).
- **The entry's Update keeps rule 1 true.** `PartyReferenceDataDirectoryEntry/Update` refuses `PartyType: Person` while corporate references are recorded, with `party.corporate-references-need-organisation`. The client clears the references first. An invariant must not depend on the order in which commands arrive.
- **Accepted drift.** A referenced entry can later change its own party type, for example the officer's entry becoming an organisation. The references that point at it are not checked again: that would need a cross-stream index on the write side, the same reason ADR 0033 gave for not enforcing uniqueness. The rule holds when a reference is recorded, and a consumer that needs the current type reads the referenced entry.
- **No link with Associations.** A Reference reference and an association are independent records. Neither is derived from the other, and a client that wants both registers both.

### Error codes

| Code | Kind | When |
|---|---|---|
| `party.corporate-references-need-organisation` | BusinessRule (422) | Corporate references on an entry that is not an organisation, or the entry Update to `Person` while they are recorded |
| `party.self-reference` | BusinessRule (422) | A reference names the party itself |
| `party.referenced-party-not-registered` | BusinessRule (422) | A reference names no registered entry |
| `party.referenced-party-type-mismatch` | BusinessRule (422) | The corporate customer or legal entity is not an organisation, or the officer is not a person |

The Associations codes (`party.self-association`, `party.associate-not-registered`) are not reused, so each code names what failed. A business-rule `Error` carries no field errors ([ADR 0015](0015-problem-details-mapping.md)), so these 422 answers name the BIAN path at the start of their `detail` (for example `CompanyOfficerReference.PartyReference must name a party of type Person.`), not in `errors`. A malformed id or role is still `party.reference-invalid` (400), with the path in `errors`.

## Consequences

- **Staging is finished.** `PartyReferenceFields.Staged` is empty: all 24 BIAN `UpdateReference` fields are recorded. `PartyReferenceChangesTests` pins that, and `PartyReferencePartialUpdateProperties` covers the four new fields in the partial-update law.
- **The command handler reads other streams.** `UpdatePartyReferenceHandler` loads up to three more entry streams through `IPartyDirectory`, as `RegisterPartyAssociationHandler` does. It never uses a read port, so the CQRS architecture rules are unchanged.
- **HTTP.** The Reference adapter reuses the Associations `InvolvedPartyModel`. `CompanyOfficerRole` is read as raw JSON, so an object can be refused part by part instead of failing to bind.
- **PII.** Who a company's officer is, and in which role, is personal data. The values live only in this context's stream and view, are listed for 7.2, and a log-capture test proves that no new code logs them.
- **1.13 published language.** Integration events will carry these references as entry ids, so consumers keep their own copies (tech-stack §8.4).
- **Enforcement.** These tests pin the rules:
  - `CorporateReferencesDecisionTests` and `PartyCorporateReferencesTests` (core);
  - the `UpdatePartyReferenceHandlerTests` reference cases;
  - `CorporateReferencesEndpointTests` (Alba);
  - `PartyReferenceRoundTripTests` (PostgreSQL, with a view rebuild).
