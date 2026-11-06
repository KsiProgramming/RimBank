# Plan: Party Reference Data Directory: API adapter (Register)

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). The adapter lives in `src/Party/RimBank.Party.PartyReferenceDataDirectory.Api/` and its tests in `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Api.Tests/`. Test namespaces end with `.Tests`, for example `RimBank.Party.PartyReferenceDataDirectory.Api.Registration.Tests` (tech-stack §3.3). Use switch expressions only (IDE0066). Package versions go only in `Directory.Packages.props`, and lock files are regenerated. Check the current ASP.NET Core OpenAPI, Scalar and Alba APIs with `ctx7` before writing them.

## 1. Request and response models (unit tests)
- [x] Create `RimBank.Party.PartyReferenceDataDirectory.Api.csproj` (Web SDK library; references `RimBank.Core`, `RimBank.Problems`, `RimBank.Http`, Wolverine for `IMessageBus`; nothing §4 forbids) and add it to `RimBank.slnx`.
- [x] Create the test project `RimBank.Party.PartyReferenceDataDirectory.Api.Tests` (xUnit v3, Shouldly) and add it to `RimBank.slnx`.
- [x] Tests first (`RegisterPartyRequestTests`, `RegisterPartyResponseTests`):
  - `"Person"` / `"Organisation"` → the matching `PartyType`;
  - `null`, `""` or whitespace → `party.party-type-required` with `errors["PartyType"]`;
  - `"Alien"`, `"person"` or `"0"` → `party.party-type-invalid`;
  - `PartyReference` and the date fields set → ignored (same command as without them);
  - `RegisterPartyAcknowledged` → response with the plain GUIDs, `PartyType` name, `DirectoryEntryDateType = "OpenDate"` and `DirectoryEntryDate` = `OpenDate`;
  - the serialised JSON uses BIAN's PascalCase names (`PartyReferenceDataDirectoryEntryId`, `PartyType`, …).
- [x] Implement `Registration/RegisterPartyRequest` (`ToCommand(IdempotencyKey) → Result<RegisterParty>`), `Registration/RegisterPartyResponse` (`From(RegisterPartyAcknowledged)`) and `Registration/RegisterPartyRequestErrors.PartyTypeRequired`, with XML docs citing ADR 0024 and tech-stack §5.
- [x] Run `RimBank.Architecture.Tests`: the project is recognised as the Party context's `.Api` adapter and uses only its own core namespace. `adapters-dispatch-through-the-message-bus` now accepts `ICommandBus`, the interface `IMessageBus` inherits `InvokeAsync<T>` from (pinned against Wolverine by a solution test).

## 2. Endpoint, host wiring and HTTP tests (Alba + Testcontainers)
- [x] Add `Alba` to `Directory.Packages.props`. Extract `TestTokens` from `RimBank.Host.Tests/Security/` into a new `tests/RimBank.Host.Testing` library, and point `RimBank.Host.Tests` at it (its tests stay green).
- [x] In Api.Tests: an assembly-fixture `PostgresqlServer` (one container, one database per test, as in the Postgresql.Tests project), and `PartyApiHost`: `AlbaHost.For<Program>` with the Party and Wolverine connection strings, test JWT settings and a `FixedTimeProvider`.
- [x] Tests first (`RegisterPartyEndpointTests`):
  - a valid request with a key and a token → `200`, the body matches `RegisterPartyResponse`, `ETag: "1"`, and the entry's stream exists with one `PartyRegistered`;
  - the same key and body again → `200` with an identical body and `ETag`, and still one stream;
  - the same key with another `PartyType` → `409` `application/problem+json` (ADR 0018 code);
  - no `Idempotency-Key` → `400` `shared.idempotency-key-missing`; and an invalid one → `shared.idempotency-key-invalid`;
  - missing `PartyType` → `400` `party.party-type-required`; `"Alien"` → `400` `party.party-type-invalid`; both with `errors.PartyType`;
  - malformed JSON or an empty body → `400` Problem Details with a stable shared code;
  - no token → `401` `shared.unauthenticated`;
  - every error response has `type`, `status`, `errorCode` and `traceId`, and no stream or stored outcome is left behind.
- [x] Implement `Registration/RegisterPartyEndpoint` and `PartyReferenceDataDirectoryApi.MapPartyReferenceDataDirectoryEndpoints()`: `MapPost("/PartyReferenceDataDirectory/Register", …)`, `.RequireIdempotencyKey()`, `.WithName("PartyReferenceDataDirectoryEntry/Register")`, the BIAN summary, and `Produces`/`ProducesProblem` for 200, 400, 401 and 409.
- [x] A malformed body was a code-less 400 in Production and a 500 in Development: add `shared.request-malformed` in `RimBank.Problems` (with a `RimBank.Problems.Tests` case), mapped once in the host for bodiless 400s and `BadHttpRequestException`, with host tests in both environments.
- [x] `RimBank.Host`: reference the project; `PartyReferenceDataDirectoryModule.MapEndpoints` calls `MapPartyReferenceDataDirectoryEndpoints()`; update its XML doc ("Its HTTP endpoints arrive in 1.5").

## 3. Conformance, OpenAPI, docs and roadmap
- [x] Conformance test (`PartyReferenceDataDirectoryConformanceTests` in Api.Tests): `ImplementedRoute.From(...)` of the Party endpoints `.ShouldConformTo(...)` the BIAN `PartyReferenceDataDirectory.yaml` (POST, a command, an existing route).
- [x] OpenAPI + Scalar: `AddOpenApi` in `AddServiceDefaults`, and `MapOpenApi` + `MapScalarApiReference` in `MapDefaultEndpoints` (Development only); add `Microsoft.AspNetCore.OpenApi` and `Scalar.AspNetCore` to `Directory.Packages.props`; the `Idempotency-Key` header comes from an operation transformer in `RimBank.Host`. Tests: in Development, `/openapi/v1.json` holds `POST /PartyReferenceDataDirectory/Register` with a required `Idempotency-Key` header and `/scalar/` serves the UI; outside Development both are 404. `AnonymousEndpointTests` covers the Development routes.
- [x] ADR 0024: add `party.party-type-required` to the error table, and a "Register request" paragraph (ignored fields, flattened enum wrappers, 200 + `ETag`). ADR 0015: list `shared.request-malformed`.
- [x] tech-stack §3.3: add `RimBank.Host.Testing/` to the tests tree; tech-stack §10, CONTRIBUTING and the `allow-anonymous-only-in-service-defaults` description mention the Development-only OpenAPI document and Scalar UI.
- [x] Roadmap: tick **1.5**.
- [x] Run `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally (Docker running): green except the pre-existing, load-dependent `RimBank.Postgresql.Tests` `CallerScopeTests.Concurrent_calls_with_the_same_key_commit_once_per_caller`, which passes alone and is untouched by this phase.
