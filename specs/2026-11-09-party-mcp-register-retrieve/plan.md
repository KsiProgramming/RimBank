# Plan: MCP adapter: Register & Retrieve

## 1. MCP wiring for context tools
- [x] `src/RimBank.Mcp/RimBank.Mcp.csproj`: reference `RimBank.Core` and `WolverineFx` (for `IMessageBus`), and update the header comment (root conventions stay core-free; context folders follow tech-stack §4's `.Mcp` row).
- [x] `RimBank.Host/Mcp/McpComposition.AddRimBankMcp`: add `AddRimBankToolAuthorization()` after `AddRimBankToolErrors()` and before the modules' tools. This replaces the SDK's `AddAuthorizationFilters()`, whose denial cannot be mapped (ADR 0039).
- [x] Pin the SDK 2.3.1 denial behaviour with a fake tool `[Authorize(Policy = …)]` in `RimBank.Host.Tests/Mcp`: a denied call and a filtered `tools/list`.
- [x] `ToolOperationAttribute` and `ToolAuthorizationFilter` (call: `shared.forbidden` 403 with `instance` and `traceId`; list: hide the tool). They are pinned by `McpToolAuthorizationTests` with a fake authorized module.
- [x] `dotnet test tests/RimBank.Architecture.Tests`: `shared-mcp-dependencies`, `mcp-dependencies` and `assembly-names` stay green.

## 2. ADR 0039
- [x] `docs/adr/0039-party-mcp-tools.md`, following [requirements.md](requirements.md) "ADRs to write".
- [x] Add amendment notes to ADR 0021 (input schemas are mirrored) and ADR 0022 (the forbidden mapping and authorization filters).

## 3. Register tool (command)
- [x] `src/RimBank.Mcp/Party/PartyReferenceDataDirectory/Registration/`, namespace `RimBank.Party.PartyReferenceDataDirectory.Mcp`:
  - `RegisterPartyArguments` mirrors `RegisterPartyRequest` (BIAN names), with `ToCommand(IdempotencyKey)` reusing `PartyTypes.FromBianName`.
  - `RegisterPartyResult` mirrors `RegisterPartyResponse` and adds `version`.
  - `RegisterPartyTool` has `[McpServerToolType]`, and its method has:
    - `[McpServerTool(Name = "party_reference_data_directory_register", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]`;
    - `[ToolOperation(OperationId)]`, with `OperationId = "PartyReferenceDataDirectoryEntry/Register"`;
    - a BIAN description.
  - The tool validates `idempotencyKey` with `IdempotencyKey.TryCreate`, then calls `bus.InvokeAsync<Result<RegisterPartyAcknowledged>>`, then `.ToToolResult(…)` with structured content and a JSON text block.
- [x] `PartyReferenceDataDirectoryTools.AddPartyReferenceDataDirectoryTools(this IMcpServerBuilder)`: `WithTools<RegisterPartyTool>()`.
- [x] `RimBank.Host/Modules/Party/PartyReferenceDataDirectoryModule.AddMcpTools`: call the extension, and update the class summary.
- [x] `tests/RimBank.Mcp.Tests`: references to `RimBank.Host`, `RimBank.Host.Testing`, Testcontainers.PostgreSql, Alba and `ModelContextProtocol` (client). Add `Party/PartyReferenceDataDirectory/`:
  - `PartyMcpHost` (host on its own database, in-process `McpClient` with `TestTokens`, and an Alba handle for parity);
  - the `PostgresqlServer` assembly fixture.
- [x] `Registration/RegisterPartyToolTests`:
  - the round-trip, and a stored stream;
  - the same key replays the same result, with one stream;
  - a different payload with the same key is 409 `shared.idempotency-key-reused`;
  - a missing or invalid key is 400;
  - a missing or invalid `PartyType` is 400 with the `party.*` code;
  - the read scope only is `shared.forbidden` and hidden from the list, with no stream stored.

## 4. Retrieve tool (query)
- [x] `Retrieval/`:
  - `RetrievePartyReferenceDataDirectoryEntryResult` mirrors the `.Api` response and adds `version`.
  - `RetrievePartyReferenceDataDirectoryEntryTool`:
    - `[McpServerTool(Name = "party_reference_data_directory_retrieve", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]`;
    - `[ToolOperation("PartyReferenceDataDirectoryEntry/Retrieve")]`;
    - the argument `partyReferenceDataDirectoryEntryId`;
    - an invalid id returns `EntryIdInvalid`;
    - it calls `InvokeAsync<Result<PartyReferenceDataDirectoryEntryView>>`.
- [x] Register it in `AddPartyReferenceDataDirectoryTools`.
- [x] `Retrieval/RetrievePartyReferenceDataDirectoryEntryToolTests`:
  - found, with `version` equal to the HTTP `ETag`;
  - an unknown id is 404 `isError`;
  - a malformed id is 400;
  - the write scope only is `shared.forbidden`.

## 5. Guards, parity and docs
- [x] `PartyToolCatalogTests`:
  - `tools/list` (all scopes) equals the snake_case names of the `OperationPolicyCatalog` operations minus the named 1.17 allow-list;
  - each tool's descriptor has all four annotations as in the ADR 0021 table, and a non-empty description and input schema;
  - each tool's `[ToolOperation]` policy equals an endpoint's `OperationId` and the catalog kind (query is `ReadOnly`).
- [x] `PartyMcpParityTests` (the minimum the Definition of done requires):
  - Register and Retrieve through MCP and through HTTP give equal JSON for the BIAN fields (ids and dates normalised), and MCP's `version` equals HTTP's `ETag`;
  - for invalid party type, unknown id and reused key, the Problem Details `type`, `status`, `errorCode` and `errors` are equal.
- [x] Docs:
  - tech-stack §3.3 (`RimBank.Mcp` and `RimBank.Mcp.Tests` lines) and §6 (mirrored models, `version`, `shared.forbidden`);
  - CONTRIBUTING "MCP" (how to add a tool; a call from MCP Inspector);
  - tick roadmap 1.16;
  - fill in "Proven by" in ADR 0039.
- [x] `dotnet build`, `dotnet test`, `dotnet format --verify-no-changes`.
