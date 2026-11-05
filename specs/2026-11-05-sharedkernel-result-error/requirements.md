# SharedKernel: Result & Error

Roadmap phase: 0.3
Branch: plan/0.3-sharedkernel-result-error

## Context
This is the first phase with C# code. It adds `RimBank.SharedKernel` and its test project, so it also sets the test platform used by every later phase and turns on the CI test step and the CodeQL `csharp` leg prepared in 0.2.

`Result<T>` and `Error` are the failure vocabulary for the whole core: decisions (0.6), command and query handlers (0.11, 1.3, 1.6), the `Error` → Problem Details mapping (0.9, 0.16) and idempotency conflicts (0.12) all build on them.

Relevant constitution:
- [mission.md](../mission.md), principle 3: "Expected business failures are values (`Result`), not exceptions"; types are sealed by default.
- [mission.md](../mission.md), principle 8: every error has a stable, documented error code and becomes RFC 9457 Problem Details on HTTP and MCP.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): `RimBank.SharedKernel` references the **BCL only**, so this is hand-written with no third-party Result library.
- [tech-stack.md §5](../tech-stack.md#5-api--error-contract): Problem Details `type` URN (`urn:rimbank:problem:party:party-not-found`), `errorCode`, and the `errors` (field → messages) extension for validation failures.
- [tech-stack.md §7](../tech-stack.md#7-domain-modelling-conventions) and [§8.2](../tech-stack.md#82-read-side): handlers return `Result<T>`; `NotFound` is an `Error`, which becomes a 404.
- [tech-stack.md §9](../tech-stack.md#9-testing-stack): xUnit v3 + Shouldly.

There is no BIAN service domain in this phase. It adds no commands, queries or read models, but it provides the return types that both sides use: command handlers return `Result<T>` (acknowledgement), and query handlers return `Result<TView>`.

## Scope
### In scope
- `src/RimBank.SharedKernel` (namespace `RimBank.SharedKernel`, BCL only) and `tests/RimBank.SharedKernel.Tests`, added to `RimBank.slnx`.
- `ErrorKind` enum: `Validation`, `NotFound`, `Conflict`, `BusinessRule`, `Unauthorized`, `Forbidden`, `Unexpected`.
- `Error`: `sealed record` with `Code`, `Message`, `Kind` and optional `FieldErrors` (field → messages, `IReadOnlyDictionary<string, IReadOnlyList<string>>`), plus factory methods per kind (`Error.NotFound(code, message)`, `Error.Validation(code, message, fieldErrors)`, …).
- **Error code convention:** `<context>.<error-name>`, lower-case kebab-case segments (for example `party.party-not-found`, `shared.validation-failed`), checked when the `Error` is created (a bad code is a programmer error, so it throws `ArgumentException`). 0.9 maps it to `urn:rimbank:problem:<context>:<error-name>`.
- `Result<T>`: sealed, immutable, either a success with a value or a failure with an `Error`, created with `Result.Success(value)` / `Result.Failure<T>(error)`. `IsSuccess`/`IsFailure`, `Match`, `Map`, `Bind`, `MapError`, `Tap`, and implicit conversions from `T` and `Error`.
- `Result` (non-generic) for commands that only acknowledge, with `Result.Success()` / `Result.Failure(error)` and the same combinators.
- Async helpers: `MapAsync` and `BindAsync` (and `MatchAsync` over `Task<Result<T>>`) for handlers that await ports.
- LINQ query syntax: `Select` and `SelectMany` over `Result<T>`.
- Test platform: xUnit v3 on Microsoft.Testing.Platform (`global.json` `test.runner`), with the CI test step changed to match.
- ADR 0006 (Result & Error model and error code convention) and ADR 0007 (test platform).

### Out of scope
- Mapping `Error` to Problem Details, the URN `type`, `traceId`, and HTTP status codes: 0.9 (HTTP) and 0.16 (MCP). This phase only fixes the code format they rely on.
- Collecting several errors in one result (applicative validation). For now `FieldErrors` on a single `Validation` error is enough.
- FsCheck law tests: FsCheck arrives in 0.5 (Money). The combinator laws are covered by example-based tests here.
- Coverage collection or thresholds: not on the roadmap.
- ArchUnitNET's "SharedKernel is BCL only" rule: 0.7.
- Typed IDs (0.4), Money (0.5), decider and projection abstractions (0.6).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Library vs hand-written | Hand-written | SharedKernel must be BCL only (tech-stack §4); the type is small and central, so we want full control of it |
| `Result<T>` shape | `sealed class` with an internal constructor and two states; factories live on the non-generic `Result` (CA1000 forbids static members on generic types, and `T` is inferred); `Value` / `Error` throw `InvalidOperationException` when read in the wrong state | Sealed by default (principle 3); no inheritance hierarchy to pattern-match; reading `Value` from a failure is a programmer error |
| `Error` shape | `sealed record Error` with get-only `Code`, `Message`, `Kind` + `IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors` (empty by default, copied on creation, order-independent equality); CA1716 suppressed on the type | Value equality for tests; `FieldErrors` feeds the Problem Details `errors` extension (tech-stack §5) without changing the type later (user choice) |
| `ErrorKind` set | Validation, NotFound, Conflict, BusinessRule, Unauthorized, Forbidden, Unexpected | Covers BIAN statuses 400/401/403/404/409/500 and 422 for business rules (4.7); 429 is infrastructure (7.1), so it is not a domain kind |
| Error code format | `<context>.<error-name>`, regex `^[a-z][a-z0-9]*(-[a-z0-9]+)*\.[a-z][a-z0-9]*(-[a-z0-9]+)*$`, validated in the constructor | Stable, documented codes (principle 8); maps 1:1 to the URN `type` (user choice) |
| Non-generic `Result` | Yes | Commands that only acknowledge must not invent a dummy payload |
| Async + LINQ helpers | `MapAsync`, `BindAsync`, `Match` on `Task<Result<T>>`; `Select`/`SelectMany` | User choice; keeps handler code flat when chaining ports |
| Null handling | `Result<T>.Success(null)` throws `ArgumentNullException` for reference types | A success always has a value; absence is `NotFound` |
| Test platform | xUnit v3 on Microsoft.Testing.Platform, enabled with `global.json` `"test": { "runner": "Microsoft.Testing.Platform" }` | Default for xUnit v3 templates on the .NET 10 SDK; answers the open question from 0.2 |
| Package versions | `xunit.v3` and `Shouldly` in `Directory.Packages.props` (CPM), with the latest stable versions at implementation time | tech-stack §1, §9 |

## ADRs to write
- **0006: Result & Error model and error code convention**: failures as values, the `ErrorKind` set and its intended HTTP status mapping (applied in 0.9), the code format and its URN mapping, `FieldErrors`, and why exceptions stay for programmer and infrastructure faults.
- **0007: Test platform**: xUnit v3 on Microsoft.Testing.Platform, the `global.json` setting, the CI `dotnet test` command line, and how the test results are reported (TRX).

## Open questions
- ~~**`dotnet test` arguments in MTP mode**~~ Answered during group 1 (SDK 10.0.301, xunit.v3 4.0.2): `dotnet test --solution RimBank.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults`. xUnit's built-in `--report-xunit-trx` is enough, and a relative `--results-directory` resolves against the working directory. Recorded in ADR 0007; `ci.yml` and CONTRIBUTING updated.
- **Test method names:** the `latest-recommended` analysers flag underscores in test names (CA1707). Answered: CA1707 is off for `tests/**` in `.editorconfig` (ADR 0007).
