# Party Reference Data Directory: BankRelations BQ

Roadmap phase: 1.12
Branch: plan/1.12-party-bank-relations-bq

## Context
BankRelations is the last Behavior Qualifier of Party Reference Data Directory still missing. [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md) made it a **singleton entity of the entry aggregate**, like Demographics and Reference: its `BankRelationsId` is created at Register (1.2–1.5), its section of the entry is empty until the first Update, its event is `BankRelationsUpdated`, and its Retrieve reads the entry's inline view. The catalogue already names the command `UpdateBankRelations` and the query `RetrieveBankRelations` for 1.12.

The [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) gives `BankRelations/Update` (`PUT …/{partyreferencedatadirectoryid}/BankRelations/{bankrelationsid}/Update`) and `BankRelations/Retrieve` (`GET …/Retrieve`), with the same body (`UpdateBankRelations`, `RetrieveBankRelations`):
- `BankRelationType`: a string, "the type of relationship (e.g. relationship manager)", no enum;
- `BusinessUnitEmployeeReference` (`Involvedparty`: `PartyReference`, `InvolvementReference`): "the associated employee or unit";
- `BankRelationReference` (`PartyRelationship`): `RelationshipType`, `RelationshipValidityPeriod`, `RelationshipStartDate`, `RelationshipEndDate` (ISO `Datetime` objects with `DateTimeContent`), `RelationshipLifecycleStatus` (ISO `Status`), `RelationshipIdentification`.

**Chosen in the interview:** the core slice with a checked reference. 1.12 records the relation type as a closed enum, the employee or unit as a **checked directory entry** (bank staff and business units are parties too, as ADR 0033 and ADR 0034 decided for party links), and the relationship's start date, end date and lifecycle status. Every other field is refused.

It depends on 1.2–1.8 and on the checked-reference pattern of 1.11 and 1.21 (ADR 0033, ADR 0034), all merged. It unblocks the BankRelations MCP tools (**1.17**) and completes the BQs of Stage 1, so 1.13 (published language) can follow.

Drivers:
- Mission principles 3 (enums and value objects, no primitive obsession), 4 (event-sourced truth), 5 (the command acknowledges from the decided state; the reference check reads the **write side**), 8 (Problem Details), 10 (small step: one BQ, a core slice of its fields).
- Tech-stack §3.4 (`BankRelations/` folder, flat namespace, ADR 0026), §5 (routes, `Idempotency-Key`, `If-Match`, `ETag`), §7 (BQ as an entity, the default), §8.1–8.3 (inline view, rebuildable).

**CQRS split:**
- **Command side:** `UpdateBankRelations`. The handler loads the entry stream through `IPartyDirectory`; when the request **sets** an employee or unit reference, it also loads that entry stream (never a view). A pure `Decide` checks the facts and appends `BankRelationsUpdated` with the expected version. It acknowledges from the decided state.
- **Query side:** `RetrieveBankRelations` reads the BankRelations section of the **inline** `PartyReferenceDataDirectoryEntryView` through `IPartyDirectoryEnquiry`, after checking the BQ id. No new view, no async projection.

## Scope
### In scope
- **`BankRelationType` (enum) + `BankRelationTypes` (BIAN-style spelling, read in any case, after `PartyTypes`):**
  - `RelationshipManager`, `AccountOfficer` (staff roles: the reference must be a **Person**);
  - `ServicingBranch`, `ServicingBusinessUnit` (unit roles: the reference must be an **Organisation**).
  - BIAN gives no list; ADR 0035 records this one and how to extend it.
- **`BankRelations` entity (core `BankRelations/`):** a nullable section of the entry state and view, so events and views stored before 1.12 still load and rebuild:
  - `Type` (`BankRelationType`);
  - `BusinessUnitEmployee` (`PartyReferenceDataDirectoryEntryId`);
  - `Relationship`: `StartDate` and `EndDate` (UTC `DateTimeOffset`, read from `DateTimeContent` as ISO 8601; a date alone or a value without an offset is UTC), `LifecycleStatus` (`BankRelationLifecycleStatus`: `Active`, `Suspended`, `Ended`, read from `RelationshipLifecycleStatus.StatusDescription`).
- **Rules (checked on the resulting BankRelations, in this order):**
  1. request values: unknown or refused field → 400 `party.bank-relations-field-not-supported`; bad type, status, date or id → 400 `party.bank-relations-invalid` (`errors` names each BIAN path); `StartDate` ≤ `EndDate`; a reference without a type is `party.bank-relations-invalid` naming `BankRelationType`;
  2. the reference is the entry itself → 422 `party.self-reference`;
  3. the reference is not a registered entry → 422 `party.referenced-party-not-registered`;
  4. the reference's party type does not fit the relation type → 422 `party.referenced-party-type-mismatch`.
  - Codes 2–4 are reused from ADR 0034: they already mean "a reference in this directory". As there, the 422 `detail` starts with the BIAN path.
  - A kept reference is not loaded again; a type change re-checks the kept reference against the new type (one entry load).
  - **Accepted drift:** the referenced entry may later change its own `PartyType`; it is not re-checked (no cross-stream index, as ADR 0034).
  - No check against today, and no "active" flag derived from the dates (a view must not depend on the clock, tech-stack §8.2). `Ended` does not require an `EndDate`.
- **Update (`PUT …/BankRelations/{bankrelationsid}/Update`):**
  - partial update as in 1.9–1.21: an omitted field keeps its value, a given one replaces it, a blank value or empty object clears it;
  - checks in order: route ids (400 `shared.invalid-identifier`) → body (400) → entry exists (404 `party.party-not-found`) → BQ id belongs to the entry (404 `party.behavior-qualifier-not-found`) → `If-Match` (409 `party.version-conflict`) → rules 2–4 (422);
  - requires `Idempotency-Key` and `If-Match` (428 / 400 / 409, ADR 0027);
  - every accepted Update appends `BankRelationsUpdated` with the full resulting BankRelations, even when nothing changes; answers `200` with the new entry version as `ETag`.
