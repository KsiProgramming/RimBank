# Party Reference Data Directory: Retrieve adapters

Roadmap phase: 1.7
Branch: plan/1.7-party-retrieve-adapters

## Context
1.6 delivered the read side in the core only: `PartyReferenceDataDirectoryEntryView`, its pure projection, the `RetrievePartyReferenceDataDirectoryEntry` query and handler, and the `IPartyDirectoryEnquiry` read port, proven against an in-memory adapter. Nothing projects the view in PostgreSQL yet, nothing implements the port, and no route dispatches the query. This phase adds the two adapters: the **inline Marten view** with a Marten `IPartyDirectoryEnquiry` in `.Postgresql`, and `GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve` in `.Api`, returning the entry's version as `ETag`. It unblocks 1.8 (Update sends that `ETag` back in `If-Match`), 1.9–1.12 (BQ Retrieve queries read the same view), 1.14 (conformance of the subset) and 1.16 (MCP Retrieve tool, parity with this endpoint).

Drivers: mission principles 5 (a query reads a read model and never changes state), 8 (Problem Details everywhere) and 9; tech-stack §4 (`.Postgresql` implements the read port over projected documents; `.Api` dispatches through `IMessageBus`), §5 (BIAN route, `Retrieve` is `GET`, query responses come only from read models, stream version as `ETag`, Problem Details), §8.2–8.3 (single-stream view read right after a command → **inline**, wired with `AddInlineView`, rebuilt with `RebuildViewAsync`), §9 (Testcontainers, Alba); [ADR 0016](../../docs/adr/0016-per-context-marten-stores.md), [ADR 0017](../../docs/adr/0017-wolverine-wiring.md), [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md). BIAN source: `PartyReferenceDataDirectoryEntry/Retrieve` in [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) (responses 200, 400, 401, 403, 404, 429, 500; body `RetrievePartyReferenceDataDirectoryEntry`: `PartyReference`, `DirectoryEntryDateType`, `DirectoryEntryDate`, `PartyType`).

