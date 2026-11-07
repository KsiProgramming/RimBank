# Party Reference Data Directory: Demographics BQ

Roadmap phase: 1.9
Branch: plan/1.9-party-demographics-bq

## Context
1.2–1.8 built the Control Record end to end: Register, Retrieve and Update, with `If-Match` optimistic concurrency (ADR 0027). Since 1.2, every entry has had a `DemographicsId`, but its Demographics section is still empty. This phase gives the first Behavior Qualifier real behaviour: BIAN `Demographics/Update`, `Demographics/Exchange` and `Demographics/Retrieve`. ADR 0024 models it as an **entity inside the entry aggregate**, so it uses the entry's stream, version and inline view.

It reuses 1.8's `AppendToEntry`, `StreamVersionTag.ReadIfMatch`/`SetOn` and the retry on a lost race, and adds no new port. It sets the pattern for the other singleton BQs, Reference (1.10) and BankRelations (1.12). It also unblocks the Demographics MCP tools (1.17).

Drivers:
- Mission principles 3 (pure decisions, value objects), 4 (event-sourced truth), 5 (commands acknowledge from the decided state, queries read views), 8 (Problem Details) and 9 (idempotency).
- Mission quality bar "Security": the Demographics fields are PII.
- Tech-stack §3.4 (`Demographics/` folder, flat namespace, ADR 0026), §5 (routes, `If-Match`, `ETag`, `Idempotency-Key`), §7 (BQ as an entity, `TimeProvider`), §8.1–8.3 (inline view).
- [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md): the commands `UpdateDemographics`, `ExchangeDemographics` and `RetrieveDemographics`, the events `DemographicsUpdated` and `DemographicsExchanged`, and `party.behavior-qualifier-not-found`. Its Personal data section says `EthnicityReligion` is never stored. It leaves "Exchange semantics decided in 1.9".

BIAN source: [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml).
- `PUT …/{partyreferencedatadirectoryid}/Demographics/{demographicsid}/Update` and `…/Exchange`, and `GET …/Retrieve`.
- The `UpdateDemographics`, `ExchangeDemographics` and `RetrieveDemographics` schemas all have the same fields:
  - `Socio-EconomicClassification`;
  - `EthnicityReligion`;
  - `Employment` (`Involvedparty`: `PartyReference`, `InvolvementReference`);
  - `EmploymentHistory`, `EducationHistory` and `ServicingConstraints`.
- Exchange is described only as "Accept, verify, reject etc." and has no action field.

**CQRS split:**
- **Command side:** `UpdateDemographics` and `ExchangeDemographics`. Each loads the entry, checks the version and the BQ id, calls a pure `Decide` on the entry, appends to the entry stream with the expected version, and acknowledges from the decided state.
- **Query side:** `RetrieveDemographics` reads the existing **inline** `PartyReferenceDataDirectoryEntryView`, which gains a Demographics section, through `IPartyDirectoryEnquiry`, after checking the BQ id. There is no new view and no async projection.

## Scope
### In scope
- **Demographics model (core, `Demographics/` folder).** These value objects live in the entry state:
  - `PartyDemographics`: `SocioEconomicClassification`, `Employment`, `EmploymentHistory`, `EducationHistory` and `ServicingConstraints`;
  - `Employment`: `PartyReference` and `InvolvementReference`, both free text; the employer is not checked against the directory;
  - `DemographicsVerificationStatus`: `Unverified` | `Verified` | `Rejected`, plus a `VerificationDate`.
  - Text is trimmed and length-checked in the core: Max140Text for the classification and the `Employment` fields, Max2048Text for the histories and constraints.
  - `EthnicityReligion` is not modelled. It is accepted in requests, ignored and never returned (ADR 0024).
- **Update (chosen in the interview): a partial update.**
  - An omitted field keeps its value, and `""` clears it. A given `Employment` object replaces the whole employment.
  - Every accepted Update appends `DemographicsUpdated`, which carries the **full resulting details** and the resulting status.
  - The status goes back to `Unverified` only when the details actually change. An Update that changes nothing keeps the status.
- **Exchange (chosen in the interview): verification of the current details.**
  - The request body has a RimBank extension field, `ExchangeActionType`: `Verify` | `Reject`, required.
  - Exchange appends `DemographicsExchanged` with the outcome status and the `VerificationDate`, taken from `TimeProvider`.
  - The BIAN detail fields in the body are accepted and ignored.
  - Exchanging details that were never recorded (all empty) is `party.demographics-not-recorded` (422).
  - Every Update that changes the details resets the status to `Unverified`.
- **Handlers.** Every command and query on the BQ checks, in this order:
  1. the entry exists, or `party.party-not-found`;
  2. the path's `demographicsid` is the entry's `DemographicsId`, or `party.behavior-qualifier-not-found`;
  3. commands only: the expected version is the current one, or `party.version-conflict`;
  4. then the decision.
