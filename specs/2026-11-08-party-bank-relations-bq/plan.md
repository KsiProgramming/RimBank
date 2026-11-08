# Plan: Party Reference Data Directory: BankRelations BQ

Every group is written test first. After each group the solution builds and its tests pass (one commit per group, one PR, branched from `main`).
- Test namespaces end with `.Tests`; context namespaces stay flat: every core type is in `RimBank.Party.PartyReferenceDataDirectory`, every HTTP type in `….Api` (tech-stack §3.3, ADR 0026).
- Switch expressions only (IDE0066).
- Business rules, validation errors and value parsing live in `RimBank.Core`; the adapters only translate the transport.
- Testcontainers and Alba tests need Docker; if it is not available locally, they run in CI.

## As built (differences from the plan below)
- **Commits.** One per group: domain (values, entity, decision); flows and view; PostgreSQL; HTTP and demo; docs.
- **No `BankRelationPeriod` value object.** The dates are two fields of `BankRelationsDetails`, because a partial update can change one bound only; "start ≤ end" is checked on the resulting details by the decision (`BankRelationsErrors.EndDateBeforeStartDate`).
- **Fewer files.** The raw input (`BankRelationsInput`, `BankRelationReferenceInput`) lives next to `BankRelationsChanges.Create`, and `BankRelationsFields` only names paths: no top-level BIAN field is refused, only parts of the three objects. The changes reuse `PartyReferenceChange<T>`.
- **Shared reading of dates.** `PartyReferenceDates.TryReadUtcInstant` reads a date or date-time as UTC, and `AssociationValidityPeriod` now uses it too.
- **The 422 errors stay in `PartyReferenceErrors`**, documented as shared with BankRelations, rather than moving to a new class.
- **The handler decides what to load** with `BankRelationsDetails.ReferenceToCheck(changes)`, which the decision also uses, and passes the loaded party type (`UpdateBankRelationsDetails.ReferencedPartyType`). No port member was added.
- **Tests.** `BankRelationTypesTests` also covers the lifecycle statuses; the projection cases are in `BankRelationsViewProjectionTests`; the HTTP helpers are in `BankRelationsScenarios`.
- **ADR 0035** is the new ADR (0034 was taken by 1.21).

