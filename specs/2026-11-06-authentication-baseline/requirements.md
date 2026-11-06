# Authentication baseline

Roadmap phase: 0.13
Branch: plan/0.13-authentication-baseline

## Context
The quality bar says "authenticated by default" ([mission.md](../mission.md), quality bar "Security"; principle 9). Tech-stack §1 fixes OpenID Connect / JWT bearer with Keycloak run by Aspire for local development, and §10 says every endpoint requires authentication by default. BIAN declares 401 and 403 ([tech-stack.md §5](../tech-stack.md#5-api--error-contract)), and both must be RFC 9457 Problem Details (principle 8).

This phase depends on 0.8 (AppHost, `RimBank.Host`, health probes already `AllowAnonymous`, ADR 0014), 0.9 (Problem Details baseline, ADR 0015), 0.11 (Wolverine bus, ADR 0017) and 0.12 (idempotency, ADR 0018). All are merged. ADR 0018 deferred "scoping keys to the authenticated caller (0.13/1.15)", and this phase closes that follow-up.

It unblocks 0.16 (MCP authorization through Keycloak), 1.5 (the first protected `POST`), 1.15 (per-operation policies, which need an authenticated principal) and the "actor" part of the audit quality bar (every state change traceable to an actor).

As in 0.10–0.12 there is no real context yet, so the behaviour is proven on the **test-only fake modules** in `RimBank.Host.Tests` and the fake contexts in `RimBank.Postgresql.Tests` (non-goal "no speculative coverage").

**CQRS split:**
- **Command side:** the idempotency gate scopes keys by caller as `(context, command type, caller, key)`. Authentication itself sits in front of both sides.
- **Query side:** no change. Queries are authenticated like everything else but carry no key.
- **Read models:** none. The idempotency outcome stays a write-side record that is never projected.

## Scope
### In scope
- **AppHost:** a Keycloak resource (`Aspire.Hosting.Keycloak`) with a committed **dev-only** realm import `src/RimBank.AppHost/Realms/rimbank-realm.json`. It contains realm `rimbank`, audience/client `rimbank-api`, and a confidential test client `rimbank-dev-client` with client credentials. `RimBank.Host` references and waits for it. The admin password is an Aspire parameter (user secrets). The client secret is not a real secret: it is dev-only and documented as such.
- **Host (`RimBank.Host/Security/`):** `AddRimBankAuthentication` registers JWT bearer through `AddKeycloakJwtBearer` (`Aspire.Keycloak.Authentication`), with audience `rimbank-api`, `MapInboundClaims = false` and `NameClaimType = "sub"`. A `FallbackPolicy` requires an authenticated user (**deny by default**). `UseAuthentication`/`UseAuthorization` run in `HostComposition`. Only `/health` and `/alive` stay anonymous.
- **Problem Details for 401/403:** `SharedProblems.Unauthenticated` (`shared.unauthenticated`, `ErrorKind.Unauthorized`) and `SharedProblems.Forbidden` (`shared.forbidden`, `ErrorKind.Forbidden`) in `RimBank.Problems`. The status-code pages of the Problem Details baseline write them for the bodiless 401/403 of authentication, as `application/problem+json` with `errorCode` and `traceId`. The `WWW-Authenticate: Bearer` header is kept.
- **Actor (SharedKernel):** `Actor`, a `readonly record struct` with `Subject` (opaque OIDC `sub`, 1–255 visible ASCII characters) and an optional `ClientId` (`azp`), plus `TryCreate` returning `Result` and `Actor.System` for messages with no human or client caller (later `.Subscriptions`, scheduled messages). The host builds it from the `ClaimsPrincipal`. In this phase it is used only for idempotency scoping. Recording it in event metadata comes with the first events (1.x).
- **Caller-scoped idempotency:** the host relays the actor's subject onto the Wolverine message context (user name) for every authenticated request. `IdempotencyGate` scopes the outcome id (`<command type>:<caller>:<key>`) and the advisory-lock hash by caller, falling back to `Actor.System` when no user name is present. Two callers that use the same key never replay or conflict with each other.
- **Anonymous-endpoint guard:**
  - an ArchUnitNET rule `allow-anonymous-only-in-service-defaults`: no type outside `RimBank.ServiceDefaults` uses `AllowAnonymousAttribute` or calls `AllowAnonymous()`, with a bad fixture;
  - a host test that enumerates every `RouteEndpoint` and asserts that only the health probes carry `IAllowAnonymous`.
- **Constitution and docs:** tech-stack §1/§3.3/§10 (Keycloak resource, realm folder, `Security/` in the host, link to ADR 0019); ADR 0018 consequences updated ("scoped to caller since 0.13"); a CONTRIBUTING "Authentication" section (how to get a dev token, and that new endpoints are protected unless they are health probes).
- **ADR 0019.**

### Out of scope
- Per-BIAN-operation authorisation policies and command/query scopes (1.15).
- MCP authorization (OAuth 2.1 protected-resource metadata) and MCP `isError` mapping (0.15/0.16).
- Recording the actor in event metadata or headers for audit (with the first events, 1.x; audit query in 7.3).
- Production identity provider configuration, token exchange and Keycloak deployment (`WithRealmImport` is dev-only; 7.x).
- An end-to-end Keycloak token test in `RimBank.AppHost.Tests` and an `.http` demo. The user chose host tests only as validation. The existing AppHost startup test must keep passing with Keycloak added.
- Rate limiting of 401s (7.1).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Identity provider wiring | `Aspire.Hosting.Keycloak` + `Aspire.Keycloak.Authentication` (`AddKeycloakJwtBearer`) | Tech-stack §1 already names Keycloak via Aspire. Service discovery resolves the authority, so no URL is hard-coded. |
| Deny by default | `AuthorizationOptions.FallbackPolicy = RequireAuthenticatedUser()` | Every endpoint a context maps is protected without opting in (§10). Forgetting `RequireAuthorization()` cannot open an endpoint. |
| Anonymous exceptions | Only `/health` and `/alive` (in `RimBank.ServiceDefaults`), enforced by an ArchUnit rule and a runtime endpoint test | Two independent checks make deny by default hard to break. |
| 401/403 body | `shared.unauthenticated` / `shared.forbidden` Problem Details from the JwtBearer events | One error contract (principle 8, ADR 0015). The status-code pages alone would give no stable `errorCode`. |
| Claims | `MapInboundClaims = false`, name = `sub` | Stable, opaque subject. No mapping to legacy SOAP claim URIs. |
| Test tokens | Host tests sign JWTs with an in-memory RSA key and override `JwtBearerOptions` (`TokenValidationParameters`, no metadata fetch) | Fast and deterministic, with no container in host tests (user's choice: test-first, local signing key). |
| Caller identity type | `Actor` (SharedKernel) with `Subject` and `ClientId` | Audit language ("who did what"), no ASP.NET in the kernel, reusable by MCP (0.16) and audit (1.x/7.3). |
| How the caller reaches the bus | The host relays `Actor.Subject` as the Wolverine message-context user name. The gate reads the envelope user name. | `RimBank.Http` must not reference Wolverine (§4), and the core must not see it. The user name also flows to cascaded messages and OpenTelemetry (`enduser.id`). |
| Idempotency scope | `(context, command type, caller, key)`, with `Actor.System` when there is no caller | Closes the ADR 0018 follow-up: clients cannot collide with or probe each other's keys. Internal messages keep a stable scope. |
| Existing stored outcomes | No migration | Nothing is deployed yet (pre-v0.1.0). The id format change is noted in ADR 0019. |

## ADRs to write
- **0019 Authentication baseline:** Keycloak via Aspire (dev-only realm import), JWT bearer settings, deny by default with the fallback policy, the anonymous allow-list and its two guards, 401/403 Problem Details codes, `Actor` and how it reaches the bus, caller-scoped idempotency (amends ADR 0018), and follow-ups (1.15 policies, 0.16 MCP, 1.x actor in event metadata, 7.x production IdP).

## Open questions
Answered during implementation (ADR 0019):
- **Relaying the caller from a plain Minimal API:** `IMessageContext.UserName` is settable on the request-scoped context, and `InvokeAsync` copies it onto the envelope, but only when `WolverineOptions.EnableRelayOfUserName` is on (`MessageBus.TrackEnvelopeCorrelation`). The host and `AddServiceDomainStore` both switch it on. `The_callers_subject_reaches_the_message_handler` proves it end to end.
- **Preview packages:** both Keycloak integrations exist only as previews (`13.6.4-preview.1.26523.11`). They are pinned as `AspireKeycloakVersion`, and the new build guard RB0004 keeps that version a preview of `AspireVersion`.
- **Additional finding:** outside Development, JWT bearer refuses the service-discovery authority (`https+http://keycloak`) on every request. The host now requires `Authentication:Authority` (HTTPS) outside Development and refuses to start without it. Tests that run as Production set it.
- **Additional finding:** joining caller and key with a colon is ambiguous (`u:x` + `k` = `u` + `x:k`), so the outcome id length-prefixes the caller. `Callers_whose_subject_and_key_join_to_the_same_text_do_not_clash` proves it.
- **Additional finding:** 401/403 bodies come from the existing status-code pages, not from JWT bearer events, so any future scheme gets them too.
