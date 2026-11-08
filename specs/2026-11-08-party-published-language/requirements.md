# Party Reference Data Directory: published language

Roadmap phase: 1.13
Branch: plan/1.13-party-published-language

## Context
Every Behavior Qualifier of Party Reference Data Directory now has a working write and read side (1.2–1.12, 1.18–1.21). Until now, nothing has left the context. 1.13 publishes the first **integration events** of RimBank: the two Control Record channels of the [BIAN AsyncAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/asyncapi-3.x/yamls/PartyReferenceDataDirectory.yaml), `PartyReferenceDataDirectoryEntry/Created` and `PartyReferenceDataDirectoryEntry/Updated`. Their payload is the CR schema: `PartyReference`, `PartyType`, `DirectoryEntryDateType` (`OpenDate` / `RefreshDate`), `DirectoryEntryDate`.

[ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md#integration-channels) already maps them: `PartyRegistered` → Created and `PartyReferenceDataDirectoryEntryUpdated` → Updated. It also defers the 8 BQ channels until a consumer needs them, and keeps their PII unpublished before 7.2. The conformance catalogue already lists both CR channels with phase `1.13`.

**Chosen in the interview:** full CR state plus a small envelope, versioned v1 through a stable message identity. The mapping is a pure mapper in core, and the command handlers cascade its result into Wolverine's Marten outbox.

It depends on 0.10 and 0.11 (Marten store, Wolverine outbox, durable local queue, ADR 0016–0017) and on 1.2–1.8 (Register and Update). It unblocks **4.1** (Current Account subscribes to these events to know that a party exists) and the AsyncAPI half of **1.14**.

Drivers:
- Mission principle 7: loosely coupled contexts; consumers own their copy, and no callbacks to read.
- Tech-stack §3 (`RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage` is its own project and BCL only; integration events only, never views or domain types).
- Tech-stack §4 dependency table (core uses only its own `.PublishedLanguage`).
- Tech-stack §7 (integration events are versioned and backwards compatible).
- Tech-stack §8.4 (mapped from domain events, never from read models; event-carried state; a contract test pins each schema).
- ADR 0017 (handler, then cascade, then `SaveChangesAsync`: events and outgoing envelopes commit in one transaction).

**CQRS split:**
- **Command side only.** `RegisterParty` and `UpdatePartyReferenceDataDirectoryEntry` still decide and append as today. When the decision accepts, they also return the mapped integration event as a cascaded message. A failed decision returns `null`, so nothing is published.
- **Query side:** unchanged. No view is read or added. Integration events are never built from `PartyReferenceDataDirectoryEntryView`.

## Scope
### In scope
- **New project `src/Party/RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage/`** (namespace `RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage`, BCL only, no package reference). It is added to `RimBank.slnx` and referenced by `RimBank.Core`, and it has its own `packages.lock.json`.
- **Integration events (sealed records, BCL types only):**
  - `PartyReferenceDataDirectoryEntryCreated`: `EventId` (Guid), `OccurredAt` (DateTimeOffset), `PartyReferenceDataDirectoryEntryId` (Guid), `Version` (long, the entry's stream version after this event), `PartyType` (string `Person` / `Organisation`), `DirectoryEntryDateType` (`OpenDate`), `DirectoryEntryDate` (the open date).
  - `PartyReferenceDataDirectoryEntryUpdated`: the same fields, with `DirectoryEntryDateType` `RefreshDate` and the refresh date.
  - Constants classes `PartyTypes` / `DirectoryEntryDateTypes` for the allowed strings. Strings, not enums, so a new value never breaks an older consumer's deserialisation.
  - No PII and no BQ ids, because the only known consumer (4.1) needs "this party exists, of this type". `OccurredAt` is the domain event's date, so the mapping stays pure. Each event gets a new Guid v7 `EventId`. Consumers deduplicate and order on (`PartyReferenceDataDirectoryEntryId`, `Version`).
- **Versioning:** each event has a stable message identity, `party-reference-data-directory-entry.created.v1` and `….updated.v1`. It is assigned in the adapter wiring, so `.PublishedLanguage` keeps no Wolverine attribute. Changes may only add optional fields. A breaking change is a new `…V2` type, published next to v1 until consumers move. The schema snapshot enforces this.
- **Pure mapper in core** (`RimBank.Core/Party/PartyReferenceDataDirectory/Publication/PartyReferenceDataDirectoryPublication.cs`, flat namespace per ADR 0026): `Created(PartyRegistered, version, eventId)` and `Updated(PartyReferenceDataDirectoryEntryUpdated, version, eventId)`. BQ events have no mapping, because no published channel exists for them yet (ADR 0024), so their handlers publish nothing.
- **Handlers:** `RegisterPartyHandler` and `UpdatePartyReferenceDataDirectoryEntryHandler` return `(Result<…Acknowledged>, <IntegrationEvent>?)`. This is the cascade pattern of the 0.11 fake ledger, with BCL types only and no Wolverine type in core. The event id comes from the handler's `TimeProvider` (`Guid.CreateVersion7`).
- **Wolverine:** each event declares `Channel` and `MessageIdentity` as constants. A shared naming strategy in `RimBank.Postgresql` (`PublishedLanguageMessageNaming`, registered by `AddServiceDomainStore`) gives any `*.PublishedLanguage` type its declared identity on the wire. The event goes through the party store's durable local queue: its envelope is in `party_reference_data_directory.wolverine_incoming_envelopes`, in the same transaction as the events (ADR 0017). With no subscriber in production yet, Wolverine logs it and discards it. That is accepted and documented: 4.1 adds the first consumer.
- **Schema contract:** a committed approved JSON schema per event (with its channel and identity), exported with `System.Text.Json.Schema.JsonSchemaExporter` using Wolverine's wire settings (camelCase), and compared in a test that also proves Wolverine writes the same names.
- **Catalogue:** `PartyReferenceDataDirectoryCatalogue` maps each CR channel to its CLR type, and the tests assert that each address exists in the AsyncAPI YAML.
- **Docs:**
  - ADR 0036 (new);
  - ADR 0024 integration-channel table: names the two types and identities, still in phase 1.13;
  - tech-stack §3 tree and §8.4 (link to ADR 0036);
  - CONTRIBUTING: a "changing a published event" note;
  - roadmap 1.13 ticked.

### Out of scope
- The 8 BQ channels (Demographics, Reference, Associations, BankRelations Created/Updated): deferred by ADR 0024 until a consumer needs them, and after the PII protection of 7.2.
- Any consumer, `.Subscriptions` project, inbox handling or local reference read model: 4.1–4.2.
- Full AsyncAPI conformance, including payload shape against the BIAN schema: 1.14.
- Authorisation of subscribers, and a RabbitMQ transport: 1.15, and context extraction (Stage 8+).
- Back-publishing events for entries registered before 1.13. No consumer exists yet, so there is nothing to replay. A later consumer rebuilds its copy from events published from now on, or an ADR in 4.1 decides a replay.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Payload | Full CR state (id, type, date type and date) plus `EventId`, `OccurredAt`, `Version` | Event-carried state (§8.4); the version lets 4.1 drop duplicates and out-of-order events; no PII |
| Value types | Strings with constants classes, not enums | Unknown future values must not break older consumers (4.1 "tolerant of unknown fields") |
| Versioning | Stable message identity `…created.v1` / `…updated.v1`, additive changes only, a breaking change means a new type | A C# rename must not change the wire contract; `.PublishedLanguage` stays BCL only |
| Where the mapping lives | Pure mapper in `RimBank.Core` (`Publication/`), called by the command handlers | Business mapping belongs in core (memory: business logic in core); testable without I/O; uses the decided events, never a view |
| How it reaches the outbox | Handler cascade `(Result<Ack>, Event?)` | Same transaction as the events (ADR 0017); a failed decision publishes nothing by construction; no Wolverine type in core |
| Contract test | `JsonSchemaExporter` (BCL) snapshot against a committed approved file | No new package (ADR 0013); a removed, renamed or retyped field fails |
| BQ channels | Not published | ADR 0024: no consumer yet, PII before 7.2 |

## ADRs to write
- **ADR 0036, Party published language:** the event shape and envelope fields, string values, message identity and versioning rules, mapping through a handler cascade from decided domain events, the "no route until 4.1" consequence, and the schema snapshot as the compatibility gate.
- ADR 0024 amended (integration-channel table).

## Open questions
- None. Resolved during implementation: Wolverine 6.62 has a public `IMessageTypeNaming` and `WolverineMessageNaming.InsertFirst<T>()`, so the identity is a `const` on the BCL record, read by a naming strategy in `RimBank.Postgresql` (ADR 0036).
