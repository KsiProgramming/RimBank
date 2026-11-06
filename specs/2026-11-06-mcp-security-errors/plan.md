# Plan: MCP security & errors

Test-first throughout: each group starts with failing tests, and the solution builds green at the end of each group.

## 1. Tool errors: `RimBank.Mcp` and the call-tool filter
- [x] `docs/adr/0022-mcp-authorization-and-tool-errors.md` from `template.md`: context, options (where the mapping lives: `RimBank.Problems` vs host only vs new `RimBank.Mcp`; challenge everywhere vs only on `/mcp`), and a decision skeleton (written in full in group 3).
- [x] New projects `src/RimBank.Mcp/` (references SharedKernel, `RimBank.Problems`, `ModelContextProtocol.AspNetCore`) and `tests/RimBank.Mcp.Tests/`. Add both to `RimBank.slnx`, regenerate the lock files (both runtimes where ADR 0014 requires it), and add a nested `.editorconfig` only if needed.
- [x] Failing unit tests in `tests/RimBank.Mcp.Tests/Errors/ToolProblemTests.cs` (namespace `RimBank.Mcp.Errors.Tests`):
  - `A_not_found_error_is_an_error_result_with_problem_details` (`IsError`, `structuredContent` with `type`, `status = 404`, `errorCode`, `traceId`, `instance = /mcp`);
  - `A_validation_error_carries_its_field_errors` (`errors` map);
  - `Text_content_is_the_same_json_as_structured_content`;
  - `Structured_content_equals_the_http_problem_details_for_the_same_error` (against `ErrorProblemDetails.Create` + `ProblemDetailsEnrichment.Apply`);
  - `ToToolResult_maps_success_through_the_projection_and_failure_through_ToolProblem`.
- [x] `src/RimBank.Mcp/Errors/ToolProblem.cs` and `ResultToolExtensions.cs` (`ToToolResult`).
- [x] `src/RimBank.Mcp/Errors/ToolErrorFilter.cs` and `McpServerBuilderExtensions.AddRimBankToolErrors()` (`WithRequestFilters` → `AddCallToolFilter`): an exception becomes `shared.unexpected`, logged at Error with `traceId`. Rethrow `OperationCanceledException` when the call's token is cancelled, and rethrow `McpProtocolException`.
- [x] Host fake tools `tests/RimBank.Host.Tests/Mcp/McpErrorServiceDomainModule.cs` (service domain `FakeErrorServiceDomain`): `fake_error_service_domain_retrieve` (returns `NotFound` through `ToToolResult`), `fake_error_service_domain_register` (validation `Error` with field errors), `fake_error_service_domain_update` (throws an exception with a secret-looking message).
- [x] Failing host tests `tests/RimBank.Host.Tests/Mcp/McpToolErrorTests.cs` (namespace `RimBank.Host.Mcp.Tests`):
  - `A_failed_result_is_an_isError_problem_details_result`;
  - `A_validation_failure_carries_errors`;
  - `A_throwing_tool_is_shared_unexpected_without_exception_text` (the message and type name do not appear in the result);
  - `The_traceId_of_the_result_is_the_request_trace`;
  - `An_unknown_tool_stays_a_json_rpc_error`;
  - `Malformed_arguments_are_a_validation_problem_that_does_not_echo_them` and `A_missing_argument_is_a_validation_problem` (`shared.tool-arguments-invalid`, see the answered open question).
- [x] `McpComposition.AddRimBankMcp`: call `.AddRimBankToolErrors()` before the modules' `AddMcpTools`. Reference `RimBank.Mcp` from `RimBank.Host`.
- [x] Architecture: rules `shared-mcp-dependencies` and `shared-mcp-only-in-mcp-and-host` in `ArchitectureRules.Composition.cs`, a bad fixture `RimBank.Mcp.FixtureViolations` (and `CoreUsesSharedMcp`, `ApiUsesSharedMcp.Api`) in `RimBank.Architecture.Fixtures` registered in `FixtureContexts`, `RimBank.Mcp` in `ContextAddress` and in the `SolutionRulesTests`/`SolutionOnlyRulesTests` lists, and `RimBank.Mcp` allowed in `problems-only-in-adapters-and-host` (the `.Mcp` adapter rule already allows it).

