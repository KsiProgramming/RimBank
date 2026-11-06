# Contributing to RimBank

Thank you for helping build an open core banking platform. This guide explains how work is organised so that every contribution stays small, reviewable and faithful to BIAN.

## Before you start

Read the constitution in [`specs/`](specs/):

- [mission.md](specs/mission.md): principles and the quality bar. They are non-negotiable.
- [tech-stack.md](specs/tech-stack.md): libraries, naming, dependency rules, and the API, MCP and error contracts.
- [roadmap.md](specs/roadmap.md): the ordered phases and the **Definition of done**.

The BIAN v14 specifications in [`doc-0/release14.0.0/`](doc-0/release14.0.0/) are the source of truth for service domains, operations and events.

## How work flows

1. **One roadmap phase = one pull request**, about one focused day of work.
2. **Plan first.** Each phase gets a planning package in `specs/YYYY-MM-DD-<slug>/` (`requirements.md`, `plan.md`, `validation.md`), written on a `plan/<phase>-<slug>` branch. With Claude Code, run `/plan-feature`.
3. **Implement** following `plan.md`, keeping the solution building and tests green after each task group.
4. **Validate** against `validation.md` and the Definition of done, then open the PR using the template.
5. **Tick the roadmap checkbox** for the phase in the same PR.

Work outside the roadmap (bug fixes, docs) is welcome as small, focused PRs. Open an issue first for anything larger.

## Rules that reviewers check

