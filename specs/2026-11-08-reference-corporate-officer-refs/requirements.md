# Party Reference Data Directory: Reference BQ (corporate and officer references)

Roadmap phase: 1.21
Branch: plan/1.21-reference-corporate-officer-refs

## Context
1.10, 1.18, 1.19 and 1.20 record every BIAN Reference field except four, which are still refused with `party.reference-field-not-supported` and tagged `1.21` in `PartyReferenceFields.Staged` ([ADR 0029](../../docs/adr/0029-reference-fields-in-stages.md), [ADR 0032](../../docs/adr/0032-reference-legal-standing-and-documents.md)). These are the four references to other parties, all in the [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) (`UpdateReference`, `RetrieveReference`):

- `CorporateCustomerReference` ("the corporate entity, Company for company customers"), an `Involvedparty` (`PartyReference`, `InvolvementReference`, both strings);
- `CorporateCustomerLegalEntityReference` ("the legal entity reference, for company customers"), an `Involvedparty`;
- `CompanyOfficerReference` ("one or more identified company officers for a corporate customer"), an `Involvedparty`;
- `CompanyOfficerRole` ("the specific role played by the identified officer", ISO 20022 `Party/Role/PartyRole`), typed as BIAN `Party`. That schema has **no role property** (ContactPoint, Identification, MoneyLaunderingCheck, …).

They were held back until after the Associations BQ (1.11, [ADR 0033](../../docs/adr/0033-party-associations.md)). ADR 0033 set the rule for a party link: the referenced party is a **directory entry id, checked on the write side**. It rejected unchecked party links next to checked ones. This phase applies the same guarantee to the Reference fields. **Chosen in the interview:** it adds party-type rules, because BIAN describes all four fields as "for company customers".

The Reference record stays in the entry aggregate (ADR 0024). The phase reuses the routes, the `PartyReferenceUpdated` event, `ETag` / `If-Match` and the inline view. It adds **no new operation, port, route or view**. It also changes one decision outside the Reference BQ: the entry's `Update` (1.8), so that the party-type rule cannot be broken from that side.

It depends on 1.8, 1.10, 1.11, 1.18, 1.19 and 1.20, all done. It completes the Reference BQ, leaving zero staged fields, which unblocks the Reference MCP tools (1.17) and the published language (1.13).

Drivers:
- Mission principles:
  - 3: typed ids and value objects, no primitive obsession;
  - 5: decisions use only write-side state, never a view;
  - 7: no reference outside this directory is resolved;
  - 8: Problem Details;
  - 10: one small phase.
- Tech-stack:
  - §3.4: value objects live in `Reference/` (ADR 0026);
  - §4 CQRS: the command handler uses only the write port `IPartyDirectory`;
  - §5: routes, `If-Match`, `Idempotency-Key`;
  - §8.1: the cross-aggregate check is done on write-side loads, as in Associations;
  - §8.3: inline view, rebuildable.

**CQRS split:**
- **Command side:**
  - `UpdatePartyReference`:
    - the handler loads the entry as today;
    - **it also loads, through `IPartyDirectory.LoadEntryAsync`, each entry referenced in the request**, at most three, folding each to "registered, and of which party type";
    - it passes those facts to the pure `Decide`, as `RegisterPartyAssociationHandler` does.
  - `UpdatePartyReferenceDataDirectoryEntry`: its `Decide` refuses a change of party type to `Person` while corporate references are recorded.
- **Query side:** `RetrievePartyReference` reads the same **inline** `PartyReferenceDataDirectoryEntryView`, whose Reference section gains the four fields. There is no async projection.

