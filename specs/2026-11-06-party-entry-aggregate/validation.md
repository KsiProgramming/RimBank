# Validation: Party Reference Data Directory: entry aggregate

## Done when (from roadmap)
> **1.2 Aggregate:** `PartyReferenceDataDirectoryEntry` state, `Register` decision, `PartyRegistered` event, invariants, unit tests.
> + Definition of done (roadmap.md)

Definition-of-done items that do not apply here, because the phase is pure domain code: projection tests and rebuildable views (there is no view until 1.6), Testcontainers tests (no adapter), Problem Details on HTTP/MCP and MCP parity (no endpoint or tool). The two new error codes get their Problem Details mapping for free through `RimBank.Problems` when 1.5 exposes them.

## Automated checks
- [ ] **Typed id contract tests** (`RimBank.Core.Tests`): `PartyReferenceDataDirectoryEntryId`, `DemographicsId`, `PartyReferenceId` and `BankRelationsId` each pass `TypedIdContractTests<TId>` (Guid v7, `TimeProvider` timestamp, parse/format, JSON, validation errors).
- [ ] **Decider unit tests** (`DeciderSpecification`):
  - Register from `Initial` → `PartyRegistered` with every field (Person and Organisation);
  - Register on a registered entry → `Conflict` `party.party-already-registered`;
  - Register with an undefined `PartyType` → `Validation` `party.party-type-invalid`;
  - `Initial` is not registered; `Evolve(PartyRegistered)` holds all six values; `Evolve` of an unknown event throws `UnreachableException`.
- [ ] **FsCheck decider laws** (`DeciderLaws`): `DecisionsAreDeterministic` and `DecidedEventsEvolve`, over generated sequences that include repeat registrations and invalid party types.
- [ ] **Architecture tests** green, unchanged: the aggregate state implements `IDecider`, the ids are `readonly record struct`, there are no `double`/`float` members, namespaces follow `RimBank.Party.PartyReferenceDataDirectory.<Folder>`, test namespaces end with `.Tests`, there are no forbidden folder names, and the core has no Marten, Wolverine or ASP.NET reference.
- [ ] `CoreReferencesTests` green: `RimBank.Core` still references only SharedKernel and the BCL.
- [ ] `RimBank.Conformance.Tests` catalogue tests green (ADR 0024 table unchanged apart from error codes).
- [ ] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL: green in CI.

## Manual / demo checks
- [ ] Break an invariant on purpose: make `Decide` accept Register on a registered entry. The `party.party-already-registered` test fails, and its message shows the Given/When/Then. Revert.
- [ ] Break purity on purpose: read `DateTimeOffset.UtcNow` inside `Decide` instead of the command's open date. `DecisionsAreDeterministic` fails. Revert.
- [ ] Reading check: the event, command and error-code names match ADR 0024 (`PartyRegistered` carries the three singleton BQ ids), and the error table in ADR 0024 lists the two new codes.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.2 ticked in the same PR
- [ ] ADR 0024 amended with the new error codes (no new ADR needed)
- [ ] No handler, port, adapter or view added (those belong to 1.3–1.7); `doc-0/` untouched
- [ ] No PII in the event beyond what the phase defines (party type and dates only)
