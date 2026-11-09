# MCP adapter: Register & Retrieve

Roadmap phase: 1.16
Branch: plan/1.16-party-mcp-register-retrieve

## Context
Mission principle 6 says every service domain is usable by AI agents through MCP tools that are thin adapters over the same core as HTTP. Principle 8 says an MCP error is the same Problem Details as on HTTP. The wiring already exists. `/mcp` runs Streamable HTTP in stateless mode with the module hook `AddMcpTools` ([ADR 0021](../../docs/adr/0021-mcp-wiring-and-tool-conventions.md)). `RimBank.Mcp` provides `ToToolResult` and `ToolErrorFilter`, and the protected-resource metadata is in place ([ADR 0022](../../docs/adr/0022-mcp-authorization-and-tool-errors.md)). The per-operation policies and their scopes exist too ([ADR 0038](../../docs/adr/0038-per-operation-authorisation-policies.md)). But no context ships a tool yet, so `tools/list` is "method not available".

This phase adds the first context tools as the folder `Party/PartyReferenceDataDirectory/` of **`RimBank.Mcp`**, namespace `RimBank.Party.PartyReferenceDataDirectory.Mcp`. There is no per-context `.Mcp` project ([ADR 0025](../../docs/adr/0025-one-api-project.md), [ADR 0026](../../docs/adr/0026-flat-context-namespaces.md)). The phase covers the two BIAN operations of the Control Record in [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml): `PartyReferenceDataDirectoryEntry/Register` and `PartyReferenceDataDirectoryEntry/Retrieve`.

Depends on 0.15, 0.16, 1.5, 1.7 and 1.15, all merged. It unblocks 1.17 (Update and the BQ tools reuse the pattern, the test host and the tool-list guard), 4.14 (money-moving tools) and 7.9 (MCP hardening).

**CQRS split** (tech-stack §8):
- **Command (write side):** `party_reference_data_directory_register` dispatches the existing `RegisterParty` through `IMessageBus.InvokeAsync<Result<RegisterPartyAcknowledged>>`. It answers from the acknowledgement, never from a read model. Idempotency comes from the existing `CommandIdempotencyPolicy` ([ADR 0018](../../docs/adr/0018-idempotency-keys-for-commands.md)), fed by a required `idempotencyKey` argument.
- **Query (read side):** `party_reference_data_directory_retrieve` dispatches the existing `RetrievePartyReferenceDataDirectoryEntry`. It reads only the existing `PartyReferenceDataDirectoryEntryView` through `PartyDirectoryEnquiry`, an **inline** projection. **No read model is added or changed**, so there is no new projection or rebuild work.

## Scope
### In scope
- **Project references:** `RimBank.Mcp` gains `RimBank.Core` and Wolverine (`IMessageBus` only), as tech-stack §4 already allows for a context's `.Mcp` part. The root namespace `RimBank.Mcp` still references neither, and `shared-mcp-dependencies` keeps checking that.
- **Tool `party_reference_data_directory_register`:**
  - Arguments: `partyType` and `idempotencyKey`, both required. BIAN's ignored Register fields are not offered (ADR 0039).
  - Answer: `structuredContent` with the BIAN Register response fields and the entry's `version`, and the same JSON as text.
  - Annotations: `ReadOnly=false`, `Destructive=false`, `Idempotent=true`, `OpenWorld=false`.
  - The description comes from the BIAN operation.
- **Tool `party_reference_data_directory_retrieve`:**
  - Argument: `partyReferenceDataDirectoryEntryId` (string, validated like the route value).
  - Answer: the BIAN Retrieve response fields and `version`, so that 1.17's Update tool can get its `expectedVersion` ([ADR 0027](../../docs/adr/0027-optimistic-concurrency-for-commands.md)).
  - Annotations: `ReadOnly=true`, `Destructive=false`, `Idempotent=true`, `OpenWorld=false`.
- **MCP models as mirrors:** the `.Mcp` folder has its own argument and result records with the BIAN property names, mirroring the `.Api` models. `mcp-dependencies` (.Mcp never references .Api) is unchanged. A parity guard proves that the MCP JSON equals the HTTP JSON.
- **Authorisation:**
  - Each tool carries `[ToolOperation("<operation id>")]`, the same named policy as its endpoint (ADR 0038). It is not `[Authorize]`, because the SDK's filter would refuse with a JSON-RPC error (see Open questions).
  - `ToolAuthorizationFilter` (added by `AddRimBankToolAuthorization()`) hides a tool the caller may not use from `tools/list`.
  - **A denied call becomes an `isError` result with `shared.forbidden` (403) Problem Details**, from the same filter. Nothing is dispatched, so nothing is stored.
- **Errors:** every failure uses `ToToolResult`:
  - an invalid `PartyType` is `party.party-type-required` or `party.party-type-invalid` (400);
  - a reused key is 409, and a key in use is 409;
  - an invalid id is 400, and an unknown entry is 404;
  - an unbindable or missing argument is `shared.tool-arguments-invalid`.
- **Wiring:**
  - One extension, `AddPartyReferenceDataDirectoryTools()`, in the `.Mcp` folder.
  - `PartyReferenceDataDirectoryModule.AddMcpTools` calls it.
  - With the tools registered, `tools/list` is served.
