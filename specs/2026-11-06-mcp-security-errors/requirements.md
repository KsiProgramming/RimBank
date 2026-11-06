# MCP security & errors

Roadmap phase: 0.16
Branch: plan/0.16-mcp-security-errors

## Context
Mission principle 6 says an AI agent gets the same business rules, authorisation, audit trail and errors as any other consumer. Principle 8 says an MCP error is a tool result whose content is the same Problem Details JSON as on HTTP. The quality bar says "authenticated by default". [Tech-stack §6](../tech-stack.md#6-mcp-model-context-protocol-adapter) fixes the MCP authorization spec (OAuth 2.1, Keycloak as authorization server), the `isError` error contract and the audit rule. [ADR 0021](../../docs/adr/0021-mcp-wiring-and-tool-conventions.md) defined that contract and left four follow-ups to this phase: protected-resource metadata, the `isError` mapping, replacing the SDK's default exception handling, and checking that the actor relay reaches tool calls.

Since 0.15, `/mcp` is already behind the deny-by-default fallback policy, so a call without a token gets a 401 `shared.unauthenticated` Problem Details response ([ADR 0019](../../docs/adr/0019-authentication-baseline.md)). But an MCP client cannot yet *discover* where to get a token, and a failing tool still goes through the SDK's generic error text.

This phase depends on 0.9 (Problem Details, ADR 0015), 0.11 (Wolverine bus and user-name relay, ADR 0017), 0.13 (Keycloak, JWT bearer, `Actor`, ADR 0019) and 0.15 (`/mcp` and the module hook, ADR 0021). All are merged. It completes Stage 0 and unblocks 1.16/1.17 (the first `.Mcp` adapter, which uses `RimBank.Mcp` to return its errors), 1.15 (per-operation policies, which add scopes to the metadata) and 4.14 (the money-moving scope).

As in 0.10–0.15 there is no real context yet, so the behaviour is proven with **test-only fake tools** in `RimBank.Host.Tests` (non-goal "no speculative coverage"). No tool ships in `src/`.

**CQRS split:**
- **Command side:** none in `src/`. One test-only fake tool dispatches a message through `IMessageBus` to prove that the caller's subject reaches the envelope (the user name that scopes idempotency keys, ADR 0019).
- **Query side:** none in `src/`. The fake failing tools model a Retrieve that returns `NotFound` and a command that returns a validation `Error`. The mapping does not care whether a command or a query produced the `Result`.
- **Read models:** none.

## Scope
### In scope
- **MCP authorization discovery (host, `Security/` + `Mcp/`):**
  - Add the SDK's MCP authentication scheme (`AddMcp`). It forwards authentication to the existing JWT bearer scheme, so token validation stays exactly as in ADR 0019.
  - Serve RFC 9728 protected-resource metadata at `/.well-known/oauth-protected-resource/mcp`, with `resource` set to the absolute `/mcp` URL, `authorization_servers` set to the realm authority, and `bearer_methods_supported = ["header"]`. `scopes_supported` stays empty until 1.15.
  - Apply an `Mcp` authorization policy (authenticated user, challenged by the MCP scheme) to the `/mcp` endpoint. A 401 on `/mcp` then carries `WWW-Authenticate: Bearer resource_metadata="…"` and keeps the `shared.unauthenticated` Problem Details body. HTTP API endpoints keep the plain `Bearer` challenge with no MCP metadata.
- **Dev MCP client in the Keycloak realm:** a public client `rimbank-mcp-client` in `src/RimBank.AppHost/Realms/rimbank-realm.json`. It uses the authorization code flow with PKCE `S256`, loopback redirect URIs only (`http://127.0.0.1/*`, which Keycloak accepts on any port, and `http://localhost:6274/*` for MCP Inspector) and the `rimbank-api` audience mapper, so MCP Inspector or Claude Code can sign in locally. It is dev-only like `rimbank-dev-client`. It has no client-credentials grant and no dynamic registration. The realm also gets a dev-only user, `rimbank-developer`, because the authorization code flow needs someone to sign in (added during implementation).
- **New shared library `src/RimBank.Mcp/`** (like `RimBank.Http` for `.Api`). It references SharedKernel, `RimBank.Problems` and the MCP SDK, and is used only by `.Mcp` adapters and `RimBank.Host`. It contains:
  - `ToolProblem.Create(Error, HttpContext?)`: returns a `CallToolResult` with `IsError = true`. Its `StructuredContent` is the Problem Details object that `ErrorProblemDetails.Create` builds, enriched by `ProblemDetailsEnrichment.Apply` (same `type`, `title`, `status`, `detail`, `errorCode`, `errors`, `traceId`, `instance = /mcp`). Its `Content` is one text block with the same JSON.
  - `result.ToToolResult(...)`: a success maps through a caller-supplied projection, and a failure maps through `ToolProblem`. `.Mcp` adapters use it in 1.16.
  - The call-tool filter `ToolErrorFilter` and its registration extension `IMcpServerBuilder.AddRimBankToolErrors()`. An unexpected exception from a tool becomes `shared.unexpected` (no exception message or type), and the exception is logged at Error with its `traceId`. `OperationCanceledException` on a cancelled call and `McpProtocolException` pass through as the SDK expects, and so do unknown-tool and malformed-argument JSON-RPC errors.
  - The host calls `AddRimBankToolErrors()` in `AddRimBankMcp`, before the modules add their tools.
- **Architecture:** a `shared-mcp-dependencies` rule (renamed during implementation, because `mcp-dependencies` already names the `.Mcp` adapter rule) (RimBank.Mcp: SharedKernel, `RimBank.Problems`, ASP.NET Core and the MCP SDK only; never a core, an adapter, Marten or Wolverine) with a bad fixture `RimBank.Mcp.FixtureViolations`, and a `shared-mcp-only-in-mcp-and-host` rule with two bad fixtures. `RimBank.Mcp` is added to the composition-project lists in `ContextAddress` and the solution rule tests. A `.Mcp` adapter may reference `RimBank.Mcp`.
- **Actor relay on tool calls:** a test proves that the token's `sub` reaches the Wolverine envelope user name when a tool dispatches through `IMessageBus`. If the stateless transport does not resolve the tool's services from the request scope that `ActorRelay` sets, the host adds a call-tool filter that sets the user name from `context.User` (in the host, because `RimBank.Mcp` must not reference Wolverine).
- **ADR 0022 "MCP authorization and tool errors"**. It amends ADR 0019 (the MCP challenge, the metadata path, the dev MCP client) and ADR 0021 (the error contract is now implemented, and tool errors are owned by `RimBank.Mcp`).
- **Docs:** tech-stack §3.3 (`RimBank.Mcp`, `RimBank.Mcp.Tests`), §4 (new row; `.Mcp` may reference `RimBank.Mcp`), §6 (link to ADR 0022); CONTRIBUTING "MCP" (return errors with `ToToolResult`/`ToolProblem`, never throw for business failures; how to connect a local MCP client through `rimbank-mcp-client`); ADR 0021 follow-ups marked done; roadmap tick.

### Out of scope
- Per-operation and per-tool authorisation policies, scopes in `scopes_supported`, and the SDK's `AddAuthorizationFilters` / `[Authorize]` on tools (1.15).
- Dynamic client registration and RFC 8707 resource indicators in Keycloak. Tokens keep the `rimbank-api` audience, the same as HTTP (see Decisions).
- The first real `.Mcp` adapter and HTTP/MCP parity tests on a real operation (1.16/1.17).
- Recording the actor and the MCP client identity in event metadata (with the first events, 1.x). This phase only proves that the actor reaches the bus.
- Money-moving tool scope and default-off switch (4.14). MCP rate limiting and the tool-call audit query (7.9).
- A real-Keycloak AppHost test and a manual MCP Inspector demo. The user chose in-process host tests as validation, and the dev client is checked only by a realm-file test.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Discovery mechanism | The SDK's `AddMcp` scheme with `ResourceMetadata`, forwarding authentication to JWT bearer | The MCP authorization spec requires RFC 9728 metadata and a `resource_metadata` challenge (tech-stack §6). The SDK implements both, and forwarding keeps one token validator (ADR 0019). |
| Where the MCP challenge applies | Only on `/mcp`, through an `Mcp` policy on the endpoint | HTTP clients do not need MCP discovery. The endpoint policy still requires an authenticated user, so deny by default holds. |
| Metadata document access | Anonymous, served by the MCP authentication handler (not a route endpoint) | RFC 9728 needs it readable before sign-in. It holds no data, and the anonymous route allow-list (`/health`, `/alive`) stays unchanged. ADR 0022 records it as the one non-endpoint exception. |
| Authorization server URL | `Authentication:Authority` when set. In Development the AppHost passes Keycloak's realm URL, which clients can reach, as `Mcp:AuthorizationServer` | Service discovery's `https+http://keycloak` is meaningless to an external MCP client. |
| Token audience | `rimbank-api` for both HTTP and MCP; no RFC 8707 resource indicator | One audience keeps one validator. Keycloak's resource indicator support is limited. ADR 0022 records the deviation from the spec's "SHOULD". |
| Dev MCP client | Public `rimbank-mcp-client`, auth code + PKCE `S256`, loopback redirects | The MCP spec requires PKCE for public clients, and loopback redirects keep it local-only. |
| Where the error mapping lives | New `RimBank.Mcp` library (user's choice), mirroring `RimBank.Http` | Every `.Mcp` adapter and the host share one mapping. An extracted context takes it along. Keeps `RimBank.Problems` free of the MCP SDK. |
| Error shape | `isError: true`, Problem Details in `structuredContent` and as JSON text in `content`, `instance = /mcp`, `traceId` from `ProblemDetailsEnrichment` | ADR 0021 contract. The same `traceId` source as HTTP. |
| Exceptions | Call-tool filter → `shared.unexpected`, logged at Error with `traceId`; cancellation and `McpProtocolException` pass through | Replaces the SDK's generic text (ADR 0021). No internal details leak (§5, §6). |
| Actor on tool calls | Test first. Add a host call-tool filter only if `ActorRelay` does not already reach the tool's bus | Audit and caller-scoped idempotency must hold over MCP (§6, ADR 0019). |

## ADRs to write
- **0022 MCP authorization and tool errors:** the decisions above, how the RFC 9728 metadata works with deny by default and the status-code pages, the dev MCP client, the audience deviation, `RimBank.Mcp` and its dependency rule (a tech-stack §4 change), the filter's pass-through cases, and how the actor reaches tool calls. It amends ADR 0019 and ADR 0021.

## Open questions
Answered during implementation (ADR 0022):
- **The 401 body:** the SDK's MCP challenge still leaves a bodiless 401, so the status-code pages turn it into `shared.unauthenticated`, now with `WWW-Authenticate: Bearer resource_metadata="..."` (`Mcp_without_a_valid_token_is_401_problem_details_with_resource_metadata`).
- **Metadata vs the fallback policy:** the MCP authentication handler answers `/.well-known/oauth-protected-resource/mcp` itself, before authorization. It is not a route endpoint, so `AnonymousEndpointTests` and `allow-anonymous-only-in-service-defaults` stay unchanged.
- **The actor on tool calls:** in stateless mode a tool resolves `IMessageBus` from the request scope, so `ActorRelay`'s user name reaches the envelope with no extra filter (`A_tool_call_relays_the_callers_subject_to_the_message_bus`).
- **Argument-binding failures:** SDK 2.3.1 throws before the tool runs, a `JsonException` for a wrong JSON type and an `ArgumentException` for the parameter `arguments` when a required argument is missing. The filter maps both to a new `shared.tool-arguments-invalid` (400) validation problem that does not repeat the arguments. This is a small change from "the filter does not rewrite it", made because the SDK would otherwise report a 500.
- **Additional finding:** the filter sees tool exceptions before the SDK's own generic handling, so wrapping `next` is enough to replace it.
- **Additional finding:** a host started alone in Development, with neither `Mcp:AuthorizationServer` nor `Authentication:Authority`, announces no authorization server instead of refusing to start, because several host tests and `dotnet run` rely on service discovery there. Outside Development the address is required and must use HTTPS.
