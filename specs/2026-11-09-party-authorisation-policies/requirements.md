# Authorisation policies

Roadmap phase: 1.15
Branch: plan/1.15-party-authorisation-policies

## Context
The quality bar requires access to be "authenticated by default, authorised per BIAN operation" ([mission.md](../mission.md), quality bar "Security"; principle 6: an AI agent gets the same authorisation as any other consumer). Tech-stack §10 says "Authorisation policies are defined per BIAN operation", and §6 says MCP tool calls go through the same per-operation policies as HTTP. Today every Party endpoint only needs an authenticated caller, through the fallback policy of [ADR 0019](../../docs/adr/0019-authentication-baseline.md). The 403 `shared.forbidden` Problem Details response exists, but nothing triggers it yet.

ADR 0019 and [ADR 0022](../../docs/adr/0022-mcp-authorization-and-tool-errors.md) both leave this phase as a follow-up: per-operation policies, separate command and query scopes, and `scopes_supported` in the MCP protected-resource metadata. The BIAN spec ([PartyReferenceDataDirectory.yaml](../../doc-0/release14.0.0/apis-iso20022_ext-ddd/oas3/yamls/PartyReferenceDataDirectory.yaml)) declares a `403` on every operation but defines no security scheme or scopes, so the scope names are RimBank's own decision.

Depends on 0.13 (authentication, `TestTokens`), 0.16 (MCP authorization), and 1.5–1.12 (the 13 Party endpoints). All are merged. It unblocks 1.16/1.17: their MCP tools take the same named policies through `[Authorize(Policy = …)]`. It also sets the pattern that 4.14 reuses for the money-moving scope.

**CQRS split** (tech-stack §8): the policy follows the operation's role. **Commands** (Register, Update, Exchange, and the Register/Update of each BQ: 8 operations) require the context's **write** scope. **Queries** (the 5 `Retrieve` operations, `GET`) require its **read** scope. Commands, queries, handlers and read models do not change. Authorisation runs in front of the adapter, so no read model is added or changed and there is no projection work.

## Scope
### In scope
- **Two scopes for the context:** `party-reference-data-directory:write` (commands) and `party-reference-data-directory:read` (queries).
- **One named policy per BIAN operation.** The policy name is the operation id, for example `PartyReferenceDataDirectoryEntry/Register`. Each policy requires one of the two scopes. Because every operation has its own policy, a later phase can give one operation its own scope without touching any endpoint.
- **Scope check:** an `OperationScopeRequirement` and its handler in `RimBank.Host/Security/`. The handler reads the space-separated OAuth `scope` claim (RFC 8693/9068, as Keycloak issues it). It never matches substrings.
- **Module convention:** `IServiceDomainModule` gets `AddAuthorizationPolicies(AuthorizationBuilder)`. `PartyReferenceDataDirectoryModule` declares its 13 operations, each as a command or a query, through a host helper (`policies.AddCommand(operationId, writeScope)` / `AddQuery(operationId, readScope)`).
- **Endpoint helper:** `RequireOperation(operationId)` in the root conventions of `RimBank.Api` (namespace `RimBank.Api`, next to `RequireIdempotencyKey`). It does `.WithName(operationId)` and `.RequireAuthorization(operationId)`. All 13 Party endpoints use it in place of `.WithName(OperationId)`, and each declares `.ProducesProblem(403)`.
- **403 behaviour:** a token without the required scope gets 403 `shared.forbidden` Problem Details, from the existing status-code pages. On a command this happens before the `Idempotency-Key` and `If-Match` checks, and on a query before the 404.
- **MCP metadata:** `scopes_supported` in the protected-resource metadata lists the scopes of every module's policies.
- **Dev realm:** both scopes are added as client scopes in `rimbank-realm.json` (`include.in.token.scope: true`). They are default scopes of `rimbank-dev-client` and `rimbank-mcp-client`, so local flows keep working.
- **Test tokens:** `TestTokens.Issue(scopes: …)`. By default it issues every scope the host knows, so existing tests stay green. Denial tests pass explicit scopes.
- **Docs:** ADR 0038; tech-stack §6 and §10 (scope naming and the per-operation policy convention); CONTRIBUTING "Authentication" (a new BIAN endpoint calls `RequireOperation` and its module registers the policy); ADR 0014, 0019 and 0022 consequences amended.

### Out of scope
- MCP tools and their `[Authorize]` policies, through the SDK's `AddAuthorizationFilters`: added with the tools in **1.16/1.17**.
- One scope per operation or per BQ: possible later without endpoint changes, but not needed now.
- Resource-level authorisation (who may read *which* party): not in Stage 1, and later with PII protection (**7.2**) or KYC contexts.
- The money-moving scope and default-off tools (**4.14**).
- An ArchUnit rule for the convention: the runtime coverage guard is enough (user's choice).
- A manual Keycloak `.http` demo with a read-only token (not chosen; the CONTRIBUTING text explains how to request fewer scopes).
- Production IdP scope provisioning (**7.x**).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Scope granularity | Per context, read and write: `party-reference-data-directory:read` / `:write` | User's choice. It matches the command/query split of §8 and keeps contexts isolated. It also leaves room for a dedicated money-moving scope (4.14). |
| Policy unit | One named policy per BIAN operation, named by its operation id | Meets "authorised per BIAN operation" literally. A later scope split changes only the module's registration. MCP tools reuse the same names in 1.16. |
| Command/query kind | Declared once, in the module's registration (`AddCommand` / `AddQuery`) | One place per context. The coverage guard cross-checks it against the HTTP verb (`GET` = query, as the conformance harness does, ADR 0020). |
| Where scopes are checked | An ASP.NET Core authorization requirement in `RimBank.Host/Security` | ADR 0019: cores and adapters never read the token. A bus middleware was rejected because it would put token concepts next to the cores. |
| Scope claim format | The space-separated `scope` claim (`MapInboundClaims = false`), split on whitespace with exact matches | Keycloak and RFC 9068 issue scopes this way. `RequireClaim` would compare the whole string. |
| Endpoint wiring | `RequireOperation(operationId)` in the `RimBank.Api` root, replacing `.WithName` | One call names the endpoint and protects it, so a name without its policy is hard to write by accident. `RimBank.Http` no longer exists (ADR 0025). |
| Unknown policy name | ASP.NET Core fails the request when a policy is missing, and the coverage guard catches it in CI | A mistake can never silently fall back to "authenticated only". |
| Dev realm scopes | Default client scopes on both dev clients | `aspire run`, MCP Inspector and Claude Code keep working with no change. Tests prove the denial paths with signed tokens. |

## ADRs to write
- **0038 Per-operation authorisation policies.** Covers the scope naming (`<service-domain-kebab>:read|write`), one policy per BIAN operation named by its id, the module registration convention (amends ADR 0014), the scope requirement and claim format, the order of 403 before the idempotency, concurrency and 404 checks, `scopes_supported` (amends ADR 0022), the dev realm client scopes (amends ADR 0019), and the follow-ups (1.16 tool `[Authorize]`, 4.14 money-moving scope, resource-level authorisation later).

## Open questions
- Should the policy registration helper also expose the scope per operation to the OpenAPI document (a `security` requirement)? The proposed default is no for now; the BIAN document has none. Confirm during implementation whether Scalar needs it to send tokens.