- **Projection.** The entry view gains a nullable `Demographics` section (details, status and verification date). It is empty, or `null` in documents from before 1.9, until the first Update.
- **HTTP (`RimBank.Api/Party/PartyReferenceDataDirectory/Demographics/`):**
  - Update and Exchange (`PUT`) require `Idempotency-Key` and `If-Match`. They answer 200 with the BIAN fields, `VerificationStatus`, `VerificationDate`, the entry and Demographics ids, and the new `ETag`, all built from the acknowledgement.
  - Retrieve (`GET`) answers 200 from the view with the same shape and the entry's `ETag`. An `If-None-Match` that names the current version is 304, as for the entry Retrieve.
  - A malformed `demographicsid` is 400 `shared.invalid-identifier` with `errors["demographicsid"]`.
- **Conformance:** the three operations are added to `_implemented`.
- **Docs:** the new ADR 0028, ADR 0024 amendments, and the tech-stack §3.4 tree.

### Out of scope
- The `Demographics/Created` and `/Updated` integration channels: deferred by ADR 0024, until a consumer needs them and PII protection lands (7.2).
- The MCP tools (`…_update_demographics`, `…_exchange_demographics`, `…_retrieve_demographics`) and their parity tests: **1.17**.
- Per-operation scopes, for example a separate scope for Exchange (verification): **1.15**.
- PII masking in logs and encryption at rest: **7.2**. This phase only makes sure no demographics values are logged by new code.
- Checking `Employment.PartyReference` against the directory, verification evidence or documents, and an `Accept` action that differs from `Verify`: not chosen.
- Full request and response shape conformance: **1.14**.
- A captured-log PII test and `.http` demo requests: not chosen in the interview.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Exchange semantics | Verify or Reject the current details through an `ExchangeActionType` extension field; the BIAN detail fields are ignored | Chosen in the interview. It gives "Accept, verify, reject" a real business meaning (KYC-style verification) that Party Lifecycle Management can build on. It extends BIAN's body, so it needs ADR 0028 (mission, Source of truth 3) |
| Update semantics | Partial: omitted keeps, `""` clears, `Employment` is replaced as a whole | Chosen in the interview. Consistent with 1.8's optional `PartyType` |
| Status reset | Only an Update that changes the details sets the status back to `Unverified` | Verified data stays verified after a no-op Update, and changed data is never shown as verified |
| No-op Update | Still appends `DemographicsUpdated` | As in 1.8, an accepted Update is a business fact. The version moves, so `If-Match` stays simple |
| Event payload | `DemographicsUpdated(EntryId, DemographicsId, Details, Status)` carries the full resulting details; `DemographicsExchanged(EntryId, DemographicsId, Status, VerificationDate)` | Event-carried state, ready for the deferred BQ channels (tech-stack §8.4) |
| Field limits | Trimmed text; Max140Text for the classification and `Employment`; Max2048Text for the histories and constraints; any field too long → `party.demographics-invalid` (400) with per-field `errors` | ISO 20022 text types; the validation lives in the core (memory: business logic in core) |
| BQ model | Value objects in the entry state, with decisions added to the entry's `Decide` | ADR 0024: an entity of the entry, one stream, one version |
| Retrieve query | `RetrieveDemographics : IQuery<PartyReferenceDataDirectoryEntryView>`; the endpoint maps the section | ADR 0024: "each returns its BQ section, after checking the BQ id". A view is only what an `IProjection` returns (ADR 0012) |
| Retrieve before the first Update | 200 with ids, `VerificationStatus: Unverified` and no detail fields | ADR 0024: "until a BQ's first Update, its section of the entry is empty". The BQ exists from Register |
| Concurrency | The entry's stream version is the `ETag` and `If-Match` for all three operations | ADR 0024 Concurrency; ADR 0027 |
| New error codes | `party.demographics-invalid` (400); `party.exchange-action-type-required` and `party.exchange-action-type-invalid` (400); `party.demographics-not-recorded` (BusinessRule, 422); `shared.invalid-identifier` on `demographicsid` | Stable codes, documented in ADR 0024 |
| Folders | `Demographics/` in `RimBank.Core` and `RimBank.Api`, flat namespaces | Tech-stack §3.4, ADR 0026, ADR 0025 |
| Existing view documents | No migration. The section is nullable, and `null` reads as empty and `Unverified` | The shape only grows by an optional field. Rebuild is still proven |
| Work order | Inside-out, test first: core → Postgres → HTTP → docs | Chosen in the interview |

## ADRs to write
- **ADR 0028 (new): Demographics Exchange as verification.**
  - The `ExchangeActionType` extension (`Verify` | `Reject`).
  - The verification status lifecycle and its reset on a changed Update.
  - The ignored body fields, and the not-recorded rule.
  - Options considered: accepting external details as verified, or deferring Exchange.
- **Amend ADR 0024:**
  - a "Demographics requests (1.9)" paragraph covering Update merge semantics, Exchange (pointing to 0028), Retrieve, the responses and the BQ id checks;
  - the confirmed event names and payloads;
  - the new error codes;
  - replace "Its exact semantics are decided in 1.9" with a link to 0028.
- **Tech-stack §3.4 tree:** the `Demographics/` contents.

## Open questions
- None.
