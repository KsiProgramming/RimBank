# 0036. Party Reference Data Directory: published language

- Status: Accepted
- Date: 2026-11-08
- Roadmap phase: 1.13

## Context

Until 1.13, nothing left Party Reference Data Directory. Current Account (roadmap 4.1) will need to know that a party exists, without querying Party ([mission](../../specs/mission.md) principle 7, consumers own their copy). [Tech-stack §8.4](../../specs/tech-stack.md#84-read-models-across-contexts-loose-coupling) sets the producer rules: integration events are mapped from domain events through the outbox, never from read models; they carry the state consumers need; they are versioned and change only in backwards-compatible ways; and a contract test pins each schema.

[ADR 0024](0024-party-reference-data-directory-mapping.md#integration-channels) maps the two Control Record channels of the [BIAN AsyncAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/asyncapi-3.x/yamls/PartyReferenceDataDirectory.yaml) to phase 1.13: `PartyRegistered` maps to `PartyReferenceDataDirectoryEntry/Created`, and `PartyReferenceDataDirectoryEntryUpdated` maps to `…/Updated`. It defers the 8 BQ channels. The BIAN payload is the Control Record schema: `PartyReference`, `PartyType`, `DirectoryEntryDateType` (`OpenDate` / `RefreshDate`) and `DirectoryEntryDate`.

Constraints:
- `.PublishedLanguage` is BCL only ([tech-stack §4](../../specs/tech-stack.md#4-dependency-rules-enforced-by-architecture-tests)), so it cannot use Wolverine's `[MessageIdentity]` attribute.
- The core uses no Wolverine type ([ADR 0017](0017-wolverine-wiring.md)).
- No new package is allowed without a reason ([ADR 0013](0013-warning-and-package-governance.md)).

## Options considered

**Payload**
1. **The Control Record state plus a small envelope** (`EventId`, `OccurredAt`, `Version`). Consumers can order and deduplicate. There is no PII.
2. The same, with the Behavior Qualifier ids. No consumer needs them yet.
3. Only the id and party type. Too thin: a consumer could not order events.

**Versioning**
1. **A stable message identity declared on the type as a constant** (`MessageIdentity`), given to Wolverine by a naming strategy in the adapter wiring. Changes may only add fields.
2. A `V1` suffix on the CLR type. Simpler, but the wire name would be the CLR name, so a rename or a move would break consumers.
3. A version field in the payload. It does not say which type to deserialise to.

**Mapping and delivery**
1. **A pure mapper in core, cascaded by the command handler** as `(Result<Ack>, Event?)`, the 0.11 pattern. The event commits with the domain events in one transaction.
2. Marten event forwarding in `.Postgresql`. Handlers stay unchanged, but the business mapping moves into an adapter, and the transaction behaviour depends on more wiring.

**Contract test**
1. **A JSON schema exported with `System.Text.Json.Schema.JsonSchemaExporter` (BCL), compared to a committed approved file.**
2. A snapshot package (Verify). It needs a new package for little gain.

## Decision

- **Project:** `src/Party/RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage` (namespace `RimBank.Party.PartyReferenceDataDirectory.PublishedLanguage`, BCL only). It holds `PartyReferenceDataDirectoryEntryCreated` and `PartyReferenceDataDirectoryEntryUpdated`, with the fields:
  - `EventId` (Guid v7);
  - `OccurredAt`;
  - `PartyReferenceDataDirectoryEntryId`;
  - `Version` (the entry's stream version after the event);
  - `PartyType`;
  - `DirectoryEntryDateType`;
  - `DirectoryEntryDate`.

  `PartyType` and `DirectoryEntryDateType` are strings, with the allowed values in `PartyTypes` and `DirectoryEntryDateTypes`. They are not enums, so a value added later never breaks an older consumer. There is no PII and no BQ id.
- **Identity:** each event declares `public const string Channel` (its BIAN channel) and `public const string MessageIdentity`: `party-reference-data-directory-entry.created.v1` and `…updated.v1`.
  - `RimBank.Postgresql.PublishedLanguageMessageNaming` gives any type in a `*.PublishedLanguage` namespace the identity it declares. It is registered once, ahead of Wolverine's own naming, by `AddServiceDomainStore`, through `WolverineMessageNaming.InsertFirst`. Every other message keeps Wolverine's default name.
  - **Compatibility:** a field may be added; a field is never removed, renamed or retyped. A breaking change is a new type with a `.v2` identity, published next to v1 until consumers move.
- **Mapping:** `PartyReferenceDataDirectoryPublication` (core, `Publication/`) maps a decided `PartyRegistered` or `PartyReferenceDataDirectoryEntryUpdated` to its integration event. `OccurredAt` and `DirectoryEntryDate` are the open or refresh date, so the mapper is pure. `RegisterPartyHandler` and `UpdatePartyReferenceDataDirectoryEntryHandler` return `(Result<…Acknowledged>, <event>?)`, with the event id from their `TimeProvider`. An error returns `null`, so a refused command publishes nothing by construction. An idempotent replay does not run the handler (ADR 0018), so it publishes nothing again. Behavior Qualifier commands publish nothing.
- **Delivery:** the cascaded event goes to the context's durable local queue (ADR 0017's namespace rule also covers the child namespace `.PublishedLanguage`). Its envelope is saved in `party_reference_data_directory.wolverine_incoming_envelopes` in the same transaction as the events. Until a consumer exists (4.1), no handler receives it, and Wolverine logs it and discards it (`UnknownMessageBehavior.LogOnly`, the default). Events are not replayed for entries registered before 1.13, because there is no consumer to replay for. 4.1 decides how a new consumer catches up.
- **Wire format:** Wolverine's default System.Text.Json serializer, so property names are camelCase on the wire (`partyType`). Reads are case-insensitive, and an unknown field is ignored. BIAN names the same fields in PascalCase; 1.14 compares them without regard to case, and lists the envelope fields as deviations ([ADR 0037](0037-bian-body-and-payload-conformance.md)).
- **Contract:** `PartyPublishedLanguageSchemaTests` (Party `.Postgresql.Tests`, `Publication/`) exports each event's schema with the wire settings and compares it, with its channel and identity, to `Publication/Approved/<identity>.approved.json`. On a mismatch it writes `<identity>.received.json` (git-ignored). The test also checks:
  - Wolverine writes the same property names;
  - an event survives the wire round trip;
  - a v1 reader tolerates an added field;
  - every published type goes on the wire under its declared identity.

## Consequences

- Any change to a published event shows up in review as a diff of the approved file. Updating that file on purpose is the only way to change the contract, and a removal or rename must be a new versioned type instead.
- `PartyReferenceDataDirectoryCatalogue` (conformance) gives each 1.13 channel its event type. Its tests check that each event's `Channel` matches its row, that only deferred channels have no event, and that the addresses exist in the AsyncAPI.
- Integration tests (`PublishedLanguageOutboxTests`) prove:
  - exactly one event per accepted Register or Update, with the right version and payload;
  - the stored envelope carries the v1 identity and the expected JSON;
  - nothing for a conflict, an unknown entry, a replay or a BQ update;
  - one envelope per committed Control Record event, all in the context's own schema.
- Next contexts follow the same pattern: a BCL `.PublishedLanguage` whose types declare `Channel` and `MessageIdentity`, a pure mapper in core, a handler cascade and an approved schema per event. The naming strategy needs no per-context wiring.
- **4.1** must decide how Current Account receives these events (its handler in `.Subscriptions` will run on Party's queue unless it routes them to its own), and how a new consumer catches up on parties registered before it existed.
- Wolverine's message naming is process-wide (static). The naming strategy only answers for `*.PublishedLanguage` types, so it changes no other message name.
