# Validation: MCP adapter: remaining operations

## Done when (from roadmap)
> **1.17 MCP adapter: remaining operations:** Update and BQ tools; a test that the tool list matches the implemented BIAN operations.
> + Definition of done (roadmap.md)

The phase adds no read model and changes none, so the projection-test and rebuild items of the Definition of done do not apply. The read side reuses the existing inline views.

## Automated checks
- [ ] **Tool-list guard** (`PartyToolCatalogTests`, user's choice):
  - `_toolsIn117` is gone;
  - the registered tools are exactly the `ToolName` of every operation in `OperationPolicyCatalog` (13 tools);
  - every tool has one `[ToolOperation]` whose policy exists, a description, and the annotations of its kind (`readOnly` for queries only, never `destructive`, `idempotent`, never `openWorld`).
- [ ] **Required arguments:**
  - every command tool requires `idempotencyKey`;
  - every Update and Exchange tool also requires `expectedVersion`, and every command tool except Register requires `request`;
  - query tools take neither key nor version.
- [ ] **Parity per operation** (`PartyMcpParityTests` and the per-BQ parity tests, user's choice). For each of the 11 operations:
  - the tool's `structuredContent` without `version` equals the HTTP body, and `version` equals the `ETag`;
  - at least one error gives the same Problem Details members (`type`, `title`, `status`, `detail`, `errorCode`, `errors`). The error is a validation 400, an unknown id 404, a stale version 409 or a refused BIAN part.
- [ ] **Version round-trip** (user's choice):
  - Retrieve gives `version` n, and Update with n succeeds with n + 1;
  - Update with n again is 409 Problem Details with the HTTP code;
  - a missing `expectedVersion` is `shared.tool-arguments-invalid`;
  - the same `idempotencyKey` and `request` return the first answer without a second event.
- [ ] **Authorisation:**
  - a read-scope-only token lists only the 5 Retrieve tools;
  - its call to a command tool is `isError` with `shared.forbidden` (403), and nothing is appended;
  - a call without a token is rejected, as tested in 0.16.
- [ ] **Tool tests per BQ** (`RimBank.Mcp.Tests/Party/PartyReferenceDataDirectory/{Update,Demographics,BankRelations,Associations,Reference}/`), with the cases listed in plan.md groups 1–5, against a Testcontainers PostgreSQL through `PartyMcpHost`.
- [ ] **Unchanged suites stay green:**
  - `RimBank.Architecture.Tests`, including `mcp-dependencies` (`.Mcp` does not reference `.Api`) and `shared-mcp-dependencies` (the `RimBank.Mcp` root does not reference core or Wolverine);
  - `RimBank.Core.Tests` and `RimBank.Api.Tests`, including the BIAN conformance tests.
- [ ] `dotnet build -warnaserror`, `dotnet test` and `dotnet format --verify-no-changes` pass locally.
- [ ] GitHub Actions CI green: build, test, format, CodeQL.

## Manual / demo checks
- [ ] Read through the `tools/list` output (from a test log or a local run): the input schema of `party_reference_data_directory_update_reference` shows a nested `request` object with BIAN property names, and its description says how left-out and empty fields behave.
- [ ] Optional (not a merge gate, per the user's choice): MCP Inspector against the AppHost's `/mcp` with a Keycloak token, following CONTRIBUTING "MCP". Register, then Retrieve (take `version`), then `party_reference_data_directory_update_reference` with a `ResidentialAddress` and that `expectedVersion`. Then `party_reference_data_directory_retrieve_reference` shows the address, and a second Update with the old version gives an `isError` 409 Problem Details.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.17 ticked in the same PR
- [ ] ADR 0040 written; ADR 0039 consequences and tech-stack §6 updated to match
- [ ] CONTRIBUTING "MCP" explains `request` and `expectedVersion`
- [ ] No business rule or validation added to `RimBank.Mcp`: the mirrors only translate JSON into the existing core inputs (business logic in core)
- [ ] No PII in logs or tool error details: Problem Details only, with no request echo or stack trace
- [ ] PR opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<1.17 issue>`)
