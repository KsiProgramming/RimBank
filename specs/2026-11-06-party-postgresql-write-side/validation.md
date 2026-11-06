# Validation: Party Reference Data Directory: PostgreSQL adapter (write side)

## Done when (from roadmap)
> **1.4 PostgreSQL adapter (write side):** Marten event-stream implementation of `PartyDirectory`, with integration tests.
> + Definition of done (roadmap.md)

Some Definition-of-done items do not apply here. The phase adds no read model, so there is no projection test and no rebuild check (1.6/1.7). It adds no new error path, BIAN operation, HTTP endpoint or MCP tool, so there are no new Problem Details and no parity test (1.5, 1.16).

## Automated checks
- [x] **Shared convention** (`RimBank.Postgresql.Tests`): a typed id is stored as a plain GUID string and an enum as its name, and both round-trip. The existing fake-context tests stay green.
- [x] **Adapter integration tests** (`RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests`, Testcontainers PostgreSQL 17):
  - an unknown entry → empty history;
  - start + commit → the same `PartyRegistered` loads back in a new session, with typed ids, `PartyType` and `OpenDate` intact;
  - a duplicate stream id fails at commit and leaves one event;
  - the stored type name is `party_registered`, with JSON as plain GUIDs and enum names;
  - the stream sits in schema `party_reference_data_directory` under role `rimbank_party_reference_data_directory`.
- [x] **Bus round trip** (`RegisterPartyRoundTripTests`):
  - `RegisterParty` through `IMessageBus` commits one stream, and Marten's stream version = `ack.Version` = 1;
  - the same key replays the first ack with no second stream;
  - the same key with a different payload → the ADR 0018 conflict;
  - an invalid `PartyType` → `party.party-type-invalid`, with nothing stored.
- [x] **Architecture tests** green (existing rules): `RimBank.Party.PartyReferenceDataDirectory.Postgresql` references only `RimBank.Core` (its own namespace), SharedKernel, `RimBank.Postgresql`, Marten and Wolverine.Marten; the core still has no Marten or Wolverine reference; no cross-context references.
- [x] `RimBank.Core.Tests` unchanged and green (the in-memory adapter still mirrors Marten's duplicate-stream behaviour).
- [x] `RimBank.Host.Tests` and `RimBank.AppHost.Tests` green with the Party module registered.
- [ ] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL: green in CI.

## Manual / demo checks
- [x] Demo: `dotnet test tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` with Docker running, all green. `RegisterPartyRoundTripTests` shows a real Register committed to PostgreSQL through the bus.
- [x] Reading check: `MartenPartyDirectory` holds no business logic (no `Decide`, no validation), only a mapping between typed ids and Marten stream ids. The AppHost holds no password in source.

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 1.4 ticked in the same PR
- [x] ADR 0016 amended (Serialization), and the 1.4 consequences in ADR 0016/0017 updated; no new ADR needed
- [x] No HTTP, MCP, view or projection code added (1.5, 1.6, 1.7, 1.16); `RimBank.Core` and `doc-0/` untouched
- [x] No secrets in source or logs: role passwords come only from Aspire parameters or user secrets, and the stored events hold no PII (party type, dates and ids only)
