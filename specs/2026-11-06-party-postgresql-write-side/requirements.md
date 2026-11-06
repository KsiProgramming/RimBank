# Party Reference Data Directory: PostgreSQL adapter (write side)

Roadmap phase: 1.4
Branch: plan/1.4-party-postgresql-write-side

## Context
1.3 delivered `RegisterParty`, its handler and the write port `IPartyDirectory` (`LoadEntryAsync`, `StartEntry`), proven only against `InMemoryPartyDirectory`. This phase adds the first real `.Postgresql` adapter. It implements that port over Marten event streams and registers the Party store through the shared convention (`AddServiceDomainStore`, [ADR 0016](../../docs/adr/0016-per-context-marten-stores.md)). It also wires the context into the host, so `RegisterParty` can be dispatched through `IMessageBus` with Wolverine's transaction, outbox and idempotency ([ADR 0017](../../docs/adr/0017-wolverine-wiring.md), [ADR 0018](../../docs/adr/0018-idempotency-keys-for-commands.md)). ADR 0016 and ADR 0017 both say that the first real context (1.4) adds its connection string and the `Wolverine` connection string to the AppHost.

It unblocks 1.5 (`POST /PartyReferenceDataDirectory/Register`, which only has to map HTTP onto a command the host already handles) and 1.7 (the inline view registered on this same store).

Drivers: mission principles 2 (hexagonal: Marten only in the adapter), 4 (event-sourced truth) and 9 (production-grade: idempotency, real PostgreSQL tests); tech-stack §2 (own schema, role, store), §3.1/§3.3 (project names), §4 (`.Postgresql` may reference only its own core, SharedKernel, `RimBank.Postgresql`, Marten, Wolverine.Marten), §7 (`TimeProvider`) and §9 (Testcontainers); [ADR 0008](../../docs/adr/0008-typed-identifier-pattern.md) (each adapter registers `TypedIdJsonConverterFactory` once); [ADR 0014](../../docs/adr/0014-host-composition-and-module-entry-point.md) (module entry point); [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md) (the entry id is the stream id).

**CQRS split:** **write side only**. This phase adds the Marten implementation of the write port `IPartyDirectory` and makes the existing command `RegisterParty` dispatchable. It adds no query, read port, view or projection, so there is no inline or async read model yet. The inline entry view comes in 1.6 and 1.7.

