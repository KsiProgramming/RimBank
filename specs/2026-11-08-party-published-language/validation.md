# Validation: Party Reference Data Directory: published language

## Done when (from roadmap)
> **1.13 Published language:** `PartyReferenceDataDirectoryEntryCreated/Updated` integration events, mapped from domain events (not from read models), carrying the state consumers need, versioned, published through the outbox, and pinned by a schema contract test.
> + Definition of done (roadmap.md)

In practice: every accepted Register publishes one Created, and every accepted Update publishes one Updated, in the same transaction as the domain events. A rejected command publishes nothing. Both schemas are pinned by approved snapshots.

## Automated checks
- [ ] **Core (Shouldly)** in `RimBank.Core.Tests`:
  - `PartyReferenceDataDirectoryPublicationTests`:
    - `PartyRegistered` maps to Created (`OpenDate`);
    - `PartyReferenceDataDirectoryEntryUpdated` maps to Updated (`RefreshDate`);
    - every party type has a published value, and an unknown one is refused;
    - version, `OccurredAt` and the `PartyType` string are carried.
  - Register and Update handler tests: one cascaded event on success, with the right version; `null` on `party.party-not-found`, `party.version-conflict` and decision errors.
- [ ] **PostgreSQL (Testcontainers)** `PublishedLanguageOutboxTests`:
  - Register publishes exactly one Created (version 1), and Update exactly one Updated (version 2);
  - a stale version, an unknown entry, an idempotent replay and a BQ Update publish nothing;
  - the envelope (v1 identity, expected JSON payload) is in the context's own `wolverine_incoming_envelopes`, one per committed Control Record event.
- [ ] **Schema contract** `PartyPublishedLanguageSchemaTests`:
  - the exported JSON schema of each event equals its committed approved file;
  - removing, renaming or retyping a field fails the test;
  - Wolverine writes the same property names; the round trip is stable; an unknown extra field is tolerated; each type goes on the wire under its identity.
- [ ] **Naming** `PublishedLanguageMessageNamingTests` (`RimBank.Postgresql.Tests`): a type outside `.PublishedLanguage` keeps Wolverine's name, and registering twice is harmless.
- [ ] **Catalogue / AsyncAPI** `PartyReferenceDataDirectoryCatalogueTests`: both CR channels (phase 1.13) have a CLR type, and their addresses exist in `asyncapi-3.x/yamls/PartyReferenceDataDirectory.yaml`. The BQ channels stay `deferred`.
- [ ] **Architecture tests** green: the new `.PublishedLanguage` project uses the BCL only and holds no views; only `RimBank.Core` (own context) references it; there is no Wolverine type in core.
- [ ] Existing API, MCP and Postgresql tests for Register and Update are unchanged and green (the acknowledgement and the responses are not affected).
- [ ] `dotnet format --verify-no-changes`, full `dotnet test`, CodeQL: green in GitHub Actions.

## Manual / demo checks
- [ ] `dotnet run --project src/RimBank.AppHost`, then run the Register and Update requests of `src/RimBank.Host/RimBank.Host.http`:
  - `200`/`201` responses unchanged;
  - in the party schema, `wolverine_incoming_envelopes` (or the Aspire dashboard's Wolverine traces) shows one envelope per command, with the v1 message type;
  - a stale `If-Match` returns `409` `party.version-conflict` Problem Details and produces no envelope.
- [ ] Paste the approved schema files and the demo trace in the PR description.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.13 ticked in the same PR
- [ ] ADR 0036 written; ADR 0024 integration-channel table updated
- [ ] No PII in the published events or in their logs (party type and dates only)
- [ ] tech-stack §3 and §8.4 and CONTRIBUTING updated
