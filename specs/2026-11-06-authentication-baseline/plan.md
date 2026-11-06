# Plan: Authentication baseline

Test-first throughout: each group starts with failing tests, and the solution builds green at the end of each group.

## 1. Problem codes and Actor (SharedKernel, Problems)
- [x] Tests: `tests/RimBank.SharedKernel.Tests/Actors/ActorTests.cs` (namespace `RimBank.SharedKernel.Actors.Tests`): valid and invalid subjects (empty, over 255 characters, whitespace or non-ASCII), optional `ClientId`, `Actor.System`, value equality.
- [x] `src/RimBank.SharedKernel/Actor.cs`: `readonly record struct Actor(string Subject, string? ClientId)`, `TryCreate` returning `Result<Actor>`, `System`, and errors `shared.actor-invalid`.
- [x] Tests: `tests/RimBank.Problems.Tests` cover `SharedProblems.Unauthenticated` → 401 and `SharedProblems.Forbidden` → 403, with `type` URN, `errorCode` and `traceId`.
- [x] `src/RimBank.Problems/SharedProblems.cs`: add `Unauthenticated` (`shared.unauthenticated`) and `Forbidden` (`shared.forbidden`), and document both Problem Details types.

## 2. Host authentication and deny by default
- [x] `Directory.Packages.props`: `Aspire.Keycloak.Authentication` (Aspire group, `$(AspireVersion)`), plus the `Microsoft.AspNetCore.Authentication.JwtBearer` pin if needed. Restore and update the lock files.
- [x] Test helper `tests/RimBank.Host.Tests/Security/TestTokens.cs`: an in-memory RSA key, `Issue(subject, clientId, audience, expires)`, and `UseTestTokens(builder)`, which post-configures `JwtBearerOptions` (`IssuerSigningKey`, `ValidIssuer`, `ValidAudience`, `Configuration` set so there is no metadata fetch).
- [x] Tests: `tests/RimBank.Host.Tests/Security/AuthenticationTests.cs` (namespace `RimBank.Host.Security.Tests`) against a fake module endpoint:
  - no token, malformed token, expired token, wrong audience and wrong signature each give 401 `application/problem+json` with `errorCode = shared.unauthenticated`, a `traceId` and `WWW-Authenticate: Bearer`;
  - a valid token gives 200;
  - an authenticated caller hitting a test endpoint with an unmet policy gives 403 `shared.forbidden`;
  - `/health` and `/alive` give 200 without a token.
- [x] `src/RimBank.Host/Security/AuthenticationComposition.cs`: `AddRimBankAuthentication(builder)` (`AddKeycloakJwtBearer("keycloak", "rimbank", …)`, audience `rimbank-api`, `MapInboundClaims = false`, `NameClaimType = "sub"`, `RequireHttpsMetadata` false in Development only), `FallbackPolicy`, and the `OnChallenge`/`OnForbidden` handlers that write the Problem Details through `RimBank.Problems`.
- [x] `HostComposition.Build`: call `AddRimBankAuthentication`, and add `UseAuthentication()`/`UseAuthorization()` after the Problem Details middleware and before the endpoints.
- [x] Update existing host tests (`Errors`, `Messaging`, `Modules`, `Observability`) to send a test token where they call module endpoints. Their assertions stay the same.

## 3. Anonymous-endpoint guard
- [x] Bad fixture `tests/RimBank.Architecture.Fixtures/Bad/…/AnonymousEndpoint.cs`, which calls `.AllowAnonymous()` from a fake `.Api`.
- [x] Rule `allow-anonymous-only-in-service-defaults` in `ArchitectureRules.Composition.cs`: no type outside `RimBank.ServiceDefaults` depends on `AllowAnonymousAttribute` or calls `AuthorizationEndpointConventionBuilderExtensions.AllowAnonymous`. Add it to `FixtureRulesTests` (bites the bad fixture) and `SolutionRulesTests` (green on `src/`).
- [x] Host test `tests/RimBank.Host.Tests/Security/AnonymousEndpointTests.cs`: build the host with a fake module and enumerate `EndpointDataSource` `RouteEndpoint`s. Only `/health` and `/alive` may carry `IAllowAnonymous`.

## 4. Caller-scoped idempotency
- [x] Spike, then record the answer in the ADR: set the Wolverine user name on the request-scoped `IMessageContext` from a Minimal API (open question 1).
- [x] Tests in `tests/RimBank.Postgresql.Tests/Idempotency` (Testcontainers):
  - `Same_key_from_two_callers_is_handled_twice` (two events, no 409);
  - `Same_key_and_caller_replays` (the existing replay test runs with a user name);
  - `Same_key_from_another_caller_with_a_different_payload_is_not_reused`;
  - `Message_without_a_user_name_is_scoped_to_system`;
  - concurrency is per caller (a different caller is not `in-use`).
- [x] `src/RimBank.Postgresql/IdempotencyGate.cs`: read `Envelope.UserName` (or `Actor.System.Subject`), use it in the outcome id `<command type>:<caller>:<key>` and in the advisory-lock hash. `CommandIdempotencyPolicy` passes the envelope into the gate frame.
- [x] `src/RimBank.Host/Security/ActorRelay.cs`: middleware (or an endpoint filter on all groups) that builds the `Actor` from `HttpContext.User` (`sub`, `azp`) and sets it as the message-context user name. A host test sends a command from a fake endpoint with a token and asserts that the handler's envelope user name equals the token subject.

## 5. AppHost Keycloak resource
- [x] `Directory.Packages.props`: `Aspire.Hosting.Keycloak` (`$(AspireVersion)`).
- [x] `src/RimBank.AppHost/Realms/rimbank-realm.json`: realm `rimbank`, client `rimbank-api` (bearer-only audience), and `rimbank-dev-client` (confidential, service account, audience mapper to `rimbank-api`). Add a header note in `Realms/README.md` saying it is dev-only and its secret is not a real one.
- [x] `Program.cs`: `KeycloakResource = "keycloak"`, `AddKeycloak(KeycloakResource, adminPassword: param).WithDataVolume().WithRealmImport("./Realms")`, and the host `.WithReference(keycloak).WaitFor(keycloak)`.
- [x] Make sure the existing `RimBank.AppHost.Tests` (model and Docker startup) still pass. Extend the model test only as far as needed for the new resource to compile (no token test, see requirements).

## 6. ADR and docs
- [x] `docs/adr/0019-authentication-baseline.md` (from `template.md`), with the content listed in requirements.
- [x] ADR 0018: consequence "scoped to the caller since ADR 0019". Remove the deferred item.
- [x] `specs/tech-stack.md`: §3.3 (`RimBank.Host` `Security/`, `RimBank.AppHost/Realms/`), §10 (deny by default, the anonymous allow-list, the actor, link to ADR 0019), and §5 (401/403 codes).
- [x] `CONTRIBUTING.md`: an "Authentication" section (getting a dev token with client credentials against the Aspire Keycloak; the anonymous guard).
- [x] `specs/roadmap.md`: tick 0.13.
- [x] `dotnet format --verify-no-changes`, the full `dotnet test`, and the lock files committed.
