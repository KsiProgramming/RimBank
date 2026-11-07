# Party Reference Data Directory: Retrieve flow (query side)

Roadmap phase: 1.6
Branch: plan/1.6-party-retrieve-query-side

## Context
1.2–1.5 delivered the whole Register command side: the entry aggregate and `PartyRegistered`, the `RegisterParty` handler and `IPartyDirectory` write port, their Marten implementation, and `POST /PartyReferenceDataDirectory/Register`. This phase adds the context's first **read side**, in the core only: the entry's view, its pure projection, the `RetrievePartyReferenceDataDirectoryEntry` query and handler, and the read port, proven against an in-memory test adapter. It unblocks 1.7 (inline Marten projection, Marten read port and `GET /PartyReferenceDataDirectory/{id}/Retrieve` with an `ETag`) and 1.8 (Update, whose events this projection will follow).

It follows mission principle 5 (a query reads a read model and never changes state), tech-stack §8.2 (a query handler reads views through the read port and returns `Result<TView>`, `NotFound` becoming a 404), §8.3 (the entry view is a single-stream **inline** view, wired in 1.7), §4 CQRS rules (the query handler uses only the read port and views; the projection is pure and uses no `TimeProvider`), ADR 0010 (`IProjection<TView, TEvent>`, `ProjectionSpecification` test helper), ADR 0012 (role markers `IQuery<TView>` and `IReadPort`; ports named with an `I`) and [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md) (view name, the queries it serves, `party.party-not-found`, the `ETag` as the entry's stream version). BIAN source: `PartyReferenceDataDirectoryEntry/Retrieve` in [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) (response `RetrievePartyReferenceDataDirectoryEntry`: `PartyReference`, `DirectoryEntryDateType`, `DirectoryEntryDate`, `PartyType`).

Wolverine already discovers the core's handlers for Party (`AddServiceDomainStore`, 1.4) and generates handler code dynamically on first use (`TypeLoadMode.Dynamic`). So the new query handler, whose read port has no implementation until 1.7, does not break the host's startup or the Register flow. Nothing in the host dispatches the query until 1.7.

**CQRS split:** **read side only**. One query (`RetrievePartyReferenceDataDirectoryEntry`), one query handler, one read port (`IPartyDirectoryEnquiry`), one view and its projection. The view's lifecycle is **inline** (ADR 0024), wired with `AddInlineView` in 1.7. No command, write port or aggregate change.

## Scope
### In scope
- `Retrieval/PartyReferenceDataDirectoryEntryView.cs`: `sealed record PartyReferenceDataDirectoryEntryView(Guid Id, long Version, PartyType PartyType, DateTimeOffset OpenDate, DemographicsId DemographicsId, PartyReferenceId PartyReferenceId, BankRelationsId BankRelationsId)`.
  - `Id` is the entry's stream id as a `Guid`, because the generic `ViewProjector` (ADR 0016) projects a view with a `Guid Id`. The typed `EntryId` is a computed property (`PartyReferenceDataDirectoryEntryId.From(Id)`) that is not serialised.
  - `Version` is the entry's stream version, projected purely: 1 on `PartyRegistered`, +1 on each later entry event. 1.7 returns it as the `ETag`, and its integration test checks it against Marten's stream version.
  - The BQ ids are in the view so that the BQ Retrieve queries (1.9, 1.10, 1.12) can check `party.behavior-qualifier-not-found` against it (ADR 0024).
- `Retrieval/PartyReferenceDataDirectoryEntryViewProjection.cs`: `sealed class … : IProjection<PartyReferenceDataDirectoryEntryView, PartyReferenceDataDirectoryEntryEvent>`, with a switch expression: `PartyRegistered` creates the view at version 1, and any other entry event returns the view with `Version + 1` (none exist yet; 1.8 adds the first). An event before registration leaves no view.
- `Retrieval/RetrievePartyReferenceDataDirectoryEntry.cs`: `sealed record RetrievePartyReferenceDataDirectoryEntry(PartyReferenceDataDirectoryEntryId EntryId) : IQuery<PartyReferenceDataDirectoryEntryView>`.
- `Retrieval/RetrievePartyReferenceDataDirectoryEntryHandler.cs`: a static class with `HandleAsync(RetrievePartyReferenceDataDirectoryEntry, IPartyDirectoryEnquiry, CancellationToken)` returning `Task<Result<PartyReferenceDataDirectoryEntryView>>`. A missing view is `party.party-not-found` (`ErrorKind.NotFound`).
- `IPartyDirectoryEnquiry.cs` at the context root (`: IReadPort`), with only what this query needs: `Task<PartyReferenceDataDirectoryEntryView?> FindEntryAsync(PartyReferenceDataDirectoryEntryId entryId, CancellationToken cancellationToken)`.
- `PartyReferenceDataDirectoryEntryErrors.PartyNotFound` (`party.party-not-found`, already listed in ADR 0024). It sits next to the entry's other errors because 1.8's commands reuse it. It is a static class, not the aggregate, so the query handler may use it.
- In-memory test adapter `InMemoryPartyDirectoryEnquiry` in `tests/RimBank.Core.Tests/Party/PartyReferenceDataDirectory/`. It is seeded with **events** and folds them through the real projection, so handler tests read the same view a rebuild would.
- tech-stack §3.4 tree: `PartyDirectoryEnquiry.cs` → `IPartyDirectoryEnquiry.cs` (ADR 0012 naming, as 1.3 did for `IPartyDirectory`).

### Out of scope
- `AddInlineView`, the Marten `IPartyDirectoryEnquiry`, `RebuildViewAsync` coverage, the `GET …/Retrieve` endpoint, its response model and the `ETag` header: **1.7**.
- Mapping BIAN's `PartyReference` (ISO 20022 `Party`) in the response: it is Demographics/Reference data (ADR 0024), so **1.7** returns it empty or omitted, and **1.9/1.10** fill it.
- Update events and their projection cases: **1.8**. BQ sections of the view and the BQ Retrieve queries: **1.9–1.12**. Association view: **1.11**.
- Authorisation policies: **1.15**. MCP Retrieve tool: **1.16**.
- An FsCheck projection/aggregate agreement property and an explicit host start-up test: not chosen in the planning interview. With a single event type, the example-based projection tests cover the same ground.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| View fields | `Id`, `Version`, party type, open date, three BQ ids | Chosen in the planning interview. The CR fields BIAN's Retrieve returns, the BQ ids that ADR 0024 checks on BQ queries, and the version 1.7 needs for the `ETag` |
| View id type | `Guid Id` plus a computed typed `EntryId` | `ViewProjector` is a `SingleStreamProjection<TView, Guid>`. Keeping `Guid` avoids changing the shared Marten adapter, and the core still exposes the typed id |
| Version source | Projected purely by the view | Chosen in the planning interview. It keeps the read port a plain `FindEntryAsync` with no Marten metadata, and the projection stays testable without a database. Every entry event is a `PartyReferenceDataDirectoryEntryEvent`, so each one reaches `Project` and the count equals the stream version. 1.7's integration test pins this |
| Read port name and surface | `IPartyDirectoryEnquiry.FindEntryAsync` only | ADR 0012 naming. No speculative methods (mission non-goals); 1.11 adds the association lookup |
| Not found | Handler returns `party.party-not-found` | ADR 0024 error table; `RimBank.Problems` already maps `NotFound` to 404 |
| In-memory adapter | Seeded with events, folded through the projection | Handler tests then exercise the real projection, as a rebuild would |
| Build order | Test first: view + projection → port, adapter, query, handler → docs | Chosen in the planning interview |

## ADRs to write
- None. ADR 0024 already names the view, query, read port, inline lifecycle and `party.party-not-found`. Its text says `PartyDirectoryEnquiry`; the interface is `IPartyDirectoryEnquiry` per ADR 0012, as 1.3 did for `PartyDirectory` → `IPartyDirectory`, and the ADR is left as is.

## Open questions
- None.
