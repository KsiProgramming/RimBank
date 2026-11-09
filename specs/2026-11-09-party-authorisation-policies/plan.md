# Plan: Authorisation policies

Order: ADR first, then the host policy machinery with its tests, then the module convention and Party registration, then the endpoints, then the dev realm and docs. The solution builds and all tests pass after each group. Until group 4, `TestTokens` issues every scope, so the existing tests are unaffected.

## 1. ADR and constitution
- [ ] `docs/adr/0038-per-operation-authorisation-policies.md`, as decided in requirements.md.
- [ ] Amend the Consequences of ADR 0014 (module gains `AddAuthorizationPolicies`), ADR 0019 (1.15 follow-up done, realm scopes) and ADR 0022 (`scopes_supported`; tool `[Authorize]` in 1.16).
- [ ] tech-stack §10: scope naming and "one named policy per BIAN operation, registered by the module". §6: MCP tools use the same policy names.

## 2. Scope requirement and policy registration (host)
- [ ] `src/RimBank.Host/Security/OperationScopeRequirement.cs` with `OperationScopeHandler`: it splits the `scope` claim on whitespace and requires an exact match.
- [ ] `src/RimBank.Host/Security/OperationPolicies.cs`: `AuthorizationBuilder` extensions `AddCommand(operationId, scope)` and `AddQuery(operationId, scope)`. Each adds a policy named by the operation id that requires an authenticated user and the scope. It also records the operation's kind and scope in an `OperationPolicyCatalog` singleton, used by the MCP metadata and the coverage guard.
- [ ] Register the handler in `AddRimBankAuthentication`.
- [ ] Unit/host tests in `tests/RimBank.Host.Tests/Security/OperationPolicyTests.cs`, with a fake module and fake endpoints:
  - the scope is present → allowed;
  - the scope is missing, or `scope` holds only a prefix or superstring (`…:reader`) → 403 `shared.forbidden`, `application/problem+json`;
  - no token → 401;
  - the same operation id registered twice → the host refuses to start.

## 3. Module convention
- [ ] `IServiceDomainModule.AddAuthorizationPolicies(AuthorizationBuilder policies)`, called for every module from `ServiceDomainModuleComposition`, in list order. Update the fake modules in `RimBank.Host.Tests`.
- [ ] `PartyReferenceDataDirectoryModule`: the constants `ReadScope` / `WriteScope`. It registers 8 commands with `AddCommand(…OperationId, WriteScope)` and 5 queries with `AddQuery(…OperationId, ReadScope)`, using each endpoint's `OperationId` constant.
- [ ] `McpComposition` / `AuthenticationComposition`: `ProtectedResourceMetadata.ScopesSupported` = the distinct scopes in the catalog. Extend the existing `McpAuthorizationTests` metadata assertion.

## 4. Endpoints
- [ ] `src/RimBank.Api/OperationAuthorization.cs`: `RequireOperation(this RouteHandlerBuilder, string operationId)` → `.WithName(operationId).RequireAuthorization(operationId)`.
- [ ] Replace `.WithName(OperationId)` with `.RequireOperation(OperationId)` and add `.ProducesProblem(StatusCodes.Status403Forbidden)` in all 13 endpoints under `src/RimBank.Api/Party/PartyReferenceDataDirectory/`.
- [ ] Check that the OpenAPI document test and the BIAN conformance tests still pass (403 is declared by BIAN, so it needs no deviation).

## 5. Test tokens and HTTP authorisation tests
- [ ] `tests/RimBank.Host.Testing/TestTokens.cs`: an `IEnumerable<string>? scopes` parameter on `Issue`. By default it issues every catalog scope, and `[]` issues none. Add the `scope` claim as one space-separated string.
- [ ] `tests/RimBank.Api.Tests/Party/PartyReferenceDataDirectory/AuthorisationTests.cs` (Alba on `PartyApiHost`), parameterised over the 13 operations:
  - a read-only token on a command → 403 `shared.forbidden`, also without `Idempotency-Key` or `If-Match` (403 comes before 400/428);
  - a write-only token on a query → 403, also for an unknown id (403 comes before 404);
  - a read token on a query → 200 or 404 as usual;
  - a write token on a command → the usual success;
  - no token → 401.
  - The test also checks that nothing was appended to the stream after a 403 command.
- [ ] `tests/RimBank.Api.Tests/OperationAuthorizationCoverageTests.cs` (the coverage guard): enumerate every `RouteEndpoint` with an `EndpointNameMetadata`, except the health probes and the dev-only OpenAPI/Scalar endpoints, and check:
  - it carries exactly one `AuthorizeAttribute` policy equal to its name;
  - the catalog has that operation;
  - `GET` ⇔ query ⇔ read scope, and every other verb ⇔ command ⇔ write scope;
  - every catalog operation is mapped by some endpoint (no orphan policies).

## 6. Dev realm and contributor docs
- [ ] `src/RimBank.AppHost/Realms/rimbank-realm.json`: client scopes `party-reference-data-directory:read` and `:write` (`openid-connect`, `include.in.token.scope: true`), added to the `defaultClientScopes` of `rimbank-dev-client` and `rimbank-mcp-client`. The AppHost startup test stays green.
- [ ] CONTRIBUTING "Authentication": the scopes, how a new BIAN endpoint is protected (`RequireOperation` and the module's `AddCommand`/`AddQuery`), and how to request a token with fewer scopes.
- [ ] Tick roadmap 1.15.
- [ ] `dotnet format --verify-no-changes` and the full test run.
