# Validation: Party Reference Data Directory: BankRelations BQ

## Done when (from roadmap)
> **1.12 BankRelations BQ:** the party's relationships with the bank; Update command and Retrieve query.
> + Definition of done (roadmap.md)

Concretely: `BankRelations/Update` and `BankRelations/Retrieve` work over HTTP for the fields in requirements.md, every refused field and broken rule answers Problem Details, and the BankRelations section folds into the inline entry view and rebuilds from the event store.

## Automated checks
- [ ] **Core (Shouldly, FsCheck)** in `RimBank.Core.Tests`:
  - value tests: `BankRelationTypesTests` (4 values, any case, required party type), `BankRelationLifecycleStatusesTests`, `BankRelationPeriodTests` (ISO 8601, date alone as UTC, start ≤ end), `BankRelationsChangesTests` (refused paths first, every invalid path named, reference without type);
  - `BankRelationsDecisionTests`: accepted set/keep/clear; `party.behavior-qualifier-not-found`, `party.version-conflict`, `party.self-reference`, `party.referenced-party-not-registered`, `party.referenced-party-type-mismatch` (staff role → Organisation, unit role → Person, type change against a kept reference);
  - `BankRelationsPartialUpdateProperties`: omitted fields keep their value; an empty Update keeps the state but still appends;
  - projection test (Given events → Then view), including a history without any `BankRelationsUpdated` (section stays empty);
  - handler tests for Update and Retrieve; the referenced entry is loaded through `IPartyDirectory` only, and only when set or re-checked.
- [ ] **PostgreSQL (Testcontainers)** `BankRelationsRoundTripTests`: inline read-your-write, stale version 409, idempotent replay appends once, `RebuildViewAsync` gives the same view.
- [ ] **HTTP (Alba)** `UpdateBankRelationsEndpointTests` / `RetrieveBankRelationsEndpointTests`:
  - `200` with the response shape and `ETag`; `304` on matching `If-None-Match`;
  - `400` `shared.invalid-identifier`, `party.bank-relations-invalid`, `party.bank-relations-field-not-supported` (each refused path in `errors`);
  - `404` `party.party-not-found`, `party.behavior-qualifier-not-found`;
  - `409` stale `If-Match`; `428` missing `If-Match`; missing `Idempotency-Key`;
  - `422` self, unregistered (including an association id), type mismatch, with the BIAN path at the start of `detail`;
  - every error is `application/problem+json`.
- [ ] **Log privacy** `BankRelationsLogPrivacyTests`: no submitted BankRelations value (reference id, dates, status) appears in logs on success, refusal, conflict or retrieve.
- [ ] **Conformance**: `PartyReferenceDataDirectoryConformanceTests` lists `BankRelations/Update` (PUT, command) and `BankRelations/Retrieve` (GET, query) and is green.
- [ ] **Architecture tests** green (no read port in the command handler, pure projection, flat namespaces, no `double`/`float`).
- [ ] `dotnet format --verify-no-changes`, full `dotnet test`, CodeQL: green in GitHub Actions.

## Manual / demo checks
- [ ] `dotnet run --project src/RimBank.AppHost`, then run the "BankRelations BQ (roadmap 1.12, ADR 0035)" block of `src/RimBank.Host/RimBank.Host.http`:
  - Update with `BankRelationType: RelationshipManager` pointing at a Person → `200`, `ETag` incremented;
  - Retrieve → `200` with the same values; repeat with `If-None-Match` → `304`;
  - `ServicingBranch` pointing at the Person → `422` `party.referenced-party-type-mismatch`, Problem Details;
  - reference to the customer itself → `422` `party.self-reference`;
  - `RelationshipIdentification` in the body → `400` `party.bank-relations-field-not-supported` naming the path.
- [ ] Paste the demo output in the PR description.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.12 ticked in the same PR
- [ ] ADR 0035 written; ADR 0024 and tech-stack §3.4 amended
- [ ] No BankRelations value (PII) in logs; the new fields listed for 7.2
- [ ] MCP tools not added (1.17), and the PR says so, so the HTTP/MCP parity gap is explicit
- [ ] PR opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<issue>`)
