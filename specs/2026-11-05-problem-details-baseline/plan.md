# Plan: Problem Details baseline

## 1. ADR and constitution
- [x] `docs/adr/0015-problem-details-mapping.md` (from `template.md`): the decisions in requirements.md, with the BIAN `HTTPError` interpretation and a JSON example of a 404 and a 400 with `errors`
- [x] `specs/tech-stack.md` §3.3: add `src/RimBank.Problems/` and `tests/RimBank.Problems.Tests/` to the layout
- [x] `specs/tech-stack.md` §4: add a `RimBank.Problems` row (SharedKernel, ASP.NET Core; must not reference contexts or Host), and add it to the "may reference" column of `.Api` and `.Mcp`
- [x] `specs/tech-stack.md` §5: link ADR 0015 from "each core `Error` maps … in one place"

## 2. Mapping library (`RimBank.Problems`)
- [x] `src/RimBank.Problems/RimBank.Problems.csproj`: `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, a reference to `RimBank.SharedKernel`, lock file; add to `RimBank.slnx`
- [x] `ErrorStatus.cs`: `For(ErrorKind)` as a switch expression (ADR 0006 table); an unknown kind throws `ArgumentOutOfRangeException`
- [x] `ProblemType.cs`: `For(Error)` → `urn:rimbank:problem:<context>:<error-name>`, plus `Prefix` const
- [x] `ErrorProblemDetails.cs`: `Create(Error, string? instance)` → `ProblemDetails` / `HttpValidationProblemDetails`; `errorCode` extension; `title` from `ReasonPhrases.GetReasonPhrase(status)`
- [x] `ErrorHttpResults.cs`: `ToHttpResult(this Error)` → `IResult`
- [x] `SharedProblems.cs`: `NotFound` (`shared.not-found`) and `Unexpected` (`shared.unexpected`) `Error` instances with fixed business-language messages
- [x] `tests/RimBank.Problems.Tests` (xUnit v3, Shouldly, FsCheck; namespace `RimBank.Problems.Tests`; add to `RimBank.slnx`):
  - [x] `ErrorStatusTests`: one case per `ErrorKind` (400, 401, 403, 404, 409, 422, 500), plus a test that every enum value is mapped
  - [x] `ErrorProblemDetailsTests`: `type`, `title`, `status`, `detail`, `instance`, `errorCode`; `errors` only for `Validation` with field errors; no `errors` key otherwise
  - [x] `ProblemTypeProperties` (FsCheck): any valid code gives an absolute URI `urn:rimbank:problem:{a}:{b}` whose segments match the code

## 3. Host wiring
- [x] `RimBank.Host.csproj`: reference `RimBank.Problems`
- [x] `src/RimBank.Host/Errors/UnhandledExceptionHandler.cs`: `IExceptionHandler` that logs at `Error` level (exception + traceId) and writes `ErrorProblemDetails.Create(SharedProblems.Unexpected, …)` through `IProblemDetailsService`
- [x] `src/RimBank.Host/Errors/ProblemDetailsComposition.cs`: `AddRimBankProblemDetails()` (`AddProblemDetails` + `CustomizeProblemDetails` for `traceId`/`instance`, exception handler registration), and `UseRimBankProblemDetails()` (`UseExceptionHandler()`, `UseStatusCodePages()`), which maps a bodiless 404 to `SharedProblems.NotFound`
- [x] `HostComposition.Build`: call both before the modules are added and mapped, so module endpoints are covered; the exception handler also runs in Development, replacing the developer exception page
- [x] `RimBank.Host.http`: `GET {{host}}/PartyReferenceDataDirectory/does-not-exist/Retrieve`, with the expected 404 Problem Details response in a comment

## 4. Host tests (`tests/RimBank.Host.Tests/Errors/`, namespace `RimBank.Host.Errors.Tests`)
- [x] `FaultyServiceDomainModule` (test-only `IServiceDomainModule`): `GET /faulty/boom` throws `InvalidOperationException("internal-secret")`; `GET /faulty/missing` returns `Error.NotFound("sample.sample-not-found", …).ToHttpResult()`; `GET /faulty/invalid` returns a `Validation` error with field errors
- [x] `ProblemDetailsEndpointTests`, theory over `Development` and `Production`:
  - [x] an unknown route → 404, `application/problem+json`, `errorCode` `shared.not-found`, `traceId` and `instance` present
  - [x] `/faulty/boom` → 500, `application/problem+json`, `errorCode` `shared.unexpected`; the body doesn't contain `internal-secret`, `InvalidOperationException` or `   at `
  - [x] `/faulty/missing` → 404 with `type` `urn:rimbank:problem:sample:sample-not-found`
  - [x] `/faulty/invalid` → 400 with `errors` containing the field messages
  - [x] `traceId` in the body equals the `traceparent` trace ID when the request sends one
- [x] Existing `HealthEndpointTests` stay green (the probes aren't affected)

## 5. Architecture rules and wrap-up
- [x] `ArchitectureRules.Composition.cs` (or a new `ArchitectureRules.Problems.cs`): `ProblemsOnlyInAdaptersAndHost` (only `.Api`, `.Mcp`, `RimBank.Host` and `RimBank.Problems` itself depend on `RimBank.Problems`) and `ProblemsDependencies` (SharedKernel, ASP.NET Core and BCL only)
- [x] Bad fixtures in `RimBank.Architecture.Fixtures/Bad/` (a core type and a `.Postgresql` type using `RimBank.Problems`), a stub for `RimBank.Problems` types if needed, and a `FixtureRulesTests` case per rule
- [x] Add `RimBank.Problems` to the allowed non-context roots (namespace rule, ADR 0011) and to the solution scan in `CodeBases`
- [x] `dotnet format --verify-no-changes`, `dotnet test` green locally
- [x] Tick **0.9** in `specs/roadmap.md`
