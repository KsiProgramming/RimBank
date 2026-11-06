# Validation: MCP security & errors

## Done when (from roadmap)
> **0.16 MCP security & errors:** MCP authorization through Keycloak, the `Error` → Problem Details mapping as an `isError` tool result, and a test that an unauthenticated call is rejected.
> + Definition of done (roadmap.md)

## Automated checks
- [x] `RimBank.Mcp.Tests/Errors/ToolProblemTests`: a failed `Result` becomes `isError: true`, and `structuredContent` equals the HTTP Problem Details for the same `Error` (`type`, `title`, `status`, `detail`, `errorCode`, `errors`, `traceId`, `instance = /mcp`). The text content is the same JSON. `ToToolResult` maps success and failure.
- [x] `RimBank.Host.Tests/Mcp/McpToolErrorTests` (SDK `McpClient` in-process, test token):
  - a `NotFound` tool gives `isError` with `errorCode` and status 404 in `structuredContent`;
  - a validation failure carries `errors`;
  - a throwing tool gives `shared.unexpected`, and neither the exception message nor its type appears anywhere in the result;
  - the `traceId` matches the request's trace;
  - an unknown tool stays a JSON-RPC error (`-32602`), and a wrongly typed or missing argument is `shared.tool-arguments-invalid` (400) without the argument values.
- [x] `RimBank.Host.Tests/Mcp/McpAuthorizationTests` (**the unauthenticated-call rejection test**):
  - `/mcp` with no token and with an invalid token gives 401, `application/problem+json`, `errorCode = shared.unauthenticated`, a `traceId`, and `WWW-Authenticate: Bearer resource_metadata="…/.well-known/oauth-protected-resource/mcp"`;
  - an SDK client without a token cannot connect or list tools;
  - the metadata document is readable anonymously, with `resource` = absolute `/mcp`, `authorization_servers` = the configured realm authority, and `bearer_methods_supported = ["header"]`;
  - HTTP API routes keep a plain `Bearer` challenge without `resource_metadata`.
- [x] `A_tool_call_relays_the_callers_subject_to_the_message_bus`: the handler sees the token's `sub` as the envelope user name.
- [x] `RimBank.AppHost.Tests`: the realm file's `rimbank-mcp-client` is public, has PKCE `S256`, only loopback redirect URIs, the `rimbank-api` audience and no direct or service-account grants; the dev user is marked local-only. The host resource gets `Mcp__AuthorizationServer`.
- [x] Existing suites stay green: `McpEndpointTests` (0.15), `AnonymousEndpointTests` (only `/health` and `/alive` carry `IAllowAnonymous`), `AuthenticationTests`, and the messaging and idempotency tests.
- [x] `RimBank.Architecture.Tests` green, including the new `shared-mcp-dependencies` and `shared-mcp-only-in-mcp-and-host` rules. Their bad fixtures (`RimBank.Mcp.FixtureViolations`, `CoreUsesSharedMcp`, `ApiUsesSharedMcp.Api`) must fail them. `allow-anonymous-only-in-service-defaults` is unchanged and green.
- [x] `dotnet format --verify-no-changes` clean, and lock files regenerated (both runtimes where required).
- [ ] GitHub Actions CI (build, test, format, CodeQL) green on the PR.

## Manual / demo checks
- [x] Read ADR 0022 and check that it answers the four open questions in requirements.md and records the audience (RFC 8707) deviation.
- [x] No MCP Inspector demo in this phase (user's choice). The in-process host tests are the demo, and CONTRIBUTING explains how to do it locally with `rimbank-mcp-client`. The existing Docker AppHost startup test was run locally: Keycloak imports the updated realm and the host starts with `Mcp__AuthorizationServer`.

## Merge criteria
- [ ] All checks above pass in CI
- [x] Roadmap checkbox 0.16 ticked in the same PR (Stage 0 complete)
- [x] ADR 0022 written. ADR 0019 and ADR 0021 amended.
- [x] tech-stack §3.3/§4/§6 and CONTRIBUTING updated (the §4 change is covered by ADR 0022)
- [x] No permanent tool in `src/`: fake tools live only in `RimBank.Host.Tests`
- [x] No exception text, stack trace, bearer token or tool argument in a tool result. No bearer token or tool argument in logs: unexpected exceptions are logged at Error with the tool name and `traceId`, as the HTTP exception handler logs them, and unbindable arguments are not logged (§10, ADR 0021 logging rule).
- [x] The dev realm's new client is documented as local-only, and no real secret is committed
