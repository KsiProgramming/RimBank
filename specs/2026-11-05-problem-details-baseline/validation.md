# Validation: Problem Details baseline

## Done when (from roadmap)
> **0.9 Problem Details baseline:** global exception handler, `Error` → Problem Details mapping convention, 404/500 tests asserting `application/problem+json`.
> + Definition of done (roadmap.md)

Phase description → checks:
- *Global exception handler* → `ProblemDetailsEndpointTests` (500 cases)
- *`Error` → Problem Details mapping convention* → `ErrorStatusTests`, `ErrorProblemDetailsTests`, `ProblemTypeProperties`, ADR 0015
- *404/500 tests asserting `application/problem+json`* → `ProblemDetailsEndpointTests`

Definition of done items that don't apply: this phase adds no command, query or read model (no CQRS split, projection test or rebuild) and no BIAN operation (no MCP parity; the MCP side of the error contract is 0.16).

## Automated checks
- [ ] `tests/RimBank.Problems.Tests`:
  - [ ] Each `ErrorKind` maps to the ADR 0006 status (400, 401, 403, 404, 409, 422, 500), and every enum value is covered
  - [ ] `type` is `urn:rimbank:problem:<context>:<error-name>`, `errorCode` equals `Error.Code`, `detail` equals `Error.Message`, `title` is the reason phrase
  - [ ] `errors` is present only for `Validation` errors that have field errors
  - [ ] FsCheck: every valid code produces a well-formed absolute URN
- [ ] `tests/RimBank.Host.Tests` (`WebApplicationFactory<Program>`, `Development` and `Production`):
  - [ ] Unknown route → 404 `application/problem+json`, `errorCode` `shared.not-found`, with `traceId` and `instance`
  - [ ] Throwing endpoint → 500 `application/problem+json`, `errorCode` `shared.unexpected`, no exception message, type name or stack frame in the body, in both environments
  - [ ] A `NotFound` `Error` returned by an endpoint → 404 with the context's URN; a `Validation` `Error` → 400 with `errors`
  - [ ] `traceId` matches the incoming `traceparent`
  - [ ] Health probe tests still green
- [ ] `tests/RimBank.Architecture.Tests`: `RimBank.Problems` used only by `.Api`, `.Mcp` and Host; `RimBank.Problems` references only SharedKernel, ASP.NET Core and the BCL; each rule fails on its bad fixture; the namespace rule accepts `RimBank.Problems`
- [ ] `dotnet build` with no warnings (`TreatWarningsAsErrors`), lock files committed, `dotnet format --verify-no-changes`
- [ ] GitHub Actions CI and CodeQL green

## Manual / demo checks
- [ ] `dotnet run --project src/RimBank.Host`, then send the `RimBank.Host.http` request `GET /PartyReferenceDataDirectory/does-not-exist/Retrieve` → `404`, `Content-Type: application/problem+json`, body similar to:
  ```json
  {
    "type": "urn:rimbank:problem:shared:not-found",
    "title": "Not Found",
    "status": 404,
    "detail": "No resource exists at this address.",
    "instance": "/PartyReferenceDataDirectory/does-not-exist/Retrieve",
    "errorCode": "shared.not-found",
    "traceId": "<32 hex characters>"
  }
  ```
- [ ] Through the AppHost (`dotnet run --project src/RimBank.AppHost`), the same request shows up in the Aspire dashboard trace whose ID equals `traceId`
- [ ] `/health` still returns `Healthy` as `text/plain`

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox **0.9** ticked in the same PR
- [ ] ADR 0015 written; tech-stack §3.3, §4 and §5 updated
- [ ] No exception messages, stack traces or PII in any response body; exceptions are logged with the `traceId`
- [ ] No in-code warning suppressions (ADR 0013); package versions only in `Directory.Packages.props`