- **Tool-list guard, pulled forward from 1.17:**
  - A test checks that the registered tools are exactly the snake_case names of the catalog's implemented BIAN operations (`OperationPolicyCatalog`). A named allow-list holds the 11 operations that get their tools in 1.17.
  - A test checks that each tool's policy equals its endpoint's name and operation id.
  - 1.17 empties the allow-list.
- **Docs:**
  - ADR 0039.
  - tech-stack §3.3 (the `RimBank.Mcp` and `RimBank.Mcp.Tests` lines) and §6 ("input schemas reuse the `.Api` request models" becomes "mirror them, proven equal by a parity test").
  - CONTRIBUTING "MCP": adding a tool, and calling it from MCP Inspector or Claude Code.
  - The comments of `PartyReferenceDataDirectoryModule` and `RimBank.Mcp.csproj`.
  - The roadmap tick.

### Out of scope
- The Update, Demographics, Reference, Associations and BankRelations tools, and an empty allow-list (**1.17**).
- Money-moving scope, default-off switch and `destructive` tools (**4.14**).
- Per-client rate limiting and the tool-call audit query (**7.9**). The caller already reaches the envelope through `ActorRelay` (ADR 0022).
- An HTTP-style `If-None-Match` / 304 on Retrieve. MCP has no conditional requests: the result always carries `version`.
- Sharing one model type between `.Api` and `.Mcp`, for example by relaxing `mcp-dependencies`. That option was rejected (see Decisions).
- New architecture rules for tool names and annotations. The tool-list and annotation tests cover them for now, and ADR 0021 leaves the rules to "if reviews show they are needed".

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Tool set | Register and Retrieve only | The roadmap row. One reviewable PR, and 1.17 repeats the pattern. |
| Argument and result models | `.Mcp` mirrors the `.Api` models with BIAN property names, and a parity test proves equal JSON | User's choice. It keeps `mcp-dependencies` and lets each adapter move alone when a context is extracted (tech-stack §2). The parity test is what stops drift. Amends tech-stack §6's "reuse". |
| Version on results | A `version` field next to the BIAN fields in `structuredContent` | MCP has no `ETag` header. Update in 1.17 needs `expectedVersion` (ADR 0027). The BIAN conformance subset check (ADR 0037) applies to HTTP bodies only, and ADR 0039 records the extra field. |
| Idempotency key | Required tool argument `idempotencyKey`, passed into `RegisterParty` like the header value | ADR 0018 and 0021: every command tool takes a key. The same `IdempotencyKey.TryCreate` errors as HTTP. |
| Authorisation | `[ToolOperation(OperationId)]`, checked by RimBank's call and list filters | ADR 0038 follow-up. Same policies and scopes as HTTP (principle 6). `[Authorize]` was dropped during implementation (see Open questions). |
| Denied tool call | An `isError` result with `shared.forbidden` Problem Details | User's choice. Principle 8: MCP errors are the same Problem Details as on HTTP. The tool stays hidden from `tools/list`. |
| Tool arguments | camelCase method parameters, only those that change the outcome | Found in implementation: parameters give a flat, described input schema. A body record would make agents nest their arguments. |
| Operation ids in `.Mcp` | Literal constants in each tool type, cross-checked against the `.Api` endpoints by a test | `.Mcp` cannot reference `.Api`, and the core holds no transport names. The test makes a typo fail CI. |
| Tool-list guard | Pulled forward from 1.17, with an allow-list of the operations not yet covered | User's choice. The guard exists before the tools multiply, and 1.17 only shrinks the list. |
| Test home | `RimBank.Mcp.Tests` gains a Party folder with a Testcontainers host (namespace `RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests`) | Tech-stack §3.3 and §9: "per context from 1.16: MCP client → tools … parity". |
| Order of work | Wiring → ADR → Register → Retrieve → module and docs | User's choice. The build is green after each group. |

## ADRs to write
- **0039 Party MCP tools: arguments, results and authorisation** (written). It covers:
  - mirrored models with a parity guard (amends tech-stack §6);
  - the `version` field on results, and no 304 on MCP;
  - the `idempotencyKey` argument;
  - `[ToolOperation]` and the RimBank authorisation filters, with `shared.forbidden` (amends ADR 0022 and ADR 0038);
  - `RimBank.Mcp`'s new references for its context folders, while its root stays core-free;
  - the tool-list guard and its 1.17 allow-list.

## Open questions
Answered during implementation (ADR 0039):
- **SDK 2.3.1 authorization denial:**
  - The SDK's `AddAuthorizationFilters` filter always runs before RimBank's filters, and refuses with a JSON-RPC `-32600` that no filter can map.
  - Without that filter, a tool with `[Authorize]` fails.
  - So tools carry `[ToolOperation]`, and RimBank's own filters apply the policy to calls and to the list.
- **Input schema:** tools take flat camelCase parameters with `[Description]`. An empty idempotency key is `shared.idempotency-key-invalid`, as on HTTP. A missing argument is `shared.tool-arguments-invalid`.
