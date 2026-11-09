# Plan: MCP adapter: remaining operations

All tools live in `src/RimBank.Mcp/Party/PartyReferenceDataDirectory/<Folder>/`, namespace `RimBank.Party.PartyReferenceDataDirectory.Mcp`. Each tool:
- is a `[McpServerToolType]` class with `Name` and `OperationId` constants, `[ToolOperation(OperationId)]` and explicit annotations;
- has a description taken from the BIAN operation (and the HTTP endpoint's description);
- builds the core command or query from its arguments and `request` mirror;
- dispatches through `IMessageBus`, and answers with `ToToolResult(… ToolResults.Structured(…))`.

Tests live in `tests/RimBank.Mcp.Tests/Party/PartyReferenceDataDirectory/<Folder>/`, namespace `RimBank.Party.PartyReferenceDataDirectory.Mcp.Tests`. The build, `dotnet format --verify-no-changes` and the tests are green after each group.

## 1. CR Update and the command-tool conventions
- [x] Spike: a `request` record parameter with `[JsonPropertyName]` and `[JsonExtensionData]`. Check the SDK 2.3.1 input schema (nested properties, `required`, `additionalProperties`), and how a missing or unbindable `request` is reported (`shared.tool-arguments-invalid`). Record the result for ADR 0040.
- [x] `Update/UpdatePartyReferenceDataDirectoryEntryRequest.cs`: mirror of the `.Api` request (`PartyType` plus the ignored BIAN fields).
- [x] `Update/UpdatePartyReferenceDataDirectoryEntryResult.cs`: reuse the Retrieve result if the shapes match, otherwise a mirror of the `.Api` response plus `version`.
- [x] `Update/UpdatePartyReferenceDataDirectoryEntryTool.cs`, `party_reference_data_directory_update`:
  - parameters: `partyReferenceDataDirectoryEntryId`, `request`, `expectedVersion` (long), `idempotencyKey`;
  - dispatches `UpdatePartyReferenceDataDirectoryEntry`.
- [x] Register it in `PartyReferenceDataDirectoryTools`, and remove its entry from `_toolsIn117`.
- [x] Test helpers in `PartyMcpHost` or a shared `PartyTools` test class:
  - `RegisterAsync` → id plus `version`;
  - `CallCommandAsync(name, args)`;
  - a parity helper that compares the tool's `structuredContent` (without `version`) to an HTTP body, and `version` to the `ETag`.
- [x] Tests in `Update/UpdatePartyReferenceDataDirectoryEntryToolTests.cs`:
  - success returns version + 1;
  - a stale version is 409;
  - a missing `expectedVersion` is `shared.tool-arguments-invalid`;
  - an invalid `PartyType` is 400;
  - an unknown id is 404;
  - the same key and request give the same answer.
- [x] Parity: success body and `version`, a stale version gives the same Problem Details as HTTP's 409, and an invalid `PartyType` the same 400.

## 2. Demographics (Update, Exchange, Retrieve)
- [x] `Demographics/`: mirrors of `UpdateDemographicsRequest`, `ExchangeDemographicsRequest`, `DemographicsEmployment` and `DemographicsResponse` (as the result, plus `version`).
- [x] `UpdateDemographicsTool`, `ExchangeDemographicsTool` (commands), and `RetrieveDemographicsTool` (query, `readOnly`, reads the entry view through `RetrievePartyReferenceDataDirectoryEntry` as the endpoint does).
- [x] Registration, and remove their 3 entries from the allow-list.
- [x] Tool tests:
  - Update and Retrieve round-trip;
  - an invalid `ExchangeActionType`;
  - an unknown `demographicsId` is 404;
  - a stale version is 409.
- [x] Parity for all three tools: the success body, plus one error each (validation 400, unknown BQ 404).

## 3. BankRelations (Update, Retrieve)
- [x] `BankRelations/`: mirrors of `UpdateBankRelationsRequest`, `BankRelationReferenceModel` and `BankRelationsResponse`.
- [x] `UpdateBankRelationsTool` (command), and `RetrieveBankRelationsTool` (query).
- [x] Registration, and remove their 2 entries from the allow-list.
- [x] Tool tests:
  - an employee (a person) and a unit (an organisation) recorded and retrieved;
  - an unregistered or wrong-type party is refused with the HTTP error code;
  - a stale version is 409.
- [x] Parity: the success body for both tools, and a wrong-type reference gives the same Problem Details.

## 4. Associations (Register, Update, Retrieve)
- [x] `Associations/`: mirrors of `PartyAssociationRequest` (with `[JsonExtensionData]`), `InvolvedPartyModel`, `AssociationValidityPeriodModel` and `PartyAssociationResponse`.
- [x] Tools:
  - `RegisterPartyAssociationTool` (command, no `expectedVersion`);
  - `UpdatePartyAssociationTool` (command, with the association's `expectedVersion`);
  - `RetrievePartyAssociationTool` (query).
- [x] Registration, and remove their 3 entries from the allow-list.
- [x] Tool tests:
  - register, then retrieve, then update with the association's version;
  - an unknown associate is refused;
  - a BIAN field that is not recorded is refused with the HTTP code;
  - an unknown `associationsId` is 404.
- [x] Parity: the success body for all three, plus a refused field and a stale association version.

## 5. Reference (Retrieve, Update)
- [x] `Reference/`: mirrors of `UpdatePartyReferenceRequest` and its nested models:
  - `CountryModel`, `ResidencyStatusModel`, `PostalAddressModel`;
  - `EmailAddressModel`, `MobileNumberModel`, `SocialNetworkContactModel`;
  - `PartyLegalEntityModel`, `GovernmentIssuedIdentityModel`;
  - `PartyDocumentModel`, `DocumentDirectoryEntryModel`;
  - `InvolvedPartyModel`, shared with Associations;
  - and the `JsonElement` `CompanyOfficerRole`.
- [x] The same `UnsupportedFields()` as HTTP, using the core's `PartyReferenceFields`. Only the JSON is translated into `PartyReferenceInput`, and validation stays in `PartyReferenceChanges.Create` in core.
- [x] Mirror of `PartyReferenceResponse` as the result, plus `version`.
- [x] `RetrievePartyReferenceTool` (query), and `UpdatePartyReferenceTool` (command).
- [x] Registration, and remove the last 2 entries from the allow-list.
- [x] Tool tests:
  - a partial update keeps the fields left out and clears the empty ones;
  - an address replaces the whole address;
  - an invalid LEI, e-mail and country give the HTTP codes;
  - corporate and officer references only on an organisation;
  - an unsupported part is `party.reference-field-not-supported`.
- [x] Parity:
  - a full Reference (every field, including addresses, a document and officer references) gives the same body;
  - one validation error and one refused part give the same Problem Details.

## 6. Guard, ADR and docs
- [x] `PartyToolCatalogTests`:
  - delete `_toolsIn117`; the tool list now equals the catalog's operations exactly;
  - rename the test to `The_tool_list_is_the_implemented_operations`.
- [x] Catalog test: every command tool requires `idempotencyKey`, every Update or Exchange tool requires `expectedVersion`, and query tools take neither. This replaces the two single-tool tests from 1.16.
- [x] Authorisation test: a read-scope-only caller lists only the 5 Retrieve tools, and gets `shared.forbidden` when it calls one Update tool. Nothing is stored.
- [x] Version round-trip integration test:
  - Retrieve → Update with its version → version + 1;
  - the old version again is 409;
  - the new version works.
- [x] `docs/adr/0040-party-mcp-command-tools.md` (Decisions and Open questions resolved).
- [x] Update the ADR 0039 "Consequences" line on 1.17 to point to ADR 0040.
- [x] tech-stack §6, Naming bullet: a command's body is one `request` object with BIAN names, and Update and Exchange tools take `expectedVersion`.
- [x] CONTRIBUTING "MCP": an Update call example with `request` and `expectedVersion`.
- [x] `PartyReferenceDataDirectoryTools` comment ("every implemented BIAN operation").
- [x] `specs/roadmap.md`: tick 1.17.
