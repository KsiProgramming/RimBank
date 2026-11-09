# Validation: Authorisation policies

## Done when (from roadmap)
> **1.15 Authorisation policies:** per-operation policies (separate scopes for command and query operations), with tests.
> + Definition of done ([roadmap.md](../roadmap.md))

Concretely: each of the 13 Party BIAN operations has its own named policy. The 8 commands require `party-reference-data-directory:write` and the 5 `Retrieve` queries require `party-reference-data-directory:read`. A token without the right scope gets a 403 `shared.forbidden` Problem Details response.

## Automated checks
- [ ] `RimBank.Host.Tests` `Security/OperationPolicyTests`:
  - an exact scope match is allowed;
  - a missing, prefix or superstring scope gets 403 `application/problem+json` with `errorCode: shared.forbidden` and a `traceId`;
  - no token gets 401 `shared.unauthenticated`;
  - an operation registered twice stops the host.
- [ ] `RimBank.Api.Tests` `Party/PartyReferenceDataDirectory/AuthorisationTests` (Alba), for each of the 13 operations:
  - a wrong-kind scope gets 403 Problem Details;
  - a right-kind scope gets the usual result;
  - no token gets 401.
  - 403 comes before the `Idempotency-Key` 400, the `If-Match` 428 and the `Retrieve` 404.
  - A denied command appends no event.
- [ ] `RimBank.Api.Tests` `OperationAuthorizationCoverageTests`:
  - every named BIAN endpoint carries the policy named by its operation id;
  - the catalog kind matches the HTTP verb (`GET` = query = read scope, others = command = write scope);
  - there are no orphan policies.
- [ ] `RimBank.Host.Tests` `Mcp/McpAuthorizationTests`: `scopes_supported` lists both scopes.
- [ ] Existing suites stay green with default test tokens (all scopes): Api, Host, Mcp, Conformance (OpenAPI/AsyncAPI), Architecture, AppHost startup.
- [ ] `dotnet format --verify-no-changes` is clean, and the CI workflow is green (build, test, format, CodeQL).

## Manual / demo checks
- [ ] Review the 13 endpoint diffs: each has `.RequireOperation(OperationId)` and `.ProducesProblem(403)`.
- [ ] Review `rimbank-realm.json`: both client scopes exist and are default scopes of `rimbank-dev-client` and `rimbank-mcp-client`. No real secret is added.
- [ ] `aspire run` still starts. A dev-client token (client credentials) still calls `POST /PartyReferenceDataDirectory/Register` and `GET …/{id}/Retrieve` successfully. A spot check, not a scripted demo.

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox 1.15 ticked in the same PR
- [ ] ADR 0038 written. ADR 0014, 0019 and 0022 consequences amended. tech-stack §6 and §10 updated.
- [ ] CONTRIBUTING "Authentication" explains the scopes and how a new endpoint is protected
- [ ] No tokens, scopes-with-subjects or PII written to logs by the new handler
- [ ] PR opened with the `open-phase-pr` skill (labels, milestone, board, `Closes #<issue>`)
