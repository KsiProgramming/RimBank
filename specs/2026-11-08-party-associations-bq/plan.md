# Plan: Party Reference Data Directory: Associations BQ

Every group is written test first. After each group, the solution builds and its tests pass (one commit per group, one PR).
- Test namespaces end with `.Tests`, and context namespaces stay flat: every core type is in `RimBank.Party.PartyReferenceDataDirectory`, and every HTTP type in `….Api` (tech-stack §3.3, ADR 0026).
- Use switch expressions only (IDE0066).
- Business rules live in `RimBank.Core`. The adapters only translate the transport (memory: business logic in core).
- The Testcontainers and Alba tests need Docker. If it is not available locally, they run in CI.

## 1. Domain: the `PartyAssociation` aggregate
- [ ] `Associations/PartyAssociationId.cs`: a Guid v7 `readonly record struct` implementing `ITypedId<PartyAssociationId>` (ADR 0008), with its typed-ID contract test.
- [ ] `Associations/PartyRelationshipType.cs` (enum, 9 values) and `PartyRelationshipTypes.cs` (BIAN spelling, reading in any case, after the `PartyTypes` pattern).
- [ ] `Associations/AssociationValidityPeriod.cs`: `Create(from?, to?)` → `Result`, reading ISO 8601 (a date alone or a value without an offset as UTC), `From` ≤ `To`, at least one bound.
- [ ] `Associations/PartyAssociationErrors.cs`: `AssociationInvalid` (Validation, with field errors), `AssociationFieldNotSupported` (Validation), `AssociateCannotChange` (BusinessRule), `AssociationNotFound` (NotFound), `SelfAssociation` and `AssociateNotRegistered` (BusinessRule).
- [ ] `Associations/AssociationFields.cs`: the BIAN paths, the recorded ones, and the refused ones with their phase (`1.21` or none), after `PartyReferenceFields`.
- [ ] `Associations/PartyAssociationChanges.cs` + `PartyAssociationInput.cs`: the raw values an adapter sends, turned into typed changes. Unsupported fields are checked first, then every value, gathered into one `party.association-invalid` naming each BIAN path.
- [ ] `Associations/Events.cs`: `PartyAssociationEvent` (base), `PartyAssociationRegistered`, `PartyAssociationUpdated`.
- [ ] `Associations/PartyAssociation.cs` (state + `Evolve`, `IDecider`) and `Decisions.cs`:
  - Register decision: owner registered, associate ≠ owner, associate registered (the handler passes the two entry states);
  - Update decision: the association exists, the owner matches, the expected version matches, the associate is unchanged.
- [ ] Tests in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Associations/`:
  - `PartyRelationshipTypeTests`, `AssociationValidityPeriodTests`, `PartyAssociationChangesTests`;
  - `PartyAssociationDecisionTests` (Given / When / Then) for every error code and for the accepted paths;
  - FsCheck `PartyAssociationLawsProperties`: the evolve/decide laws, owner and associate never change, and an Update with no changes keeps the state.

## 2. Command and query flows (core, in-memory ports)
- [ ] `IPartyDirectory`: `LoadAssociationAsync`, `StartAssociation`, `AppendToAssociation(id, expectedVersion, events)`; the remarks are updated.
- [ ] `IPartyDirectoryEnquiry`: `FindAssociationAsync`.
- [ ] `Associations/RegisterPartyAssociation.cs` (command, `ICommand`), `RegisterPartyAssociationAcknowledged.cs`, `RegisterPartyAssociationHandler.cs`: create the id (`TimeProvider`), load both entry streams, decide, start the stream, and acknowledge from the decided state with version 1.
- [ ] `Associations/UpdatePartyAssociation.cs` (with `ExpectedVersion`), its acknowledgement and handler: load, decide, and append with the expected version.
- [ ] `Associations/PartyAssociationView.cs` and `PartyAssociationViewProjection.cs` (pure `IProjection`, time only from event data).
- [ ] `Associations/RetrievePartyAssociation.cs` (`IQuery<PartyAssociationView>`) and its handler: not found, or the owner is not the path entry → `party.association-not-found`.
- [ ] `InMemoryPartyDirectory` and `InMemoryPartyDirectoryEnquiry` gain the association streams and views, with their own tests.
- [ ] Tests:
  - handler tests for Register (unknown owner, self, unknown associate, accepted), Update (not found, wrong owner, stale version, another associate, accepted) and Retrieve (found, not found, wrong owner);
  - `PartyAssociationViewProjectionTests` (Given events → Then view).

## 3. PostgreSQL adapter
- [ ] `PartyReferenceDataDirectoryPostgresql.Configure`: map `party_association_registered` and `party_association_updated`, and add `AddInlineView<PartyAssociationView, PartyAssociationViewProjection, PartyAssociationEvent>()`.
- [ ] `MartenPartyDirectory`: the three association members (start with an exclusive stream, append with the expected version, as for the entry).
- [ ] `MartenPartyDirectoryEnquiry.FindAssociationAsync`.
- [ ] `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/PartyAssociationRoundTripTests.cs`:
  - Register then Retrieve in the same store (inline read-your-write), next to an entry stream;
  - Update with a stale version → `party.version-conflict`;
  - the same `Idempotency-Key` and payload on Register returns the first acknowledgement and starts no second stream;
  - `RebuildViewAsync<PartyAssociationView>` gives the same view.
- [ ] `MartenPartyDirectoryTests` / `MartenPartyDirectoryEnquiryTests` gain the association members.

## 4. HTTP adapter
- [ ] `src/RimBank.Api/Party/PartyReferenceDataDirectory/Associations/`:
  - `AssociationRouteValues.cs` (`associationsid` read as a typed id, else `shared.invalid-identifier`);
  - `InvolvedPartyModel.cs`, `AssociationValidityPeriodModel.cs` (a new model: here `FromDateTime` and `ToDateTime` are plain strings, unlike the nested `DateTimeContent` of the 1.20 document period), `RegisterPartyAssociationRequest.cs`, `UpdatePartyAssociationRequest.cs`, `PartyAssociationResponse.cs`. The requests capture unknown members, so the core can refuse them by path;
  - `RegisterPartyAssociationEndpoint.cs` (`POST`, `RequireIdempotencyKey()`, `ETag`), `UpdatePartyAssociationEndpoint.cs` (`PUT`, `Idempotency-Key`, `If-Match`), `RetrievePartyAssociationEndpoint.cs` (`GET`, `ETag`, `304`), each with its `OperationId`.
- [ ] `PartyReferenceDataDirectoryApi`: map the three endpoints.
- [ ] `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Associations/`: `RegisterPartyAssociationEndpointTests`, `UpdatePartyAssociationEndpointTests`, `RetrievePartyAssociationEndpointTests` (see validation.md).
- [ ] `PartyReferenceDataDirectoryConformanceTests._implemented` gains the three operation ids.
- [ ] `src/RimBank.Host/RimBank.Host.http`: an "Associations BQ (roadmap 1.11, ADR 0033)" block that registers a second party, associates the two, updates the period, retrieves, and shows a self-association 422.

## 5. Docs and roadmap
- [ ] `docs/adr/0033-party-associations.md` (from `template.md`), as in requirements.md.
- [ ] ADR 0024: the "Associations requests (1.11)" paragraph, the events table, and the error codes table.
- [ ] `specs/tech-stack.md` §3.4: the `Associations/` tree and the `RimBank.Api` folder list.
- [ ] `specs/roadmap.md`: tick 1.11.
- [ ] `dotnet format --verify-no-changes` and the full `dotnet test` run.
- [ ] Add an "As built" section to this plan if the implementation differs.