## Scope
### In scope
- **Recorded fields (core `Reference/`):**

  | BIAN path | Recorded as | Rule |
  |---|---|---|
  | `CorporateCustomerReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | A Guid v7 entry id. Registered, not the entry itself, and an **Organisation** entry |
  | `CorporateCustomerLegalEntityReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | Same as above |
  | `CompanyOfficerReference.PartyReference` | `PartyReferenceDataDirectoryEntryId` | Registered, not the entry itself, and a **Person** entry. One officer, because BIAN gives a single object |
  | `CompanyOfficerRole` | `CompanyOfficerRole` (Max35Text) | Plain string, trimmed, not blank, at most 35 characters (for example `Director`). It is recorded only together with an officer |

  - **A new record, `PartyCorporateReferences`** (corporate customer, legal entity, officer and role), is added to `PartyReferenceDetails` as a nullable `[JsonIgnore]`-computed member with a `CurrentCorporateReferences` accessor, as in 1.18 to 1.20. Events and view documents from 1.10 to 1.20 still load and fold.
  - The corporate customer and the legal entity may be the same entry.
- **Refused parts:** `InvolvementReference`, at any of the three references, is refused with `party.reference-field-not-supported` and no phase. The role is carried by `CompanyOfficerRole`, as ADR 0033 did for `AssociateType`. A `CompanyOfficerRole` sent as an object (BIAN's `Party` shape) is refused the same way.
- **Party-type rules (chosen in the interview):**
  1. The four fields are recorded only on an **Organisation** entry. Recording one on a Person entry is `party.corporate-references-need-organisation` (422). This rule is checked in `Decide` on the resulting Reference.
  2. The corporate customer and legal entity references point at Organisation entries, and the officer at a Person entry. Otherwise the result is `party.referenced-party-type-mismatch` (422). A business-rule error carries no `errors` map (ADR 0015), so each 422 names the BIAN path at the start of its `detail`.
  3. **The entry's `Update` (1.8) refuses a change of party type to `Person` while any corporate reference is recorded**, with the same `party.corporate-references-need-organisation`. The client clears the references first. Without this, rule 1 could be broken from the other side.
- **Cross-aggregate checks (handler loads, `Decide` decides):**
  - a reference to the entry itself is `party.self-reference` (422);
  - a reference that is not a registered entry is `party.referenced-party-not-registered` (422), naming the path in its `detail`. An association id sent as an entry id loads no events, so it fails the same way and is never a cast error (ADR 0033);
  - only the references **given in this request** are loaded and checked. Kept ones were checked when they were recorded.
- **Partial update (as in 1.18 to 1.20):**
  - an omitted field keeps its value, a given one replaces it, and a blank `PartyReference` or role clears it;
  - a role with no resulting officer is `party.reference-invalid` naming `CompanyOfficerRole`. This is checked in `Decide` on the result, as for the PEP description in 1.20, so clearing the officer while a role is kept is refused unless the same request clears the role;
  - every accepted Update appends `PartyReferenceUpdated` with the full resulting Reference.
- **Malformed input:** an id that is not a Guid v7, or a role that is blank or too long, goes into `party.reference-invalid` (400), and `errors` names each path. The unsupported-field check still runs first, then the input reading, then the handler loads, then `Decide`.
- **Staged table:** the four fields leave `PartyReferenceFields.Staged`, which becomes empty. The `PartyReferenceChangesTests` pin goes from 4 to 0.
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`):**
  - the Associations `InvolvedPartyModel` is reused (`PartyReference`, with its other parts, such as `InvolvementReference`, reported as unsupported). `CompanyOfficerRole` is read as raw JSON, so an object is refused part by part;
  - the Update request and the shared Update/Retrieve response gain the four fields, and empty values are left out of the response;
  - the new error codes are mapped through `RimBank.Problems` (`Error.BusinessRule` → 422).
- **Demo:** requests in `src/RimBank.Host/RimBank.Host.http` that register an Organisation and a Person, link them through the Reference Update, and show one 422.
- **Docs:**
  - new ADR 0034;
  - ADR 0029 amended (the staged table is empty);
  - ADR 0024 amended (the Reference paragraph, and the entry Update rule);
  - ADR 0032 amended (a Consequences note that 1.21 is done);
  - tech-stack §3.4 updated (the `Reference/` tree).
- **Roadmap:** the 1.21 row is ticked and reworded to name the party-type rules.

### Out of scope
- **More than one officer, or officer history.** BIAN gives a single object, so one officer is recorded, as with the single document in 1.20. No phase is planned.
- **Keeping the rule in step when a *referenced* entry later changes its party type.** That would need a cross-stream index on the write side (ADR 0033 refused one for uniqueness). The rule holds when a reference is recorded. Later drift is documented in ADR 0034 as accepted staleness.
- **Deriving Associations from these references, or the reverse.** The two stay independent records. A client that wants both registers both.
- **A role code list.** BIAN gives none, so the role is free text. No phase is planned.
- **MCP tools (1.17), per-operation scopes (1.15), the Reference channels (after 7.2), and masking and encryption at rest (7.2).**
- **Checks not chosen in the interview:** a dedicated Testcontainers round-trip beyond the minimum that the Definition of done requires (see validation.md). The FsCheck partial-update law *was* extended in the end: its coverage test fails when a recorded field is missing from the law.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Shape of a party reference | A checked directory entry id (`PartyReferenceDataDirectoryEntryId`) | Same guarantee as the Associations associate. ADR 0033 refused two kinds of party link with different guarantees |
| Party-type rules | The fields go only on Organisation entries. The corporate and legal entity references point at Organisations, and the officer at a Person | Chosen in the interview. BIAN describes all four as "for company customers". This is the first party-type rule in the Reference BQ, and ADR 0034 records why it departs from 1.18 and 1.20: those fields are the party's own, while these are links whose meaning depends on the party types |
| Entry Update guard | The entry `Update` refuses a change to `Person` while corporate references are recorded | Keeps rule 1 true on the write side. An invariant must not depend on the order in which commands arrive |
| Drift of the referenced entry | Not guarded, and documented | Guarding it would need a cross-stream index, which costs more than the harm (same reasoning as ADR 0033 on uniqueness) |
| Number of officers | One | BIAN gives a single object. Same choice as the document in 1.20 |
| `CompanyOfficerRole` | A plain Max35Text string, needing an officer | BIAN's `Party` schema has no role property, so the field is read as the role text it describes (mission "Source of truth" 3) |
| `InvolvementReference` | Refused, no phase | The role is carried by `CompanyOfficerRole`. Same reasoning as ADR 0033 |
| Which references are loaded | Only those given in the request | Kept ones were checked when they were recorded, and this keeps the handler to at most three loads |
| Error codes | New: `party.self-reference`, `party.referenced-party-not-registered`, `party.referenced-party-type-mismatch`, `party.corporate-references-need-organisation` (all 422 BusinessRule). Reused: `party.reference-invalid`, `party.reference-field-not-supported` | Business rules are 422, malformed input is 400, as in ADR 0033. The Associations codes are not reused, so each error names what failed |
| Event | `PartyReferenceUpdated`, with the same name and a larger `Details` | The new member is nullable, so earlier events still fold |
| Work order | Inside-out, test first: value objects → changes, decision, handler loads, entry Update guard, projection and HTTP (one commit) → PostgreSQL / rebuild → demo, log check and docs | Chosen in the interview. Same order as 1.19 and 1.20, so no commit drops fields silently |

## ADRs to write
- **ADR 0034 (new): Reference BQ corporate and officer references.** It records:
  - the checked entry ids, and why opaque text and "retire to Associations" were rejected;
  - the three party-type rules, and why the Reference BQ now has one when 1.18 and 1.20 had none;
  - the entry Update guard;
  - the accepted drift when a referenced entry changes its party type;
  - one officer, the role as Max35Text text, and `InvolvementReference` refused;
  - the four new error codes.

  Options considered: checked ids without party-type rules; retiring the fields to Associations; opaque references.
- **Amend ADR 0029:** a "Recorded (1.21)" table, an empty staged table, and the pin count.
- **Amend ADR 0024:** the Reference paragraph, and the new rule in the entry Update.
- **Amend ADR 0032:** a Consequences note that the 1.21 split is done.
- **Tech-stack §3.4:** the `Reference/` tree gains `PartyCorporateReferences.cs` and `CompanyOfficerRole.cs`.

## Open questions
- None.
