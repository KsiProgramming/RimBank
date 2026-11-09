# 0019. Authentication baseline

- Status: Accepted
- Date: 2026-11-06
- Roadmap phase: 0.13

## Context

The mission's quality bar says RimBank is "authenticated by default" and that every state change is traceable to an actor. [tech-stack §1](../../specs/tech-stack.md#1-platform) names OpenID Connect / JWT bearer, with Keycloak run by Aspire for local development. [§10](../../specs/tech-stack.md#10-security--supply-chain) says every endpoint requires authentication by default, and [§5](../../specs/tech-stack.md#5-api--error-contract) says the BIAN statuses 401 and 403 are RFC 9457 Problem Details like every other error ([ADR 0015](0015-problem-details-mapping.md)). Roadmap 0.13 asks for Keycloak in Aspire, JWT bearer, deny by default, and a 401 Problem Details for an unauthenticated request.

[ADR 0018](0018-idempotency-keys-for-commands.md) left one follow-up for this phase: idempotency keys are scoped per context and command type, but not yet per caller, so two clients that pick the same key could replay or block each other's commands.

There is no context yet. As in 0.10–0.12, the baseline is proven with test-only modules and fake contexts.

## Options considered

How endpoints are protected:

1. **Opt in per endpoint** (`RequireAuthorization()` on each `.Api` group). Explicit, but forgetting it opens an endpoint silently.
2. **Deny by default** through the authorization fallback policy, with an explicit list of anonymous endpoints. A new endpoint is protected without doing anything. Per-operation policies (1.15) then narrow access further.

How the 401 and 403 bodies are written:

1. **JWT bearer `OnChallenge` / `OnForbidden` events** that write Problem Details. This is specific to one scheme, and it must repeat the `WWW-Authenticate` header logic.
2. **The status-code pages of the Problem Details baseline** (ADR 0015), which already turn a bodiless 404 into `shared.not-found`. The bearer handler keeps writing its standard status and `WWW-Authenticate` header, and the host adds the body. This works for any future scheme too.

How the caller reaches the idempotency policy, which runs on the message bus, not in HTTP:

1. **Put the caller on every command.** It makes the core aware of identity, and every command and every `.Subscriptions` translation would have to carry it.
2. **Read `HttpContext` from the bus.** Wolverine chains also run for `.Mcp` and `.Subscriptions`, where there is no HTTP request.
3. **Relay the caller's subject as the Wolverine user name.** The host sets `IMessageContext.UserName` for the request. With `EnableRelayOfUserName`, Wolverine copies it onto the envelope of every message the request sends (including `InvokeAsync`), onto Marten's last-modified-by, and onto the trace (`enduser.id`). The policy reads `Envelope.UserName`. `.Mcp` (0.16) can set it the same way.

## Decision

- **Keycloak via Aspire, for local development only.** The AppHost adds `AddKeycloak("keycloak")` with a data volume and imports `src/RimBank.AppHost/Realms/rimbank-realm.json`: realm `rimbank`, audience client `rimbank-api`, and the confidential client `rimbank-dev-client` for the client-credentials grant. Its secret is published in the file and marked as dev-only. The admin password is an Aspire-generated parameter in user secrets. The host references Keycloak and waits for it.
- **The Keycloak integrations are previews.** `Aspire.Hosting.Keycloak` and `Aspire.Keycloak.Authentication` only exist as previews of each Aspire release (`13.6.4-preview.1.26523.11` for Aspire 13.6.4). We accept them, pinned centrally as `AspireKeycloakVersion`. The new build guard **RB0004** fails the build unless that version is a preview of `AspireVersion`, so the two are raised together.
- **JWT bearer** (`RimBank.Host/Security/AuthenticationComposition`) through `AddKeycloakJwtBearer("keycloak", "rimbank")`. The audience must be `rimbank-api`, `MapInboundClaims = false`, and the name claim is `sub`.
  - **In Development**, the authority is found through Aspire service discovery, and HTTP metadata is allowed for the local Keycloak.
  - **In every other environment**, `Authentication:Authority` must be an HTTPS realm address. Otherwise the host **refuses to start** rather than fetch signing keys over an unprotected connection.
