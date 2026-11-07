# Validation: Party Reference Data Directory: Retrieve adapters

## Done when (from roadmap)
> **1.7 Retrieve adapters:** inline Marten projection implementing `PartyDirectoryEnquiry`, and `GET /PartyReferenceDataDirectory/{id}/Retrieve` returning an `ETag` (404 returns Problem Details).
> + Definition of done (roadmap.md)

Some Definition-of-done items are covered elsewhere. There is no new projection logic (1.6's projection unit tests stay as they are). The MCP tool and its parity test come in 1.16. "Rebuildable from the event store" is proven here against a real PostgreSQL with `RebuildViewAsync`.

## Automated checks
- [ ] **Read port wiring** (`RimBank.Postgresql.Tests`, `ReadPortRegistrationTests`): a fake context's query handler reads its inline view through a port registered with `AddReadPort`, outside a transaction.
- [ ] **Marten read side** (`RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests`, Testcontainers):
  - unknown entry → `null`;
  - read-your-write after `RegisterParty` through the bus;
  - view `Version` = Marten stream version;
  - `RebuildViewAsync` recreates the view;
  - the query through `IMessageBus` returns the view, or `party.party-not-found`.
- [ ] **Entity tags** (`RimBank.Http.Tests`, `StreamVersionTagTests`): strong format; `If-None-Match` matches an exact tag, a weak one, a list and `*`, and nothing else.
- [ ] **HTTP** (`RimBank.Party.PartyReferenceDataDirectory.Api.Tests`, Alba + Testcontainers):
  - Register then Retrieve → 200 with the same fields and the same `ETag` as Register;
  - unknown id → 404 `application/problem+json` `party.party-not-found`;
  - malformed, empty or non-v7 id → 400 `shared.invalid-identifier` with `errors["partyreferencedatadirectoryid"]`;
  - matching `If-None-Match` or `*` → 304 with the `ETag` and no body, and a stale tag → 200;
  - no token → 401 Problem Details;
  - the existing Register tests are unchanged and green.
- [ ] **Conformance** (existing test): the mapped routes, now including `GET …/{partyreferencedatadirectoryid}/Retrieve`, conform to the BIAN YAML.
- [ ] **Architecture tests** green. The endpoint dispatches only through `IMessageBus`. `.Api` does not reference `.Postgresql`. `MartenPartyDirectoryEnquiry` stays in `.Postgresql`. The query handler still uses only the read port and the view.
- [ ] `RimBank.Host.Tests` green: the host starts with the read port, and Register is unchanged.
- [ ] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL: green in CI.

## Manual / demo checks
- [ ] The demo is the Alba suite: `dotnet test tests/Party/RimBank.Party.PartyReferenceDataDirectory.Api.Tests --filter "FullyQualifiedName~Retrieval"`, with every case above green.
- [ ] Reading check: the response is built only from the view, never from the write port or the aggregate. The 404 and 400 bodies are `application/problem+json` with `type`, `errorCode` and `traceId`. The route parameter is named `partyreferencedatadirectoryid`, as in BIAN.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.7 ticked in the same PR
- [ ] ADR 0024 (Retrieve request) and ADR 0016 (`AddReadPort`) amended; no new ADR needed
- [ ] tech-stack §3.3 comments updated
- [ ] The PR notes the one-off `RebuildViewAsync` for development databases with entries from before 1.7
- [ ] No PII in logs, and no new PII returned (`PartyReference` stays omitted until 1.9/1.10)
- [ ] `doc-0/` untouched
