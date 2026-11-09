# MCP adapter: remaining operations

Roadmap phase: 1.17
Branch: plan/1.17-party-mcp-remaining-tools

## Context
Mission principle 6 says every operation a context offers over HTTP is also an MCP tool over the same core. Principle 8 says an MCP error is the same Problem Details as on HTTP. Phase 1.16 shipped the first two Party tools (Register, Retrieve) and the conventions they follow ([ADR 0039](../../docs/adr/0039-party-mcp-tools.md)):
- mirrored `.Mcp` models proven equal to HTTP by parity tests;
- `version` on every result;
- a required `idempotencyKey` on commands;
- `[ToolOperation]` with RimBank's call and list authorisation filters;
- a tool-list guard with an allow-list of the 11 operations still missing a tool.

This phase adds those 11 tools to the folder `Party/PartyReferenceDataDirectory/` of **`RimBank.Mcp`** (namespace `RimBank.Party.PartyReferenceDataDirectory.Mcp`, [ADR 0025](../../docs/adr/0025-one-api-project.md), [ADR 0026](../../docs/adr/0026-flat-context-namespaces.md)) and empties the allow-list. It closes Stage 1: every Party operation in [PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) that RimBank implements ([ADR 0024](../../docs/adr/0024-party-reference-data-directory-mapping.md)) is reachable over HTTP and MCP.

It depends on 1.8–1.12 (the operations), 1.15 (policies) and 1.16 (tool conventions, test host, guard), all merged. It unblocks Stage 2's MCP work, 4.14 (money-moving tools) and 7.9 (MCP hardening).

**CQRS split** (tech-stack §8):
- **Commands (write side), 7 tools.** Each dispatches the existing command through `IMessageBus.InvokeAsync<Result<…Acknowledged>>` and answers from the acknowledgement, never from a read model. The commands are `UpdatePartyReferenceDataDirectoryEntry`, `UpdateDemographics`, `ExchangeDemographics`, `UpdatePartyReference`, `RegisterPartyAssociation`, `UpdatePartyAssociation` and `UpdateBankRelations`. Idempotency comes from `CommandIdempotencyPolicy` ([ADR 0018](../../docs/adr/0018-idempotency-keys-for-commands.md)). Every Update and Exchange tool takes an `expectedVersion` ([ADR 0027](../../docs/adr/0027-optimistic-concurrency-for-commands.md)).
- **Queries (read side), 4 tools.** Demographics, Reference and BankRelations Retrieve read the existing **inline** `PartyReferenceDataDirectoryEntryView`. Associations Retrieve reads the existing **inline** `PartyAssociationView`. **No read model is added or changed**, so there is no new projection, projection test or rebuild step.

## Scope
### In scope
- **11 tools**, named by the existing `ToolName` rule (ADR 0021):

  | Operation | Tool | Kind | Arguments |
  |---|---|---|---|
  | `PartyReferenceDataDirectoryEntry/Update` | `party_reference_data_directory_update` | command | entry id, `request`, `expectedVersion`, `idempotencyKey` |
  | `Demographics/Update` | `party_reference_data_directory_update_demographics` | command | entry id, `demographicsId`, `request`, `expectedVersion`, `idempotencyKey` |
  | `Demographics/Exchange` | `party_reference_data_directory_exchange_demographics` | command | entry id, `demographicsId`, `request`, `expectedVersion`, `idempotencyKey` |
  | `Demographics/Retrieve` | `party_reference_data_directory_retrieve_demographics` | query | entry id, `demographicsId` |
  | `Reference/Update` | `party_reference_data_directory_update_reference` | command | entry id, `referenceId`, `request`, `expectedVersion`, `idempotencyKey` |
  | `Reference/Retrieve` | `party_reference_data_directory_retrieve_reference` | query | entry id, `referenceId` |
  | `Associations/Register` | `party_reference_data_directory_register_associations` | command | entry id, `request`, `idempotencyKey` |
  | `Associations/Update` | `party_reference_data_directory_update_associations` | command | entry id, `associationsId`, `request`, `expectedVersion`, `idempotencyKey` |
  | `Associations/Retrieve` | `party_reference_data_directory_retrieve_associations` | query | entry id, `associationsId` |
  | `BankRelations/Update` | `party_reference_data_directory_update_bank_relations` | command | entry id, `bankRelationsId`, `request`, `expectedVersion`, `idempotencyKey` |
  | `BankRelations/Retrieve` | `party_reference_data_directory_retrieve_bank_relations` | query | entry id, `bankRelationsId` |

