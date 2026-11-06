# Plan: MCP wiring

Test-first throughout: each group starts with failing tests, and the solution builds green at the end of each group.

## 1. Package and ADR draft
- [ ] `Directory.Packages.props`: `ModelContextProtocol.AspNetCore` (latest stable, currently 2.3.1). Check it against ADR 0013's package policy (cooldown, vulnerability audit).
- [ ] `.github/dependabot.yml`: put the `ModelContextProtocol*` packages in one group.
- [ ] `src/RimBank.Host/RimBank.Host.csproj` and `tests/RimBank.Host.Tests/RimBank.Host.Tests.csproj` reference the package. Restore and regenerate the lock files (both runtimes where ADR 0014 requires it).
- [ ] `docs/adr/0021-mcp-wiring-and-tool-conventions.md` from `template.md`: context, options (stateless vs stateful; module hook vs scanning vs each context calling `AddMcpServer`), and decision skeleton.

## 2. `/mcp` endpoint and module hook
- [ ] Test helper `tests/RimBank.Host.Tests/Mcp/McpTestClient.cs`: creates an SDK `McpClient` over `HttpClientTransport` (Streamable HTTP, endpoint `/mcp`), using the `WebApplicationFactory` handler and a `TestTokens` bearer token.
- [ ] Fake module `tests/RimBank.Host.Tests/Mcp/McpServiceDomainModule.cs`: `ServiceDomain = "FakeServiceDomain"`, with `AddMcpTools` registering a `[McpServerToolType]` class with `fake_service_domain_retrieve` (`ReadOnly = true`, `Idempotent = true`, `Destructive = false`, a description), which returns a fixed text.
- [ ] Tests `tests/RimBank.Host.Tests/Mcp/McpEndpointTests.cs` (namespace `RimBank.Host.Mcp.Tests`):
  - `Client_connects_and_reads_the_server_info` (name `RimBank`, version = product version);
  - `Tool_list_contains_the_module_tool_with_its_annotations` (name, description, `ReadOnlyHint`, `IdempotentHint`, `DestructiveHint`);
  - `Calling_the_tool_returns_its_result` (`isError` false, expected text);
  - `Mcp_without_a_token_is_401_problem_details` (`application/problem+json`, `errorCode = shared.unauthenticated`, `WWW-Authenticate: Bearer`);
  - `A_module_without_tools_adds_none` (host with only the existing fake modules: empty tool list).
- [ ] `src/RimBank.Host/Mcp/McpComposition.cs`: `AddRimBankMcp(IHostApplicationBuilder, IReadOnlyList<IServiceDomainModule>)` (`AddMcpServer` with server info, `WithHttpTransport` with `SessionMode = Stateless`, then `module.AddMcpTools(mcp)` for each module) and `MapRimBankMcp(IEndpointRouteBuilder)` (`MapMcp("/mcp")`).
- [ ] `IServiceDomainModule`: add `void AddMcpTools(IMcpServerBuilder tools)` with XML docs. Add an empty implementation in each existing fake module (`Modules`, `Errors`, `Security`, `Messaging` and any others in the test projects).
- [ ] `HostComposition.Build`: call `AddRimBankMcp` after the modules' `AddServices`, and `MapRimBankMcp` after `UseAuthentication`/`UseAuthorization` and before the module endpoints.
- [ ] Check that `AnonymousEndpointTests` stays green: `/mcp` must not carry `IAllowAnonymous`.
- [ ] Answer the two open questions (duplicate tool names; whether the 401 body reaches the status-code pages) and record the answers in the ADR.

## 3. ADR, docs and roadmap
- [ ] Finish ADR 0021: endpoint and stateless mode; the module hook (amends ADR 0014); naming rule with examples from the BIAN Party Reference Data Directory and Current Account specs; the annotation table per operation kind; the error contract (`isError: true`, Problem Details JSON in `content` text and in `structuredContent`, same `type`/`errorCode`/`traceId` as HTTP, no exception text, SDK default exception handling replaced in 0.16); follow-ups (0.16 auth and errors, 1.16 first tools and parity, 4.14 money-moving scope, 7.9 hardening).
- [ ] ADR 0014: consequence "third member `AddMcpTools` since ADR 0021".
- [ ] `specs/tech-stack.md`: §3.3 (`Mcp/` in `RimBank.Host`), §6 (stateless session, the module hook, link to ADR 0021).
- [ ] `CONTRIBUTING.md`: an "MCP" section (a context's `.Mcp` exposes one `IMcpServerBuilder` extension, and its module calls it; naming and annotations per ADR 0021; `/mcp` needs a bearer token, the same as HTTP).
- [ ] `specs/roadmap.md`: tick 0.15.
- [ ] `dotnet format --verify-no-changes`, the full `dotnet test`, and the lock files committed.
