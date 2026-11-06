# Party Reference Data Directory: BIAN → DDD mapping

Roadmap phase: 1.1
Branch: plan/1.1-party-bian-ddd-mapping

## Context
Stage 0 is done. Stage 1 builds the first real context, `RimBank.Party.PartyReferenceDataDirectory`, and phases 1.2–1.17 each take one piece of it. Before 1.2 writes the first aggregate, this phase records in one ADR how the BIAN spec becomes a DDD model: aggregate boundaries, Behavior Qualifiers, the command/query split of every operation, and the read models with their projection lifecycle. The source of truth is BIAN ([mission](../mission.md#source-of-truth) §1, §3). Both the "BQ as its own aggregate" choice ([tech-stack §7](../tech-stack.md#7-domain-modelling-conventions)) and any BIAN interpretation need an ADR.

Inputs:
- [PartyReferenceDataDirectory.yaml (OpenAPI)](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml): 17 operations. The Control Record (CR) has Register, Control, Update, Retrieve, Request, Execute and Exchange. The BQs have Demographics Update/Exchange/Retrieve, Reference Update/Retrieve, Associations Register/Update/Retrieve and BankRelations Update/Retrieve.
- [PartyReferenceDataDirectory.yaml (AsyncAPI)](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/asyncapi-3.x/yamls/PartyReferenceDataDirectory.yaml): 10 channels, `<CR or BQ>/Created` and `<CR or BQ>/Updated`.
- Already in place: the decider and projection contracts (ADR 0010), the CQRS role markers (ADR 0012), per-context stores with `AddInlineView` (ADR 0016), Problem Details codes `<context>.<error-name>` (ADR 0006, 0015), MCP tool naming (ADR 0021), the one core project (ADR 0023) and the conformance harness (ADR 0020).

This phase unblocks 1.2–1.17, and 1.11 in particular, which changes shape because of the Associations decision.

**CQRS split:** this phase ships no runtime code. The ADR and the catalogue test classify every BIAN operation: the 6 `Retrieve` operations are queries and the other 11 are commands (mission principle 5).

## Scope
### In scope
- **ADR 0024 "Party Reference Data Directory: BIAN → DDD mapping"** decides:
  - **Two aggregates.** `PartyReferenceDataDirectoryEntry` (the CR, aggregate root) holds Demographics, Reference and BankRelations as entities. **Associations is its own aggregate**, `PartyAssociation`, with one stream per association. The ADR justifies this under tech-stack §7: an association has its own lifecycle (a validity period, its own Register operation and its own `{associationsid}`). A party can have many associations, which would make the entry stream a concurrency hotspot. And an association links two entries, so it should not live in only one of their streams.
  - **Identity.** `PartyReferenceDataDirectoryEntryId` and `PartyAssociationId` are Guid v7 typed IDs (ADR 0008), each the stream id of its aggregate. The singleton BQs (Demographics, Reference, BankRelations) have no Register operation in BIAN, so each has exactly one instance per entry. The handler generates their BQ ids at entry Register and passes them into the decision, which returns them in the Register acknowledgement. They are the `{demographicsid}`, `{referenceid}` and `{bankrelationsid}` route values, and an unknown BQ id is a 404.
  - **Operation catalogue.** All 17 operations get a row: command or query, the command or query name, the target phase (1.2–1.12) or *deferred*, and the MCP tool name (ADR 0021). The CR Control, Request, Execute and Exchange operations are **deferred** (not in Stage 1; no speculative coverage).
  - **Channel catalogue.** All 10 channels get a row. `PartyReferenceDataDirectoryEntry/Created` and `/Updated` go to 1.13. The 8 BQ channels are **deferred** until a consumer needs them.
  - **Domain events**, named in the past tense. For the entry: `PartyRegistered`, `PartyReferenceDataDirectoryEntryUpdated`, `DemographicsUpdated`, `DemographicsExchanged`, `PartyReferenceUpdated`, `BankRelationsUpdated`. For the association: `PartyAssociationRegistered`, `PartyAssociationUpdated`. The ADR also maps them to the channels listed above (the mapping is implemented in 1.13).
  - **Cross-aggregate rule.** Registering an association requires the owning entry and the associate entry to exist. This is checked by loading their **write-side** streams through the `PartyDirectory` write port (tech-stack §8.1), never a view. The associate must be an entry in this directory.
  - **Read models.** Both views are **inline**, single-stream projections (tech-stack §8.3):
    - `PartyReferenceDataDirectoryEntryView` (entry stream) serves the CR Retrieve and the Demographics, Reference and BankRelations Retrieve queries. Each BQ query returns its section of the view.
    - `PartyAssociationView` (association stream) serves Associations/Retrieve. A path whose entry id does not match the association's owner is a 404.
    - Stage 1 has **no async views**. "List a party's associations" is not a BIAN operation, so it is out of scope.
  - **Concurrency.** The `ETag` / `If-Match` value is the stream version of each aggregate (tech-stack §5). BQ updates on the entry share its version.
  - **PII.** Demographics `EthnicityReligion` is **not modelled**: it is special-category data, and keeping none is the safest option until 7.2 PII protection. The other Demographics fields are PII and are flagged for 7.2.
  - **Error codes** with their Problem Details kind: `party.party-not-found`, `party.association-not-found`, `party.behavior-qualifier-not-found`, `party.associate-not-registered`, plus the shared concurrency conflict.
- **Catalogue test** in `RimBank.Conformance.Tests`. A C# table of the ADR's operation and channel catalogue that must match the real YAML exactly: no missing row, no extra row, and the kind of each row equal to the spec's (Retrieve ⇔ query). It pins the ADR and is extended by later phases.
- **Docs:** the tech-stack §3.4 tree (an `Associations/` folder holding the `PartyAssociation` aggregate), the roadmap 1.11 row ("own aggregate, ADR 0024"), and a roadmap note that the CR Control, Request, Execute and Exchange operations and the BQ channels are deferred.

### Out of scope
- Any code in `src/`. The aggregate is 1.2, the flows 1.3–1.12, the published language 1.13, route and channel conformance 1.14, authorisation scopes 1.15 and MCP 1.16–1.17.
- The detailed field-level design of each BQ (value objects, validation rules) and the semantics of Demographics/Exchange (accept, verify, reject). Each comes with its BQ phase (1.9–1.12), within this ADR's boundaries.
- CR Control (terminate), Request, Execute and Exchange: deferred, not scheduled.
- PII masking and encryption at rest (7.2).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Aggregate boundaries | Entry aggregate with 3 BQ entities, plus a separate `PartyAssociation` aggregate | Chosen in the planning interview. Associations have their own lifecycle, are many per party and link two entries (tech-stack §7 exception, justified in the ADR) |
| Singleton BQ ids | Generated at entry Register and returned in the acknowledgement | BIAN routes need `{bqid}` but have no BQ Register; keeps BQ updates on the entry stream |
| Associate reference | Must be a registered entry, checked on the write side | Money-free invariant, but still not checked against a view (mission principle 5) |
| Read models | Two inline single-stream views; BQ Retrieves read sections of the entry view | Read-your-write Retrieve (tech-stack §8.3); nothing needs async yet |
| Deferred operations | CR Control/Request/Execute/Exchange and the 8 BQ channels | Not on the Stage 1 roadmap; no speculative coverage (mission non-goals) |
| EthnicityReligion | Not modelled | Special-category PII; data minimisation until 7.2 |
| Phase output | ADR + docs + catalogue test, no production code | Chosen in the planning interview |
| Catalogue location | `RimBank.Conformance.Tests`, as a C# table | Next to the harness and the real YAML loaders (ADR 0020); the ADR markdown is not parsed |

## ADRs to write
- **0024 Party Reference Data Directory: BIAN → DDD mapping**

## Open questions
- None. The detailed field design is left to each BQ phase on purpose.
