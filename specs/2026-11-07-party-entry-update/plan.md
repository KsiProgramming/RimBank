# Plan: Party Reference Data Directory: Update

Every group is written test first. After each group, the solution builds and its tests pass (one commit per group).
- Test namespaces end with `.Tests`, and context namespaces stay flat (tech-stack §3.3, ADR 0026).
- Use switch expressions only (IDE0066).
- The Testcontainers and Alba tests need Docker. If it is not available locally, they run in CI.

## 1. Core: Update decision, command and projection
- [ ] Tests first in `RimBank.Core.Tests/Party/PartyReferenceDataDirectory/` (namespace `RimBank.Party.PartyReferenceDataDirectory.Tests`):
  - `PartyReferenceDataDirectoryEntry/UpdateTests` (Given / When / Then):
    - a registered entry with a new type → `PartyReferenceDataDirectoryEntryUpdated` with that type and the refresh date;
    - with no type → the current type and the refresh date;
    - the same type → the event is still appended;
    - a not-registered entry → `party.party-not-found`;
    - an undefined `PartyType` value → `party.party-type-invalid`.
  - `PartyReferenceDataDirectoryEntryTests`: `Evolve` sets `PartyType` and `RefreshDate` and keeps the ids and `OpenDate`. Extend `EntryExamples` with an Update.
  - `PartyReferenceDataDirectoryEntryLawsProperties`: the generators also produce Updates after Register, and the existing laws still hold.
  - `PartyTypesTests`: `FromOptionalBianName`:
    - `null` → keep (no value);
    - `Person` / `Organisation` → the type;
    - blank, another case or unknown → `party.party-type-invalid`.
  - `Update/UpdatePartyReferenceDataDirectoryEntryHandlerTests` (with `InMemoryPartyDirectory` and `FakeTimeProvider`):
    - success → an acknowledgement with version 2, the new type, `RefreshDate` = now and the BQ ids, and the port holds 2 events;
    - an expected version of 0 or 3 against version 1 → `party.version-conflict` and nothing appended;
    - an unknown entry → `party.party-not-found`, checked before the version;
    - an invalid type → nothing appended.
  - `Retrieval/PartyReferenceDataDirectoryEntryViewProjectionTests`: Given `PartyRegistered` + `PartyReferenceDataDirectoryEntryUpdated` → Then version 2, the new type and `RefreshDate`. An Update with no view leaves no view.
  - `InMemoryPartyDirectoryTests`: `AppendToEntry` with the right expected version appends; with a wrong one it throws, as Marten does.
- [ ] `PartyReferenceDataDirectoryEntry/Events.cs`: `PartyReferenceDataDirectoryEntryUpdated(EntryId, PartyType, RefreshDate)`.
- [ ] `PartyReferenceDataDirectoryEntry.cs`: add a `RefreshDate` (`DateTimeOffset?`) member and an `Evolve` case.
- [ ] `Decisions.cs`: the decider input `UpdatePartyReferenceDataDirectoryEntryDetails(PartyType? PartyType, DateTimeOffset RefreshDate)` and its `Decide` cases. Not registered → `PartyNotFound`; an undefined type → `PartyTypeInvalid`; otherwise accept with `PartyType ?? state.PartyType`.
- [ ] `PartyReferenceDataDirectoryEntryErrors.VersionConflict`: `Error.Conflict("party.version-conflict", …)`.
- [ ] `PartyTypes.FromOptionalBianName(string?)` → `Result<PartyType?>`.
- [ ] `Update/UpdatePartyReferenceDataDirectoryEntry.cs` (`ICommand`: `EntryId`, `ExpectedVersion` (`long`), `PartyType?`, `IdempotencyKey`).
- [ ] `Update/UpdatePartyReferenceDataDirectoryEntryAcknowledged.cs` (id, version, party type, open date, refresh date, BQ ids).
- [ ] `Update/UpdatePartyReferenceDataDirectoryEntryHandler.cs`. The steps, in order:
  1. Load the stream.
  2. Not registered → not found.
  3. `history.Count != ExpectedVersion` → `VersionConflict`.
  4. `Decide` with `time.GetUtcNow()`.
  5. `AppendToEntry(id, ExpectedVersion, events)`.
  6. Acknowledge from the evolved state.
