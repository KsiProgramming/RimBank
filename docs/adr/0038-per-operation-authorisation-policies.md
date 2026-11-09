# 0038. Per-operation authorisation policies

- Status: Accepted
- Date: 2026-11-09
- Roadmap phase: 1.15

## Context

The quality bar says "authorised per BIAN operation" ([mission](../../specs/mission.md), quality bar "Security"). Principle 6 says an AI agent gets the same authorisation as any other consumer. [Tech-stack §10](../../specs/tech-stack.md#10-security--supply-chain) defines policies per BIAN operation, and §6 has MCP tool calls go through the same policies.

Until 1.15, every Party endpoint only needed an authenticated caller, through the fallback policy of [ADR 0019](0019-authentication-baseline.md). The 403 `shared.forbidden` Problem Details existed, but nothing produced it. ADR 0019 left per-operation policies and separate command and query scopes to this phase. [ADR 0022](0022-mcp-authorization-and-tool-errors.md) left `scopes_supported` in the MCP protected-resource metadata to it as well.

The [BIAN OpenAPI](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml) declares a 403 on every operation, but no security scheme and no scopes. The scope names are therefore RimBank's own.

The Party Reference Data Directory has 13 operations: 8 commands (Register, Update, Demographics Update and Exchange, Reference Update, Associations Register and Update, BankRelations Update) and 5 `Retrieve` queries.

## Options considered

**Scope granularity**
1. **Per context, read and write.** Two scopes follow the command/query split of [ADR 0003](0003-separate-commands-and-queries.md), and a context's scopes are its own.
2. One scope per operation: 13 scopes today, and more with every Behavior Qualifier.
3. Read and write per Behavior Qualifier: 10 scopes, separating the PII-heavy Reference from the rest. No consumer needs that yet.
4. Global `rimbank:read` / `rimbank:write`. This does not isolate contexts and gives no pattern for the money-moving scope of 4.14.

**Where the check runs**
1. **An ASP.NET Core authorization requirement in the host, on a policy named by the operation.** HTTP endpoints and, from 1.16, MCP tools (the SDK's `[Authorize]` filters) refer to the same policy.
2. Policies on two route groups, one for commands and one for queries. Less code, but there is no per-operation policy to split later.
3. Wolverine middleware on `ICommand` / `IQuery`. It would cover both channels, but it puts token concepts next to the cores, which ADR 0019 rules out.

## Decision

**Scopes (option 1).** Each context has `<service-domain-kebab>:read`, needed by its queries, and `<service-domain-kebab>:write`, needed by its commands. For this context they are `party-reference-data-directory:read` and `party-reference-data-directory:write`, the constants `ReadScope` and `WriteScope` of `PartyReferenceDataDirectoryModule`.

**One policy per BIAN operation, named by its operation id.** Examples are `PartyReferenceDataDirectoryEntry/Register` and `Associations/Retrieve`, the same name as the endpoint (`WithName`). Each policy requires an authenticated user and one scope. A later phase can give a single operation its own scope by changing only its registration.

**Registration by the module.** `IServiceDomainModule` gains a fourth member, `AddOperationPolicies(OperationPolicies)`. This amends [ADR 0014](0014-host-composition-and-module-entry-point.md).
- The host calls it for every module, in list order, after every `AddServices` and before `AddMcpTools`.
- A module declares each operation once, as a command (`AddCommand(operationId, WriteScope)`) or a query (`AddQuery(operationId, ReadScope)`).
- The registrations make up an `OperationPolicyCatalog`, a singleton in the host.
- An operation registered twice, or a scope with whitespace, stops the host from starting.
- Policy names share the host-wide namespace of endpoint names. If two contexts ever have the same operation id, both the endpoint names and the policies clash at startup, and the newer context qualifies its ids.

**Endpoints.** `RequireOperation(operationId)` lives in the root conventions of `RimBank.Api`, next to `RequireIdempotencyKey`. It names the endpoint, requires the policy of that name and declares the 403 response, and every BIAN endpoint calls it in place of `WithName`. A policy name with no registration makes ASP.NET Core fail the request, so a mistake can never fall back to "authenticated only".

**The check.** The `OperationScopeRequirement` is met by `OperationScopeAuthorization`. It reads the `scope` claim (RFC 9068, as Keycloak issues it, with `MapInboundClaims = false`) as a space-separated list and needs an exact, case-sensitive match. A prefix or a longer scope does not count. The type's name does not end in `Handler`, because Wolverine would take such a type in the host assembly for a message handler.

**Order of the answers.**
- No token or an invalid token: 401, as before.
- An authenticated token without the scope: 403 `shared.forbidden`, from the existing status-code pages ([ADR 0015](0015-problem-details-mapping.md)). Authorization runs before endpoint filters and binding, so a denied command is refused before its `Idempotency-Key` 400 and its `If-Match` 428, and before any outcome or event is stored. A denied query is refused before its 404, so a caller without the read scope cannot probe which records exist.

**MCP.** The protected-resource metadata lists the catalog's distinct scopes in `scopes_supported` (amends ADR 0022), so an MCP client knows which scopes to request. The tools of 1.16 use the same policy names through the SDK's authorization filters.

**Dev realm** (amends ADR 0019).
- `rimbank-realm.json` declares the two scopes as client scopes, with `include.in.token.scope` on.
- They are default client scopes of `rimbank-dev-client` and `rimbank-mcp-client`, so local flows get both.
- A realm import that lists its own client scopes gets none of Keycloak's built-in ones. Checked on Keycloak 26.6, a user's token then loses `sub` (so every call is `shared.actor-invalid`), and a client asking for `openid profile email` gets `invalid_scope`. So the realm also declares three of them:
  - `basic`, holding only the `sub` mapper, as a default scope of both clients;
  - `profile` and `email`, with no mappers, as optional scopes of the MCP client. Clients may request them, but RimBank tokens carry no name, email or other personal data.
- The other built-in scopes (`roles`, `web-origins`, `acr`, `address`, `phone`) are not declared, because RimBank reads none of their claims. `offline_access` exists, but Keycloak refuses it (`not_allowed`) for the dev user. MCP clients sign in again when the token expires.
- A Keycloak data volume created before 1.15 keeps the old realm. Remove the volume `rimbank-keycloak-data` to import the new one (CONTRIBUTING "Authentication").

## Consequences

- A new BIAN endpoint calls `RequireOperation(OperationId)`, and its context's module registers the operation as a command or a query. CONTRIBUTING "Authentication" says so, and `OperationAuthorizationCoverageTests` fails when one of the two is missing, when the kind does not match the HTTP verb (`GET` is a query on the read scope, every other verb a command on the write scope), or when a policy protects no endpoint.
- Test tokens grant every catalog scope by default (`TestTokens.Issue(scopes: …)`), so tests that are not about authorisation keep working. Denial tests pass explicit scopes.
- Deployments must grant callers the scopes they need. A token without any RimBank scope can no longer call a BIAN operation.
- Follow-ups:
  - **1.16 / 1.17:** each MCP tool carries its operation's policy, and a test checks that the tool and the endpoint share it. *Done in [ADR 0039](0039-party-mcp-tools.md) with `[ToolOperation(OperationId)]` instead of `[Authorize]`, so that a refused call is the `shared.forbidden` Problem Details.*
  - **4.14:** the money-moving scope, a third scope next to read and write for the Deposit and Payment operations.
  - **7.2 / later:** resource-level authorisation (which party a caller may see) is not part of this decision.
  - **7.x:** scope provisioning in the production identity provider.
- Proven by:
  - `tests/RimBank.Host.Tests/Security/OperationPolicyTests`: exact match, prefix, longer and other-case scopes, 401 before 403, the catalog, and duplicate registration;
  - `tests/RimBank.Host.Tests/Mcp/McpAuthorizationTests`: `scopes_supported`;
  - `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/AuthorisationTests`: the 13 operations with the other kind's scope, with no scope and with no token, nothing stored after a denied command, and the write scope alone registering while the read scope alone retrieves;
  - `tests/RimBank.Api.Tests/OperationAuthorizationCoverageTests`;
  - `tests/RimBank.AppHost.Tests/Model/DevRealmTests`: the dev clients' scopes.