## 1. Domain: BankRelations values, entity and decision
- [ ] `BankRelations/BankRelationType.cs` (enum, 4 values) and `BankRelationTypes.cs` (spelling, read in any case, and the required `PartyType` per value), after `PartyTypes` / `PartyRelationshipTypes`.
- [ ] `BankRelations/BankRelationLifecycleStatus.cs` (enum) and `BankRelationLifecycleStatuses.cs`.
- [ ] `BankRelations/BankRelationPeriod.cs`: optional `StartDate` / `EndDate`, ISO 8601 read as UTC, start ≤ end.
- [ ] `BankRelations/BankRelationsDetails.cs`: type, employee/unit `PartyReferenceDataDirectoryEntryId?`, period, status.
- [ ] `BankRelations/BankRelationsFields.cs`: recorded and refused BIAN paths (no phase).
- [ ] `BankRelations/BankRelationsChanges.cs` + `BankRelationsInput.cs`: raw adapter values → typed partial changes; refused fields first, then every value, gathered into one `party.bank-relations-invalid` naming each BIAN path; a reference without a resulting type is invalid.
- [ ] `BankRelations/BankRelationsErrors.cs`: `BankRelationsInvalid`, `BankRelationsFieldNotSupported` (both Validation). The 422 errors come from the ADR 0034 reference errors (move them to a shared place in the context if they live in `Reference/`).
- [ ] `BankRelations/Events.cs`: `BankRelationsUpdated(entryId, bankRelationsId, BankRelationsDetails)` deriving from the entry's event base.
- [ ] Entry aggregate: nullable `BankRelations` in `PartyReferenceDataDirectoryEntry`, `Evolve` for `BankRelationsUpdated`, and an `UpdateBankRelations` decision in `Decisions.cs`: BQ id, expected version, then self → not registered → type mismatch, using the referenced entry's facts passed in by the handler.
- [ ] Tests in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/BankRelations/`:
  - `BankRelationTypesTests`, `BankRelationLifecycleStatusesTests`, `BankRelationPeriodTests`, `BankRelationsChangesTests`;
  - `BankRelationsDecisionTests` (Given / When / Then) for every error code and the accepted paths (set, keep, clear, type change re-checks the kept reference);
  - FsCheck `BankRelationsPartialUpdateProperties` (omitted fields keep their value; an Update with no changes keeps the state) and arbitraries in `EntryExamples` / a `BankRelationsArbitraries`.

## 2. Command and query flows (core, in-memory ports)
- [ ] `BankRelations/UpdateBankRelations.cs` (`ICommand`, with `ExpectedVersion`), `UpdateBankRelationsAcknowledged.cs`, `UpdateBankRelationsHandler.cs`: load the entry; load the referenced entry only when set or when the type changes; decide; append with the expected version; acknowledge from the decided state.
- [ ] `PartyReferenceDataDirectoryEntryView` gains a nullable `BankRelations` section; `PartyReferenceDataDirectoryEntryViewProjection` folds `BankRelationsUpdated` (pure, no clock).
- [ ] `BankRelations/RetrieveBankRelations.cs` (`IQuery<…>`) and `RetrieveBankRelationsHandler.cs`: entry not found / BQ id foreign → the existing 404 codes.
- [ ] In-memory adapters in `RimBank.Core.Tests` handle the new event (no new port members expected; `IPartyDirectory.LoadEntryAsync` already covers the referenced entry).
- [ ] Tests: `UpdateBankRelationsHandlerTests` (not found, foreign BQ id, stale version, self, unregistered, association id as reference, type mismatch, accepted, kept reference not reloaded), `RetrieveBankRelationsHandlerTests`, and the projection test cases (Given events → Then view, including a pre-1.12 event history).

## 3. PostgreSQL adapter
- [ ] `PartyReferenceDataDirectoryPostgresql.Configure`: map `party_bank_relations_updated`.
- [ ] `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/BankRelationsRoundTripTests.cs`:
  - register two entries, Update BankRelations with a reference, Retrieve in the same store (inline read-your-write);
  - stale expected version → `party.version-conflict`;
  - same `Idempotency-Key` and payload returns the first acknowledgement and appends once;
  - `RebuildViewAsync<PartyReferenceDataDirectoryEntryView>` gives the same view.

## 4. HTTP adapter and demo
- [ ] `src/RimBank.Api/Party/PartyReferenceDataDirectory/BankRelations/`:
  - `BankRelationsRouteValues.cs` (`bankrelationsid` as a typed id, else `shared.invalid-identifier`);
  - `UpdateBankRelationsRequest.cs` (captures unknown members so the core refuses them by path), `BankRelationReferenceModel.cs`, `BankRelationsResponse.cs`; reuse `InvolvedPartyModel` and the 1.20 `DateTimeContent` model;
  - `UpdateBankRelationsEndpoint.cs` (`PUT`, `RequireIdempotencyKey()`, `If-Match`, `ETag`) and `RetrieveBankRelationsEndpoint.cs` (`GET`, `ETag`, `304`), each with its BIAN `OperationId`.
- [ ] `PartyReferenceDataDirectoryApi`: map both endpoints.
- [ ] `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/BankRelations/`: `UpdateBankRelationsEndpointTests`, `RetrieveBankRelationsEndpointTests`, `BankRelationsLogPrivacyTests` (after `ReferenceLogPrivacyTests`).
- [ ] `PartyReferenceDataDirectoryConformanceTests._implemented` gains `BankRelations/Update` and `BankRelations/Retrieve`.
- [ ] `src/RimBank.Host/RimBank.Host.http`: a "BankRelations BQ (roadmap 1.12, ADR 0035)" block: register a customer, a relationship manager (Person) and a branch (Organisation); set the manager; retrieve; change to `ServicingBranch` pointing at the person (422 type mismatch); point at the customer itself (422 self-reference).

## 5. Docs and roadmap
- [ ] `docs/adr/0035-party-bank-relations.md` (from `template.md`), as in requirements.md.
- [ ] ADR 0024: "BankRelations requests (1.12)", the events table and the error codes table.
- [ ] `specs/tech-stack.md` §3.4: the `BankRelations/` tree and the `RimBank.Api` folder list.
- [ ] `specs/roadmap.md`: tick 1.12.
- [ ] `dotnet format --verify-no-changes` and the full `dotnet test`.
- [ ] Run the `.http` demo against the Aspire host and paste the output in the PR.
- [ ] Add an "As built" section to this plan if the implementation differs.
