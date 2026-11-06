# Validation: Party Reference Data Directory: API adapter (Register)

## Done when (from roadmap)
> **1.5 API adapter: Register:** `POST /PartyReferenceDataDirectory/Register` with validation returning Problem Details; the response is built from the command outcome, not from a read model.
> + Definition of done (roadmap.md)

Some Definition-of-done items do not apply here. The phase adds no read model, so there is no projection test and no rebuild check (1.6/1.7). The HTTP/MCP parity test for Register is assigned to 1.16 by the roadmap. Per-operation authorisation is 1.15.

## Automated checks
- [x] **Model unit tests** (`RimBank.Party.PartyReferenceDataDirectory.Api.Tests`): `PartyType` parsing (valid, missing → `party.party-type-required`, unknown or wrong case → `party.party-type-invalid`); ignored BIAN fields; the response is mapped from `RegisterPartyAcknowledged` with BIAN PascalCase JSON names and plain GUIDs.
- [x] **HTTP integration tests** (Alba over `Program` + Testcontainers PostgreSQL 17, test-signed JWT):
  - `200` with body, entry and BQ ids, `ETag: "1"`, and one `PartyRegistered` in the new stream;
  - same key and body → identical `200` replay, still one stream;
  - same key, different payload → `409` Problem Details (ADR 0018);
  - missing or invalid `Idempotency-Key` → `400` `shared.idempotency-key-missing` / `shared.idempotency-key-invalid`;
  - missing or unknown `PartyType` → `400` with `errors.PartyType`;
  - malformed or empty JSON → `400` `shared.request-malformed`, in `Test` and `Development` (it was a 500 in Development before this phase);
  - no token → `401` `shared.unauthenticated`;
  - every error is `application/problem+json` with `type`, `status`, `errorCode` and `traceId`, and leaves no stream and no stored outcome.
- [x] **No read model in the command path:** the endpoint uses only `IMessageBus` and `RegisterPartyAcknowledged` (checked by the existing CQRS architecture rules and by reading the code).
- [x] **BIAN conformance:** the implemented Party routes conform to `PartyReferenceDataDirectory.yaml` (route exists, `POST`, command). `PartyReferenceDataDirectoryCatalogueTests` stays green.
- [x] **OpenAPI:** in Development the document lists the Register operation with a required `Idempotency-Key` header and `/scalar/` serves the UI; outside Development neither is mapped (404 Problem Details).
- [x] **Architecture tests** green: `RimBank.Party.PartyReferenceDataDirectory.Api` references only `RimBank.Core` (its own namespace), SharedKernel, `RimBank.Problems`, `RimBank.Http`, ASP.NET Core and Wolverine; no `.Postgresql`, no other context; anonymous endpoints only in `RimBank.ServiceDefaults`; test namespaces end with `.Tests`.
- [x] `RimBank.Host.Tests` green after the `TestTokens` extraction; `RimBank.Problems.Tests` green (plus `shared.request-malformed`, also tested at host level in both environments); `RimBank.Core.Tests` unchanged.
- [ ] Locked restore, `dotnet build -c Release` (warnings as errors), `dotnet test --solution RimBank.slnx`, `dotnet format --verify-no-changes` and CodeQL: green in CI.

## Manual / demo checks
- [x] Demo: `dotnet test tests/Party/RimBank.Party.PartyReferenceDataDirectory.Api.Tests` with Docker running, all green. `RegisterPartyEndpointTests` shows a real HTTP Register committed to PostgreSQL.
- [x] Reading check: the endpoint holds no business rule (no `Decide`, no state), only request parsing, one `InvokeAsync` and response mapping. No domain type is serialised.

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 1.5 ticked in the same PR
- [x] ADR 0024 amended (validation code, ignored request fields); ADR 0015 amended if a shared code is added; no new ADR needed
- [x] No Retrieve, view, projection, `If-Match` or MCP code added (1.6, 1.7, 1.8, 1.16); `RimBank.Core` and `doc-0/` untouched
- [x] No secrets in source; no request body or PII logged by the endpoint (the request may carry `PartyReference`, which is ignored and never logged)
