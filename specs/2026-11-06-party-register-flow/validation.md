# Validation: Party Reference Data Directory: Register flow (command side)

## Done when (from roadmap)
> **1.3 Register flow (command side):** `RegisterParty` command and handler, `PartyDirectory` write port, in-memory test adapter.
> + Definition of done (roadmap.md)

Some Definition-of-done items do not apply here, because the phase has no adapter, view or endpoint. There is no projection test or rebuildable view (none until 1.6), no Testcontainers test (no real adapter until 1.4), no HTTP/MCP Problem Details (the endpoint comes in 1.5 and the tool in 1.16) and no MCP parity. The errors the handler returns are the existing 1.2 codes, already mapped by `RimBank.Problems`.

## Automated checks
- [x] **In-memory adapter tests** (`InMemoryPartyDirectoryTests`): an unknown entry has an empty history; start then load gives the same events; starting an existing stream throws.
- [x] **Handler unit tests** (`RegisterPartyHandlerTests`, `FixedTimeProvider`):
  - `Person` and `Organisation` register: one stream with one `PartyRegistered`, ack `Version` 1, `OpenDate` = the fixed time, four distinct Guid v7 ids at the fixed time, ack fields = event fields = the folded state;
  - an undefined `PartyType` → `Validation` `party.party-type-invalid`, no stream started;
  - an already-registered entry → `Conflict` `party.party-already-registered`, nothing appended;
  - the idempotency key does not change the handler's outcome;
  - the ack round-trips through System.Text.Json.
- [x] **Architecture tests** green with the first real handler and port (`SolutionRulesTests.The_party_register_flow_is_classified_as_write_side` pins the classification): `RegisterPartyHandler` is a command handler that uses no read port, view, query or query handler; `IPartyDirectory` implements `IWritePort`; namespaces follow `RimBank.Party.PartyReferenceDataDirectory.<Folder>` and test namespaces end with `.Tests`; there are no forbidden folder names; the core has no Marten, Wolverine or ASP.NET reference; there are no `double`/`float` members.
- [x] `CoreReferencesTests` green: `RimBank.Core` still references only SharedKernel and the BCL.
- [x] `RimBank.Host.Tests` green and unchanged: the host does not yet discover the Party handler (no `AddServiceDomainStore` until 1.4).
- [ ] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL: green in CI.

## Manual / demo checks
- [x] There is no HTTP or MCP demo in this phase (1.5 and 1.16). The demo is `dotnet test tests/RimBank.Core.Tests --filter "FullyQualifiedName~Registration"`, with every Register handler test green.
- [x] Reading check: `RegisterParty`, `RegisterPartyAcknowledged` and `IPartyDirectory` match the names in ADR 0024 and ADR 0012. The ack carries the three BQ ids that ADR 0024 requires. The handler never reads `IdempotencyKey`, `DateTimeOffset.UtcNow` or a view.

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 1.3 ticked in the same PR
- [x] No ADR needed (no new error code, event or deviation from the tech stack); tech-stack §3.4 tree updated
- [x] No Marten, Wolverine, HTTP or MCP code added (those belong to 1.4, 1.5 and 1.16); `doc-0/` untouched
- [x] No PII added: the command and the ack carry only the party type, dates and ids
