# 0039. Party MCP tools: arguments, results and authorisation

- Status: Accepted
- Date: 2026-11-09
- Roadmap phase: 1.16

## Context

Mission principle 6 says an AI agent reaches every service domain through MCP tools that are thin adapters over the same core as HTTP. Principle 8 says an MCP error is the same Problem Details as on HTTP. Roadmap 1.16 adds the first context tools: Register and Retrieve of the Party Reference Data Directory's Control Record ([PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml), [ADR 0024](0024-party-reference-data-directory-mapping.md)). The earlier decisions left four questions open:

- **Models.** Tech-stack §6 and [ADR 0021](0021-mcp-wiring-and-tool-conventions.md) said input schemas "reuse the `.Api` request models", but the rule `mcp-dependencies` forbids `.Mcp` → `.Api`.
- **Version.** HTTP sends an entry's stream version as `ETag`. MCP has no headers, and the Update tools of 1.17 need an `expectedVersion` ([ADR 0027](0027-optimistic-concurrency-for-commands.md)).
- **Authorisation.** [ADR 0038](0038-per-operation-authorisation-policies.md) planned `[Authorize(Policy = …)]` with the SDK's `AddAuthorizationFilters()`. Pinned on SDK 2.3.1 in this phase:
  - the SDK's filter always runs before any other call-tool filter, whatever the order they are added in;
  - it refuses a call with a JSON-RPC error (`-32600`, "Access forbidden"), with no Problem Details;
  - without that filter, a tool that carries `[Authorize]` fails with "Authorization filter was not invoked".
- **Where the code lives.** `RimBank.Mcp` held only the shared conventions, which reference no core and no Wolverine.

## Options considered

**Models**
1. **Relax `mcp-dependencies`** so that `.Mcp` uses the `.Api` request and response types. There would be one type per body, but the two driving adapters would be coupled and could no longer move separately when a context is extracted (tech-stack §2).
2. **Mirror records in the `.Mcp` folder**, proven equal to the HTTP bodies by parity tests.

**Denied calls**
1. **Keep `[Authorize]`** and accept the JSON-RPC error. This breaks principle 8.
2. **Catch the SDK's exception** in `ToolErrorFilter`. This is not possible, because the SDK's filter runs first.
3. **A RimBank attribute and RimBank filters.** `[ToolOperation(OperationId)]` names the policy. A RimBank call-tool filter checks it and answers `shared.forbidden`, and a list-tools filter hides the tool. The tool carries no `IAuthorizeData`, so the SDK's authorization filters are not needed.

## Decision

**Models (option 2).** Each tool has its own result record in its `.Mcp` folder. It has the BIAN property names of the endpoint's response, plus **`version`**: the entry's stream version, the value HTTP sends as `ETag` and the `expectedVersion` of later commands. There is no `If-None-Match`/304 on MCP. `ToolResults.Structured(value)` (root of `RimBank.Mcp`) puts the JSON, written with the web defaults HTTP uses, in `structuredContent` and in one text block.

**Arguments.** Tools take camelCase method parameters with a description, not a body record. Only the arguments that change the outcome are taken:
- Register takes `partyType` and `idempotencyKey`. BIAN's ignored `PartyReference` and `DirectoryEntryDate*` fields are not offered to agents.
- Retrieve takes `partyReferenceDataDirectoryEntryId`.
- Both arguments of Register are required in the input schema, so a missing one is `shared.tool-arguments-invalid` (ADR 0022). The values go through the same core validation as HTTP:
  - `IdempotencyKey.TryCreate`, so an empty or invalid key is `shared.idempotency-key-invalid`;
  - `PartyTypes.FromBianName`;
  - `PartyReferenceDataDirectoryEntryId.TryCreate`.

  They then dispatch the same `RegisterParty` and `RetrievePartyReferenceDataDirectoryEntry` through `IMessageBus`. The command is idempotent through the existing policy ([ADR 0018](0018-idempotency-keys-for-commands.md)), and the query reads only the inline view.

**Authorisation (option 3).** A tool carries `[ToolOperation("<operation id>")]`, the same id and so the same policy and scope as its endpoint's `RequireOperation` (ADR 0038). `AddRimBankToolAuthorization()`, added by the host after `AddRimBankToolErrors()`, registers `ToolAuthorizationFilter`:
- **`tools/call`:** it finds the tool by name and evaluates the named policy with `IAuthorizationService`. If the policy is not met, the answer is `isError` with the `shared.forbidden` (403) Problem Details, and nothing is dispatched or stored. The tool error filter adds `instance` and `traceId`.
- **`tools/list`:** it removes the tools whose policy the caller does not meet, as the SDK's filter would.
- An unknown policy name throws, so the call is `shared.unexpected` and never runs without its policy.
- A tool without the attribute needs only the authenticated caller of `/mcp`. Only test fakes are like that. Every context tool has one, which the tool catalog test checks.

**Layout.** `RimBank.Mcp` references `RimBank.Core` and `WolverineFx` for its context folders. Those folders follow the `.Mcp` row of tech-stack §4. The root namespace `RimBank.Mcp` still uses neither, which `shared-mcp-dependencies` keeps checking. The Party tools are `RegisterPartyTool` and `RetrievePartyReferenceDataDirectoryEntryTool` in `src/RimBank.Mcp/Party/PartyReferenceDataDirectory/`, namespace `RimBank.Party.PartyReferenceDataDirectory.Mcp`. They are registered by `AddPartyReferenceDataDirectoryTools()`, which the module's `AddMcpTools` calls.

**Tool catalog guard (pulled forward from 1.17).** A test checks:
- the tool list equals the implemented operations of the `OperationPolicyCatalog`, minus a named list of the operations whose tools 1.17 adds;
- every tool is named after its `[ToolOperation]` id by the ADR 0021 rule;
- every tool carries the ADR 0021 annotations of its kind (a query is `ReadOnly`; every tool is non-destructive, idempotent and closed-world).

1.17 empties the list.

## Consequences

- Tech-stack §3.3, §4 and §6 and CONTRIBUTING "MCP" are updated. ADR 0021 ("input schemas reuse the `.Api` request models"), ADR 0022 (call-tool filters) and ADR 0038 (`[Authorize]` on tools) get notes that point here.
- A mirror record can drift from its HTTP model. The parity tests catch drift in members and values for every implemented tool, and 1.17 adds them for each new tool.
- If a later SDK lets a filter see its authorization denial, or answers it as a tool error, this decision can be revisited. The attribute would then become `[Authorize]`, and the tests stay.
- RimBank's tool list filter replaces the SDK's for these tools. Resources and prompts are not used.
- Follow-ups:
  - **1.17:** Update and BQ tools with `expectedVersion`, and an empty catalog list.
  - **4.14:** money-moving tools behind their own scope, off by default.
  - **7.9:** rate limiting and the tool-call audit query.
- Proven by:
  - `tests/RimBank.Host.Tests/Mcp/McpToolAuthorizationTests`: list filtering, `shared.forbidden` in both directions, and allowed calls;
  - `tests/RimBank.Mcp.Tests/Party/PartyReferenceDataDirectory/`:
    - `RegisterPartyToolTests` and `RetrievePartyReferenceDataDirectoryEntryToolTests`: results, idempotency, errors, authorisation, and nothing stored on refusal;
    - `PartyToolCatalogTests`;
    - `PartyMcpParityTests`: bodies, `version` against `ETag`, and Problem Details for an invalid party type, a reused key, a malformed or unknown id and a missing scope;
  - the unchanged architecture rules `mcp-dependencies`, `shared-mcp-dependencies` and `http-and-mcp-adapters-live-in-shared-projects`.
