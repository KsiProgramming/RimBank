# Plan: Reference BQ (corporate and officer references)

Inside-out and test first (chosen in the interview), in the same order as 1.19 and 1.20. The solution builds and every test passes after each group.

## 1. Value objects (core `Reference/`)
- [ ] `CompanyOfficerRole`, an `IPartyReferenceText` with Max35Text that is trimmed and not blank, serialized through `PartyReferenceTextJsonConverter`. Tests first in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/Reference/CompanyOfficerRoleTests.cs`.
- [ ] `PartyCorporateReferences` (`CorporateCustomer`, `LegalEntity`, `CompanyOfficer`, all `PartyReferenceDataDirectoryEntryId?`, plus `CompanyOfficerRole?`), with `IsEmpty` and `ReferencedEntries` (the ids that are set). Tests: `PartyCorporateReferencesTests`.
- [ ] Add the new member to `PartyReferenceDetails`: `CorporateReferences`, nullable and `[JsonIgnore]`-computed, with a `CurrentCorporateReferences` accessor. Extend `PartyReferenceDetailsTests` with a check that pre-1.21 JSON still deserialises.

## 2. Changes, decisions, handler, entry guard, projection and HTTP (one commit)
- [ ] `PartyReferenceFields`:
  - add constants for the four paths and their `.PartyReference` parts;
  - empty `Staged`;
  - list `….InvolvementReference` (all three) and object-shaped `CompanyOfficerRole` parts as not recorded.
- [ ] `PartyReferenceInput` and `PartyReferenceChanges`:
  - read the three ids as Guid v7 entry ids (`party.reference-invalid` naming `….PartyReference`);
  - read the role;
  - blank clears, omitted keeps.
- [ ] Update `PartyReferenceChangesTests`: the staged pin goes 4 → 0, and add the new unsupported parts.
- [ ] **Decide (`PartyReferenceDataDirectoryEntry` Reference decision):**
  - `UpdatePartyReferenceDetails` gains `ReferencedParties` (an id → `PartyType?` lookup, where `null` means not registered);
  - the checks run in this order: self-reference → not registered → type mismatch → the entry is not an Organisation (on the resulting Reference) → role without an officer (on the result);
  - add the errors to `PartyReferenceErrors`: `SelfReference`, `ReferencedPartyNotRegistered`, `ReferencedPartyTypeMismatch`, `CorporateReferencesNeedOrganisation`.
  - Given/When/Then tests go in `UpdatePartyReferenceTests`.
- [ ] **Handler (`UpdatePartyReferenceHandler`):**
  - after the entry, version and BQ checks, load each id in the given changes through `directory.LoadEntryAsync`;
  - fold each one with `Evolve` into `(IsRegistered, PartyType)`;
  - build `ReferencedParties`, then call `Decide`.
  - Tests in `UpdatePartyReferenceHandlerTests` with the in-memory port:
    - an unregistered id, an association id and a self id;
    - a Person given as the corporate customer, and an Organisation given as the officer;
    - a Person owner;
    - a kept reference is not reloaded.
- [ ] **Entry Update guard:** in the entry `Decide` for `UpdatePartyReferenceDataDirectoryEntryDetails`, refuse `PartyType = Person` when `CurrentReference.CurrentCorporateReferences` is not empty (`CorporateReferencesNeedOrganisation`). Add tests to the entry Update decision tests and the entry decider laws.
- [ ] **Projection:** `PartyReferenceDataDirectoryEntryViewProjection` carries `CorporateReferences` in the view's Reference section. Extend the projection test (Given events → Then view), including a pre-1.21 event.
- [ ] **HTTP (`src/RimBank.Api/Party/PartyReferenceDataDirectory/Reference/`):**
  - add `InvolvedPartyModel`;
  - add the four fields to the Update request and the Update/Retrieve response;
  - leave empty values out of the response;
  - report `InvolvementReference` and object-shaped role parts as unsupported;
  - map the new 422 codes through the existing `error.ToHttpResult()`.
  - Extend `PartyReferenceRequestTests` and `PartyReferenceResponseTests`.

## 3. Integration (Definition of done: adapters touched, read model rebuildable)
- [ ] In `UpdatePartyReferenceEndpointTests` and `RetrievePartyReferenceEndpointTests` (Alba):
  - a happy path that links an Organisation to an Organisation and a Person, and reads them back;
  - one 422 Problem Details (`party.referenced-party-not-registered`);
  - one 400 (`party.reference-field-not-supported` for `InvolvementReference`).
- [ ] Add a short `ReferenceScenarios` helper to register the referenced entries.
- [ ] In `PartyReferenceRoundTripTests` (Testcontainers): one case that appends the new event, rebuilds the inline view with `RebuildViewAsync`, and reads the references back. Also check that a stored pre-1.21 event still folds.

## 4. Demo, privacy and docs
- [ ] `src/RimBank.Host/RimBank.Host.http`: register an Organisation and a Person, run the Reference Update that links them, run the Retrieve, and show a 422 request (a Person as the corporate customer).
- [ ] `ReferenceLogPrivacyTests`: add the new fields to the submitted payload, and assert that no referenced id or role is logged.
- [ ] Write ADR 0034, and amend ADR 0029, ADR 0024 and ADR 0032.
- [ ] Update the `Reference/` tree in tech-stack §3.4.
- [ ] Roadmap: tick 1.21 and reword it to name the party-type rules.
- [ ] Run `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` locally before opening the PR (with the open-phase-pr skill).