- **Business language only.** Namespaces follow `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]`. All core code (domain and application logic) lives in the one `src/RimBank.Core` project, in the folder `<BusinessDomain>/<ServiceDomain>/`, and is organised by aggregate, Behavior Qualifier and business flow ([ADR 0023](docs/adr/0023-one-core-project.md)). Folders such as `Services`, `Repositories`, `Helpers`, `Managers`, `Utils` or `Dtos` are not allowed.
- **Hexagonal dependencies.** The core depends on nothing technical. Adapters depend on the core, never the other way round (enforced by architecture tests, see [Architecture rules](#architecture-rules)).
- **Pure decisions, failures as values.** `decide(command, state) → events`, `evolve(state, event) → state`, and `Result`/`Error` for business rules.
- **Commands and queries are separate.** Commands go through a decision and return an acknowledgement; queries read only from read models. Money rules are never checked against a read model ([ADR 0003](docs/adr/0003-separate-commands-and-queries.md)).
- **Loosely coupled contexts.** Read models are private. Contexts share only `.PublishedLanguage` integration events, consumed through their own `.Subscriptions` adapter; cross-context requests are events, not commands ([ADR 0004](docs/adr/0004-loosely-coupled-contexts.md)).
- **Money correctness.** `decimal` with ISO 4217 currency and minor units, never `double` or `float`.
- **Switch expressions, always.** Write `value switch { … }`, never a `switch (value) { case …: break; }` statement. When the arms have side effects, let each arm return the new state (for example a tuple) and assign it, with a small helper method for an arm that needs statements. The build flags convertible switch statements (`csharp_style_prefer_switch_expression = true:warning` in `.editorconfig`).
- **Test namespaces end with `.Tests`.** A test namespace follows the code under test from the outside in, then adds the test folder, and `.Tests` always comes last: `<namespace of the project under test>.<folder>.Tests`. For example `RimBank.SharedKernel.MoneyAndCurrency.Tests`, `RimBank.SharedKernel.TypedIds.Tests`, `RimBank.Party.PartyReferenceDataDirectory.Registration.Tests`; a file at the root of a test project uses `RimBank.SharedKernel.Tests`. Never `RimBank.SharedKernel.Tests.MoneyAndCurrency`. Shared test libraries keep their own name (`RimBank.SharedKernel.Testing`).
- **Problem Details everywhere.** Every error returns RFC 9457 Problem Details, over HTTP and MCP.
- **HTTP and MCP parity.** Every BIAN operation exposed over HTTP is also an MCP tool, with a parity test.
- **No secrets or PII** in source, logs or test fixtures.
- **ADRs.** Significant decisions, BIAN interpretations, technology changes and any change to `specs/mission.md` need an ADR in [`docs/adr/`](docs/adr/) (see [ADR 0001](docs/adr/0001-record-architecture-decisions.md)).

## Building and testing

You need the .NET SDK pinned in [global.json](global.json).

```bash
dotnet build RimBank.slnx
dotnet test --solution RimBank.slnx
dotnet format RimBank.slnx --verify-no-changes
```

Warnings are errors, and package versions live only in `Directory.Packages.props` ([ADR 0013](docs/adr/0013-warning-and-package-governance.md)):

- **Never suppress a warning in code or in a project file.** `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>` and `<WarningsNotAsErrors>` fail the build (RB0001, RB0002). Fix the code, or, when the rule really does not apply, add a `dotnet_diagnostic.<ID>.severity = none` entry to the `.editorconfig` nearest the code (the project's own, or `tests/.editorconfig`), scoped to the files it covers, with a comment giving the reason. The one exception is the MSBuild warning ASPIRE010 in `RimBank.AppHost.csproj` ([ADR 0014](docs/adr/0014-host-composition-and-module-entry-point.md)).
- **Packages:** add or raise a version only in `Directory.Packages.props` (`VersionOverride` is rejected), then run `dotnet restore` and commit the changed `packages.lock.json` files. The one version outside that file is the `Aspire.AppHost.Sdk/<version>` attribute in `src/RimBank.AppHost/RimBank.AppHost.csproj`: raise it together with `AspireVersion`, or the build fails with RB0003. The Keycloak integrations exist only as previews, so their version is `AspireKeycloakVersion`, which must be a preview of the same `AspireVersion` (RB0004, [ADR 0019](docs/adr/0019-authentication-baseline.md)). `RimBank.AppHost`, `RimBank.AppHost.Tests` and `RimBank.Architecture.Tests` keep one lock file per runtime (`packages.win-x64.lock.json`, `packages.linux-x64.lock.json`), because Aspire's orchestrator and dashboard packages depend on the machine ([ADR 0014](docs/adr/0014-host-composition-and-module-entry-point.md)). Regenerate both after a package change:

  ```bash
  for p in src/RimBank.AppHost tests/RimBank.AppHost.Tests tests/RimBank.Architecture.Tests; do
    dotnet restore $p --force-evaluate -p:NETCoreSdkRuntimeIdentifier=win-x64
    dotnet restore $p --force-evaluate -p:NETCoreSdkRuntimeIdentifier=linux-x64
  done
  dotnet restore RimBank.slnx   # back to your own runtime
  ``` CI restores in locked mode, and a package with a known vulnerability fails the build. Packages come only from nuget.org (`NuGet.config`).
- **Public API in `src/` has XML doc comments** (CS1591 is enforced there).
- The product version is set once, in `Directory.Build.props`.

Tests use xUnit v3 and Shouldly on Microsoft.Testing.Platform ([ADR 0007](docs/adr/0007-test-platform.md)). Run one class with `dotnet test --solution RimBank.slnx --filter-class <Namespace.ClassName>`.

`tests/RimBank.AppHost.Tests` starts real containers (PostgreSQL, Keycloak and the host) and needs Docker. In CI it always runs. Locally it is skipped, with the reason shown, when Docker is not reachable.

### Running RimBank locally

With Docker running:

```bash
dotnet run --project src/RimBank.AppHost
```

The console prints the Aspire dashboard URL. The dashboard shows `postgres`, the `rimbank` database, `keycloak` and `rimbank-host`, with their logs, traces and metrics. The host's probes are `GET /health` and `GET /alive` (see `src/RimBank.Host/RimBank.Host.http`). Both are anonymous and return the status only ([ADR 0014](docs/adr/0014-host-composition-and-module-entry-point.md)). PostgreSQL keeps its data in the `rimbank-postgres-data` Docker volume and Keycloak in `rimbank-keycloak-data`. Their generated passwords live in the AppHost's user secrets.

### Authentication

Every request needs a JWT bearer token for the audience `rimbank-api`, except the health probes ([ADR 0019](docs/adr/0019-authentication-baseline.md)). A missing or invalid token is a 401 `shared.unauthenticated` Problem Details response, and a refused policy is a 403 `shared.forbidden`.

- **Locally**, the AppHost runs Keycloak with the dev-only realm `rimbank` from `src/RimBank.AppHost/Realms`. Get a token with the client-credentials grant of `rimbank-dev-client`, as `src/RimBank.Host/RimBank.Host.http` and the realm's README show. Its secret is published and only for local use.
- **Outside Development**, the host needs `Authentication:Authority`, the HTTPS address of the realm (for example `https://login.example.com/realms/rimbank`). Without it, the host refuses to start. MCP clients are sent to the same address unless `Mcp:AuthorizationServer` (also HTTPS) says otherwise ([ADR 0022](docs/adr/0022-mcp-authorization-and-tool-errors.md)).
- **Deny by default:** never call `AllowAnonymous()` or use `[AllowAnonymous]`. New endpoints are protected without opting in, and the architecture rule `allow-anonymous-only-in-service-defaults` fails otherwise. Narrow access with `RequireAuthorization(...)` (per-operation policies come in roadmap 1.15).
- **Caller:** the host turns the token's `sub` and `azp` into an `Actor` (SharedKernel) and hands the subject to the message bus. Adapters and cores never read the token.
- **Tests** sign tokens with an in-memory key: call `builder.UseTestTokens()` and send `TestTokens.Issue()` (in `tests/RimBank.Host.Tests/Security`).

### MCP

The host serves one MCP server at `/mcp` (Streamable HTTP, stateless, [ADR 0021](docs/adr/0021-mcp-wiring-and-tool-conventions.md)). It needs a bearer token like every other endpoint. Its 401 points MCP clients to the protected-resource metadata at `/.well-known/oauth-protected-resource/mcp`, which names the Keycloak realm ([ADR 0022](docs/adr/0022-mcp-authorization-and-tool-errors.md)).

- **Adding tools:** a context's `.Mcp` project exposes one `IMcpServerBuilder` extension, for example `AddPartyReferenceDataDirectoryTools()`, which calls `WithTools<…>()`. The context's module calls only that extension in `AddMcpTools`. Never call `AddMcpServer`, `WithHttpTransport` or `MapMcp` from a context. A tool name registered twice stops the host.
- **Names:** one tool per BIAN operation, `<service_domain>_<operation>[_<behavior_qualifier>]` in snake_case, set explicitly with `[McpServerTool(Name = …)]`. The description is the BIAN operation's description.
- **Annotations:** set `ReadOnly`, `Destructive`, `Idempotent` and `OpenWorld` on every tool, following the table in ADR 0021. `Retrieve` tools are read-only queries. Every other tool is a command with an idempotency key.
- **Errors:** a tool dispatches through `IMessageBus` and answers with `result.ToToolResult(...)` from `RimBank.Mcp`. A failed `Result` becomes `isError` with the same Problem Details as HTTP. Never throw for a business failure. The host's tool error filter adds `instance` and `traceId`, turns arguments the SDK cannot bind into `shared.tool-arguments-invalid` (400), and turns any exception into `shared.unexpected` (500) with no exception text.
- **Caller:** a tool call carries the token's subject to the message bus like an HTTP call. Do not read the token in a tool.
- **Connecting a local client:** start the AppHost, then point MCP Inspector (`npx @modelcontextprotocol/inspector`) or Claude Code at the host's `/mcp` URL from the Aspire dashboard. Use client ID `rimbank-mcp-client` (public, PKCE, loopback redirects `http://127.0.0.1:<any port>` and `http://localhost:6274`). Sign in as `rimbank-developer` / `rimbank-developer-local-only`. Both are dev-only and published on purpose.
- **Tests** connect the SDK's `McpClient` in-process over `HttpClientTransport`, using the test server's `HttpClient` with a `TestTokens` bearer token (`McpTestHost` in `tests/RimBank.Host.Tests/Mcp`).

### Errors

Expected business failures are returned as `Result<T>` / `Result` with an `Error`, never thrown ([ADR 0006](docs/adr/0006-result-and-error-model.md)). Error codes are stable and follow `<context>.<error-name>` in lower-case kebab-case, for example `party.party-not-found`. Pick the `ErrorKind` that matches the HTTP status the caller should see, and never rename a released code.

### Typed identifiers

Every aggregate identifier is a `readonly record struct` implementing `ITypedId<TSelf>` over a version 7 GUID ([ADR 0008](docs/adr/0008-typed-identifier-pattern.md)). Copy the declaration template from the ADR next to the aggregate, and add one contract test class: `public sealed class XIdContractTests : TypedIdContractTests<XId>;` (from `tests/RimBank.SharedKernel.Testing`). Create new IDs with `XId.New(timeProvider)` in the command handler, never inside `Decide`, and turn untrusted input into an ID with `XId.TryCreate(...)`.

### Money

Amounts are always `Money` over a `decimal`, never `double` or `float` and never a bare `decimal` with a currency string beside it ([ADR 0009](docs/adr/0009-money-and-currency.md)). Turn untrusted input into money with `Money.TryCreate(amount, currencyCode)`, round explicitly with the mode the business rule names (`Round()` is banker's rounding), and divide only with `Allocate` / `Split` so no minor unit is lost. Property tests over money register `MoneyArbitraries` from `tests/RimBank.SharedKernel.Testing`: `[Properties(Arbitrary = [typeof(MoneyArbitraries)])]`.

### Aggregates and projections

An aggregate state is a `sealed record` implementing `IDecider<TSelf, TCommand, TEvent>`: `Initial` and `Evolve` in `<Aggregate>.cs`, `Decide` in a `partial` in `Decisions.cs`. A view is projected by a `sealed class …Projection` implementing `IProjection<TView, TEvent>` ([ADR 0010](docs/adr/0010-decider-and-projection-contracts.md)). Keep both pure: time and new IDs come in the command, set by the handler. Build decisions with `Decision.Accept<TEvent>(…)` / `Decision.Nothing<TEvent>()` or return an `Error`. Test them with `DeciderSpecification` (Given / When / Then) and `ProjectionSpecification` (Given / Then view), and run `DeciderLaws`, including `ProjectionAgreesWithState` for each view. All three come from `tests/RimBank.SharedKernel.Testing`. An event or view holding a collection needs value equality.

### Event store and views

A context's `.Postgresql` adapter registers its own Marten store with `RimBank.Postgresql` ([ADR 0016](docs/adr/0016-per-context-marten-stores.md)). Declare a marker `public interface I<ServiceDomain>Store : IDocumentStore;` and call `builder.AddServiceDomainStore<I<ServiceDomain>Store, TCore>("<ServiceDomain>", options => …)`, where `TCore` is any type in the core's root namespace (for example the Control Record). That gives the context schema `<service_domain>`, role `rimbank_<service_domain>`, connection string `ConnectionStrings:<ServiceDomain>`, its own async daemon and a `ready` health check. Inside the options, map every event to a stable name (`options.Events.MapEventType<TEvent>("snake_case_name")`) and wire each view by lifecycle: `options.AddInlineView<TView, TProjection, TEvent>()` for a single-stream view read right after a command, and `AddAsyncView` for everything else. A view has a `Guid Id` equal to its stream's id. `store.GetViewProgressAsync(name)` reports an async view's lag, and `store.RebuildViewAsync(name, timeout)` rebuilds a view. In Development the host creates the role and schema from the context's connection string (`RimBank:Postgresql:ProvisionRoles`). The integration tests need Docker.

### Messaging

The host runs one Wolverine bus ([ADR 0017](docs/adr/0017-wolverine-wiring.md)). `AddServiceDomainStore` also connects the context to it: Wolverine finds the handlers in the core's assembly, every handler in the core's namespace commits through the context's own store, the outbox and inbox tables live in the context's schema, and the context's messages go to its durable local queue `<service-domain>`.

- **Handlers** live in the core as plain `static` classes named `…Handler` with a `Handle` method. They take the command or query, then their ports, and never Marten or Wolverine. A command handler returns `Result<TAcknowledgement>`, or `(Result<TAcknowledgement>, TMessage?)` to publish a message after the commit. A query handler returns `Result<TView>`.
- **A failed decision commits nothing:** append events and return messages only when the decision succeeds. The transaction then has nothing to save.
- **Write ports:** in the `.Postgresql` adapter, implement the port over `IDocumentSession` (a public constructor taking only the session) and register it with `builder.AddWritePort<TPort, TImplementation>()`. Wolverine builds it from the handler's own session and makes the handler transactional, so its events and messages commit together. Do not register write ports in the container.
- **Read ports:** register them in the container, usually as singletons over the store marker (`store.QuerySession()`). A query handler opens no transaction.
- **Dispatch:** `.Api` and `.Mcp` call `IMessageBus.InvokeAsync<Result<T>>(…)` and use no other Wolverine type (`adapters-dispatch-through-the-message-bus`).
- **Idempotency:** every command record implements `ICommand` and so carries an `IdempotencyKey IdempotencyKey`. `AddServiceDomainStore` wraps every command handler with the idempotency policy ([ADR 0018](docs/adr/0018-idempotency-keys-for-commands.md)): the same key and payload return the stored acknowledgement and the handler does not run again; the same key with another payload returns `shared.idempotency-key-reused`; a key still being handled returns `shared.idempotency-key-in-use`; a missing key returns `shared.idempotency-key-missing`. Only successful results are stored, so a rejected command can be retried with the same key. Keys are scoped to the caller: two callers with the same key never replay or block each other, and messages no caller sent share the system scope ([ADR 0019](docs/adr/0019-authentication-baseline.md)). The handler never reads the key. A `.Subscriptions` adapter builds the key from the requesting record's identifier (`IdempotencyKey.From(...)`).
- **HTTP:** every `.Api` endpoint that dispatches a command requires the header with `RequireIdempotencyKey()` from `RimBank.Http` (on the endpoint or its route group), takes an `IdempotencyKeyHeader idempotency` parameter and passes `idempotency.Value` into the command. `Retrieve` (query) endpoints take no key.
- **Host message store:** Wolverine keeps its own bookkeeping in schema `wolverine` (role `rimbank_wolverine`, `ConnectionStrings:Wolverine`). Without that connection string, as before the first context, messages stay in memory.

### BIAN conformance

Each context's `.Api.Tests` references `tests/RimBank.Conformance.Testing` and has one conformance test that checks its routes and channels against its BIAN v14 specification ([ADR 0020](docs/adr/0020-bian-conformance-harness.md)). Declare the BIAN operation ids the context implements (as the spec's `operationId`), and the channel addresses it publishes to:

```csharp
var specification = await BianSpecifications.LoadAsync("PartyReferenceDataDirectory");
var routes = ImplementedRoute.From(factory.Services.GetRequiredService<EndpointDataSource>());

routes.ShouldConformTo(specification, ["PartyReferenceDataDirectoryEntry/Register", "PartyReferenceDataDirectoryEntry/Retrieve"]);
string[] published = ["PartyReferenceDataDirectoryEntry/Created"];
published.ShouldBeChannelsOf(specification);
```

The test fails if a route under `/<ServiceDomain>/` is not in the spec or uses another method than the spec (BIAN uses `POST` for `Register` and `PUT` for most other commands), if a `Retrieve` is not `GET` or a command is `GET`, if a declared operation is not mapped, or if a channel is not in the AsyncAPI file. Path parameter names do not matter (`{id}` matches `{partyreferencedatadirectoryid}`). Add each new operation to the declared list in the same PR that maps it.

### Architecture rules

`tests/RimBank.Architecture.Tests` checks the constitution's structural rules with ArchUnitNET. Each rule holds for the solution and is proven to bite against a bad context in `tests/RimBank.Architecture.Fixtures` ([ADR 0012](docs/adr/0012-cqrs-role-markers.md)). Run only these tests with:

```bash
dotnet test --project tests/RimBank.Architecture.Tests
```

| Rule | What it checks | Source |
|---|---|---|
| `shared-kernel-dependencies` | `RimBank.SharedKernel` uses the BCL only | tech-stack §4 |
| `core-dependencies` | A core uses SharedKernel and its own `.PublishedLanguage`, never a framework or its own adapters | tech-stack §4 |
| `published-language-dependencies` | `.PublishedLanguage` uses the BCL only | tech-stack §4 |
| `subscriptions-dependencies` | `.Subscriptions` never uses its own `.Api`, `.Mcp` or `.Postgresql`, ASP.NET Core, Marten, Npgsql or the MCP SDK | tech-stack §4 |
| `api-dependencies` | `.Api` never uses `.Postgresql`, Marten, Wolverine.Marten, Npgsql or the MCP SDK | tech-stack §4 |
| `mcp-dependencies` | `.Mcp` never uses `.Api`, `.Postgresql`, Marten, Wolverine.Marten or Npgsql | tech-stack §4, §6 |
| `adapters-dispatch-through-the-message-bus` | `.Api` and `.Mcp` use Wolverine only through `IMessageBus` | tech-stack §4, §6, ADR 0017 |
| `postgresql-dependencies` | `.Postgresql` never uses `.Api`, `.Mcp`, ASP.NET Core or the MCP SDK | tech-stack §4 |
| `cross-context-isolation` | The only reference to another context is `.Subscriptions` → its `.PublishedLanguage` | tech-stack §8.4, ADR 0004 |
| `published-language-has-no-views` | No view (a type a projection returns) lives in `.PublishedLanguage` | tech-stack §3.1, §8.4 |
| `service-defaults-only-in-host` | Only `RimBank.Host` uses `RimBank.ServiceDefaults` | tech-stack §1–§2, ADR 0014 |
| `nothing-depends-on-apphost` | Nothing uses `RimBank.AppHost` | tech-stack §1, ADR 0014 |
| `host-is-the-composition-root` | Nothing outside `RimBank.Host` uses it; a context never implements `IServiceDomainModule` itself | tech-stack §2, §4, ADR 0014 |
| `problems-only-in-adapters-and-host` | Only `.Api`, `.Mcp`, `RimBank.Mcp` and `RimBank.Host` use `RimBank.Problems` | tech-stack §4, ADR 0015, ADR 0022 |
| `problems-dependencies` | `RimBank.Problems` uses SharedKernel, ASP.NET Core and the BCL only | tech-stack §4, ADR 0015 |
| `http-only-in-api-and-host` | Only `.Api` and `RimBank.Host` use `RimBank.Http` | tech-stack §4, ADR 0018 |
| `http-dependencies` | `RimBank.Http` uses SharedKernel, `RimBank.Problems`, ASP.NET Core and the BCL only | tech-stack §4, ADR 0018 |
| `shared-mcp-only-in-mcp-and-host` | Only `.Mcp` and `RimBank.Host` use `RimBank.Mcp` | tech-stack §4, ADR 0022 |
| `shared-mcp-dependencies` | `RimBank.Mcp` uses SharedKernel, `RimBank.Problems`, ASP.NET Core, the MCP SDK and the BCL only | tech-stack §4, ADR 0022 |
| `allow-anonymous-only-in-service-defaults` | Nothing outside `RimBank.ServiceDefaults` calls `AllowAnonymous()` or uses `[AllowAnonymous]`: only the health probes are anonymous | tech-stack §10, ADR 0019 |
| `shared-postgresql-only-in-postgresql-adapters` | Only a context's `.Postgresql` and `RimBank.Host` (for its own message store) use `RimBank.Postgresql` | tech-stack §4, ADR 0016, ADR 0017 |
| `shared-postgresql-dependencies` | `RimBank.Postgresql` uses SharedKernel, Marten (JasperFx, Weasel, Npgsql), Wolverine, Microsoft.Extensions and the BCL only | tech-stack §4, ADR 0016, ADR 0017 |
| `command-handlers-stay-on-write-side` | A command handler never uses an `IReadPort`, a view, an `IQuery<>` or a query handler | tech-stack §4, ADR 0012 |
| `query-handlers-stay-on-read-side` | A query handler never uses an `IWritePort`, an aggregate, an `ICommand` or a command handler | tech-stack §4, ADR 0012 |
| `projections-implement-contract` | A `…Projection` is a sealed class implementing `IProjection`, and every `IProjection` is named `…Projection` | ADR 0010 |
| `projections-are-pure` | A projection has no instance fields and never uses ports, handlers or `TimeProvider` | tech-stack §4, §8.2 |
| `aggregates-implement-decider` | A type with a static `Evolve` implements `IDecider`; every `IDecider` is a sealed record in a core | tech-stack §7, ADR 0010 |
| `namespace-rule` | `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]` with a catalogued service domain, or a composition project | tech-stack §3.1–3.2, ADR 0011 |
| `assembly-names` | A project is a composition project, `RimBank.Core` (holding only catalogued core namespaces), or a context adapter named after its root namespace with an allowed suffix | tech-stack §3.1, ADR 0011, ADR 0023 |
| `cores-live-in-rimbank-core` | Every type in a context's core namespace lives in the `RimBank.Core` assembly | tech-stack §3.1, ADR 0023 |
| `business-folder-names` | No `Services`, `Repositories`, `Helpers`, `Managers`, `Utils`, `Dtos`, `Commands` or `Queries` namespace segment | mission principle 1, tech-stack §3.4 |
| `typed-ids-are-readonly-record-structs` | Every `ITypedId<T>` is a `readonly record struct` | tech-stack §7, ADR 0008 |
| `no-binary-floating-point` | No `double` or `float` (also nullable or as a generic argument) in cores, SharedKernel or `.PublishedLanguage` | quality bar, ADR 0009 |
| `test-namespaces` | Every type in a `*.Tests` assembly is in a namespace ending with `.Tests` | tech-stack §3.3 |

**Adding a context or project:**

1. Add its row to `ServiceDomainCatalogue.Solution` in the same PR as its BIAN mapping ADR ([ADR 0011](docs/adr/0011-namespace-rule.md)).
2. Put its core in `src/RimBank.Core/<BusinessDomain>/<ServiceDomain>/` and its core tests in `tests/RimBank.Core.Tests/<BusinessDomain>/<ServiceDomain>/`; never create a per-context core project ([ADR 0023](docs/adr/0023-one-core-project.md)). Its adapter projects (`.PublishedLanguage`, `.Api`, `.Mcp`, `.Postgresql`, `.Subscriptions`) go under `src/<BusinessDomain>/` and reference `RimBank.Core`.
3. Add a `ProjectReference` to each new project from `tests/RimBank.Architecture.Tests/RimBank.Architecture.Tests.csproj`. The rules only see projects referenced there, and `The_solution_loads_every_project_and_never_the_fixtures` lists them.
4. Mark the CQRS roles: commands implement `ICommand`, queries `IQuery<TView>`, the write port `IWritePort` and the read port `IReadPort`. An unmarked type escapes the CQRS rules.
5. Register the context in the host: add `src/RimBank.Host/Modules/<BusinessDomain>/<ServiceDomain>Module.cs` implementing `IServiceDomainModule`, which only calls the adapters' own `Add…` / `Map…` extension methods, and append it to `ServiceDomainModules.All` ([ADR 0014](docs/adr/0014-host-composition-and-module-entry-point.md)).

**Adding a rule:** write it in `ArchitectureRules`, add a bad context that breaks only that rule to the fixtures, list it in `FixtureContexts.Bad`, and add a row to the table above.

### What CI runs

Every push to `main` and every pull request runs two workflows (policy: [ADR 0005](docs/adr/0005-ci-and-supply-chain-policy.md)):

- **`ci` / `build`**: the same commands you can run locally:

  ```bash
  dotnet restore RimBank.slnx --locked-mode
  dotnet build RimBank.slnx -c Release --no-restore
  dotnet test --solution RimBank.slnx -c Release --no-build --report-xunit-trx --results-directory TestResults
  dotnet format RimBank.slnx --verify-no-changes --no-restore
  python3 -m unittest discover -s scripts
  ```

- **`codeql` / `analyze (actions)` and `analyze (csharp)`**: static analysis of the workflows and the C# code (the `security-and-quality` suite). It also runs weekly.

Dependabot opens weekly PRs for NuGet packages, GitHub Actions and the .NET SDK.

When you change a workflow:

- Pin every action to a full commit SHA with a version comment, for example `uses: actions/checkout@<40-char sha> # v7.0.1`.
- Keep top-level `permissions: contents: read` and widen permissions only for the job that needs it.
- Never add secrets to workflows. The one exception is `roadmap-board`'s `ROADMAP_BOARD_TOKEN` ([ADR 0013](docs/adr/0013-warning-and-package-governance.md)).

### Roadmap board

[`specs/roadmap.md`](specs/roadmap.md) is mirrored on GitHub by `scripts/sync_roadmap_board.py` ([ADR 0013](docs/adr/0013-warning-and-package-governance.md)): one milestone per stage, one issue per phase (labels `roadmap-phase` and `stage:N`), and the **RimBank roadmap** Projects board. Edit the roadmap, never the synced issues: their titles and bodies are overwritten. Ticking a phase in a PR closes its issue and moves its card to Done once the PR is merged. Move a card to In Progress yourself when you start a phase, and reference the phase issue in your PR.

The `roadmap-board` workflow runs the sync after every merge that changes the roadmap. It needs a `ROADMAP_BOARD_TOKEN` repository secret (a token with the `repo` and `project` scopes); without it, a maintainer runs the script locally:

```bash
gh auth refresh -s project                     # once
python scripts/sync_roadmap_board.py --dry-run # show what would change
python scripts/sync_roadmap_board.py
```

### Labels

Every issue and pull request carries **one `type:`** label, any **`area:`** labels that apply, and at most **one `context:`** label. The set is defined in `scripts/sync_roadmap_board.py` (`LABELS`) and created by the sync; roadmap phase issues get theirs automatically.

| Prefix | Labels | Meaning |
|---|---|---|
| `type:` | `feature`, `adr`, `bug`, `chore`, `docs`, `security` | What kind of change |
| `area:` | `shared-kernel`, `architecture`, `host`, `domain`, `read-models`, `api`, `mcp`, `persistence`, `messaging`, `security`, `observability`, `bian-conformance`, `ci`, `performance` | Which technical concern |
| `context:` | `party`, `product-directory`, `position-keeping`, `current-account`, `payments`, `finance` | Which BIAN bounded context (tech-stack §3.2) |
| `stage:` | `stage:0` … `stage:8` | Which roadmap stage (synced) |
| | `roadmap-phase` | An issue mirrored from the roadmap (synced: edit the roadmap, not the issue) |
| | `dependencies` | Opened by Dependabot |

A new label is added to `LABELS` in a PR, never created by hand, so the taxonomy stays in one place. A new bounded context adds its `context:` label in the same PR as its BIAN mapping ADR.

### Branch protection (maintainers)

Configure a branch ruleset for `main` in the repository settings:

- Require a pull request before merging, with at least one approval, and dismiss stale approvals when new commits are pushed.
- Require review from code owners ([.github/CODEOWNERS](.github/CODEOWNERS)).
- Require these status checks to pass, and require branches to be up to date: `build`, `analyze (actions)`, `analyze (csharp)`.
- Block force pushes and branch deletion.

## Commits and pull requests

- Write commit messages in the imperative mood ("Add Money arithmetic"), and keep each one focused.
- Fill in the pull request template, including the Definition of done checklist.
- At least one maintainer review is required before merging.

## Code of Conduct and security

Everyone taking part agrees to the [Code of Conduct](CODE_OF_CONDUCT.md). Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md), never in public issues.
