# 0040. Party MCP command tools: a BIAN body object and expected versions

- Status: Accepted
- Date: 2026-11-09
- Roadmap phase: 1.17

## Context

Roadmap 1.17 adds an MCP tool for each Party Reference Data Directory operation that 1.16 left out:
- the Control Record's Update;
- Demographics Update, Exchange and Retrieve;
- Reference Update and Retrieve;
- Associations Register, Update and Retrieve;
- BankRelations Update and Retrieve.

[ADR 0024](0024-party-reference-data-directory-mapping.md) maps these operations. [ADR 0039](0039-party-mcp-tools.md) fixed the conventions of the first two tools. Their results mirror the HTTP responses plus `version`, a command takes an `idempotencyKey`, and `[ToolOperation]` carries the policy. ADR 0039 also chose flat camelCase parameters for arguments, so that agents do not nest them.

That choice works for Register, whose body has one field. It does not fit the bodies of 1.17:
- **Size.** The Reference body has about 25 BIAN fields ([ADR 0029](0029-reference-fields-in-stages.md), [ADR 0032](0032-reference-legal-standing-and-documents.md), [ADR 0034](0034-reference-corporate-and-officer-references.md)). Several of them are objects: three ISO 20022 postal addresses, a government document with a validity period, and the corporate and officer references.
- **Partial updates.** The Update operations are partial updates with three states per field: left out keeps the value, empty clears it, and a value sets it. An object such as an address replaces the whole of it.
- **Refusals.** BIAN parts RimBank does not record are refused by path, for example `party.reference-field-not-supported`, through `[JsonExtensionData]` on each model.

The Update and Exchange tools also need MCP's form of HTTP's `If-Match` ([ADR 0027](0027-optimistic-concurrency-for-commands.md)), which ADR 0039 left to this phase.

## Options considered

**Arguments of a command with a body**
1. **Flat parameters**, one per BIAN field, with objects flattened (for example `residentialAddressTownName`). The schema is simplest, but Reference would take more than 60 parameters. Their names would no longer be BIAN's. The "left out, empty or set" rules and the refusal by BIAN path would have to be written again for MCP, outside the core inputs that HTTP shares.
2. **One parameter per top-level BIAN field, with BIAN objects as nested records.** This is ADR 0039's style at the top level, with BIAN names below it. It is still about 25 parameters with mixed naming, and the refusal of unknown top-level fields has no `[JsonExtensionData]` to hang on.
3. **One `request` object shaped like the BIAN request body**, next to the flat ids, `expectedVersion` and `idempotencyKey`. The JSON is the HTTP body, so the same mirror model, the same core input and the same refusals apply, and the parity tests compare like with like. Agents nest the body once, under a name that points to the BIAN schema.

**Expected version**
1. **Optional**, with the current version used when it is left out. This drops the lost-update protection that ADR 0027 requires of every command that changes a record.
2. **Required `long` parameter `expectedVersion`.** It is MCP's `If-Match`, taken from a Retrieve's or a previous command's `version`.

## Decision

**Option 3 for arguments, and a required `expectedVersion`.**

- **Shape.** A command tool whose operation has a BIAN request body takes it as one required `request` argument. `request` has the body's BIAN property names and mirrors the `.Api` request record in its own `.Mcp` folder, with the same nested models and `[JsonExtensionData]`.
- **Flat parameters.** The ids, `expectedVersion` and `idempotencyKey` stay flat camelCase parameters (ADR 0039). An id is named after its BIAN path parameter: `partyReferenceDataDirectoryEntryId`, `demographicsId`, `referenceId`, `associationsId` and `bankRelationsId`.
- **Register.** The Control Record's Register keeps its flat `partyType`, since its body has a single field (ADR 0039).
- **Translation.** The mirror translates the JSON into the existing core inputs (`DemographicsChanges`, `PartyReferenceInput`, `PartyAssociationInput`, `BankRelationsInput`), so every rule and error stays in `RimBank.Core`.
- **The partial-update rules carry over unchanged:**
  - a field left out keeps its value;
  - an empty value clears it;
  - a given address or document replaces the whole of it;
  - an unrecorded BIAN part is refused by its BIAN path, with the HTTP code;
  - a property that is not BIAN's is ignored.
- **`expectedVersion`.** Every Update and Exchange tool requires it, a `long`, and passes it to the command as HTTP passes `If-Match`.
  - A stale version is the same `party.version-conflict` (409) as on HTTP.
  - A missing one is `shared.tool-arguments-invalid` (400), because the SDK refuses a missing required argument before the tool runs (ADR 0022). HTTP answers `shared.if-match-required` (428) instead. A missing value is the one case where MCP and HTTP differ.
  - Register tools take no `expectedVersion`: they create a record.
- **Version per aggregate.** The `version` of a result is the version of the stream the operation wrote:
  - the entry's for the Control Record, Demographics, Reference and BankRelations;
  - the association's own stream for the three Associations tools, because an association is its own aggregate ([ADR 0033](0033-party-associations.md)).

  It is the same value HTTP sends as `ETag`.
- **Schema.** With SDK 2.3.1, the generated input schema of `request` is a nested object with the BIAN property names. A `request` that is missing or not an object is `shared.tool-arguments-invalid`. The tests pin both.
- **Descriptions.** Each tool's description is its endpoint's, written for `request` and `expectedVersion` instead of a body and `If-Match`.
- **Guard.** The tool-list guard of ADR 0039 no longer has an allow-list. The tools are exactly the 13 implemented operations: 8 commands and 5 queries.

## Consequences

- Tech-stack §6 (Naming) and CONTRIBUTING "MCP" describe `request` and `expectedVersion`. ADR 0039's 1.17 follow-up points here.
- Agents nest a command's body once, under `request`, and can use the BIAN schema of the operation to fill it.
- Each new body-carrying tool adds a mirror of its `.Api` request, response and nested models. This doubles the model code, as ADR 0039 already accepted. The per-operation parity tests catch drift. In particular, they cover a full Reference with every recorded field, the refused parts, and the validation, not-found, stale-version and wrong-type-reference errors.
- Money-moving tools (4.14) follow the same shape. Their `destructive` and scope rules are added then.
- Proven by `tests/RimBank.Mcp.Tests/Party/PartyReferenceDataDirectory/`:
  - `PartyToolCatalogTests`:
    - the tool list equals the implemented operations;
    - each tool has its policy and the annotations of its kind;
    - the required arguments of each kind;
    - the nested `request` schema;
    - a read-scope caller sees only the 5 Retrieve tools and gets `shared.forbidden` on a command;
  - `VersionRoundTripTests`: Retrieve, then Update, then a stale version is 409, a replay gives the same answer, and a missing `expectedVersion` is refused;
  - the tool and parity tests of each BQ: `Update/`, `Demographics/`, `BankRelations/`, `Associations/` and `Reference/`.