- **One body object per command tool (user's choice).**
  - Each command tool takes one object argument, `request`, shaped exactly like the operation's BIAN request body, with BIAN property names (for example `GovernmentIssuedIdentityReference`, `ResidentialAddress.TownName`).
  - The ids, `expectedVersion` and `idempotencyKey` stay flat camelCase parameters, as in ADR 0039.
  - The partial-update rules are those of HTTP, because the JSON is the same:
    - a field left out keeps its value;
    - an empty value clears it;
    - an address or document replaces the whole of it;
    - BIAN parts RimBank does not record are refused with the same error codes (for example `party.reference-field-not-supported`);
    - properties that are not BIAN's are ignored.
- **Mirrored models.** The `.Mcp` folder gets its own request mirrors of the 7 `.Api` request records and their nested models (postal address, document, country, residency status, e-mail, mobile, social network, legal entity, government identity, involved party, validity period, employment, bank relation reference). It also gets result mirrors of the 6 response records (Update, Demographics, Reference, Associations, BankRelations; the CR Update reuses the Retrieve shape if it is the same). `mcp-dependencies` (`.Mcp` never references `.Api`) is unchanged. Parsing and validation stay in `RimBank.Core` (the existing `…Input`/`…Changes`/`…Terms` factories). The mirrors only translate JSON into those inputs.
- **Results.** Each result is `structuredContent` with the BIAN response fields plus `version`, and the same JSON as text. The version is the entry's version, or the association's for the Associations tools (its own stream, ADR 0033).
- **Annotations** (ADR 0021 table): queries `ReadOnly=true`; commands `ReadOnly=false`. All tools are `Destructive=false`, `Idempotent=true` and `OpenWorld=false`. Nothing in Party moves money.
- **Authorisation:** `[ToolOperation(OperationId)]` on each tool. A caller with the read scope only does not see the 8 command tools (the 7 new ones and 1.16's Register) in `tools/list`, and a call to one gets `shared.forbidden` (403) Problem Details (ADR 0038, ADR 0039).
- **Errors:** every failure goes through `ToToolResult` and `ToolProblem`, with the same codes as HTTP. That covers invalid ids (400), an unknown entry, BQ or association (404), a stale `expectedVersion` (409), a reused key (409) and validation errors (400). A missing `expectedVersion`, a missing `request` or a body that cannot be bound is `shared.tool-arguments-invalid`.
- **Wiring:** `AddPartyReferenceDataDirectoryTools()` registers the 11 new tool types.
- **Guard:** `_toolsIn117` is deleted from `PartyToolCatalogTests`. The tool list equals the catalog's operations exactly. The catalog test also checks that every command tool requires `idempotencyKey`, every Update or Exchange tool also requires `expectedVersion`, and no query tool takes either.
- **Docs:**
  - ADR 0040;
  - tech-stack §6 (arguments: flat for ids and keys, one `request` object for a command's body);
  - CONTRIBUTING "MCP" (calling an Update tool with `expectedVersion`);
  - the comment of `PartyReferenceDataDirectoryTools`;
  - the roadmap tick.

### Out of scope
- Money-moving scope, default-off switch and `destructive` tools (**4.14**).
- Per-client rate limiting and the tool-call audit query (**7.9**).
- The deferred BIAN operations (CR Control, Request, Execute, Exchange and the eight BQ channels, ADR 0024). They have no HTTP endpoint, so they get no tool.
- Changing Register and CR Retrieve from 1.16. Register keeps its flat `partyType` because it has a single field and no body to mirror.
- An MCP equivalent of `If-None-Match` / 304. MCP has no conditional requests (ADR 0039).
- A manual MCP Inspector demo as a merge gate. The user did not choose it. CONTRIBUTING still explains how to run one.
- New architecture rules for tools. The catalog tests cover names, policies, annotations and required arguments.

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Tool set | All 11 remaining operations, with the allow-list emptied | User's choice and the roadmap row. It closes Stage 1's principle 6 parity. |
| Command arguments | One `request` object shaped like the BIAN request body, plus flat ids, `expectedVersion` and `idempotencyKey` | User's choice. The body is the same JSON as HTTP, so the partial-update rules (left out, empty, replaced, refused) carry over unchanged and the parity tests compare like with like. Reference has about 25 fields, some nested, which do not fit flat parameters. This changes ADR 0039's flat-only rule for body-carrying commands, so it needs ADR 0040. |
| Name of the object argument | `request` on every command tool | One name across Update, Exchange and Register. It is the BIAN request body, so agents and docs can point at the BIAN schema. |
| Request and result models | Mirrors in `.Mcp`, proven equal by parity tests | ADR 0039. `mcp-dependencies` stays, and each adapter can move alone (tech-stack §2). |
| Unrecorded BIAN parts | The mirrors keep `[JsonExtensionData]`, so they are refused exactly as on HTTP | Same error codes on both channels (principle 8). The parity tests include a refused field. |
| `expectedVersion` | A required `long` parameter on every Update and Exchange tool, taken from a Retrieve or a previous command's `version` | MCP's form of `If-Match` (ADR 0027, ADR 0039). A missing one is `shared.tool-arguments-invalid`, where HTTP answers 428. ADR 0040 records this difference. |
| Associations version | The association's own stream version | Associations is its own aggregate (ADR 0033), as its HTTP `ETag` already is. |
| Annotations | Commands are not destructive and are idempotent with their key; queries are `readOnly` | ADR 0021 table. Party moves no money. |
| Order of work | Smallest first: CR Update → Demographics → BankRelations → Associations → Reference → guard and docs | User's choice. Each group repeats the pattern on a larger body, and the build is green after each one. |
| Test home | `RimBank.Mcp.Tests/Party/PartyReferenceDataDirectory/<BQ>/`, reusing `PartyMcpHost` and the 1.16 parity helpers | Tech-stack §3.3 and §9. |

## ADRs to write
- **0040 Party MCP command tools: a BIAN body object and expected versions.** It covers:
  - the `request` object argument for body-carrying commands, which amends ADR 0039 and tech-stack §6;
  - the required `expectedVersion`, and its missing case as `shared.tool-arguments-invalid` instead of HTTP's 428;
  - how mirrored nested models and `[JsonExtensionData]` keep the refusal rules identical;
  - the Associations version;
  - the empty tool-list allow-list.

## Open questions
- **Input schema for `request`.** Does SDK 2.3.1 generate a usable nested JSON schema from a record with `[JsonPropertyName]` and `[JsonExtensionData]`, including `additionalProperties`? Check this in group 1 with the CR Update. If the schema is wrong or unreadable, describe the BIAN fields in the parameter's `[Description]` and record the workaround in ADR 0040.
- **CR Update result shape.** If `UpdatePartyReferenceDataDirectoryEntryResponse` is the same as the Retrieve response, reuse the 1.16 result mirror. Otherwise add its own. Decide in group 1 from the `.Api` model.
