# Plan: Party Reference Data Directory: API adapter (Register)

Every group is written test first, and the solution builds and its tests pass after each one (one commit per group). The adapter lives in `src/Party/RimBank.Party.PartyReferenceDataDirectory.Api/` and its tests in `tests/Party/RimBank.Party.PartyReferenceDataDirectory.Api.Tests/`. Test namespaces end with `.Tests`, for example `RimBank.Party.PartyReferenceDataDirectory.Api.Registration.Tests` (tech-stack §3.3). Use switch expressions only (IDE0066). Package versions go only in `Directory.Packages.props`, and lock files are regenerated. Check the current ASP.NET Core OpenAPI, Scalar and Alba APIs with `ctx7` before writing them.

## 1. Request and response models (unit tests)
- [ ] Create `RimBank.Party.PartyReferenceDataDirectory.Api.csproj` (Web SDK library; references `RimBank.Core`, `RimBank.Problems`, `RimBank.Http`, Wolverine for `IMessageBus`; nothing §4 forbids) and add it to `RimBank.slnx`.
- [ ] Create the test project `RimBank.Party.PartyReferenceDataDirectory.Api.Tests` (xUnit v3, Shouldly) and add it to `RimBank.slnx`.
- [ ] Tests first (`RegisterPartyRequestTests`, `RegisterPartyResponseTests`):
  - `"Person"` / `"Organisation"` → the matching `PartyType`;
  - `null`, `""` or whitespace → `party.party-type-required` with `errors["PartyType"]`;
  - `"Alien"`, `"person"` or `"0"` → `party.party-type-invalid`;
  - `PartyReference` and the date fields set → ignored (same command as without them);
  - `RegisterPartyAcknowledged` → response with the plain GUIDs, `PartyType` name, `DirectoryEntryDateType = "OpenDate"` and `DirectoryEntryDate` = `OpenDate`;
  - the serialised JSON uses BIAN's PascalCase names (`PartyReferenceDataDirectoryEntryId`, `PartyType`, …).
- [ ] Implement `Registration/RegisterPartyRequest` (`ToCommand(IdempotencyKey) → Result<RegisterParty>`), `Registration/RegisterPartyResponse` (`From(RegisterPartyAcknowledged)`) and `Registration/RegisterPartyRequestErrors.PartyTypeRequired`, with XML docs citing ADR 0024 and tech-stack §5.
- [ ] Run `RimBank.Architecture.Tests`: the project is recognised as the Party context's `.Api` adapter and uses only its own core namespace.

## 2. Endpoint, host wiring and HTTP tests (Alba + Testcontainers)
- [ ] Add `Alba` to `Directory.Packages.props`. Extract `TestTokens` from `RimBank.Host.Tests/Security/` into a new `tests/RimBank.Host.Testing` library, and point `RimBank.Host.Tests` at it (its tests stay green).
- [ ] In Api.Tests: an assembly-fixture `PostgresqlServer` (one container, one database per test, as in the Postgresql.Tests project), and `PartyApiHost`: `AlbaHost.For<Program>` with the Party and Wolverine connection strings, test JWT settings and a `FixedTimeProvider`.
- [ ] Tests first (`RegisterPartyEndpointTests`):
  - a valid request with a key and a token → `200`, the body matches `RegisterPartyResponse`, `ETag: "1"`, and the entry's stream exists with one `PartyRegistered`;
  - the same key and body again → `200` with an identical body and `ETag`, and still one stream;
  - the same key with another `PartyType` → `409` `application/problem+json` (ADR 0018 code);
  - no `Idempotency-Key` → `400` `shared.idempotency-key-missing`; and an invalid one → `shared.idempotency-key-invalid`;
  - missing `PartyType` → `400` `party.party-type-required`; `"Alien"` → `400` `party.party-type-invalid`; both with `errors.PartyType`;
  - malformed JSON or an empty body → `400` Problem Details with a stable shared code;
  - no token → `401` `shared.unauthenticated`;
  - every error response has `type`, `status`, `errorCode` and `traceId`, and no stream or stored outcome is left behind.
- [ ] Implement `Registration/RegisterPartyEndpoint` and `PartyReferenceDataDirectoryApi.MapPartyReferenceDataDirectoryEndpoints()`: `MapPost("/PartyReferenceDataDirectory/Register", …)`, `.RequireIdempotencyKey()`, `.WithName("PartyReferenceDataDirectoryEntry/Register")`, the BIAN summary, and `Produces`/`ProducesProblem` for 200, 400, 401 and 409.
- [ ] If a malformed body does not already become Problem Details, add `shared.request-body-invalid` in `RimBank.Problems` (with a `RimBank.Problems.Tests` case) and map it once for all endpoints.
- [ ] `RimBank.Host`: reference the project; `PartyReferenceDataDirectoryModule.MapEndpoints` calls `MapPartyReferenceDataDirectoryEndpoints()`; update its XML doc ("Its HTTP endpoints arrive in 1.5").

## 3. Conformance, OpenAPI, docs and roadmap
- [ ] Conformance test (`PartyReferenceDataDirectoryConformanceTests` in Api.Tests): `ImplementedRoute.From(...)` of the Party endpoints `.ShouldConformTo(...)` the BIAN `PartyReferenceDataDirectory.yaml` (POST, a command, an existing route).
- [ ] OpenAPI + Scalar: `AddOpenApi` in `AddServiceDefaults`, and `MapOpenApi` + `MapScalarApiReference` in `MapDefaultEndpoints` (Development only); add `Scalar.AspNetCore` to `Directory.Packages.props`. Test: in Development, `/openapi/v1.json` holds `POST /PartyReferenceDataDirectory/Register` with a required `Idempotency-Key` header; in Production it is 404.
- [ ] ADR 0024: add `party.party-type-required` to the error table, and a line in "Operations" that Register ignores `PartyReference` and the date fields (the server sets `OpenDate`). ADR 0015: list `shared.request-body-invalid` if added.
- [ ] tech-stack §3.3: add `RimBank.Host.Testing/` to the tests tree.
- [ ] Roadmap: tick **1.5**.
- [ ] Run `dotnet format --verify-no-changes` and the full `dotnet test --solution RimBank.slnx` locally (Docker running).
