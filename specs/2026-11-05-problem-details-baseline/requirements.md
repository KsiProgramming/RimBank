# Problem Details baseline

Roadmap phase: 0.9
Branch: plan/0.9-problem-details-baseline

## Context
Mission principle 8 says every error is RFC 9457 Problem Details with a stable, documented error code, on HTTP and on MCP. ADR 0006 (0.3) fixed the `Error` / `ErrorKind` vocabulary, the `<context>.<error-name>` code convention and the kind → HTTP status table, and left applying them to this phase (HTTP) and to 0.16 (MCP). `RimBank.Host` (0.8, ADR 0014) is the first runnable piece, so this is the point where every non-2xx response becomes `application/problem+json`. Without it, phases 0.10 to 0.16 would ship error paths that don't meet the Definition of done.

It depends on 0.3 (`Error`, `ErrorKind`), 0.7 (architecture tests) and 0.8 (`RimBank.Host`, the module convention). All of them are merged. It unblocks 1.5 and 1.7 (the first `.Api` endpoints that map `Error`), 0.12 (409 on a reused idempotency key), 0.13 (401) and 0.16 (MCP `isError` results that carry the same JSON).

Relevant constitution:
- [mission.md](../mission.md), principle 8 (uniform, standard errors) and principle 9 (production-grade from the first commit: no internal details leak).
- [tech-stack.md §5](../tech-stack.md#5-api--error-contract): `type` URN `urn:rimbank:problem:<context>:<error-name>`, `title`, `status`, `detail`, `instance`, the `errorCode`, `traceId` and `errors` extensions; "each core `Error` maps to a Problem Details response in one place"; unhandled exceptions become a generic 500.
- [tech-stack.md §4](../tech-stack.md#4-dependency-rules-enforced-by-architecture-tests): SharedKernel references the BCL only, so the mapping cannot live there.
- ADR 0006 (kind → status table, code convention), ADR 0011 (namespace rule, allowed non-context roots), ADR 0014 (host composition).
- BIAN: `doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml` declares the 404/429/500 responses with its own `HTTPError {status_code, status, message}` schema as `application/json`. RimBank replaces this with RFC 9457 (a BIAN-interpretation decision, recorded in ADR 0015).

**CQRS split:** this phase adds no command, query, handler or read model. The mapping is an adapter concern that both command endpoints (an `Error` from a decision) and query endpoints (`NotFound` from a read port) will use from 1.5 on. There is no projection, so there is no inline or async choice.

## Scope
### In scope
- **New shared adapter library `src/RimBank.Problems`** (framework reference `Microsoft.AspNetCore.App`; references `RimBank.SharedKernel`; no business logic):
  - `ErrorStatus.For(ErrorKind) → int`: the ADR 0006 table as one switch expression. It is the only place the table lives.
  - `ProblemType.For(Error)` → `urn:rimbank:problem:<context>:<error-name>`, derived from `Error.Code`.
  - `ErrorProblemDetails.Create(Error, string? instance)` → `ProblemDetails` (`HttpValidationProblemDetails` with `errors` when `Kind` is `Validation` and `FieldErrors` is not empty), with `title` = the status reason phrase, `detail` = `Error.Message`, and the `errorCode` extension.
  - `Error.ToHttpResult()` → `IResult` (`TypedResults.Problem` / `ValidationProblem`), the one-liner that `.Api` endpoints will use.
  - `SharedProblems`: the generic codes the host emits itself, `shared.not-found` (an unmatched route) and `shared.unexpected` (an unhandled exception).
- **Host wiring** in `RimBank.Host` (`HostComposition`):
  - `AddProblemDetails` with `CustomizeProblemDetails`, which adds `traceId` (the W3C trace ID from `Activity.Current`, falling back to `HttpContext.TraceIdentifier`) and `instance` (the request path) to **every** Problem Details response.
  - A global `IExceptionHandler` that logs the exception and writes a generic 500 with `type` `urn:rimbank:problem:shared:unexpected`, `errorCode` `shared.unexpected` and a fixed `detail`. It applies **in every environment, Development included**, so no exception message or stack trace is ever in a response body.
  - Status-code pages, so an unmatched route (404 with no body) returns Problem Details with `shared.not-found`.
- **Architecture rules**: `RimBank.Problems` may be referenced only by `.Api`, `.Mcp` and `RimBank.Host`, and may itself reference only SharedKernel, ASP.NET Core and the BCL. Each rule gets a bad fixture. `RimBank.Problems` is added to the allowed non-context roots of the namespace rule.
- **Constitution updates**: tech-stack §3.3 (layout) and §4 (dependency table row, `.Api`/`.Mcp` may reference `RimBank.Problems`).
- **Demo**: a `GET` to an unknown route in `src/RimBank.Host/RimBank.Host.http`.

### Out of scope
- Minimal API binding and JSON-parse 400s, 405 and 415: these still return Problem Details through status-code pages, but with the framework's default `type`. They get `shared.*` codes later, when an endpoint first accepts a body (1.5).
- 401/403 (0.13), 409 for idempotency (0.12) and concurrency (1.8), 429 (7.1): each adds its own `shared.*` code in its phase.
- The MCP `isError` tool result (0.16); it reuses `ErrorProblemDetails.Create`.
- A golden-file snapshot of the JSON shape (not selected; 0.16's parity test compares HTTP and MCP directly).
- Any real context or `.Api` project (Stage 1).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Where the mapping lives | A new shared library, `RimBank.Problems`, referenced by every `.Api`, `.Mcp` and the host | SharedKernel is BCL-only (§4); one copy of the kind → status table (ADR 0006 "one place"); 0.16 reuses it for MCP |
| Library name | `RimBank.Problems`, not `RimBank.SharedKernel.ProblemDetails` | Any namespace under `RimBank.SharedKernel` falls under the BCL-only architecture rule; a `.ProblemDetails` namespace would also clash with ASP.NET Core's `ProblemDetails` type |
| BIAN `HTTPError` schema | Replaced by RFC 9457 `application/problem+json` | Mission principle 8; BIAN's schema has no stable code, type or trace ID. Recorded as a BIAN interpretation in ADR 0015 |
| 500 response | Generic in all environments (Development too); details only in logs/traces | Principle 9; one behaviour to test; `traceId` links the response to the log |
| Host-generated codes | `shared.not-found`, `shared.unexpected` | Follows the ADR 0006 code convention (`shared` context) |
| `traceId` / `instance` | Added centrally in `CustomizeProblemDetails` | Every Problem Details response gets them, whether it comes from the mapper, the exception handler or status-code pages |
| Work order | ADR → mapping library → host wiring → host tests → architecture rule | Chosen in the interview; each step builds green |

## ADRs to write
- **ADR 0015: Problem Details mapping and the `RimBank.Problems` library.** Covers where the library sits and its allowed references, RFC 9457 instead of BIAN `HTTPError`, the generic 500 in every environment, the `shared.*` host codes, and where `traceId` and `instance` come from. It points to ADR 0006 for the kind → status table and doesn't change that table.

## Open questions
- None.
