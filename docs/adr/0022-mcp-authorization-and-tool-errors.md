# 0022. MCP authorization and tool errors

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.16

## Context

Mission principle 6 says an AI agent gets the same rules, authorisation, audit trail and errors as any other consumer. Principle 8 says an MCP error is a tool result whose content is the same Problem Details JSON as on HTTP. [tech-stack §6](../../specs/tech-stack.md#6-mcp-model-context-protocol-adapter) fixes the MCP authorization specification (OAuth 2.1, Keycloak as authorization server) and the `isError` error contract. [ADR 0021](0021-mcp-wiring-and-tool-conventions.md) defined that contract and left four follow-ups to roadmap 0.16:

- protected-resource metadata, so that a client can discover where to sign in;
- the `Error` → `isError` mapping;
- replacing the SDK's default handling of tool exceptions;
- checking that the caller reaches the message bus on a tool call.

Since 0.15, `/mcp` sits behind the deny-by-default fallback policy of [ADR 0019](0019-authentication-baseline.md). A call without a token already gets a 401 `shared.unauthenticated` response, but the challenge is a plain `Bearer`, so an MCP client cannot learn where to get a token. A tool that fails returns the SDK's generic error text.

As in 0.10–0.15 no context exists yet, so everything is proven with test-only fake tools in `RimBank.Host.Tests`.

## Options considered

Where the error mapping lives:

1. **In `RimBank.Problems`.** It already maps `Error` → Problem Details, but it would then depend on the MCP SDK, and `.Api` adapters would see MCP types.
2. **Only in the host, as a filter.** Tools would still need a way to return an `Error` as a result, and an extracted context would have to copy it.
3. **A new shared library, `RimBank.Mcp`**, used only by `.Mcp` adapters and the host, as `RimBank.Http` is for `.Api` ([ADR 0018](0018-idempotency-keys-for-commands.md)).

Where the MCP challenge applies:

1. **As the default challenge for every endpoint.** HTTP clients would get MCP discovery metadata they cannot use.
2. **Only on `/mcp`**, through an endpoint policy whose challenge scheme is the SDK's MCP scheme.

## Decision

**`RimBank.Mcp` (option 3).** The library references SharedKernel, `RimBank.Problems`, ASP.NET Core and the MCP C# SDK only. Only `.Mcp` adapters and `RimBank.Host` use it. This changes the dependency table of tech-stack §4, and two architecture rules enforce it: `shared-mcp-only-in-mcp-and-host` and `shared-mcp-dependencies`. `problems-only-in-adapters-and-host` now also allows `RimBank.Mcp`. It contains:

- `ToolProblem.Create(Error)`: `isError: true`, with the Problem Details of `ErrorProblemDetails.Create` as `structuredContent` and the same JSON as one text block of `content`. Types, titles, statuses, `errorCode` and `errors` are those of HTTP. A test checks that the JSON is byte for byte the one HTTP writes for the same error.
- `result.ToToolResult(onSuccess)`: how a `.Mcp` adapter answers. A failure is never thrown.
- `ToolErrors.ArgumentsInvalid`: `shared.tool-arguments-invalid` (400).
- `ToolErrorFilter`, a call-tool filter registered with `AddRimBankToolErrors()`. The host adds it in `AddRimBankMcp`, before the modules' tools. Around every tool call it:
  - adds the request's `instance` (`/mcp`) and `traceId` to a problem result, through `ProblemDetailsEnrichment` like HTTP, so the `traceId` is the request's W3C trace;
  - turns arguments the SDK cannot bind into `shared.tool-arguments-invalid`, without repeating them. The SDK 2.3.1 behaviour is pinned by tests: a value of the wrong JSON type throws `JsonException`, and a missing required argument throws `ArgumentException` for the parameter `arguments`, both before the tool runs. This follows the MCP specification, which reports input validation errors as tool execution errors, not protocol errors;
  - turns any other exception into `shared.unexpected` (500), with no exception message or type in the result. It logs the exception at Error with the tool name and `traceId`, as the HTTP exception handler does, and never logs the tool's arguments. The filter sees the exception before the SDK's own generic handling, so it replaces that handling;
  - lets a cancelled call and `McpProtocolException` pass through. An unknown tool therefore stays a JSON-RPC error (`-32602`), as the specification requires.

**Authorization discovery (option 2).** The host adds the SDK's MCP authentication scheme (`AddMcp`) next to JWT bearer.

- Its authentication forwards to JWT bearer, so tokens are validated exactly as in ADR 0019.
- `/mcp` requires the `Mcp` policy: an authenticated user, challenged by the MCP scheme. A 401 on `/mcp` carries `WWW-Authenticate: Bearer resource_metadata="<origin>/.well-known/oauth-protected-resource/mcp"`. It keeps the `shared.unauthenticated` Problem Details body, because the status-code pages still see a bodiless 401. Every other endpoint keeps the plain `Bearer` challenge.
- The MCP scheme serves the RFC 9728 metadata itself, before routing and authorization, so it is readable without a token. It is not a route endpoint, so the anonymous allow-list (`/health`, `/alive`), `AnonymousEndpointTests` and the rule `allow-anonymous-only-in-service-defaults` are unchanged. It holds no data.
- The metadata says:
  - `resource`: the requested `/mcp` address;
  - `resource_name`: `RimBank`;
  - `authorization_servers`: the realm;
  - `bearer_methods_supported`: `["header"]`;
  - no `scopes_supported` until per-operation scopes exist (1.15).
- **The realm address** is `Mcp:AuthorizationServer`, or `Authentication:Authority` when that key is not set. Outside Development it is required and must use HTTPS, or the host refuses to start. In Development the AppHost sets `Mcp__AuthorizationServer` to Keycloak's own endpoint, because the service-discovery name the host uses is meaningless to a client. A host started alone in Development without either key announces no authorization server.
- **Behind a proxy** the inferred `resource` is only right when forwarded headers are applied. That is part of the production setup (7.x).

**Token audience.** MCP tokens carry the same audience as HTTP tokens, `rimbank-api`, and are validated by the same JWT bearer. We do not use RFC 8707 resource indicators, which the MCP specification recommends (SHOULD) so that a token is bound to the MCP server. Keycloak's support for the `resource` parameter is limited, and one audience keeps one validator. This is a deliberate deviation. It is revisited if RimBank serves MCP from more than one resource, or with the production identity provider (7.x).

**Dev MCP client.** The dev-only realm gains `rimbank-mcp-client`:

- a public client (no secret) using the authorization code flow with PKCE `S256`, as the MCP specification requires for public clients;
- redirects only to `http://127.0.0.1/*`, which Keycloak accepts on any port as a native-app loopback redirect, and to `http://localhost:6274/*` (MCP Inspector);
- the `rimbank-api` audience mapper;
- no direct-access, service-account or device grants.

The realm also gains the user `rimbank-developer` (password `rimbank-developer-local-only`). The authorization code flow needs someone to sign in. Like the dev client secret, both are published on purpose and are for local use only. Dynamic client registration stays off.

**Caller on tool calls.** In stateless mode a tool resolves its services, including `IMessageBus`, from the `/mcp` request's scope. `ActorRelay` has already set the caller's subject as that scope's Wolverine user name, so a tool call carries its caller to the envelope, its idempotency scope and the trace exactly as an HTTP call does. No MCP-specific relay is needed, and a test pins it.

## Consequences

- *Amended by [ADR 0039](0039-party-mcp-tools.md):* `RimBank.Mcp` also holds `ToolResults.Structured`, `[ToolOperation]` and the `ToolAuthorizationFilter` added by `AddRimBankToolAuthorization()`. A call refused by its operation's policy is an `isError` result with `shared.forbidden` (403), and the SDK's `AddAuthorizationFilters` is not used.
- *Amended by [ADR 0025](0025-one-api-project.md):* a context's MCP tools are a folder of `RimBank.Mcp` (namespace `RimBank.<BusinessDomain>.<ServiceDomain>.Mcp`), next to these conventions, not a project of their own.
- A `.Mcp` adapter (1.16) answers with `await bus.InvokeAsync<Result<T>>(…)` and then `.ToToolResult(…)`. The filter takes care of the trace, `instance`, unbindable arguments and unexpected failures. CONTRIBUTING "MCP" says so.
- MCP Inspector or Claude Code can connect to a local AppHost: they follow the 401 to the metadata, sign `rimbank-developer` in through `rimbank-mcp-client`, and call `/mcp` with the token. CONTRIBUTING describes it.
- Deployments must set `Authentication:Authority` (already required by ADR 0019) or `Mcp:AuthorizationServer` to an HTTPS realm address.
- Amends [ADR 0019](0019-authentication-baseline.md) (the MCP challenge and metadata; the dev MCP client and user) and [ADR 0021](0021-mcp-wiring-and-tool-conventions.md) (its error contract is now implemented here, and the 0.16 follow-ups are done).
- Follow-ups:
  - **1.15:** per-operation scopes in `scopes_supported` (done in [ADR 0038](0038-per-operation-authorisation-policies.md)). The policies on tools, through the SDK's authorization filters (`AddAuthorizationFilters`, `[Authorize]`), come with the first tools in 1.16.
  - **1.16:** the first `.Mcp` adapter uses `ToToolResult`, and HTTP/MCP parity tests compare its Problem Details with the `.Api`'s.
  - **4.14:** the money-moving scope.
  - **7.x:** forwarded headers for the `resource` address, the production identity provider, and RFC 8707 if it becomes available there.
  - **7.9:** MCP rate limiting.
- Proven by:
  - `tests/RimBank.Mcp.Tests/Errors/ToolProblemTests`;
  - `tests/RimBank.Host.Tests/Mcp/McpToolErrorTests`, `McpAuthorizationTests` and `McpCallerTests`;
  - `tests/RimBank.AppHost.Tests/Model/DevRealmTests`, and the AppHost model test for `Mcp__AuthorizationServer`;
  - the architecture rules `shared-mcp-only-in-mcp-and-host` and `shared-mcp-dependencies`, with their bad fixtures.