- **Deny by default.** The fallback policy requires an authenticated user, so it covers every endpoint and every request that matches no endpoint. An unknown route without a token is therefore a 401, not a 404, and does not reveal which routes exist. The only anonymous endpoints are `/health` and `/alive` in `RimBank.ServiceDefaults`.
- **Two guards keep it that way:**
  - the architecture rule `allow-anonymous-only-in-service-defaults`. No type outside `RimBank.ServiceDefaults` may call `AllowAnonymous()` or use `[AllowAnonymous]` / `IAllowAnonymous`. The rule matches the method, because `RequireAuthorization()` lives on the same class and stays allowed;
  - a host test (`AnonymousEndpointTests`) that lists the running host's endpoints and only finds the health probes open.
- **401 and 403 are Problem Details** written by the status-code pages:
  - `shared.unauthenticated` (`ErrorKind.Unauthorized`, 401), with the bearer handler's `WWW-Authenticate: Bearer` header kept;
  - `shared.forbidden` (`ErrorKind.Forbidden`, 403).
- **`Actor`** (SharedKernel): the caller's `Subject` (`sub`) and optional `ClientId` (`azp`). Each is 1–255 visible ASCII characters, created with `TryCreate` returning `Result`, and a failure is `shared.actor-invalid` (401). `Actor.System` is the actor of messages no caller sent. Its subject contains a space, so no token's subject can equal it.
- **The actor relay** (`RimBank.Host/Security/ActorRelay`), placed after authorization, builds the `Actor` from the principal. It refuses an unusable one with `shared.actor-invalid` and sets its subject as the request's Wolverine user name. `EnableRelayOfUserName` is switched on by the host and also by `AddServiceDomainStore`, because the idempotency policy depends on it.
- **Caller-scoped idempotency** (amends [ADR 0018](0018-idempotency-keys-for-commands.md)):
  - The outcome id is `<command type>:<caller length>:<caller>:<key>`, and the advisory lock is taken on it.
  - The caller is `Envelope.UserName`, or `Actor.System`'s subject when there is none.
  - The length prefix keeps the id unambiguous, because a subject and a key may both contain colons (`u:x` + `k` must never read like `u` + `x:k`).
  - Stored outcomes from before this change are not migrated, because nothing is deployed yet.

## Consequences

- Every endpoint a context maps is authenticated without opting in, and a new anonymous endpoint fails the architecture tests.
- Every host test that calls a protected endpoint sends a token signed by an in-memory key (`TestTokens` in `RimBank.Host.Tests`). Those tests never fetch Keycloak metadata. A host started outside Development needs `Authentication:Authority`, so tests in Production set it.
- Two callers using the same idempotency key no longer replay, refuse or block each other. Messages without a caller (later `.Subscriptions`, scheduled messages) share the system scope, and their keys come from record identifiers (§8.4).
- The caller's subject also lands in Marten's last-modified-by and on the trace as `enduser.id`. The subject is opaque, but it is still a personal identifier: it is never logged at Information level by RimBank code, and tokens never appear in responses or logs.
- Deferred:
  - Per-BIAN-operation authorisation policies and separate command and query scopes (1.15, done in [ADR 0038](0038-per-operation-authorisation-policies.md): a read and a write scope per context, one policy per operation, and the dev realm's client scopes).
  - MCP authorization through Keycloak, and relaying the MCP caller the same way (0.16, done in [ADR 0022](0022-mcp-authorization-and-tool-errors.md): `/mcp` is challenged with `resource_metadata`, the MCP scheme serves the RFC 9728 metadata, the dev realm gains `rimbank-mcp-client` and `rimbank-developer`, and tool calls relay the caller through `ActorRelay` unchanged).
  - Recording the `Actor` (subject and client) in event metadata for audit (with the first events, 1.x; audit query in 7.3).
  - A production identity-provider setup, Keycloak deployment and key rotation runbook (7.x).
- Proven by:
  - `tests/RimBank.Host.Tests/Security`: 401 for missing, malformed, expired, wrong-audience and wrong-signature tokens; 401 for a token without a subject; 403; 401 before 404 on unknown routes; open health probes; the subject reaching the handler's envelope; only the probes allowing anonymous callers;
  - `tests/RimBank.Postgresql.Tests/Idempotency/CallerScopeTests` (Testcontainers);
  - `tests/RimBank.SharedKernel.Tests/Actors`;
  - `tests/RimBank.Problems.Tests`;
  - the architecture fixtures `ApiAllowsAnonymous` and `McpAllowsAnonymous`;
  - the AppHost model and startup tests with Keycloak.
