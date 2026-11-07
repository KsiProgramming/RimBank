# Plan: Party Reference Data Directory: Retrieve adapters

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). Test namespaces end with `.Tests` (tech-stack §3.3). Use switch expressions only (IDE0066). The Testcontainers and Alba tests need Docker; if it is not available locally, they run in CI.

## 1. Read port wiring and the inline view (Postgres read side)
- [x] Tests first in `RimBank.Postgresql.Tests` (`ReadPortRegistrationTests`, with an existing test-only fake context): a query handler that takes a fake read port, registered with `AddReadPort`, reads an inline view right after a command through `IMessageBus`; the chain is not transactional while the command chain is; the port is one instance for the host; an implementation over a session or over a bare `IDocumentStore` is refused. The fake ledger switches from a hand-made `AddSingleton` to `AddReadPort`.
- [x] `src/RimBank.Postgresql/ReadPortRegistration.cs`: `AddReadPort<TPort, TImplementation>(this IHostApplicationBuilder)`, where `TPort : class, IReadPort` and `TImplementation : class, TPort`. It requires a constructor that takes only the context's store (an `IDocumentStore`-derived interface, never a session or the bare `IDocumentStore`), registers `TImplementation` as a singleton `TPort` and references both assemblies in Wolverine code generation. The XML docs contrast it with `AddWritePort` (query session, no transaction, no outbox).
- [x] Tests first in `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests` (`MartenPartyDirectoryEnquiryTests`, namespace `RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests`):
  - an unknown entry → `null`;
  - after `RegisterParty` through the bus, `FindEntryAsync` returns the view (read-your-write: inline, same transaction) with the acknowledged id, party type, open date and BQ ids;
  - the view's `Version` equals Marten's stream version (`FetchStreamStateAsync`);
  - after deleting the view document, `RebuildViewAsync(ProjectionName<PartyReferenceDataDirectoryEntryViewProjection>())` recreates an equal view;
  - `RetrievePartyReferenceDataDirectoryEntry` through `bus.InvokeAsync<Result<…View>>` returns the view, and an unknown id returns `party.party-not-found`.
- [x] `PartyReferenceDataDirectoryPostgresql.Configure`: `options.AddInlineView<PartyReferenceDataDirectoryEntryView, PartyReferenceDataDirectoryEntryViewProjection, PartyReferenceDataDirectoryEntryEvent>()`.
- [x] `MartenPartyDirectoryEnquiry.cs`: `sealed class MartenPartyDirectoryEnquiry(IPartyReferenceDataDirectoryStore store) : IPartyDirectoryEnquiry`. `FindEntryAsync` opens `store.QuerySession()` (disposed) and calls `LoadAsync<PartyReferenceDataDirectoryEntryView>(entryId.Value)`.
- [x] `AddPartyReferenceDataDirectoryPostgresql`: `builder.AddReadPort<IPartyDirectoryEnquiry, MartenPartyDirectoryEnquiry>()`; update its XML doc ("store, write port and read port").
- [x] Run `RimBank.Architecture.Tests` and `RimBank.Host.Tests` (the host starts, Register still works).

## 2. HTTP Retrieve endpoint
- [x] Tests first in `RimBank.Http.Tests` (`StreamVersionTagTests`): `Format(3)` is `"3"` (quoted, strong); `If-None-Match` matches for `"3"`, `W/"3"`, `"1", "3"` and `*`; it does not match for `"2"`, a missing header or an unparsable one.
- [x] `src/RimBank.Http/StreamVersionTag.cs`: `Format(long version)` (1 or more), `SetOn(HttpResponse, long version)` and `IsNotModified(HttpRequest, long version)` (using `GetTypedHeaders().IfNoneMatch`; the `http-dependencies` rule now also allows `Microsoft.Net.Http.Headers`). Switch `RegisterPartyEndpoint` to `SetOn` (its tests stay green, unchanged).
- [x] Tests first in `RimBank.Party.PartyReferenceDataDirectory.Api.Tests`:
  - `Retrieval/RetrievePartyReferenceDataDirectoryEntryResponseTests`: mapping from a view (BIAN names, `OpenDate`, plain GUIDs, no `PartyReference`), and the JSON property names.
  - `Retrieval/RetrievePartyReferenceDataDirectoryEntryEndpointTests` (Alba, Testcontainers, test-signed JWTs):
    - Register, then GET → 200 `application/json`, a body equal to the Register response's fields, and the same `ETag` (`"1"`);
    - an unknown v7 id → 404 `application/problem+json`, `errorCode` `party.party-not-found`;
    - `not-a-guid`, an empty GUID and a v4 GUID → 400 `application/problem+json`, `shared.invalid-identifier`, `errors["partyreferencedatadirectoryid"]`;
    - `If-None-Match: "1"` → 304, the `ETag` and an empty body; `If-None-Match: "0"` → 200; `If-None-Match: *` → 304;
    - no `Idempotency-Key` is needed; no token → 401 Problem Details.
- [x] `src/Party/RimBank.Party.PartyReferenceDataDirectory.Api/Retrieval/RetrievePartyReferenceDataDirectoryEntryResponse.cs`: `[JsonPropertyName]` BIAN names, `From(PartyReferenceDataDirectoryEntryView)`.
- [x] `Retrieval/RetrievePartyReferenceDataDirectoryEntryEndpoint.cs`: `Route`, `OperationId` constants; `MapRetrievePartyReferenceDataDirectoryEntry` with `WithName`, `WithTags("PartyReferenceDataDirectoryEntry")`, the BIAN summary, a description (ETag, 304, PartyReference not yet returned); the invalid-id error is `RetrievePartyReferenceDataDirectoryEntryRequestErrors.EntryIdInvalid`, `Produces<…Response>(200)`, `Produces(304)`, `ProducesValidationProblem()`, `ProducesProblem(401/404/500)`. `HandleAsync(string partyreferencedatadirectoryid, IMessageBus bus, HttpContext http, CancellationToken)`: parse the id (an error becomes a validation problem with the field) → `InvokeAsync<Result<…View>>` → 304 / 200 with `ETag` / `error.ToHttpResult()`.
- [x] `PartyReferenceDataDirectoryApi.MapPartyReferenceDataDirectoryEndpoints` maps it.
- [x] `PartyReferenceDataDirectoryConformanceTests._implemented` adds `RetrievePartyReferenceDataDirectoryEntryEndpoint.OperationId`.
- [x] Run the Party `.Api.Tests`, `RimBank.Http.Tests`, `RimBank.Architecture.Tests` (`adapters-dispatch-through-the-message-bus`, `http-dependencies`) and `RimBank.Host.Tests`.

## 3. Docs and roadmap
- [ ] ADR 0024: "Retrieve request (1.7)" paragraph next to "Register request (1.5)".
- [ ] ADR 0016: the `AddReadPort` convention (query session of the context's store, singleton, no transaction).
- [ ] tech-stack §3.3 tree comments: `RimBank.Http` (Idempotency-Key header and stream-version entity tags), `RimBank.Postgresql` (adds read ports).
- [ ] `PartyReferenceDataDirectoryModule` XML doc: "store, write port and read port … its HTTP endpoints".
- [ ] PR note: a development database with entries from before 1.7 needs `RebuildViewAsync` once for `PartyReferenceDataDirectoryEntryViewProjection`.
- [ ] Roadmap: tick **1.7**.
- [ ] Run `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally.
