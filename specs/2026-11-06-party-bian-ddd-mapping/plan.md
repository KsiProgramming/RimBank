# Plan: Party Reference Data Directory: BIAN → DDD mapping

Order chosen in the planning interview: ADR first, then the catalogue test that pins it, then the docs. The solution builds and every test passes at the end of each group. No production code.

## 1. ADR 0024
- [ ] `docs/adr/0024-party-reference-data-directory-mapping.md` from `template.md` (Status: Accepted, Roadmap phase: 1.1):
  - **Context:** BIAN inputs (the OpenAPI and AsyncAPI YAML: 17 operations, 10 channels, CR *PartyReferenceDataDirectoryEntry*, 4 BQs); mission §Source of truth; tech-stack §7 (BQ = entity by default) and §8.
  - **Options:** (1) one aggregate with all 4 BQs as entities; (2) the entry aggregate with 3 BQ entities plus a separate `PartyAssociation` aggregate; (3) every BQ as its own aggregate.
  - **Decision:** option 2, then these sections:
    - aggregates and identity (typed IDs, stream ids, singleton BQ ids generated at Register);
    - an **operation table** (operationId → command/query → command or query type name → phase or *deferred* → MCP tool name, for example `party_reference_data_directory_register_associations`);
    - a **channel table** (address → source domain event → phase or *deferred*);
    - the domain event list;
    - the cross-aggregate rule (associate and owner checked on the write side through `PartyDirectory`);
    - a **read model table** (view → stream → inline/async → queries served);
    - concurrency (stream version as `ETag`);
    - PII (`EthnicityReligion` not modelled; the other Demographics fields flagged for 7.2);
    - error codes and their `ErrorKind`.
  - **Consequences:** two write-side stream types in one store; `PartyDirectory` loads and appends both; 1.11 creates a new aggregate; the BQ channels and the deferred operations are not implemented until a later ADR or phase; the catalogue test enforces the tables.

## 2. Catalogue test
- [ ] `tests/RimBank.Conformance.Tests/Catalogues/PartyReferenceDataDirectoryCatalogue.cs` (namespace `RimBank.Conformance.Catalogues.Tests`): an immutable list of rows `(OperationId, BianOperationKind Kind, string Phase)` for the 17 operations, and `(Address, string Phase)` for the 10 channels, mirroring the ADR tables. `Phase` is `"1.x"` or `"deferred"`.
- [ ] `PartyReferenceDataDirectoryCatalogueTests.cs`:
  - `Every_specified_operation_is_catalogued_once`: the set of catalogued ids equals the set of `specification.Operations` ids, with no duplicates;
  - `Each_catalogued_kind_matches_the_specification`: for every row, `Kind == specification.Operation(id).Kind` (Retrieve ⇔ query);
  - `Every_specified_channel_is_catalogued_once`: the set of catalogued addresses equals the set of `specification.Channels` addresses;
  - `Phases_are_stage_one_or_deferred`: every `Phase` matches `^1\.\d+$` or `deferred`.
- [ ] A header comment points to ADR 0024 and says that a later phase changing the mapping updates the ADR and this table in the same PR.
- [ ] `dotnet test --project tests/RimBank.Conformance.Tests`.

## 3. Docs
- [ ] `specs/tech-stack.md` §3.4 tree: `Associations/` gets the `PartyAssociation` aggregate (`PartyAssociation.cs`, `PartyAssociationId.cs`, `Events.cs`, `Decisions.cs`, `PartyAssociationView.cs`, `PartyAssociationViewProjection.cs`), with a comment linking ADR 0024.
- [ ] `specs/roadmap.md`: the 1.11 row says "own aggregate `PartyAssociation` (ADR 0024)"; add a one-line note under the Stage 1 header that CR Control/Request/Execute/Exchange and the BQ channels are deferred (ADR 0024); tick 1.1.
- [ ] `CONTRIBUTING.md`, only if it lists ADRs or the layout per context (check; otherwise no change).
- [ ] `dotnet build -c Release`, `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes`.
