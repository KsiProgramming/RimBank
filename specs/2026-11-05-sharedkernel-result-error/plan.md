# Plan: SharedKernel: Result & Error

Each group is one commit. The solution builds and the tests pass after each one. Groups 2 and 3 are written test-first (red → green → refactor).

## 1. Projects, test platform and CI
- [ ] `src/RimBank.SharedKernel/RimBank.SharedKernel.csproj` (class library, no `PackageReference` or `ProjectReference`; it inherits `Directory.Build.props`)
- [ ] `tests/RimBank.SharedKernel.Tests/RimBank.SharedKernel.Tests.csproj` (`OutputType=Exe`, references `xunit.v3` and `Shouldly`, plus a `ProjectReference` to SharedKernel)
- [ ] `Directory.Packages.props`: `PackageVersion` for `xunit.v3` and `Shouldly` (latest stable)
- [ ] `RimBank.slnx`: add both projects under `/src/` and `/tests/`
- [ ] `global.json`: add `"test": { "runner": "Microsoft.Testing.Platform" }`
- [ ] One smoke test (`SharedKernelAssemblyTests`) so the test step runs something; delete it in group 2
- [ ] Answer the open question: run `dotnet test --solution RimBank.slnx -c Release` locally, then pick the TRX reporting option and `--results-directory`
- [ ] `.github/workflows/ci.yml`: change the test step to the MTP command line; keep the artifact upload
- [ ] Check locally: restore, build (Release, warnings as errors), test, `dotnet format --verify-no-changes`

## 2. Error and ErrorKind (test-first)
- [ ] `tests/.../ErrorTests.cs`: a factory per kind sets `Kind`; value equality; `FieldErrors` empty by default and kept when given; valid codes are accepted (`party.party-not-found`, `shared.validation-failed`); invalid codes are rejected with `ArgumentException` (empty, no dot, upper case, `_`, leading or trailing `-`, three segments); an empty message is rejected
- [ ] `src/RimBank.SharedKernel/ErrorKind.cs`: `Validation`, `NotFound`, `Conflict`, `BusinessRule`, `Unauthorized`, `Forbidden`, `Unexpected`
- [ ] `src/RimBank.SharedKernel/Error.cs`: `sealed record Error`, a code check using a source-generated `[GeneratedRegex]`, factories `Validation(code, message, fieldErrors?)`, `NotFound`, `Conflict`, `BusinessRule`, `Unauthorized`, `Forbidden`, `Unexpected`; `FieldErrors` is an `IReadOnlyDictionary<string, string[]>` copied on construction (immutable), and equality compares its contents
- [ ] XML docs on the public API, explaining the code convention

## 3. Result and Result<T> (test-first)
- [ ] `tests/.../ResultOfTTests.cs`: Success/Failure state; `Value` on a failure and `Error` on a success throw `InvalidOperationException`; `Success(null)` throws; implicit conversions; `Match` takes the right branch; `Map`/`Bind`/`MapError`/`Tap` run on success and are skipped (passing the same `Error` through) on failure; identity and composition examples for `Map`, associativity example for `Bind`
- [ ] `tests/.../ResultTests.cs`: the same for non-generic `Result`, including `Bind` to `Result<T>`
- [ ] `tests/.../ResultAsyncTests.cs`: `MapAsync`, `BindAsync`, `Match` on `Task<Result<T>>`; a failure short-circuits and the async delegate is not called
- [ ] `tests/.../ResultQuerySyntaxTests.cs`: `from a in r1 from b in r2 select …` succeeds and returns the first failure
- [ ] `src/RimBank.SharedKernel/Result.cs`: non-generic `Result`
- [ ] `src/RimBank.SharedKernel/ResultOfT.cs`: `Result<T>` (sealed, private constructor, `Success`/`Failure`, implicit operators, `Match`, `Map`, `Bind`, `MapError`, `Tap`)
- [ ] `src/RimBank.SharedKernel/ResultAsyncExtensions.cs`, `ResultQueryExtensions.cs`
- [ ] Remove the smoke test from group 1

## 4. ADRs, docs and roadmap
- [ ] `docs/adr/0006-result-and-error-model.md` (MADR, status Accepted): failures as values, the kinds, the planned kind → status table for 0.9 (Validation 400, Unauthorized 401, Forbidden 403, NotFound 404, Conflict 409, BusinessRule 422, Unexpected 500), the code format and its URN, `FieldErrors`
- [ ] `docs/adr/0007-test-platform.md` (MADR, status Accepted): xUnit v3 on MTP, the `global.json` setting, the CI command line, TRX reporting
- [ ] `CONTRIBUTING.md`, "Building and testing": the updated `dotnet test` command; a short "Errors" note pointing to ADR 0006 for how to name error codes
- [ ] `specs/2026-11-05-ci-pipeline/requirements.md`: mark the "Test runner mode" open question as answered (link ADR 0007)
- [ ] `specs/roadmap.md`: tick **0.3 SharedKernel: Result & Error**
