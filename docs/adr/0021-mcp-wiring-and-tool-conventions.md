# 0021. MCP wiring and tool conventions

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.15

## Context

Mission principle 6 says every service domain is usable by AI agents as well as by applications, through MCP tools that are thin adapters over the same core as the HTTP API. Principle 8 says an MCP error is the same Problem Details as on HTTP. [tech-stack §1](../../specs/tech-stack.md#1-platform) fixes the official C# SDK (`ModelContextProtocol.AspNetCore`) with Streamable HTTP at `/mcp`, and [§6](../../specs/tech-stack.md#6-mcp-model-context-protocol-adapter) says all contexts' tools are served by `RimBank.Host` at that single endpoint. §6 also gives the naming, annotation and error rules in one line each.

Roadmap 0.15 asks for the endpoint, a smoke tool, an in-process client test, and an ADR on tool naming, annotations and error mapping. Four questions are still open:

- which transport session mode the server uses;
- how a context hands its tools to the host without the host knowing its internals ([ADR 0014](0014-host-composition-and-module-entry-point.md));
- what the naming and annotation rules mean for each BIAN operation kind;
- what an error tool result looks like, which roadmap 0.16 then builds.

No context exists yet, so as in 0.10–0.13 the wiring is proven with a test-only fake module.

## Options considered

Session mode:

1. **Stateful** (`Mcp-Session-Id`, server-to-client requests). It allows sampling, elicitation and server notifications, but sessions live in one process, so a load balancer needs sticky sessions.
2. **Stateless.** Every request stands alone. It is the SDK's default and recommendation for servers that need no server-to-client requests.

How contexts register tools:

1. **Each context calls `AddMcpServer().WithTools<…>()` itself** in its `AddServices`. This is simple, but every context could change the transport or server options, and nothing in the module contract shows that the context has tools.
2. **Assembly scanning** (`WithToolsFromAssembly`). There is no host edit, but the tool set is invisible and depends on reflection. ADR 0014 rejected scanning for the same reasons.
3. **A third module member, `AddMcpTools(IMcpServerBuilder)`.** The host creates the one server, then hands its builder to each module in list order.

## Decision

**Endpoint and transport.** `RimBank.Host/Mcp/McpComposition` adds one MCP server (`AddRimBankMcp`) and maps it at `/mcp` (`MapRimBankMcp`), after authentication and authorization and before the modules' endpoints.

- The server announces itself as `RimBank`, with the product version without the source revision suffix.
- The transport is Streamable HTTP in **stateless** mode (option 1 of the session mode list is rejected). RimBank's tools are request/response calls over the message bus and need no sampling or elicitation, and the host stays horizontally scalable with no sticky sessions (§2). If a later tool needs server-to-client requests, a new ADR revisits this.

**Security.** `/mcp` is not anonymous. The deny-by-default fallback policy of [ADR 0019](0019-authentication-baseline.md) covers it, so a call without a valid token gets a 401 `shared.unauthenticated` Problem Details response with `WWW-Authenticate: Bearer`. The anonymous allow-list is still only the two health probes. Roadmap 0.16 adds the MCP authorization discovery (OAuth 2.1 protected-resource metadata) on top ([ADR 0022](0022-mcp-authorization-and-tool-errors.md)).

**Logging.** Some of the SDK's log messages include whole JSON-RPC messages, and so tool arguments, which can carry PII. `appsettings.json` sets the `ModelContextProtocol` log category to `Warning`, so neither those messages nor a per-call Information line reach the logs (§10). Tool calls are audited through event metadata (tech-stack §6) and the audit query (7.3, 7.9), not through logs.

**Module hook (option 3).** `IServiceDomainModule` gains `AddMcpTools(IMcpServerBuilder tools)`. This amends ADR 0014.

- `HostComposition` calls it for every module in list order, after every module's `AddServices` and before the application is built.
- A context's `.Mcp` adapter exposes one extension on `IMcpServerBuilder`, for example `AddPartyReferenceDataDirectoryTools()`, which calls `WithTools<…>()` for its tool types. The module's `AddMcpTools` calls only that extension.
- A context with no tools yet leaves the body empty. The member is required, not a default interface method, so adding tools is a visible change in the module class.
- Contexts never call `AddMcpServer`, `WithHttpTransport` or `MapMcp`. Transport, server info and the route stay in the host.
- **Duplicate tool names stop the host.** The SDK keeps only one of two tools with the same name and does not report it (checked in this phase), so one context could hide another's tool. `MapRimBankMcp` therefore checks the registered `McpServerTool`s and throws at startup with the duplicated names, like ADR 0014's duplicate service domain check.
- The SDK announces the `tools` capability only when at least one tool is registered. A host without tools offers none, so until the first `.Mcp` adapter (roadmap 1.16) `tools/list` is "method not available".

**Tool naming.** Each tool maps 1:1 to a BIAN operation and is named `<service_domain>_<operation>[_<behavior_qualifier>]` in lower-case snake_case, from the BIAN names in the ext-DDD specification, never from class or handler names:

| BIAN operation | Tool |
|---|---|
| Register a Party Reference Data Directory Entry | `party_reference_data_directory_register` |
| Retrieve a Party Reference Data Directory Entry | `party_reference_data_directory_retrieve` |
| Update the Demographics Behavior Qualifier | `party_reference_data_directory_update_demographics` |
| Retrieve a Current Account Payment | `current_account_retrieve_payment` |

The name is set explicitly with `[McpServerTool(Name = …)]`. The description is the BIAN operation's description, and the input schema reuses the `.Api` request model (tech-stack §6). *Amended by [ADR 0039](0039-party-mcp-tools.md):* tools take camelCase parameters, and their results mirror the `.Api` response models with a `version`, proven equal by parity tests, because `.Mcp` never references `.Api`.

**Annotations.** Every tool sets all four hints explicitly, so it never relies on the SDK or the protocol defaults (the protocol default for `destructiveHint` is `true`):

| Operation kind | `ReadOnly` | `Destructive` | `Idempotent` | `OpenWorld` |
|---|---|---|---|---|
| `Retrieve` (query) | `true` | `false` | `true` | `false` |
| Other commands (`Register`, `Initiate`, `Update`, `Control`, `Exchange`, `Request`, `Notify`, …) | `false` | `false` | `true` (a command always carries an idempotency key, ADR 0018) | `false` |
| Commands that move money or close a facility (`Execute` a payment, `Control` close, …) | `false` | `true` | `true` only because the idempotency key is required | `false` |

Tools act only inside RimBank, so `OpenWorld` is always `false`. A `Retrieve` tool dispatches a query (`IMessageBus.InvokeAsync<Result<TView>>`). Every other tool dispatches a command and takes an idempotency key argument. Money-moving tools also need their own scope and are off by default (tech-stack §6, roadmap 4.14).

**Error contract.** Roadmap 0.16 builds this contract in `RimBank.Mcp` ([ADR 0022](0022-mcp-authorization-and-tool-errors.md)), and every `.Mcp` adapter follows it:

- A failed `Result` (business, validation, not found, conflict) is a tool result with `isError: true`. Its `structuredContent` is the RFC 9457 Problem Details object that `RimBank.Problems` builds for HTTP: same `type`, `title`, `status`, `errorCode`, `errors` and `traceId`. Its `content` is one text block with the same JSON, for clients that read only text.
- An unexpected exception becomes `shared.unexpected` in the same shape, with no exception message or stack trace. The SDK's default handling of tool exceptions is replaced in 0.16, and a test pins it.
- A JSON-RPC protocol error (unknown tool, malformed arguments) stays a JSON-RPC error, as the MCP specification requires.
- An authentication failure stays the HTTP 401/403 Problem Details on the `/mcp` request. It never becomes a tool result.

**Smoke tool.** The roadmap's `ping`-style tool is the test-only `fake_service_domain_retrieve` in `RimBank.Host.Tests/Mcp`. It is a read-only Retrieve-style tool that follows the naming rule. No permanent tool ships in `src/`, because a `rimbank_ping` would break the naming rule and the non-goal "no speculative coverage".

## Consequences

- Adding MCP to a context means one `IMcpServerBuilder` extension in its `.Mcp` project and one line in its module's `AddMcpTools`. The CONTRIBUTING "MCP" section says so.
- Every existing module implements the new member. Today these are only test fakes.
- Only `RimBank.Host` and its tests reference the MCP SDK in this phase. Each `.Mcp` adapter will reference it as §4 allows.
- Stateless mode rules out server-to-client requests (sampling, elicitation, progress notifications over a standalone stream). Revisiting that needs a new ADR, and sticky sessions or a distributed session store.
- Follow-ups:
  - **0.16 (done, [ADR 0022](0022-mcp-authorization-and-tool-errors.md)):** OAuth 2.1 protected-resource metadata and Keycloak as the MCP authorization server; the `isError` mapping and exception handling above, with tests; a check that the actor relay (ADR 0019) reaches the message context of a tool call, for audit (§6).
  - **1.16/1.17:** the first `.Mcp` adapter, HTTP/MCP parity tests, and a test that the tool list matches the implemented BIAN operations. If reviews show they are needed, also a shared `RimBank.Mcp` library and architecture rules for tool names and annotations.
  - **4.14:** the money-moving scope and the default-off switch.
  - **7.9:** per-client rate limiting and the tool-call audit query.
