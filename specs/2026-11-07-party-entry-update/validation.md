# Validation: Party Reference Data Directory: Update

## Done when (from roadmap)
> **1.8 Update:** `Update` decision, event and command endpoint, with optimistic concurrency through `If-Match` (409 Problem Details); the view is updated by the projection.
> + Definition of done (roadmap.md)

Some Definition-of-done items are covered elsewhere:
- The MCP Update tool and its HTTP parity test come in 1.17, as Register and Retrieve wait for 1.16.
- The `Updated` integration event comes in 1.13.
- "Rebuildable from the event store" is proven here against a real PostgreSQL with `RebuildViewAsync`.

## Automated checks
- [ ] **Decision** (`RimBank.Core.Tests`, `UpdateTests`, Given / When / Then):
  - a new type or no type → `PartyReferenceDataDirectoryEntryUpdated` with the resulting type and `RefreshDate`;
  - not registered → `party.party-not-found`;
  - an undefined type → `party.party-type-invalid`.
  - The entry laws (FsCheck) still hold with Updates in the generated histories.
- [ ] **Handler** (`UpdatePartyReferenceDataDirectoryEntryHandlerTests`, in-memory port):
  - success acknowledges version 2 from the decided state;
  - a stale or future expected version → `party.version-conflict`, with nothing appended;
  - an unknown entry → not found;
  - a failure appends nothing.
- [ ] **Projection** (`PartyReferenceDataDirectoryEntryViewProjectionTests`): Given `PartyRegistered` + `PartyReferenceDataDirectoryEntryUpdated` → Then a view with version 2, the new type and `RefreshDate`.
- [ ] **Marten** (Party `.Postgresql.Tests`, Testcontainers):
  - the expected-version append;
  - the inline view is updated in the same transaction (read-your-write);
  - the view version equals the stream version;
  - `RebuildViewAsync` recreates the updated view.
- [ ] **Race:**
  - five concurrent Updates on version 1 → exactly one 2 and four `party.version-conflict` results, with no exception reaching the caller, and the stream at version 2;
  - the retry convention test in `RimBank.Postgresql.Tests` is green.
- [ ] **Entity tags** (`StreamVersionTagTests`): `ReadIfMatch` accepts one strong tag only. Missing → 428 `shared.if-match-required`; weak, `*`, a list or garbage → 400 `shared.if-match-invalid`.
- [ ] **Problem mapping** (`RimBank.Problems.Tests`): `ErrorKind.PreconditionRequired` → 428 `application/problem+json`.
- [ ] **HTTP** (`RimBank.Api.Tests/Party/PartyReferenceDataDirectory/Update`, Alba + Testcontainers):
  - 200 with `ETag` `"2"` and the body built from the acknowledgement, and Retrieve then shows the change;
  - 409, 428, 400 (`If-Match`, `PartyType`, id, missing `Idempotency-Key`), 404 and 401, all `application/problem+json` with `type`, `errorCode` and `traceId`;
  - an idempotent replay returns the first answer, and the same key with another `If-Match` → 409;
  - the Register and Retrieve tests stay green, and Retrieve omits `RefreshDate` before any Update.
- [ ] **Conformance:** the mapped routes, now including `PUT …/{partyreferencedatadirectoryid}/Update`, conform to the BIAN YAML (Update is a command, not a `GET`).
- [ ] **Architecture tests** green:
  - the Update handler uses only the write port and decisions, never the view or the read port;
  - the projection stays pure;
  - the endpoint dispatches only through `IMessageBus`;
  - `.Api` does not reference `.Postgresql`.
- [ ] `RimBank.Host.Tests` green.
- [ ] CI green: locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL.

## Manual / demo checks
- [ ] The demo is the Alba suite: `dotnet test --project tests/RimBank.Api.Tests --filter "FullyQualifiedName~PartyReferenceDataDirectory.Api.Tests"`. Every Update case above is green.
- [ ] Optional, with the AppHost running and a Keycloak token, each step with a fresh `Idempotency-Key`:
  1. `POST /PartyReferenceDataDirectory/Register` `{"PartyType":"Person"}` → `ETag: "1"`.
  2. `PUT /PartyReferenceDataDirectory/{id}/Update` with `If-Match: "1"` and `{"PartyType":"Organisation"}` → 200, `ETag: "2"`.
  3. Repeat step 2 with a new key → 409 `party.version-conflict`.
  4. `GET …/{id}/Retrieve` → `Organisation`, `RefreshDate` and `ETag: "2"`.
- [ ] Reading check:
  - the Update response is built from the acknowledgement, never from the view;
  - the route parameter is `partyreferencedatadirectoryid`, as in BIAN;
  - `PartyReference` is not stored or returned.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.8 ticked in the same PR
- [ ] ADR 0027 (optimistic concurrency for commands) written; ADR 0024, 0006, 0015 and 0017 amended; tech-stack §3.4 and §5 updated
- [ ] No PII in logs, and no new PII stored or returned
- [ ] `doc-0/` untouched