- [ ] `IPartyDirectory.AppendToEntry(entryId, expectedVersion, events)`. Update the remarks ("Update (1.8) adds…").
- [ ] `InMemoryPartyDirectory.AppendToEntry` (test adapter) checks the expected version.
- [ ] View: add `RefreshDate?` to `PartyReferenceDataDirectoryEntryView`, and handle the event in `PartyReferenceDataDirectoryEntryViewProjection`.
- [ ] Run `RimBank.Core.Tests` and `RimBank.Architecture.Tests`. The handler must use only the write port, which the CQRS rules check.

## 2. PostgreSQL write side and the race
- [ ] Tests first in `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (Testcontainers):
  - `MartenPartyDirectoryTests`: `AppendToEntry` with the right expected version commits event 2. With a stale one, the commit fails with Marten's `ConcurrencyException`.
  - `UpdatePartyRoundTripTests` (through `IMessageBus`):
    - Register → Update with version 1 → the acknowledgement has version 2, and `FindEntryAsync` right after returns the updated view (inline, same transaction);
    - the view's `Version` is Marten's stream version;
    - after deleting the view document, `RebuildViewAsync` recreates an equal view;
    - an Update with version 1 again → `party.version-conflict`;
    - an unknown entry → `party.party-not-found`.
  - **Race:**
    - five concurrent Updates with expected version 1 and distinct idempotency keys → exactly one succeeds, four return `party.version-conflict` (none throws), and the stream ends at version 2;
    - a replay of the winner's key returns its first acknowledgement.
- [ ] `MartenPartyDirectory.AppendToEntry`: `session.Events.Append(entryId.Value, expectedVersion + events.Count, [.. events])`.
- [ ] `RimBank.Postgresql` (Wolverine wiring, ADR 0017): a policy on command chains, `OnException<Marten.Exceptions.ConcurrencyException>().RetryTimes(1)`. Test it with a fake context in `RimBank.Postgresql.Tests`: a command whose first commit conflicts is retried once, and its handler's `Result` is returned.
- [ ] Run `RimBank.Postgresql.Tests`, the Party `.Postgresql.Tests` and `RimBank.Host.Tests`.

## 3. HTTP Update endpoint
- [ ] SharedKernel: add `ErrorKind.PreconditionRequired`. `RimBank.Problems.ErrorStatus` maps it to 428. Extend the SharedKernel and Problems tests.
- [ ] Tests first in `RimBank.Api.Tests` (`StreamVersionTagTests`): `ReadIfMatch`:
  - `"2"` → 2;
  - missing → `shared.if-match-required` (428);
  - `W/"2"`, `*`, `"1", "2"`, `"abc"`, `"0"` and garbage → `shared.if-match-invalid` (400).
- [ ] `src/RimBank.Api/StreamVersionTag.cs`: `ReadIfMatch(HttpRequest) → Result<long>`. Its errors sit in `RimBank.Api`, next to the Idempotency-Key errors. Update the class docs, which no longer say "Commands will read it back (roadmap 1.8)".
- [ ] Tests first in `RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Update/`:
  - `UpdatePartyReferenceDataDirectoryEntryRequestTests`: BIAN field names; `PartyType` is optional; `PartyReference` and the dates are ignored.
  - `UpdatePartyReferenceDataDirectoryEntryResponseTests`: mapping from the acknowledgement; the JSON names; `RefreshDate`.
  - `UpdatePartyReferenceDataDirectoryEntryEndpointTests` (Alba, Testcontainers, test-signed JWTs). Each case starts with Register, then sends `PUT …/Update`:
    - `If-Match: "1"` and `{"PartyType":"Organisation"}` → 200, `ETag` `"2"`, and the body shows `Organisation` and `RefreshDate`; then GET Retrieve → the same type and `RefreshDate`, with `ETag` `"2"`;
    - an empty body `{}` → 200, with the type kept;
    - a stale `If-Match: "1"` after that → 409 `party.version-conflict`;
    - no `If-Match` → 428 `shared.if-match-required`;
    - `If-Match: W/"1"` → 400 `shared.if-match-invalid`;
    - `{"PartyType":"person"}` → 400 `party.party-type-invalid`;
    - an unknown v7 id → 404 `party.party-not-found`;
    - `not-a-guid` → 400 `shared.invalid-identifier`;
    - no `Idempotency-Key` → 400;
    - the same key and request → the same 200 and `ETag`, with no second event;
    - the same key with another `If-Match` → 409;
    - no token → 401.
    - Every error is `application/problem+json`.
  - `RetrievePartyReferenceDataDirectoryEntryResponseTests`: `RefreshDate` is omitted when `null`.
- [ ] `src/RimBank.Api/Party/PartyReferenceDataDirectory/Update/`:
  - `UpdatePartyReferenceDataDirectoryEntryRequest.cs` (BIAN names, flattened enum strings as in 1.5);
  - `UpdatePartyReferenceDataDirectoryEntryResponse.cs` (`From(acknowledgement)`);
  - `UpdatePartyReferenceDataDirectoryEntryEndpoint.cs`:
    - `Route`, `OperationId = "PartyReferenceDataDirectoryEntry/Update"`, `MapPut`, `RequireIdempotencyKey()`, `WithTags`, the BIAN summary, a description (`If-Match` and the ignored fields), `Produces<…Response>(200)`, `ProducesValidationProblem()`, `ProducesProblem(401/404/409/428/500)`;
    - its handler parses the id, then `ReadIfMatch`, then `FromOptionalBianName`, then `InvokeAsync<Result<…Acknowledged>>`, then answers 200 with the `ETag`, or `error.ToHttpResult()`.
- [ ] `RetrievePartyReferenceDataDirectoryEntryResponse`: add `RefreshDate` (`JsonIgnore(WhenWritingNull)`).
- [ ] `PartyReferenceDataDirectoryApi` maps the endpoint. Add the Update operation to `PartyReferenceDataDirectoryConformanceTests._implemented`.
- [ ] Run `RimBank.Api.Tests`, `RimBank.Problems.Tests`, `RimBank.SharedKernel.Tests`, `RimBank.Architecture.Tests` (`adapters-dispatch-through-the-message-bus`, `http-dependencies`) and `RimBank.Host.Tests`.

## 4. Docs and roadmap
- [ ] New `docs/adr/0027-optimistic-concurrency-for-commands.md`, using `docs/adr/template.md`.
- [ ] ADR 0024:
  - an "Update request (1.8)" paragraph (what changes, ignored fields, `RefreshDate`, response, `ETag`, `If-Match` rules, and the spec's 200 → `BadRequest` defect);
  - the event name confirmed;
  - `shared.if-match-required` / `shared.if-match-invalid` in the error table.
- [ ] ADR 0006: `PreconditionRequired`. ADR 0015: the 428 mapping. ADR 0017: the retry policy on command chains.
- [ ] Tech-stack §5: the `If-Match` bullet (required for commands on existing records; 428/400/409; MCP takes `expectedVersion`). Tech-stack §3.4 tree: the `Update/` flow.
- [ ] `IPartyDirectory` and `StreamVersionTag` XML docs are up to date.
- [ ] Add a `.http` request for Update next to the existing ones, if the repo has any (otherwise skip it).
- [ ] Roadmap: tick **1.8**.
- [ ] Run a locked restore, `dotnet build -c Release`, `dotnet test --solution RimBank.slnx` and `dotnet format --verify-no-changes`.
