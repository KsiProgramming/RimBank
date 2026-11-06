# Plan: Party Reference Data Directory: PostgreSQL adapter (write side)

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). The adapter lives in `src/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql/` and its tests in `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/`. Test namespaces end with `.Tests`, for example `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (tech-stack §3.3). Use switch expressions only (IDE0066). Package versions go only in `Directory.Packages.props`, and lock files are regenerated.

## 1. Marten write port (adapter + Testcontainers)
- [ ] Shared convention first, in `RimBank.Postgresql.Tests` (test first): a fake event carrying a typed id and an enum round-trips through the store, with the id stored as a plain GUID string and the enum as its name. Then add `TypedIdJsonConverterFactory` and enum-as-string to the store serializer in `ServiceDomainStoreRegistration` (System.Text.Json, `EnumStorage.AsString`). Check the current Marten API with `ctx7` before writing it.
- [ ] Create `RimBank.Party.PartyReferenceDataDirectory.Postgresql.csproj` (references `RimBank.Core` and `RimBank.Postgresql`, no package that §4 forbids) and add it to `RimBank.slnx`.
- [ ] Create the test project `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (xUnit v3, Shouldly, Testcontainers.PostgreSql, WolverineFx.RuntimeCompilation, `RimBank.SharedKernel.Testing`), with an assembly-fixture `PostgresqlServer` (one container, one database per test, one host started at a time), modelled on `RimBank.Postgresql.Tests/Database/`.
- [ ] Tests first (`MartenPartyDirectoryTests`), each against a host with only `AddPartyReferenceDataDirectoryPostgresql()`:
  - `LoadEntryAsync` of an unknown entry → empty;
  - `StartEntry` + `SaveChangesAsync`, then `LoadEntryAsync` in a new session → exactly the `PartyRegistered` that was started, with equal typed ids, `PartyType` and `OpenDate`;
  - starting the same entry id twice → the second `SaveChangesAsync` throws, and the stream still holds one event;
  - the stored event's type name is `party_registered`, and its JSON holds plain GUID strings and `"PartyType":"Person"`;
  - the stream lives in schema `party_reference_data_directory` and is owned by role `rimbank_party_reference_data_directory`.
- [ ] Implement `IPartyReferenceDataDirectoryStore`, `MartenPartyDirectory` and `PartyReferenceDataDirectoryPostgresql` (`Configure` + `AddPartyReferenceDataDirectoryPostgresql`), with XML docs that cite ADR 0016, 0017 and 0024.
- [ ] Run `RimBank.Architecture.Tests`: the new project is a `.Postgresql` adapter of the Party context, uses only its own core namespace, and has no forbidden folder names.

## 2. RegisterParty through the bus (end to end)
- [ ] Tests first (`RegisterPartyRoundTripTests`, host with `AddPartyReferenceDataDirectoryPostgresql()`, `FixedTimeProvider` and a test actor):
  - `bus.InvokeAsync<Result<RegisterPartyAcknowledged>>(new RegisterParty(Person, key))` succeeds. The stream `ack.EntryId` holds one `PartyRegistered` equal to the ack's fields, and the Marten stream version equals `ack.Version` (1). This settles the version check that 1.3 deferred;
  - the same key and payload again → the same ack (ids included) and still one stream (ADR 0018 replay through the stored `IdempotentOutcome`, which also proves the ack's typed ids round-trip in Marten);
  - the same key with a different `PartyType` → the idempotency conflict error from ADR 0018, and no second stream;
  - an undefined `PartyType` → `party.party-type-invalid`, and no stream and no stored outcome (only successes are stored).
- [ ] Fix any wiring gap the tests reveal in the adapter only. The core stays unchanged.

## 3. Host, AppHost, docs and roadmap
- [ ] `RimBank.Host`: reference the new project; add `Modules/Party/PartyReferenceDataDirectoryModule.cs`; set `ServiceDomainModules.All = [new PartyReferenceDataDirectoryModule()]`; register `TimeProvider.System` with `TryAddSingleton` in `HostComposition`.
- [ ] `RimBank.AppHost`: add the `PartyReferenceDataDirectory` and `Wolverine` connection strings for the host (role logins with Aspire-generated password parameters, never in source). Update its XML doc ("the first context adds its connection string here in roadmap 1.4").
- [ ] Update any host or AppHost test that pins the empty module list or the resource model. Keep `RimBank.Host.Tests` and `RimBank.AppHost.Tests` green.
- [ ] ADR 0016: add the Serialization paragraph. ADR 0016 and ADR 0017: put the "1.4 adds…" consequences in the past tense. ADR 0024: no change.
- [ ] Roadmap: tick **1.4**.
- [ ] Run `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally (Docker running).
