# 0006. Result and Error model, and the error code convention

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.3

## Context

Mission principle 3 says expected business failures are values (`Result`), not exceptions. Principle 8 says every error is returned as RFC 9457 Problem Details with a **stable, documented error code**, on both HTTP and MCP. `specs/tech-stack.md` §5 fixes the Problem Details shape: a `type` URN such as `urn:rimbank:problem:party:party-not-found`, an `errorCode` extension, and an `errors` (field → messages) extension for validation failures. §4 allows `RimBank.SharedKernel` to reference the BCL only.

Every later phase depends on this vocabulary: decisions (0.6), command and query handlers (0.11, 1.3, 1.6), the Problem Details mapping (0.9, 0.16), idempotency conflicts (0.12) and the funds check (4.7). Changing it later would touch every context, so the shape and the code format are fixed now.

## Options considered

1. **A third-party Result library** (for example FluentResults, ErrorOr, CSharpFunctionalExtensions). Ready made, but it breaks the BCL-only rule for SharedKernel, brings its own error model that we would have to bend to Problem Details, and puts a dependency at the centre of every core.
2. **Exceptions for business failures**, mapped by a global handler. Familiar, but it hides failures from signatures, makes decisions impure and costs a stack trace per refusal.
3. **A small hand-written `Result<T>` / `Result` and `Error`** in SharedKernel, shaped for Problem Details from the start.

## Decision

Option 3.

- **`Error`** is a `sealed record` with `Code`, `Message`, `Kind` and `FieldErrors` (`IReadOnlyDictionary<string, IReadOnlyList<string>>`, empty unless set). It is created through one factory per kind (`Error.NotFound(code, message)`, `Error.Validation(code, message, fieldErrors)`, …). Field errors are copied on creation, so an `Error` is immutable, and they take part in equality regardless of order.
- **`ErrorKind`** and the status each adapter will map it to (applied in 0.9 for HTTP and 0.16 for MCP):

  | Kind | HTTP status | Use for |
  |---|---|---|
  | `Validation` | 400 | Malformed or incomplete input; `FieldErrors` feeds `errors` |
  | `Unauthorized` | 401 | Caller not authenticated |
  | `Forbidden` | 403 | Caller not allowed |
  | `NotFound` | 404 | The record does not exist |
  | `Conflict` | 409 | Stale `If-Match` version, idempotency key reused with another payload |
  | `BusinessRule` | 422 | Valid input refused by a rule, for example insufficient funds (4.7) |
  | `PreconditionRequired` | 428 | A precondition the operation needs was not sent, for example a missing `If-Match` (added in 1.8, [ADR 0027](0027-optimistic-concurrency-for-commands.md)) |
  | `Unexpected` | 500 | An expected failure that is not the caller's fault |

  Rate limiting (429) is an infrastructure concern (7.1) and is not a domain kind.
- **Error code convention:** `<context>.<error-name>`. Both segments are lower-case kebab-case: they start with a letter and contain letters, digits and single hyphens (`^[a-z][a-z0-9]*(-[a-z0-9]+)*\.[a-z][a-z0-9]*(-[a-z0-9]+)*$`). `<context>` is the service domain (or business domain) in lower case without separators, for example `party`, `currentaccount`, or `shared` for cross-cutting errors. Examples: `party.party-not-found`, `currentaccount.insufficient-funds`, `shared.validation-failed`. Adapters derive `type = urn:rimbank:problem:<context>:<error-name>` and `errorCode = <code>`. A code is part of the public contract: once released, it is never renamed or reused for another meaning.
- **Invalid input to `Error` is a programmer error:** a code that breaks the convention, or an empty message, throws `ArgumentException` at construction, so a bad code fails the first test that creates it.
- **`Result<T>`** is a sealed class that is either a success with a non-null value or a failure with an `Error`. Reading `Value` on a failure, or `Error` on a success, throws `InvalidOperationException`. Absence is a failure (`NotFound`), never a null success. The non-generic **`Result`** serves commands that only acknowledge. Results are created with `Result.Success(value)`, `Result.Failure<T>(error)`, `Result.Success()`, `Result.Failure(error)`, or an implicit conversion from a value or an `Error`.
- **Combinators:** `Match`, `Map`, `Bind`, `MapError` and `Tap`; async `MapAsync`, `BindAsync` and `MatchAsync` over `Task<Result<T>>`; and LINQ `Select`/`SelectMany` so several results can be combined with `from … select`. A failure always short-circuits: later functions are not called.
- **Exceptions stay** for programmer errors (broken invariants of these types, null arguments) and infrastructure faults (database down). Adapters turn unhandled exceptions into a generic 500 Problem Details response (0.9).

## Consequences

- Handlers and decisions return `Result<T>` or `Result`, so the possible failures are visible in their signatures and can be tested without catching exceptions.
- Each context defines its errors as static members in business language (for example `PartyErrors.PartyNotFound`), and the code format is checked by the first test that touches them.
- 0.9 and 0.16 implement the kind → status table above in one place per adapter. Any change to the table needs a new ADR.
- `Error` collects field errors for a single validation failure. Accumulating several independent errors (applicative validation) is not supported; if a later phase needs it, it is added with its own ADR.
- The type is named `Error` as the constitution prescribes. CA1716 (it is a Visual Basic keyword) is suppressed on that type only, because RimBank is C# only.
- Example:

  ```csharp
  public static Result<PartyReferenceDataDirectoryEntryView> Retrieve(PartyReferenceDataDirectoryEntryView? view) =>
      view is null
          ? Error.NotFound("party.party-not-found", "No party with this identifier.")
          : view;
  ```
