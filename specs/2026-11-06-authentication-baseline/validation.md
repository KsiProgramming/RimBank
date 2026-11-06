# Validation: Authentication baseline

## Done when (from roadmap)
> **0.13 Authentication baseline:** Keycloak in Aspire, JWT bearer, deny by default, and a test that an unauthenticated request gets a 401 Problem Details response.
> + Definition of done (roadmap.md)

## Automated checks
- [x] `RimBank.Host.Tests/Security/AuthenticationTests`:
  - no, malformed, expired, wrong-audience and wrong-signature tokens each give **401** with `Content-Type: application/problem+json`, `type` = the `shared.unauthenticated` URN, `errorCode = shared.unauthenticated`, a `traceId` and `WWW-Authenticate: Bearer`;
  - a valid token gives 200;
  - an unmet policy gives **403** `shared.forbidden` Problem Details;
  - `/health` and `/alive` give 200 anonymously.
- [x] `RimBank.Host.Tests/Security/AnonymousEndpointTests`: only the health probes carry `IAllowAnonymous`.
- [x] Host relay test: a command sent from an authenticated fake endpoint reaches the handler with envelope user name = token `sub`.
- [x] `RimBank.Postgresql.Tests/Idempotency`: the new caller-scoping tests (two callers with the same key are both handled, the same caller replays, a missing user name is scoped to `system`, in-use is per caller), plus every existing ADR 0018 test green.
- [x] `RimBank.SharedKernel.Tests/Actors`: `Actor` validation and `System`.
- [x] `RimBank.Problems.Tests`: the `Unauthenticated` → 401 and `Forbidden` → 403 mappings.
- [x] `RimBank.Architecture.Tests`: `allow-anonymous-only-in-service-defaults` bites its bad fixture and is green on `src/`. All existing rules are green (no new dependency from `RimBank.Http` or a core on Wolverine or JwtBearer).
- [x] Existing `RimBank.AppHost.Tests` (model and Docker startup with Keycloak added) still green.
- [x] `dotnet format --verify-no-changes` clean and lock files regenerated (both runtimes for the AppHost and architecture projects).
- [ ] GitHub Actions CI (build, test, format, CodeQL) green on the PR.

## Manual / demo checks
- [x] `dotnet run --project src/RimBank.AppHost`: the Aspire dashboard shows `keycloak` healthy, and `rimbank-host` starts after it. (Checked through the AppHost startup test; a one-off local run also got a real client-credentials token from the dev realm: 401 without it, 404 `shared.not-found` with it.)
- [x] `/health` gives 200 with no token (host tests and the AppHost startup test).
- [x] `/health` with a malformed `Authorization: Bearer x` header still gives 200 (`The_health_probes_stay_open_to_anonymous_callers`), because the probes are anonymous.
- [x] Calling a protected fake endpoint (in the host tests) without a token gives 401 `application/problem+json` with `"errorCode":"shared.unauthenticated"`. Until 1.5 there is no real protected route to `curl`.

## Merge criteria
- [ ] All checks above pass in CI (green locally: 745 tests in Release, format)
- [x] Roadmap checkbox 0.13 ticked in the same PR
- [x] ADR 0019 written. ADR 0018 is amended for caller scoping, and both open questions are answered in ADR 0019.
- [x] No real secrets in source: the Keycloak admin password is an Aspire parameter in user secrets, and the realm's client secret is marked dev-only.
- [x] Tokens, `Authorization` headers and the subject are not logged at Information level or above (PII/secret hygiene, §10).
- [x] tech-stack §3.3/§5/§10 and CONTRIBUTING updated
