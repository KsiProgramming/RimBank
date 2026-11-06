# Validation: Party Reference Data Directory: BIAN → DDD mapping

## Done when (from roadmap)
> **1.1 ADR:** BIAN → DDD mapping for this domain: aggregate boundaries, BQs as entities, the command/query split of its operations, and its read models (which views, inline or async).
> + Definition of done (roadmap.md)

Definition-of-done items that do not apply here (no runtime code): the projection tests, the Testcontainers tests, rebuildable views, Problem Details paths and MCP parity. The ADR fixes how later phases meet them.

## Automated checks
- [x] `RimBank.Conformance.Tests/Catalogues/PartyReferenceDataDirectoryCatalogueTests` (chosen in the planning interview):
  - every operationId in `PartyReferenceDataDirectory.yaml` (OpenAPI) is catalogued exactly once, and no catalogued id is missing from the spec (17 rows);
  - each row's kind equals the spec's kind: the 6 `Retrieve` operations are queries and the other 11 are commands;
  - every AsyncAPI channel is catalogued exactly once (10 rows);
  - every phase is a Stage 1 phase or `deferred`.
- [x] Existing suites unchanged and green: `RimBank.Architecture.Tests`, `RimBank.Conformance.Tests` (spec loaders, fake context).
- [ ] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL: green in CI.

## Manual / demo checks
- [x] Break the catalogue on purpose: remove the `Associations/Register` row, then flip `Reference/Retrieve` to `Command`. Each change makes a catalogue test fail with the operation id in the message. Revert both.
- [x] Reading check: the operation, channel and read-model tables in ADR 0024 match `PartyReferenceDataDirectoryCatalogue.cs` row for row, and tech-stack §3.4 and the roadmap 1.11 row describe `PartyAssociation` as in the ADR.

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 1.1 ticked in the same PR
- [x] ADR 0024 written (it is the phase's deliverable)
- [x] No code under `src/` changed; `doc-0/` untouched
