# Validation: MCP adapter: Register & Retrieve

## Done when (from roadmap)
> **1.16 MCP adapter: Register & Retrieve:** `RimBank.Party.PartyReferenceDataDirectory.Mcp` tools for Register (command) and Retrieve (query, `readOnly`), with a parity test against the HTTP endpoints.
> + Definition of done (roadmap.md)

`RimBank.Party.PartyReferenceDataDirectory.Mcp` is the namespace of the Party folder in `RimBank.Mcp`, not a project (ADR 0025).

## Automated checks
The chosen focus is MCP client tests. The parity, authorisation and tool-list checks below are kept at minimum size because the roadmap row, the Definition of done ("an MCP tool, with a parity test") and the scope decisions (mirrored models, `shared.forbidden`, guard pulled forward) need them.

- [ ] **MCP client tests**: `tests/RimBank.Mcp.Tests/Party/PartyReferenceDataDirectory/`, with Testcontainers PostgreSQL and the in-process SDK client:
  - [ ] `tools/list` shows `party_reference_data_directory_register` and `party_reference_data_directory_retrieve`, each with a description, an input schema and the four annotations of the ADR 0021 table. Retrieve is `readOnly=true`, Register is `readOnly=false`, and both are `destructive=false`, `idempotent=true` and `openWorld=false`.
  - [ ] Register, then Retrieve, is a round-trip. Retrieve's `version` equals Register's.
  - [ ] Idempotency: the same key gives the same result and one stream. Another payload with that key is an `isError` 409 `shared.idempotency-key-reused`. An empty or invalid key is 400 `shared.idempotency-key-invalid`, and a missing one is `shared.tool-arguments-invalid`.
  - [ ] An invalid or blank `partyType` is `isError` 400 `party.party-type-invalid` or `party.party-type-required`.
  - [ ] An unknown entry is `isError` 404. A malformed id is `isError` 400. Unbindable arguments are `shared.tool-arguments-invalid`.
  - [ ] Every `isError` result has Problem Details in `structuredContent`, the same JSON as text, `instance: /mcp`, a `traceId`, and no stack trace.
- [ ] **Authorisation**:
  - [ ] With the read scope only, register is hidden from `tools/list`, a call is `isError` 403 `shared.forbidden`, and no stream or outcome is stored.
  - [ ] With the write scope only, the same holds for retrieve.
- [ ] **Parity**: `PartyMcpParityTests`. MCP and HTTP give equal BIAN fields, MCP's `version` equals HTTP's `ETag`, and the shared error cases have equal Problem Details (`type`, `status`, `errorCode`, `errors`).
- [ ] **Tool-list guard**: `PartyToolCatalogTests`. The tools equal the implemented catalog operations minus the named 1.17 allow-list, and each tool's policy is its endpoint's `OperationId`.
- [ ] **Host**: `RimBank.Host.Tests/Mcp`. A fake `[ToolOperation]` tool is hidden without its scope and refused with `shared.forbidden`. The existing `McpToolErrorTests`, `McpAuthorizationTests` and `McpCallerTests` stay green.
- [ ] **Architecture**: `RimBank.Architecture.Tests` green, in particular `mcp-dependencies`, `shared-mcp-dependencies`, `shared-mcp-only-in-mcp-and-host`, `assembly-names`, `context-namespaces-stop-at-the-adapter`, `adapters-dispatch-through-the-message-bus` and `cross-context-isolation`.
- [ ] **Unchanged suites green**: `RimBank.Api.Tests` (incl. `AuthorisationTests`, `OperationAuthorizationCoverageTests` and conformance), `RimBank.Core.Tests` and `RimBank.Postgresql.Tests`.
- [ ] `dotnet format --verify-no-changes`.
- [ ] GitHub Actions CI green (build, test, format, CodeQL).

## Manual / demo checks
Optional; not chosen as a merge gate.
- [ ] `aspire run`, then MCP Inspector (`npx @modelcontextprotocol/inspector`) on `http://localhost:<host-port>/mcp`. Sign in as `rimbank-developer`.
  - [ ] Call `party_reference_data_directory_register` with `{ "partyType": "Person", "idempotencyKey": "demo-1" }`. It returns the entry id and `version` 1.
  - [ ] Call `party_reference_data_directory_retrieve` with that id. It returns the same entry.
  - [ ] Call `party_reference_data_directory_retrieve` with a random v7 GUID. It returns `isError` with 404 Problem Details.

## Merge criteria
- [ ] All checks above pass in CI.
- [ ] Roadmap checkbox 1.16 ticked in the same PR.
- [ ] ADR 0039 written. ADR 0021 and 0022 amendment notes added.
- [ ] tech-stack §3.3 and §6, and CONTRIBUTING "MCP", updated.
- [ ] No tool arguments, PII or tokens in logs (`ModelContextProtocol` log category stays at `Warning`, ADR 0021).
- [ ] No per-context `.Mcp` project, and no `.Mcp` → `.Api` reference.
- [ ] The PR is opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<issue>`).
