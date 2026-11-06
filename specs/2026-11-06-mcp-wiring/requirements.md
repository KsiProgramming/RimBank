# MCP wiring

Roadmap phase: 0.15
Branch: plan/0.15-mcp-wiring

## Context
Mission principle 6 says every service domain is usable by AI agents as well as by applications, through MCP tools that are thin adapters over the same core as HTTP. Principle 8 says MCP errors are Problem Details too. Tech-stack §1 fixes the official C# SDK (`ModelContextProtocol.AspNetCore`) with Streamable HTTP at `/mcp`. [Tech-stack §6](../tech-stack.md#6-mcp-model-context-protocol-adapter) fixes the hosting (one endpoint in `RimBank.Host`), the tool naming, the annotations and the error contract, and §9 fixes the test style (SDK client in-process against the test host).

This phase depends on 0.8 (host composition and `IServiceDomainModule`, ADR 0014), 0.9 (Problem Details, ADR 0015), 0.11 (Wolverine bus, ADR 0017) and 0.13 (deny by default, test tokens, ADR 0019). All four are merged. 0.14 (conformance harness, ADR 0020) is also merged, but 0.15 does not depend on it.

It unblocks 0.16 (MCP authorization through Keycloak and the `isError` Problem Details mapping), 1.16/1.17 (the first `.Mcp` adapter and its parity tests) and 4.14 (money-moving tools).

As in 0.10–0.13 there is no real context yet, so the endpoint and the module hook are proven with a **test-only fake module** in `RimBank.Host.Tests` (non-goal "no speculative coverage"). No permanent tool ships in `src/`.

**CQRS split:**
- **Command side:** none in this phase. The ADR sets the rule that every non-Retrieve tool dispatches a command through `IMessageBus` and is not `readOnly`.
- **Query side:** the fake smoke tool is a Retrieve-style, `readOnly` tool that returns a fixed result. Real tools dispatch queries through `IMessageBus.InvokeAsync<TView>` (1.16).
- **Read models:** none.

## Scope
### In scope
- **Package:** `ModelContextProtocol.AspNetCore` pinned centrally in `Directory.Packages.props` (latest stable, currently 2.3.1, subject to ADR 0013's package policy), with Dependabot grouping and lock files regenerated.
- **Host (`RimBank.Host/Mcp/McpComposition.cs`):**
  - `AddRimBankMcp(builder, modules)` calls `AddMcpServer()` once, sets the server info (`RimBank`, product version), uses `WithHttpTransport` with `HttpServerSessionMode.Stateless`, and lets each module register its tools.
  - `MapRimBankMcp(app)` calls `MapMcp("/mcp")`, after authentication/authorization and before the module endpoints.
  - `HostComposition.Build` calls both.
- **Module hook:** `IServiceDomainModule` gains `AddMcpTools(IMcpServerBuilder tools)`. The host calls it for each module in list order, after `AddServices`. A context's module class calls only the registration extension that the context's `.Mcp` adapter exposes, for example `tools.AddPartyReferenceDataDirectoryTools()`, which is `WithTools<…>()` inside the `.Mcp` project. Existing fake modules get an empty implementation.
- **Security:** `/mcp` is **not** anonymous. The 0.13 fallback policy covers it, so a call without a token gets 401 `shared.unauthenticated` Problem Details. The anonymous allow-list stays `/health` and `/alive`.
- **Smoke tool (test-only):** a fake module in `tests/RimBank.Host.Tests/Mcp/` registers `fake_service_domain_retrieve` (`ReadOnly = true`, `Idempotent = true`, `Destructive = false`), which returns a fixed result. Its name follows the §6 naming rule.
- **ADR 0021 "MCP wiring and tool conventions"**, covering:
  - the endpoint, transport and stateless session mode, and why (horizontal scaling, no sampling or elicitation needed);
  - the module hook;
  - tool naming (`<service_domain>_<operation>[_<behavior_qualifier>]`, snake_case, from the BIAN names);
  - annotations (Retrieve: `readOnly` + `idempotent`; commands: not `readOnly`; money-moving or closing: `destructive`, idempotent only when a key is supplied);
  - the error mapping contract: `isError: true`, with the Problem Details JSON as content and as `structuredContent`, the same `type`, `errorCode` and `traceId` as HTTP, and no exception text. 0.16 implements it.
  - how the SDK's default exception handling must be overridden in 0.16, and what is deferred.
- **Docs:** tech-stack §3.3 (`Mcp/` in `RimBank.Host`), §6 (link to ADR 0021, stateless mode, the module hook); CONTRIBUTING "MCP" section (how a context adds tools; `/mcp` needs a bearer token); roadmap tick.

### Out of scope
- OAuth 2.1 protected-resource metadata (`/.well-known/oauth-protected-resource`), `WWW-Authenticate` `resource_metadata`, and Keycloak as MCP authorization server (0.16).
- The `Error` → `isError` tool-result mapping code, the exception filter, and their tests (0.16). The ADR only defines the contract.
- Recording the actor relay for tool calls on the Wolverine envelope (0.16, when tools first dispatch; tech-stack §6 "Audit").
- A shared `RimBank.Mcp` library, and architecture rules for tool names and annotations. These come when the first real tools exist (1.16), if they are still needed.
- Any real context tool, and HTTP/MCP parity tests (1.16/1.17).
- Dispatching through `IMessageBus` from the smoke tool, and a manual MCP Inspector demo. The user chose in-process client tests and the auth guard as validation.
- Money-moving tool scopes and default-off switch (4.14). MCP rate limiting (7.9).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| SDK | `ModelContextProtocol.AspNetCore` (official C# SDK) | Fixed by tech-stack §1. |
| Session mode | `HttpServerSessionMode.Stateless` | The SDK default and its recommendation. Tools are request/response over the bus and need no server-to-client requests. The host stays horizontally scalable, with no sticky sessions (§2 topology). |
| Endpoint | One `/mcp` in `RimBank.Host`, mapped by the host | Tech-stack §6 "Hosting". Contexts never map routes for MCP. |
| How contexts register tools | New `IServiceDomainModule.AddMcpTools(IMcpServerBuilder)`, called by the host in module order | Keeps ADR 0014's single explicit entry point. Contexts never call `AddMcpServer`/`WithHttpTransport`, so transport settings live in one place. No assembly scanning (`WithToolsFromAssembly` is not used). |
| Required vs default member | Required interface member (empty body where a context has no tools yet) | Explicit like the other two members. Adding tools later is a visible change in the module class. |
| Authentication | Covered by the 0.13 fallback policy, nothing MCP-specific | Deny by default already holds. 0.16 adds the MCP authorization discovery on top. |
| Smoke tool | Test-only fake module, named `fake_service_domain_retrieve`, `readOnly` | No speculative tools in `src/`. A `rimbank_ping` would break the §6 naming rule. |
| Error contract | Defined in ADR 0021, built in 0.16 | Matches the roadmap split: 0.15 has the ADR, 0.16 has the `isError` mapping. |

## ADRs to write
- **0021 MCP wiring and tool conventions:** the content listed under "In scope". It amends ADR 0014 (the third module member).

## Open questions
- Does the SDK reject or silently replace a tool registered twice under the same name? If it replaces it, should the host fail at startup, like ADR 0014's duplicate service domain check? Answer during group 2. Add a startup guard only if it is cheap; otherwise list it in the ADR as a follow-up for 1.16.
- Does a 401 from the fallback policy on `/mcp` reach the existing status-code pages (Problem Details body), or does the SDK's endpoint metadata change that? A test in group 2 settles it.
