# Validation: MCP wiring

## Done when (from roadmap)
> **0.15 MCP wiring:** `/mcp` Streamable HTTP endpoint in `RimBank.Host` using the MCP C# SDK, with a `ping`-style smoke tool and an in-process MCP client test. ADR on tool naming, annotations (query tools `readOnly`) and error mapping.
> + Definition of done (roadmap.md)

## Automated checks
- [ ] `RimBank.Host.Tests/Mcp/McpEndpointTests` (SDK `McpClient` in-process over Streamable HTTP, with a test token):
  - the client connects to `/mcp`, and the server info is `RimBank` with the product version;
  - `tools/list` contains `fake_service_domain_retrieve` with its description and `readOnlyHint = true`, `idempotentHint = true`, `destructiveHint = false`;
  - `tools/call` on it returns `isError = false` and the expected content;
  - a host whose modules register no tools lists no tools.
- [ ] `/mcp` without a token gives **401** with `Content-Type: application/problem+json`, `errorCode = shared.unauthenticated`, a `traceId` and `WWW-Authenticate: Bearer`.
- [ ] `RimBank.Host.Tests/Security/AnonymousEndpointTests` still green: only `/health` and `/alive` carry `IAllowAnonymous` (`/mcp` does not).
- [ ] All existing host tests green after the `IServiceDomainModule` change (fake modules updated).
- [ ] `RimBank.Architecture.Tests` green. No new dependency breaks §4 (the MCP SDK is referenced only by `RimBank.Host` and its tests in this phase), and `host-is-the-composition-root` still holds.
- [ ] `dotnet format --verify-no-changes` clean, and lock files regenerated (both runtimes where ADR 0014 requires it).
- [ ] GitHub Actions CI (build, test, format, CodeQL) green on the PR.

## Manual / demo checks
- [ ] Read ADR 0021 and check it answers the three roadmap topics: tool naming (with BIAN examples), annotations (Retrieve tools `readOnly`), and the `isError` Problem Details error contract, with the 0.16 follow-up stated.
- [ ] Check that the two open questions in requirements.md are answered in ADR 0021.
- [ ] No manual MCP Inspector or AppHost demo in this phase (user's choice). The in-process client test is the demo.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 0.15 ticked in the same PR
- [ ] ADR 0021 written, and ADR 0014 amended for the third module member
- [ ] No permanent tool in `src/`: the smoke tool lives only in `RimBank.Host.Tests`
- [ ] Bearer tokens and tool arguments are not logged at Information level or above (§10)
- [ ] tech-stack §3.3/§6 and CONTRIBUTING updated