## Scope
### In scope
- New project `src/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql/` (namespace `RimBank.Party.PartyReferenceDataDirectory.Postgresql`), referencing `RimBank.Core` and `RimBank.Postgresql`:
  - `IPartyReferenceDataDirectoryStore : IDocumentStore`, the store marker;
  - `MartenPartyDirectory(IDocumentSession session) : IPartyDirectory`:
    - `LoadEntryAsync` → `session.Events.FetchStreamAsync(entryId.Value)` mapped to `PartyReferenceDataDirectoryEntryEvent`s, oldest first, empty for an unknown stream;
    - `StartEntry` → `session.Events.StartStream(entryId.Value, events)` (staged; Wolverine's transaction commits it, and a duplicate id fails at commit);
  - `PartyReferenceDataDirectoryPostgresql.AddPartyReferenceDataDirectoryPostgresql(this IHostApplicationBuilder)`: calls `AddServiceDomainStore<IPartyReferenceDataDirectoryStore, Entry>("PartyReferenceDataDirectory", Configure)` and `AddWritePort<IPartyDirectory, MartenPartyDirectory>()`;
  - `Configure(StoreOptions)`: `MapEventType<PartyRegistered>("party_registered")` (stable snake_case names, never CLR names).
- **Typed ids and enums in Marten (shared convention fix):** `AddServiceDomainStore` registers `TypedIdJsonConverterFactory` on the store's System.Text.Json serializer, as ADR 0008 asked for 0.10, and stores enums as strings. This covers events and the stored `IdempotentOutcome` acks. The fake contexts in `RimBank.Postgresql.Tests` get a test proving it.
- Host wiring:
  - `src/RimBank.Host/Modules/Party/PartyReferenceDataDirectoryModule.cs` (`ServiceDomain = "PartyReferenceDataDirectory"`); `AddServices` calls only `AddPartyReferenceDataDirectoryPostgresql()`, and `AddMcpTools` and `MapEndpoints` stay empty;
  - `ServiceDomainModules.All = [new PartyReferenceDataDirectoryModule()]`;
  - `TimeProvider.System` registered once in the host (`TryAddSingleton`), so the handler's `TimeProvider` parameter resolves;
  - `RimBank.Host` references the new project.
- AppHost: the connection strings `PartyReferenceDataDirectory` (role `rimbank_party_reference_data_directory`) and `Wolverine`, with Aspire-generated password parameters kept in user secrets. Provisioning stays as in ADR 0016 (Development only).
- New test project `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Postgresql.Tests/` (Testcontainers, one database per test, like `RimBank.Postgresql.Tests`).
- Docs: the tech-stack §3.3 tree is unchanged (the paths already exist). Amend ADR 0016 with a short **Serialization** paragraph (typed-id converter, enums as strings) and update the "1.4 adds…" consequence lines in ADR 0016/0017 to past tense.

### Out of scope
- HTTP endpoint, request validation, the `Idempotency-Key` header and the ASP.NET Core JSON converter: **1.5**.
- The view, projection, read port (`PartyDirectoryEnquiry`) and `AddInlineView`: **1.6 / 1.7**.
- `AppendToEntry` with an expected version: **1.8**. `PartyAssociation` streams: **1.11**.
- Integration events and outbox publishing: **1.13**. MCP tools: **1.16**.
- Snapshots or stream archiving: not needed for one-event streams.
- Production role, schema provisioning and migrations: **7.5** (ADR 0016).
- A manual AppHost run is not a merge gate (not chosen in the interview); the bus round trip test covers the host path.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Phase breadth | Adapter + host module + AppHost connection strings | Chosen in the interview. ADR 0016/0017 assign the connection strings to 1.4, and it makes `RegisterParty` runnable end to end before the endpoint (1.5) |
| Stream identity | Marten `Guid` stream id = `PartyReferenceDataDirectoryEntryId.Value` | ADR 0024: the entry id is the stream id. Marten's default `StreamIdentity.AsGuid`, so no custom identity |
| Event type names | `MapEventType<PartyRegistered>("party_registered")` | Same convention as the fake contexts. A CLR rename never breaks stored events |
| Typed ids in JSON | `TypedIdJsonConverterFactory` registered once by `AddServiceDomainStore` for every context | ADR 0008 says each adapter registers it once. It is missing today, and a shared convention keeps contexts from drifting |
| Enums in JSON | Stored as strings (`Person`, `Organisation`) | Readable and stable against reordering of enum members. Set once in the shared convention |
| Port implementation | `sealed class MartenPartyDirectory(IDocumentSession)`, built by Wolverine's codegen (`AddWritePort`) | ADR 0017: the port uses the handler's own session, so events and the idempotency outcome commit together |
| Duplicate stream | Not translated; Marten's `ExistingStreamIdCollisionException` surfaces as an infrastructure fault | The handler already loads first and returns `party.party-already-registered`. A collision of a fresh Guid v7 is a race, an exception per tech-stack §7 |
| `TimeProvider` registration | `TimeProvider.System` in `RimBank.Host` | Composition-root concern (§4). Tests override it with `FixedTimeProvider` |
| Idempotency JSON | `IdempotencyGate` stores, replays and fingerprints with `StoredJson.Options` (typed-id converter, enum names) | Found while implementing: with default options a replayed `RegisterPartyAcknowledged` lost its typed ids (private constructor, get-only `Value`). The bus replay test pins it |
| Codegen references | `AddServiceDomainStore` and `AddWritePort` add the store marker's and the port implementation's assemblies to Wolverine's code generation | Found while implementing: the generated handler names types from the context's own `.Postgresql` assembly, which the fakes had hidden by sharing one assembly with their tests. Recorded in ADR 0017 |
| Host tests without a database | Host tests about the bus, telemetry and health build the host with no context (`HostWithoutContexts`) instead of `WebApplicationFactory<Program>` | `Program` now runs the Party context, which needs its database. The full `Program` is proven against real PostgreSQL by `AppHostStartupTests` |
| Work order | Test first, three groups: adapter → bus round trip → host, AppHost and docs | Chosen in the interview |

## ADRs to write
- None new. **Amend ADR 0016** with a short Serialization paragraph (typed-id converter and enums as strings in every store). This implements what ADR 0008 already decided, so a new ADR is not needed.

## Open questions
- None. (If adding the Party module makes a host or AppHost test that pins `ServiceDomainModules.All` or the AppHost's resource list fail, that test is updated in group 3. It is an expected change, not a new decision.)
