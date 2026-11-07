# 0015. Problem Details mapping and the RimBank.Problems library

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.9

## Context

Mission principle 8 says every error is RFC 9457 Problem Details with a stable, documented error code, on HTTP and on MCP. [ADR 0006](0006-result-and-error-model.md) fixed the `Error` / `ErrorKind` vocabulary, the `<context>.<error-name>` code convention and the kind → HTTP status table. It left applying them to this phase (HTTP) and to 0.16 (MCP). [tech-stack.md §5](../../specs/tech-stack.md#5-api--error-contract) fixes the response shape: a `type` URN, `title`, `status`, `detail`, `instance`, and the `errorCode`, `traceId` and `errors` extensions. It also says unhandled exceptions become a generic 500.

Three forces shape where this code lives and what it emits:

- **SharedKernel is BCL-only** (§4), so it cannot hold an ASP.NET Core `ProblemDetails` mapping. The architecture rule `shared-kernel-dependencies` covers every namespace under `RimBank.SharedKernel`.
- **Every `.Api`, every `.Mcp` (0.16) and the host itself** emit Problem Details. If each context wrote its own mapping, the kind → status table would be copied once per context and could drift.
- **BIAN declares its own error body.** The ext-DDD OpenAPI files (for example `doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml`) answer 404, 429 and 500 with an `HTTPError` schema `{status_code, status, message}` served as `application/json`. It has no stable error code, no type and no trace ID.

## Options considered

1. **A mapping per context in each `.Api`**, following a written convention. Nothing new to reference, but the table is duplicated in every context and in the host, and MCP has to copy it again.
2. **A mapping in `RimBank.Host` only.** One copy, but `.Api` and `.Mcp` may not reference the host (`host-is-the-composition-root`), so an endpoint could not turn its own `Error` into a response.
3. **A small shared adapter library, `RimBank.Problems`**, which references SharedKernel and ASP.NET Core and is referenced by `.Api`, `.Mcp` and the host.

On the BIAN body: keep BIAN's `HTTPError`, emit both shapes, or use RFC 9457 only.

## Decision

Option 3, and RFC 9457 only.

- **`RimBank.Problems`** (`src/RimBank.Problems`, a framework reference to `Microsoft.AspNetCore.App`) holds the one and only mapping:
  - `ErrorStatus.For(ErrorKind)`: the ADR 0006 table, as one switch expression.
  - `ProblemType.For(Error)`: `urn:rimbank:problem:<context>:<error-name>`, made from `Error.Code`.
  - `ErrorProblemDetails.Create(Error, instance)`: `type`, `title` (the status reason phrase), `status`, `detail` (`Error.Message`), `instance` and the `errorCode` extension. A `Validation` error that has field errors becomes an `HttpValidationProblemDetails` with `errors`.
  - `Error.ToHttpResult()`: the one-liner an endpoint returns.
  - `ProblemDetailsEnrichment.Apply(ProblemDetails, HttpContext)`: adds `traceId` and a missing `instance` to any Problem Details response.
  - `SharedProblems`: the codes the host emits itself.
- **Its name is not `RimBank.SharedKernel.*`.** Any namespace under `RimBank.SharedKernel` falls under the BCL-only rule, and a `.ProblemDetails` namespace would clash with ASP.NET Core's `ProblemDetails` type. `RimBank.Problems` is a composition project in the sense of ADR 0011: it holds no business domain.
- **Who may use it:** `.Api`, `.Mcp`, `RimBank.Host` and `RimBank.Problems` itself (`problems-only-in-adapters-and-host`). It references only SharedKernel, ASP.NET Core and the BCL, never a context or a composition project (`problems-dependencies`). A core never sees it: cores return `Error`, and adapters turn it into Problem Details.
- **RFC 9457 replaces BIAN's `HTTPError`** for every non-2xx response, with the content type `application/problem+json`. This is a BIAN interpretation: we keep BIAN's status codes (400, 401, 403, 404, 429, 500, plus 409 and 422 from ADR 0006, and 428 for a missing `If-Match` from [ADR 0027](0027-optimistic-concurrency-for-commands.md)) but not its error body. `status` already carries `status_code`, `title` carries `status`, and `detail` carries `message`. The conformance harness (0.14) checks routes and methods, not the error body.
- **Codes the host emits itself** use the `shared` context of ADR 0006:

  | Code | Status | When |
  |---|---|---|
  | `shared.request-malformed` | 400 | The framework cannot read the request into the endpoint's parameters, for example a body that is missing, is not JSON, or has the wrong shape (added in roadmap 1.5) |
  | `shared.not-found` | 404 | No endpoint matches the request |
  | `shared.unexpected` | 500 | An exception reached the host |

  Later phases add their own `shared.*` codes (401/403 in 0.13, 409 in 0.12, 429 in 7.1). Other bodiless 4xx/5xx responses (for example 405) are still `application/problem+json`, with the framework's default `type`, until a phase gives them a code.
- **Unhandled exceptions** are logged, with the trace ID, by a global `IExceptionHandler`. The response is the generic `shared.unexpected` 500 **in every environment, Development included**: the exception's message, type and stack trace are never in a response body. Developers find them in the logs and the trace, through `traceId`. The one exception is `BadHttpRequestException`, which Minimal APIs throw in Development (and turn into a bodiless status elsewhere) when they cannot read a request: it is the caller's fault, so it gets its own status, and a 400 is `shared.request-malformed` in every environment. It is logged at Information without its message, which can quote the request (roadmap 1.5).
- **`traceId`** is the W3C trace ID of the current activity (32 hex characters, the ID the Aspire dashboard and OTLP backends show), falling back to `HttpContext.TraceIdentifier` when there is no activity. **`instance`** is the request path unless the mapping already set one. Both are added centrally in `CustomizeProblemDetails`, so every Problem Details response gets them, whatever produced it (an endpoint's `Error`, the exception handler or status-code pages).

Example (`GET /PartyReferenceDataDirectory/does-not-exist/Retrieve`):

```json
{
  "type": "urn:rimbank:problem:shared:not-found",
  "title": "Not Found",
  "status": 404,
  "detail": "No resource exists at this address.",
  "instance": "/PartyReferenceDataDirectory/does-not-exist/Retrieve",
  "errorCode": "shared.not-found",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

A validation failure adds `errors`:

```json
{
  "type": "urn:rimbank:problem:party:invalid-party-name",
  "title": "Bad Request",
  "status": 400,
  "detail": "The party name is not valid.",
  "instance": "/PartyReferenceDataDirectory/Register",
  "errorCode": "party.invalid-party-name",
  "errors": { "name": ["The name is required."] },
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

## Consequences

- An endpoint in a `.Api` turns a failed `Result` into a response with `error.ToHttpResult()` and never picks a status itself. Changing the kind → status table still needs a new ADR (ADR 0006), and now changes one file.
- 0.16 builds the MCP `isError` tool result from `ErrorProblemDetails.Create` and `ProblemDetailsEnrichment`, so HTTP and MCP return the same JSON.
- Clients that expect BIAN's `HTTPError` body must read RFC 9457 instead. The field mapping above is documented for them.
- A new composition project root, `RimBank.Problems`, is added to the architecture tests (namespace and assembly-name rules), and two rules guard who uses it and what it uses. tech-stack §3.3, §4 and §5 list it.
- Because Development no longer shows the developer exception page for API requests, a developer reads exceptions from the console log or the Aspire dashboard, by `traceId`.