## 2. MCP authorization discovery, dev client and actor relay
- [x] Failing host tests `tests/RimBank.Host.Tests/Mcp/McpAuthorizationTests.cs`:
  - `Mcp_without_a_token_challenges_with_resource_metadata` (401, `application/problem+json`, `errorCode = shared.unauthenticated`, `WWW-Authenticate: Bearer resource_metadata="http://…/.well-known/oauth-protected-resource/mcp"`);
  - `Mcp_with_an_invalid_token_is_401_with_the_same_challenge`;
  - `The_protected_resource_metadata_is_readable_without_a_token` (`resource` = absolute `/mcp`, `authorization_servers` = configured authority, `bearer_methods_supported = ["header"]`);
  - `Http_endpoints_keep_the_plain_bearer_challenge` (no `resource_metadata` on a fake module's route);
  - `An_mcp_client_without_a_token_cannot_list_tools` (SDK `McpClient` fails to connect with an HTTP 401).
- [x] `AuthenticationComposition`: `.AddMcp(options => options.ResourceMetadata = …)` after `AddKeycloakJwtBearer`. Add an `Mcp` policy (`RequireAuthenticatedUser`, `AddAuthenticationSchemes(McpAuthenticationDefaults.AuthenticationScheme)`). The authorization server comes from `Authentication:Authority`, or from `Mcp:AuthorizationServer` in Development.
- [x] `McpComposition.MapRimBankMcp`: `.RequireAuthorization(McpPolicy)` on the mapped endpoint.
- [x] Answer open questions 1 and 2 (the 401 body, metadata vs fallback policy). Check that `AnonymousEndpointTests` and the `allow-anonymous-only-in-service-defaults` rule stay green.
- [x] AppHost: pass Keycloak's reachable realm URL to the host as `Mcp__AuthorizationServer` (Development). Add `rimbank-mcp-client` to `Realms/rimbank-realm.json` (public, `standardFlowEnabled`, `pkce.code.challenge.method = S256`, loopback redirect URIs, `rimbank-api` audience mapper, no direct grants or service account) and the dev user `rimbank-developer`.
- [x] Realm-file test in `tests/RimBank.AppHost.Tests` (no Docker): `The_dev_mcp_client_is_public_with_pkce_and_loopback_redirects_only`. Extend the AppHost resource-model test for the `Mcp__AuthorizationServer` variable. The Docker startup test confirms Keycloak imports the updated realm.
- [x] Actor relay: a fake tool `fake_service_domain_execute` (in `McpServiceDomainModule`) that sends a message through `IMessageBus`, and a handler that records the envelope user name. Test `A_tool_call_relays_the_callers_subject_to_the_message_bus`. It passed with no change (open question 3), so no host call-tool filter was needed; it would have added one in `RimBank.Host/Mcp/` that sets `IMessageContext.UserName` from `ActorRelay.ActorOf(context.User)`, and returns `shared.actor-invalid` as a tool problem when the actor is invalid.

## 3. ADR, docs and roadmap
- [x] Finish ADR 0022, covering the decisions table in requirements.md, the answers to the open questions, the audience deviation, the dev MCP client, `RimBank.Mcp` and its rule, and the follow-ups (1.15 scopes and `[Authorize]` on tools, 1.16 first use of `ToToolResult`, 4.14, 7.9).
- [x] ADR 0019 consequences: "MCP challenge and metadata since ADR 0022". ADR 0021: mark the 0.16 follow-ups done, linking ADR 0022.
- [x] `specs/tech-stack.md`: §3.3 (`RimBank.Mcp/`, `RimBank.Mcp.Tests/`), §4 (new `RimBank.Mcp` row; the `.Mcp` row may reference `RimBank.Mcp`), §6 (Security and Errors bullets link ADR 0022).
- [x] `CONTRIBUTING.md` "MCP": return `result.ToToolResult(...)`, never throw for business failures; how to connect MCP Inspector or Claude Code locally with `rimbank-mcp-client`.
- [x] `specs/roadmap.md`: tick 0.16.
- [x] `dotnet format --verify-no-changes`, the full `dotnet test`, and the lock files committed.