**CQRS split:** **read side only**. The endpoint dispatches the existing query with `bus.InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>` and answers only from the view. The view `PartyReferenceDataDirectoryEntryView` is **inline** (same transaction as the entry's events, ADR 0024), so a Retrieve right after Register sees it. No command, write port, aggregate or core change.

## Scope
### In scope
- **`RimBank.Postgresql`: `AddReadPort<TPort, TImplementation>`** (new `ReadPortRegistration`), the read-side twin of `AddWritePort`: requires `TPort : IReadPort` and an implementation whose only constructor parameter is the context's store, registers it as a singleton (it holds no state; the store is a singleton too), and tells Wolverine's code generation about both assemblies. A handler that takes a read port gets no transaction, no `IDocumentSession` and no idempotency policy (that policy already applies only to `ICommand`).
- **`.Postgresql`:**
  - `PartyReferenceDataDirectoryPostgresql.Configure` adds `options.AddInlineView<PartyReferenceDataDirectoryEntryView, PartyReferenceDataDirectoryEntryViewProjection, PartyReferenceDataDirectoryEntryEvent>()`.
  - `MartenPartyDirectoryEnquiry(IPartyReferenceDataDirectoryStore store) : IPartyDirectoryEnquiry`: opens a query session on the context's own store and loads the view by the entry's Guid. It only maps the typed id and holds no logic.
  - `AddPartyReferenceDataDirectoryPostgresql` registers it with `AddReadPort`.
- **`RimBank.Http`: stream-version entity tags** (new `StreamVersionTag`): format a version as a strong `ETag` (`"<version>"`), and check `If-None-Match` against it (RFC 9110: weak comparison, a list of tags, or `*`). `RegisterPartyEndpoint` switches to it (no behaviour change). 1.8 adds `If-Match` to the same type.
- **`.Api`:**
  - `Retrieval/RetrievePartyReferenceDataDirectoryEntryEndpoint`: `GET /PartyReferenceDataDirectory/{partyreferencedatadirectoryid}/Retrieve`, `WithName("PartyReferenceDataDirectoryEntry/Retrieve")`, the BIAN summary, no `Idempotency-Key`.
    - The route value is bound as a `string` and parsed with `PartyReferenceDataDirectoryEntryId.TryCreate`. A non-GUID, empty or non-v7 value is **400** `shared.invalid-identifier` with `errors["partyreferencedatadirectoryid"]`.
    - An unknown entry is **404** `party.party-not-found` (from the handler, through `error.ToHttpResult()`).
    - Success is **200** with the response and `ETag: "<view.Version>"`. When `If-None-Match` matches, it is **304 Not Modified** with the `ETag` and no body.
  - `Retrieval/RetrievePartyReferenceDataDirectoryEntryResponse`: BIAN fields `PartyType`, `DirectoryEntryDateType` (`OpenDate`), `DirectoryEntryDate`, plus `PartyReferenceDataDirectoryEntryId`, `DemographicsId`, `ReferenceId` and `BankRelationsId` (plain GUIDs), mapped from the view. Same names and shape as the Register response (1.5). `PartyReference` is omitted until 1.9/1.10 store it.
  - `PartyReferenceDataDirectoryApi` maps it.
- Conformance test: `_implemented` gains the Retrieve operation id (the existing test fails otherwise).
- Docs: ADR 0024 gets a "Retrieve request (1.7)" paragraph. ADR 0016 gets a short `AddReadPort` note. The tech-stack §3.3 tree comments for `RimBank.Http` and `RimBank.Postgresql` are updated.

### Out of scope
- `If-Match`, `party.version-conflict` and the Update command: **1.8**.
- `PartyReference` in the response (names, identifiers): **1.9 / 1.10**.
- Full request and response shape conformance: **1.14**. Per-operation authorisation and scopes: **1.15** (until then, any authenticated caller). MCP Retrieve tool and parity: **1.16**.
- `Cache-Control` headers, the OpenAPI document test for Retrieve, extra architecture pins and a `.http` demo: not chosen in the planning interview.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Read port wiring | New generic `AddReadPort<TPort, TImplementation>` in `RimBank.Postgresql`: a singleton whose only constructor parameter is the context's store, opening a **query session** per read | Chosen in the interview. Mirrors `AddWritePort` (ADR 0017) without its transaction. A query never needs the handler's write session or outbox, and `RimBank.Postgresql` is where per-context Marten conventions live (ADR 0016) |
| View lifecycle | Inline, through `AddInlineView` | ADR 0024 and tech-stack §8.3: callers read the entry right after Register |
| Malformed id | 400 with the existing `shared.invalid-identifier` and `errors["partyreferencedatadirectoryid"]` | Chosen in the interview (BIAN declares 400). `TypedId.TryCreate(string)` already rejects non-GUID, empty and non-v7 values with this code, so no new error code is needed. A `{id:guid}` constraint would turn it into a 404 instead |
| Not found | 404 `party.party-not-found` from the handler | ADR 0024 error table, already mapped by `RimBank.Problems` |
| Response body | BIAN Retrieve fields + entry and BQ ids, as in the Register response | One shape for clients across Register and Retrieve. The BQ ids are the route values of 1.9–1.12. `PartyReference` is omitted until it is stored |
| `ETag` | Strong `"<version>"`, from the view's projected `Version` | tech-stack §5. 1.6 made the version a pure projection, and this phase's integration test pins it to Marten's stream version |
| Conditional GET | `If-None-Match` matching (weak comparison, list or `*`) → 304, with the `ETag` and no body | Chosen in the interview. Cheap, standard (RFC 9110), and saves clients a body when nothing changed |
| Entity-tag helper | `StreamVersionTag` in `RimBank.Http`, also used by Register | Every `.Api` formats and compares versions the same way. 1.8 adds `If-Match` beside it. `RimBank.Http` may use ASP.NET Core (tech-stack §4) |
| Existing databases | No migration; a development database with entries registered before 1.7 is rebuilt once with `RebuildViewAsync` (noted in the PR) | tech-stack §8.3: a new view ships with its rebuild step. There is no deployed environment yet |
| Work order | Adapter-out, test first, three groups: Postgres read side → HTTP endpoint → docs | Chosen in the interview |

## ADRs to write
- None new. **Amend ADR 0024** with a "Retrieve request (1.7)" paragraph: the route value is validated with `shared.invalid-identifier`, the response shape (BIAN fields + ids, no `PartyReference` yet), `ETag` and `If-None-Match` → 304. **Amend ADR 0016** with the `AddReadPort` convention (a read port reads through a query session of the context's store, with no transaction).

## Open questions
- None.
