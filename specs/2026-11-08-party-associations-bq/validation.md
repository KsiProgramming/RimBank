# Validation: Party Reference Data Directory: Associations BQ

## Done when (from roadmap)
> **1.11 Associations BQ:** party-to-party relationships as their own aggregate `PartyAssociation` (ADR 0024), with its own stream and inline view; Register/Update commands (owner and associate checked on the write side) and Retrieve query.
> + Definition of done (roadmap.md)

The MCP tools for these operations are roadmap **1.17**, as for every BQ so far. So the Definition of done's parity item is met there, not here.

## Automated checks
- [ ] `RimBank.Core.Tests` (`RimBank.Party.PartyReferenceDataDirectory.Tests`, folder `Associations/`):
  - the typed-ID contract for `PartyAssociationId`;
  - `PartyRelationshipTypeTests`: all 9 values, any case in, BIAN spelling out, an unknown value refused;
  - `AssociationValidityPeriodTests`: date and date-time inputs, no offset read as UTC, `From` > `To` refused, an empty period refused;
  - `PartyAssociationChangesTests`: each refused path (and its `1.21` tag where it applies), the unsupported check first, one `party.association-invalid` naming every bad path;
  - `PartyAssociationDecisionTests` (Given / When / Then): `party.party-not-found`, `party.self-association`, `party.associate-not-registered`, `party.association-not-found` (missing, wrong owner), `party.version-conflict`, `party.associate-cannot-change`, the type cannot be cleared, and accepted Register and Update (an unchanged Update still appends);
  - handler tests with `InMemoryPartyDirectory` / `InMemoryPartyDirectoryEnquiry` for Register, Update and Retrieve, including the check order.
- [ ] FsCheck `PartyAssociationLawsProperties`: the evolve/decide laws, owner and associate never change, and an Update with no changes keeps the state.
- [ ] Projection test (Given events → Then view) in `PartyAssociationViewProjectionTests`: Register, then Updates that replace and clear the period.
- [ ] `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (Testcontainers), `PartyAssociationRoundTripTests`:
  - both stream types live in the one context store;
  - inline read-your-write after Register and Update;
  - a stale version on the association stream → `party.version-conflict`;
  - an idempotent Register (the same key returns the first acknowledgement, one stream);
  - `RebuildViewAsync` gives the same `PartyAssociationView` (DoD: read models can be rebuilt).
- [ ] `RimBank.Api.Tests` (Alba), `Associations/`:
  - Register `200`, with `ETag` `"1"` and the response shape; Update `200` with `ETag` `"2"`; Retrieve `200`, and `304` on a matching `If-None-Match`;
  - `application/problem+json` with a stable `errorCode` for:
    - 400: `shared.invalid-identifier` (each route id), `party.association-invalid` (missing associate or type, bad type, bad date, `From` > `To`, `errors` paths), `party.association-field-not-supported` (`ProductInstanceReference`, `AssociateReference.InvolvementReference`), a missing `Idempotency-Key`, `shared.if-match-invalid`;
    - 404: `party.party-not-found` (Register on an unknown owner), `party.association-not-found` (unknown association, or a path entry that is not the owner, on Update and Retrieve);
    - 409: `party.version-conflict`;
    - 422: `party.self-association`, `party.associate-not-registered`, `party.associate-cannot-change`;
    - 428: `shared.if-match-required`.
- [ ] `PartyReferenceDataDirectoryConformanceTests`: the three Associations routes are mapped with the spec's methods (`POST`, `PUT`, `GET`), and `Retrieve` is the only `GET`.
- [ ] `RimBank.Conformance.Tests` green: the catalogue rows stay `1.11`.
- [ ] `RimBank.Architecture.Tests` green:
  - the new command handlers use only `IPartyDirectory` (no view, no read port);
  - the query handler uses only `IPartyDirectoryEnquiry`;
  - `PartyAssociationViewProjection` is pure and implements `IProjection`, and `PartyAssociation` implements `IDecider`;
  - `PartyAssociationId` is a `readonly record struct`;
  - flat namespaces, no `double` or `float`, no business rule in `.Api`.
- [ ] `dotnet format --verify-no-changes` clean, with no warnings.
- [ ] GitHub Actions CI green: build, test, format, CodeQL.

## Manual / demo checks
Optional (no manual demo was chosen in the interview). The requests are in `src/RimBank.Host/RimBank.Host.http`, for reviewers:
- [ ] Run `dotnet run --project src/RimBank.AppHost`, register two parties, then send `POST /PartyReferenceDataDirectory/{ownerId}/Associations/Register` with `Idempotency-Key` and this body:
  ```json
  {
    "AssociateReference": { "PartyReference": "<associate entry id>" },
    "AssociateType": "partyisparentofparty",
    "AssociationValidFromToDate": { "FromDateTime": "2024-01-01" }
  }
  ```
  Expected: 200, `ETag` `"1"`, `AssociateType` `PartyIsParentOfParty`, `FromDateTime` `2024-01-01T00:00:00Z`.
- [ ] `PUT …/Associations/{associationsid}/Update` with `If-Match: "1"` and `{"AssociationValidFromToDate":{"FromDateTime":"2024-01-01","ToDateTime":"2025-12-31"}}` → 200, `ETag` `"2"`, the type kept.
- [ ] `GET …/Associations/{associationsid}/Retrieve` → 200 with the same values; with `If-None-Match: "2"` → 304.
- [ ] Register with the owner's own id as the associate → 422 `party.self-association`.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.11 ticked in the same PR
- [ ] ADR 0033 written; ADR 0024 and tech-stack §3.4 amended
- [ ] No association value logged by new code; no secrets in source
- [ ] The PR opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<issue>`)
