# Validation: Party Reference Data Directory: Retrieve flow (query side)

## Done when (from roadmap)
> **1.6 Retrieve flow (query side):** `PartyReferenceDataDirectoryEntryView`, its pure projection, `RetrievePartyReferenceDataDirectoryEntry` query and handler, `PartyDirectoryEnquiry` read port and in-memory adapter, with projection unit tests.
> + Definition of done (roadmap.md)

Some Definition-of-done items do not apply here, because the phase is core only. There is no Testcontainers test (the Marten read port and inline view come in 1.7, which also proves the rebuild with `RebuildViewAsync`). There is no HTTP or MCP Problem Details yet (the endpoint comes in 1.7 and the tool in 1.16), and no MCP parity. The only new error, `party.party-not-found`, is a `NotFound` that `RimBank.Problems` already maps to a 404. "Rebuildable from the event store" is shown here by the projection being a pure fold from `null`, which the projection tests exercise.

## Automated checks
- [x] **Projection unit tests** (`PartyReferenceDataDirectoryEntryViewProjectionTests`, `ProjectionSpecification`): `PartyRegistered` (`Person`, `Organisation`) → the view with `Version` 1 and the event's fields; no events → no view; `EntryId` = the event's id; the view round-trips through System.Text.Json and `EntryId` is not serialised.
- [x] **In-memory adapter tests** (`InMemoryPartyDirectoryEnquiryTests`): unknown entry → `null`; seeded events → the projected view.
- [x] **Handler unit tests** (`RetrievePartyReferenceDataDirectoryEntryHandlerTests`): a registered entry → its view; an unknown entry → `ErrorKind.NotFound` `party.party-not-found`; two entries → each query returns its own view.
- [x] **Architecture tests** green, with `SolutionRulesTests.The_party_retrieve_flow_is_classified_as_read_side` pinning that the handler is a query handler using only `IPartyDirectoryEnquiry` and the view (no write port, aggregate, command or command handler), that `IPartyDirectoryEnquiry` is an `IReadPort`, and that the projection is pure (no port, handler or `TimeProvider`). Namespaces follow `RimBank.Party.PartyReferenceDataDirectory.Retrieval` and test namespaces end with `.Tests`; there are no forbidden folders or `double`/`float` members.
- [x] `CoreReferencesTests` green: `RimBank.Core` still references only SharedKernel and the BCL.
- [ ] `RimBank.Host.Tests` and `RimBank.Party.PartyReferenceDataDirectory.Api.Tests` green and unchanged: the host starts and Register works with the new handler discovered. (`RimBank.Host.Tests` green locally; the Party `.Api.Tests` need Docker and run in CI.)
- [ ] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL: green in CI.

## Manual / demo checks
- [x] There is no HTTP or MCP demo in this phase (1.7 and 1.16). The demo is `dotnet test tests/RimBank.Core.Tests --filter "FullyQualifiedName~Retrieval"`, with every projection and handler test green.
- [x] Reading check: the view, projection, query, handler and port names match ADR 0024 and tech-stack §3.4 (with the `I` prefix from ADR 0012). The projection never reads a clock. The handler never touches `IPartyDirectory` or the aggregate.

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 1.6 ticked in the same PR
- [x] No ADR needed (no new error code, event, view or deviation from the tech stack); tech-stack §3.4 tree updated
- [x] No Marten, HTTP or MCP code added (those belong to 1.7 and 1.16); `doc-0/` untouched
- [x] No PII added: the view carries only the party type, dates, ids and version
