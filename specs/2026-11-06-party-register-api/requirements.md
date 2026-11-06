# Party Reference Data Directory: API adapter (Register)

Roadmap phase: 1.5
Branch: plan/1.5-party-register-api

## Context
1.2 to 1.4 delivered the `PartyReferenceDataDirectoryEntry` aggregate, the `RegisterParty` command and handler, and its Marten write side. `RegisterParty` already runs through `IMessageBus` in the host, with Wolverine's transaction and idempotency ([ADR 0017](../../docs/adr/0017-wolverine-wiring.md), [ADR 0018](../../docs/adr/0018-idempotency-keys-for-commands.md)). Nothing reaches it over HTTP yet. This phase adds the first `.Api` adapter, `RimBank.Party.PartyReferenceDataDirectory.Api`, with the BIAN operation `PartyReferenceDataDirectoryEntry/Register` ([OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml), `POST /PartyReferenceDataDirectory/Register`).

It unblocks 1.7 (the Retrieve endpoint is the second route of this project), 1.8 (Update, with `If-Match` against the version this phase returns as `ETag`), 1.14 (conformance of the implemented subset) and 1.16 (the MCP Register tool reuses this phase's request model, tech-stack §6).

Drivers: mission principles 5 (a command returns only an acknowledgement), 6 (HTTP is a thin adapter over the core), 8 (Problem Details everywhere) and 9 (idempotency, security); tech-stack §3.1/§3.3 (project names), §4 (`.Api` may reference only its own core, SharedKernel, `RimBank.Problems`, `RimBank.Http`, ASP.NET Core and Wolverine), §5 (BIAN routes, request/response models in `.Api`, a command response built from the outcome, `Idempotency-Key`, Problem Details), §1 (Minimal APIs with built-in OpenAPI and Scalar), §9 (Alba, Testcontainers, conformance); [ADR 0014](../../docs/adr/0014-host-composition-and-module-entry-point.md) (`MapEndpoints` in the module), [ADR 0015](../../docs/adr/0015-problem-details-mapping.md), [ADR 0019](../../docs/adr/0019-authentication-baseline.md), [ADR 0020](../../docs/adr/0020-bian-conformance-harness.md) and [ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md).

**CQRS split:** **command side only**. The endpoint dispatches the existing command `RegisterParty` with `bus.InvokeAsync<Result<RegisterPartyAcknowledged>>` and builds its response from `RegisterPartyAcknowledged`, never from a read model (tech-stack §5). This phase adds no query, read port, view or projection, so there is no inline or async read model. The inline entry view and `GET …/Retrieve` come in 1.6 and 1.7.

## Scope
### In scope
- New project `src/Party/RimBank.Party.PartyReferenceDataDirectory.Api/` (namespace `RimBank.Party.PartyReferenceDataDirectory.Api`), added to `RimBank.slnx`, referencing `RimBank.Core`, `RimBank.Problems` and `RimBank.Http`:
  - `Registration/RegisterPartyRequest`: BIAN field names (`PartyReference`, `DirectoryEntryDateType`, `DirectoryEntryDate`, `PartyType`). `PartyType` is required and read as a string, so the adapter (not the JSON binder) decides a missing or unknown value. The other fields are accepted and ignored: the server sets `OpenDate`, and the party's details arrive through the BQ updates (1.9, 1.10).
  - `Registration/RegisterPartyResponse`: the BIAN Register body (`PartyType`, `DirectoryEntryDateType` = `OpenDate`, `DirectoryEntryDate`) plus `PartyReferenceDataDirectoryEntryId`, `DemographicsId`, `ReferenceId` and `BankRelationsId` (plain GUIDs), mapped from `RegisterPartyAcknowledged`.
  - `Registration/RegisterPartyEndpoint`: `POST /PartyReferenceDataDirectory/Register` → validate → `RegisterParty(partyType, idempotency.Value)` → `200 OK` with the response and `ETag: "<version>"`, or `error.ToHttpResult()`. It carries `RequireIdempotencyKey()`, a `WithName("PartyReferenceDataDirectoryEntry/Register")` and the BIAN summary.
  - `PartyReferenceDataDirectoryApi.MapPartyReferenceDataDirectoryEndpoints(this IEndpointRouteBuilder)`, the one extension the module calls.
- Validation errors (400, Problem Details with `errors["PartyType"]`): `party.party-type-required` (new, missing or empty) and `party.party-type-invalid` (existing, unknown value). A malformed or missing JSON body is 400 Problem Details with a stable shared code (reuse one if the host already emits it, otherwise add `shared.request-body-invalid` to `RimBank.Problems`).
- JSON: the `.Api` models use BIAN's PascalCase property names (`[JsonPropertyName]`) and plain GUIDs and strings. No domain type is serialised (tech-stack §5), so no typed-id converter is needed in ASP.NET Core's JSON options.
- Host: `PartyReferenceDataDirectoryModule.MapEndpoints` calls `MapPartyReferenceDataDirectoryEndpoints()`, and `RimBank.Host` references the new project.
- OpenAPI: the built-in document (`AddOpenApi` / `MapOpenApi`) and the Scalar UI, mapped **in Development only** from `RimBank.ServiceDefaults` beside the health probes, the only place anonymous endpoints are allowed (rule `allow-anonymous-only-in-service-defaults`). The document shows the Register operation with its `Idempotency-Key` header.
- New test project `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Api.Tests/` (Alba, Testcontainers PostgreSQL, test-signed JWTs).
- Docs: ADR 0024 (the new validation code and the "ignored request fields" note), the tech-stack §3.3 tree comment if needed, and the module's XML doc.

### Out of scope
- Retrieve, its view and projection, and `Location`/`ETag` on a GET: **1.6 / 1.7**.
- `If-Match` and optimistic concurrency: **1.8**.
- Storing `PartyReference` details (names, identifiers, contacts): **1.9 / 1.10** (BQ updates).
- Integration events: **1.13**. Full OpenAPI/AsyncAPI conformance of the subset: **1.14**.
- Per-operation authorisation policies and scopes: **1.15**. Until then the fallback policy (any authenticated caller) applies.
- MCP Register tool and HTTP/MCP parity test: **1.16**.
- A `.http` demo file and a manual AppHost run (not chosen in the interview).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Request strictness | Tolerant: `PartyReference`, `DirectoryEntryDateType` and `DirectoryEntryDate` are accepted and ignored | Chosen in the interview. BIAN puts them in the Register body, but the core decides `OpenDate` (tech-stack §7, `TimeProvider`) and the party's details are BQ data (ADR 0024). Documented in ADR 0024 and the OpenAPI description |
| Success status | `200 OK` | BIAN declares `200` for Register. `201` + `Location` would point to a route that does not exist before 1.7 |
| Response body | BIAN Register fields + entry and BQ ids, built from `RegisterPartyAcknowledged` | tech-stack §5: the response comes from the command outcome. The BQ ids are the route values of 1.9–1.12 (ADR 0024) |
| Version | `ETag: "<version>"` header (strong, quoted), not in the body | tech-stack §5: the stream version travels as `ETag` / `If-Match` (1.8) |
| JSON names | BIAN PascalCase via `[JsonPropertyName]` on `.Api` models only | Faithful to the BIAN contract without changing the host's global JSON options |
| `PartyType` binding | `string?` in the request, parsed by the adapter (exact, case-sensitive `Person` / `Organisation`) | A missing or unknown value becomes our own Problem Details code, not a framework binding error |
| Missing `PartyType` code | `party.party-type-required` (400), defined in `.Api` | ADR 0024: each request phase adds its own validation codes. It is a request-shape rule, not a domain rule |
| Unknown `PartyType` | Existing `party.party-type-invalid` | Same meaning as the core's decision. One code for clients |
| Test host | Alba over the real `Program`, with Testcontainers connection strings and test-signed JWTs | Chosen in the interview (tech-stack §9). It adds the `Alba` package to `Directory.Packages.props` |
| Test tokens | Extract `RimBank.Host.Tests/Security/TestTokens.cs` into a shared test library `tests/RimBank.Host.Testing` | Api.Tests now, and Mcp.Tests in 1.16, need the same signed tokens. One copy, no drift |
| OpenAPI + Scalar | Development only, mapped in `RimBank.ServiceDefaults` | tech-stack §1 already names them, so no ADR. Keeps the anonymous-endpoint rule unchanged |
| Work order | Test first, three groups: models and mapping → endpoint and HTTP tests → conformance, OpenAPI, docs | Chosen in the interview |

## ADRs to write
- None new. **Amend ADR 0024**: add `party.party-type-required` to the error-code table, and note that Register ignores `PartyReference` and the directory entry date fields. Add `shared.request-body-invalid` to ADR 0015's shared codes if it is new.

## Open questions
- None. (If `Program` cannot be hosted by Alba without the AppHost-provided settings, the test host overrides them through configuration in `PartyApiHost`. That is a test-wiring detail, not a new decision.)