- **Retrieve (`GET …/BankRelations/{bankrelationsid}/Retrieve`):** from the entry view, with the entry version as `ETag` and `304` on a matching `If-None-Match`; unknown entry 404 `party.party-not-found`, foreign BQ id 404 `party.behavior-qualifier-not-found`.
- **Response (Update and Retrieve, `200`):** `BankRelationsId`, `PartyReferenceDataDirectoryEntryId`, `BankRelationType`, `BusinessUnitEmployeeReference { "PartyReference" }`, `BankRelationReference { RelationshipStartDate { DateTimeContent }, RelationshipEndDate { DateTimeContent }, RelationshipLifecycleStatus { StatusDescription } }`; unknown values are left out. Update builds it from its acknowledgement.
- **Refused (400 `party.bank-relations-field-not-supported`, no phase):** `BusinessUnitEmployeeReference.InvolvementReference` (the role is `BankRelationType`), `BankRelationReference.RelationshipType`, `RelationshipValidityPeriod` (duplicates start/end), `RelationshipIdentification`, the other `Datetime` parts (`TimeZoneCode`, `DaylightSavingIndicator`, `DateTimeType`) and the other `Status` parts (`StatusReason`, `StatusDateTime`, `ValidityTime`). Checked before the value checks, as for Reference.
- **PostgreSQL:** stable stored event name `party_bank_relations_updated`; the existing inline entry view folds it.
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/BankRelations/`):** two endpoints, models and route values, mapped in `PartyReferenceDataDirectoryApi`; reuses the Associations `InvolvedPartyModel`.
- **Demo** block in `src/RimBank.Host/RimBank.Host.http`.
- **Docs:** ADR 0035 (new), ADR 0024 amended, tech-stack §3.4, roadmap 1.12 ticked.

### Out of scope
- Several bank relations per party (for example two relationship managers): BIAN's body is one object and ADR 0024 makes the BQ a singleton. A later phase would need an ADR (likely its own aggregate, as Associations).
- The refused fields above: no phase planned.
- A bank-side employee or organisation-unit context (HR, branch network): not in the roadmap; staff and units are recorded as directory entries.
- The `BankRelations/Created` and `BankRelations/Updated` channels (deferred, ADR 0024), the MCP tools (**1.17**), per-operation scopes (**1.15**), and masking and encryption at rest (**7.2**).
- Listing a party's bank relations from the employee's side ("whose relationship manager is X"): not a BIAN operation; would need an async cross-stream view and an ADR.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Recorded fields | Type, employee/unit reference, start/end dates, lifecycle status; the rest refused | Chosen in the interview. The minimum that makes a bank relation meaningful (principle 10) |
| `BankRelationType` | Closed enum of 4 values, plain string, read in any case | BIAN gives free text with one example. An enum lets the party-type rule exist and avoids primitive obsession (principle 3). Extending it is an ADR amendment |
| Employee or unit reference | A registered entry id, checked on the write side, never the entry itself | Chosen in the interview. Same guarantee as every party link (ADR 0033, ADR 0034); one kind of link, one guarantee |
| Party-type rule | Staff roles → Person, unit roles → Organisation; the owning entry may be either type | Follows BIAN's "employee or unit". The owner's type does not matter, so no guard on the entry's own Update (unlike ADR 0034) |
| Error codes | Reuse `party.self-reference`, `party.referenced-party-not-registered`, `party.referenced-party-type-mismatch`; add `party.bank-relations-invalid` and `party.bank-relations-field-not-supported` | The 422 codes already mean "a reference in this directory" (ADR 0034). One 400 code per BQ contract, as for Reference and Associations |
| Lifecycle status | `Active` / `Suspended` / `Ended` from `StatusDescription`; no cross-rule with dates | ISO `Status` has no code list. Keeps the rules small; dates and status are what the party declares |
| Dates | `DateTimeContent` as ISO 8601, stored UTC, start ≤ end, no check against today | Same reading as the 1.20 document dates and 1.11 periods; projections must not read the clock (§8.2) |
| Update semantics | Partial: omitted keeps, given replaces, blank/empty clears | Same as 1.9–1.21 |
| Read model | BankRelations section in the existing inline entry view, nullable | ADR 0024; old events and views still fold and rebuild |
| Version and `ETag` | The entry's stream version | ADR 0024 "Concurrency" |
| Work order | Domain-out, test first, one PR, one commit per group, from `main` | Chosen in the interview. 1.21 is merged (#127), so there is nothing to rebase on |

## ADRs to write
- **ADR 0035 (new): BankRelations: what is recorded.** The recorded and refused fields, the 4 relation types and their party-type rule, the lifecycle status values, the reused 422 codes and the two new 400 codes, the accepted drift, and why one relation per party. Options considered: free-text type with an unchecked reference; enum type with an unchecked reference; enum type with a checked reference (chosen).
- **Amend ADR 0024:** a "BankRelations requests (1.12)" paragraph (routes, check order, headers, response), the events table (`BankRelationsUpdated` final shape) and the error codes table (the new codes, and 1.12 added to the reused ones).
- **Tech-stack §3.4:** the `BankRelations/` tree with the real files, and `BankRelations/` in the `RimBank.Api` folder list.

## Open questions
- None. (The 4 relation types are a proposal; adjust in review of ADR 0035 if you want other values.)
