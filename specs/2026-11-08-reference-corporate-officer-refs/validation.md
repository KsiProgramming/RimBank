# Validation: Reference BQ (corporate and officer references)

## Done when (from roadmap)
> **1.21 Reference BQ: corporate and officer references:** `CorporateCustomerReference`, `CorporateCustomerLegalEntityReference`, `CompanyOfficerReference` and `CompanyOfficerRole`, references to other parties, added to the Reference Update and Retrieve after the Associations BQ (ADR 0032).
> + Definition of done (roadmap.md)

## Automated checks
Chosen in the interview: core and handler tests, and the log-capture check.
- [ ] **Core (`RimBank.Core.Tests`, `…/Reference/`):**
  - `CompanyOfficerRoleTests`: trims the role, and refuses a blank one or one over 35 characters;
  - `PartyCorporateReferencesTests`;
  - `PartyReferenceDetailsTests`: pre-1.21 JSON still loads;
  - `PartyReferenceChangesTests`:
    - **the staged pin is 0**;
    - `InvolvementReference` and an object-shaped role are refused as unsupported;
    - a malformed id is `party.reference-invalid` naming `….PartyReference`.
- [ ] **Decision tests (Given/When/Then, `UpdatePartyReferenceTests` and the entry decision tests):**
  - a self-reference gives `party.self-reference`;
  - an unregistered reference gives `party.referenced-party-not-registered`;
  - a Person as the corporate customer or legal entity, or an Organisation as the officer, gives `party.referenced-party-type-mismatch`;
  - corporate references on a Person entry give `party.corporate-references-need-organisation`;
  - a role with no officer, including clearing the officer while the role is kept, gives `party.reference-invalid`;
  - the entry `Update` to `Person` while references are recorded gives `party.corporate-references-need-organisation`. The entry decider laws still hold.
- [ ] **Handler tests (`UpdatePartyReferenceHandlerTests`, in-memory port):**
  - the referenced entries are loaded through the write port;
  - an association id gives `party.referenced-party-not-registered`, never an exception;
  - a reference that is kept is not reloaded;
  - the acknowledgement carries the references, built from the decided state.
- [ ] **Projection test:** events → the view's Reference section holds the references, and a pre-1.21 event folds to empty references.
- [ ] **Definition of done minimum, because the HTTP adapter and the read model change:**
  - Alba: a happy PUT and GET, one 422 and one 400, each `application/problem+json` with `errorCode`;
  - Testcontainers: `PartyReferenceRoundTripTests` rebuilds the inline view with the new fields.
- [ ] **Log capture (`ReferenceLogPrivacyTests`):** no submitted referenced id or role appears in the logs.
- [ ] `RimBank.Architecture.Tests` stays green. In particular the CQRS rule: the Reference command handler uses only `IPartyDirectory`, never `IPartyDirectoryEnquiry` or a view.
- [ ] The existing BIAN conformance tests stay green (no new route).
- [ ] `dotnet format --verify-no-changes`, and the CI build, test and CodeQL jobs, are green.

## Manual / demo checks
Run in `src/RimBank.Host/RimBank.Host.http` against `dotnet run --project src/RimBank.AppHost`, with a dev-realm token:
- [ ] `POST /PartyReferenceDataDirectory/Register` for an Organisation (A), another Organisation (B) and a Person (P) gives three 201 responses, each with an `ETag`.
- [ ] `PUT /PartyReferenceDataDirectory/{A}/Reference/{refId}/Update` with `If-Match` and an `Idempotency-Key`, and the body `{"CorporateCustomerReference":{"PartyReference":"<B>"},"CorporateCustomerLegalEntityReference":{"PartyReference":"<B>"},"CompanyOfficerReference":{"PartyReference":"<P>"},"CompanyOfficerRole":"Director"}`, gives a 200 that echoes the four fields and a new `ETag`.
- [ ] `GET /PartyReferenceDataDirectory/{A}/Reference/{refId}/Retrieve` returns the same four fields.
- [ ] The same Update with `"CorporateCustomerReference":{"PartyReference":"<P>"}` gives a **422** `application/problem+json` with `errorCode: party.referenced-party-type-mismatch` and a `detail` that starts with `CorporateCustomerReference.PartyReference`.
- [ ] An Update with `"CompanyOfficerReference":{"PartyReference":"<P>","InvolvementReference":"x"}` gives a **400** `party.reference-field-not-supported`.
- [ ] `PUT /PartyReferenceDataDirectory/{A}/Update` with `PartyType: Person` gives a **422** `party.corporate-references-need-organisation`.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.21 ticked in the same PR
- [ ] ADR 0034 written, and ADRs 0029, 0024 and 0032 and tech-stack §3.4 amended
- [ ] No referenced id or role in the logs (log-capture test)
- [ ] Every new error path returns Problem Details with a documented code
- [ ] The PR is opened with the open-phase-pr skill (labels, milestone, board, `Closes #<1.21 issue>`)
