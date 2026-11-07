# 0025. One API project for all service domains

- Status: Accepted
- Date: 2026-11-07
- Roadmap phase: 1.7

## Context

Stage 0 gave every context its own HTTP adapter project, `RimBank.<BusinessDomain>.<ServiceDomain>.Api` (tech-stack §3.1, [ADR 0011](0011-namespace-rule.md), [ADR 0023](0023-one-core-project.md)), and its own MCP adapter project, `.Mcp`. A separate `RimBank.Http` held the HTTP conventions every `.Api` shared (the `Idempotency-Key` header, [ADR 0018](0018-idempotency-keys-for-commands.md)). Roadmap 1.5 created the first one, `RimBank.Party.PartyReferenceDataDirectory.Api`, with its own test project.

In the review of roadmap 1.7 (PR #119) the project owner rejected this layout, as they had before: a project per context only to hold a few endpoints is ceremony, and the API should be one thing. It is the same reasoning ADR 0023 applied to the cores. Like the cores, nothing in the isolation promises depends on separate projects:

- Every dependency, CQRS and isolation rule in `tests/RimBank.Architecture.Tests` finds a type's context and part (`.Api`, `.Mcp`, …) from its **namespace**, not its assembly (ADR 0011, ADR 0023).
- `.Api` and `.Mcp` already reach their core only through `IMessageBus` (`adapters-dispatch-through-the-message-bus`), and never reference `.Postgresql` (`api-dependencies`, `mcp-dependencies`).

## Options considered

1. **Keep a project per context** (`.Api`, `.Mcp`). A missing project reference stops one context's endpoints from using another context's types at compile time. But it doubles the project count for no business reason, and the owner rejected it.
2. **Endpoints as folders of `RimBank.Host`.** Fewest projects. But the host is the composition root (Program, modules, authentication, Problem Details, the Wolverine bus, `/mcp`): its rule `host-is-the-composition-root` keeps business logic and adapters out of it, and mixing both makes that rule hard to state.
3. **One `RimBank.Api` project** for every context's HTTP adapter, one folder per context, with the shared HTTP conventions at its root, and the same for MCP in the existing `RimBank.Mcp`. The host stays the composition root and maps the API.

## Decision

Option 3.

- **`src/RimBank.Api`** holds:
  - at its root, namespace `RimBank.Api`, the HTTP conventions every context's endpoints share: `IdempotencyKeyHeader` and `RequireIdempotencyKey()` (ADR 0018), and `StreamVersionTag` (the `ETag`, and `If-None-Match` → 304). The `RimBank.Http` project is merged into it.
  - one folder per context, `<BusinessDomain>/<ServiceDomain>/` (for example `Party/PartyReferenceDataDirectory/Registration/`). Its namespaces keep the `.Api` suffix (`RimBank.Party.PartyReferenceDataDirectory.Api.Registration`), so every namespace-based rule keeps working unchanged.
  - its references: `RimBank.Core`, `RimBank.SharedKernel`, `RimBank.Problems`, ASP.NET Core and Wolverine (`IMessageBus` only). It never references a `.Postgresql` adapter.
- **`tests/RimBank.Api.Tests`** is the one HTTP test project, with the same folders: the conventions' tests at its root (`RimBank.Api.Tests`) and each context's mapping, Alba and conformance tests under its folder (`RimBank.Party.PartyReferenceDataDirectory.Api.Registration.Tests`).
- **`RimBank.Host`** stays the composition root. A context's module still calls one extension from its HTTP adapter, now a type of `RimBank.Api` (`MapPartyReferenceDataDirectoryEndpoints()`, [ADR 0014](0014-host-composition-and-module-entry-point.md)).
- **MCP follows the same layout** when the first context tools arrive (roadmap 1.16): a context's MCP tools are a folder of the existing `RimBank.Mcp` project (namespace `RimBank.<BusinessDomain>.<ServiceDomain>.Mcp`), next to the MCP conventions at its root ([ADR 0022](0022-mcp-authorization-and-tool-errors.md)). No per-context `.Mcp` project is created.
- **`.PublishedLanguage`, `.Postgresql` and `.Subscriptions` stay per context.** This decision is about the driving adapters only.
- **Project names** (amends ADR 0011 and ADR 0023): a `src/` project is a composition project, `RimBank.Core`, `RimBank.Api`, `RimBank.Mcp`, or a context adapter whose suffix is `.PublishedLanguage`, `.Postgresql` or `.Subscriptions`. `RimBank.Api` and `RimBank.Mcp` hold only their own root namespace and the `.Api` (or `.Mcp`) parts of catalogued contexts (`assembly-names`). A type in a context's `.Api` or `.Mcp` namespace in any other assembly fails the new solution-only rule `http-and-mcp-adapters-live-in-shared-projects`.

## Consequences

- A new context adds a folder under `RimBank.Api` and `RimBank.Api.Tests`, and later under `RimBank.Mcp`, instead of two adapter projects and two test projects.
- One context's endpoints could use another context's types and still compile. As inside `RimBank.Core`, `cross-context-isolation` catches it in CI and names the type.
- **Extracting a context** moves its folder out of `RimBank.Api` (and `RimBank.Mcp`) into the new service's API project, with unchanged namespaces. The host wiring changes as before (tech-stack §2).
- `RimBank.Api.Tests` loads the Testcontainers assembly fixture of the Party tests, so it needs Docker even for the conventions' tests, which did not need it before.
- The rules `http-only-in-api-and-host` and `http-dependencies` keep their ids and now read the `RimBank.Api` root namespace. The architecture fixtures' stub of the header moves to `RimBank.Api`.
- tech-stack §2, §3.1, §3.3, §4, §5, §6, §8.4 and §9, CONTRIBUTING and README are updated. ADR 0018 and ADR 0022 get notes that point here.
- Proven by `SolutionOnlyRulesTests` (the new `assembly-names` and `http-and-mcp-adapters-live-in-shared-projects` cases: `RimBank.Api` holding a core or `.Postgresql` namespace fails, and so do a per-context `.Api` or `.Mcp` project and an endpoint type in the host), `SolutionRulesTests` (every rule holds with `RimBank.Api` loaded), and the unchanged `RimBank.Api.Tests`.
