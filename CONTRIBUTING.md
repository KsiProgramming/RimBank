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

- **Business language only.** Namespaces follow `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]`. Core code is organised by aggregate, Behavior Qualifier and business flow. Folders such as `Services`, `Repositories`, `Helpers`, `Managers`, `Utils` or `Dtos` are not allowed.
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

- **Never suppress a warning in code or in a project file.** `#pragma warning disable`, `[SuppressMessage]`, `<NoWarn>` and `<WarningsNotAsErrors>` fail the build (RB0001, RB0002). Fix the code, or, when the rule really does not apply, add a `dotnet_diagnostic.<ID>.severity = none` entry to the `.editorconfig` nearest the code (the project's own, or `tests/.editorconfig`), scoped to the files it covers, with a comment giving the reason.
- **Packages:** add or raise a version only in `Directory.Packages.props` (`VersionOverride` is rejected), then run `dotnet restore` and commit the changed `packages.lock.json` files. CI restores in locked mode, and a package with a known vulnerability fails the build. Packages come only from nuget.org (`NuGet.config`).
- **Public API in `src/` has XML doc comments** (CS1591 is enforced there).
- The product version is set once, in `Directory.Build.props`.

Tests use xUnit v3 and Shouldly on Microsoft.Testing.Platform ([ADR 0007](docs/adr/0007-test-platform.md)). Run one class with `dotnet test --solution RimBank.slnx --filter-class <Namespace.ClassName>`.

### Errors

Expected business failures are returned as `Result<T>` / `Result` with an `Error`, never thrown ([ADR 0006](docs/adr/0006-result-and-error-model.md)). Error codes are stable and follow `<context>.<error-name>` in lower-case kebab-case, for example `party.party-not-found`. Pick the `ErrorKind` that matches the HTTP status the caller should see, and never rename a released code.

### Typed identifiers

Every aggregate identifier is a `readonly record struct` implementing `ITypedId<TSelf>` over a version 7 GUID ([ADR 0008](docs/adr/0008-typed-identifier-pattern.md)). Copy the declaration template from the ADR next to the aggregate, and add one contract test class: `public sealed class XIdContractTests : TypedIdContractTests<XId>;` (from `tests/RimBank.SharedKernel.Testing`). Create new IDs with `XId.New(timeProvider)` in the command handler, never inside `Decide`, and turn untrusted input into an ID with `XId.TryCreate(...)`.

### Money

Amounts are always `Money` over a `decimal`, never `double` or `float` and never a bare `decimal` with a currency string beside it ([ADR 0009](docs/adr/0009-money-and-currency.md)). Turn untrusted input into money with `Money.TryCreate(amount, currencyCode)`, round explicitly with the mode the business rule names (`Round()` is banker's rounding), and divide only with `Allocate` / `Split` so no minor unit is lost. Property tests over money register `MoneyArbitraries` from `tests/RimBank.SharedKernel.Testing`: `[Properties(Arbitrary = [typeof(MoneyArbitraries)])]`.

### Aggregates and projections

An aggregate state is a `sealed record` implementing `IDecider<TSelf, TCommand, TEvent>`: `Initial` and `Evolve` in `<Aggregate>.cs`, `Decide` in a `partial` in `Decisions.cs`. A view is projected by a `sealed class …Projection` implementing `IProjection<TView, TEvent>` ([ADR 0010](docs/adr/0010-decider-and-projection-contracts.md)). Keep both pure: time and new IDs come in the command, set by the handler. Build decisions with `Decision.Accept<TEvent>(…)` / `Decision.Nothing<TEvent>()` or return an `Error`. Test them with `DeciderSpecification` (Given / When / Then) and `ProjectionSpecification` (Given / Then view), and run `DeciderLaws`, including `ProjectionAgreesWithState` for each view. All three come from `tests/RimBank.SharedKernel.Testing`. An event or view holding a collection needs value equality.

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
| `postgresql-dependencies` | `.Postgresql` never uses `.Api`, `.Mcp`, ASP.NET Core or the MCP SDK | tech-stack §4 |
| `cross-context-isolation` | The only reference to another context is `.Subscriptions` → its `.PublishedLanguage` | tech-stack §8.4, ADR 0004 |
| `published-language-has-no-views` | No view (a type a projection returns) lives in `.PublishedLanguage` | tech-stack §3.1, §8.4 |
| `command-handlers-stay-on-write-side` | A command handler never uses an `IReadPort`, a view, an `IQuery<>` or a query handler | tech-stack §4, ADR 0012 |
| `query-handlers-stay-on-read-side` | A query handler never uses an `IWritePort`, an aggregate, an `ICommand` or a command handler | tech-stack §4, ADR 0012 |
| `projections-implement-contract` | A `…Projection` is a sealed class implementing `IProjection`, and every `IProjection` is named `…Projection` | ADR 0010 |
| `projections-are-pure` | A projection has no instance fields and never uses ports, handlers or `TimeProvider` | tech-stack §4, §8.2 |
| `aggregates-implement-decider` | A type with a static `Evolve` implements `IDecider`; every `IDecider` is a sealed record in a core | tech-stack §7, ADR 0010 |
| `namespace-rule` | `RimBank.<BusinessDomain>.<ServiceDomain>[.<Adapter>]` with a catalogued service domain, or a composition project | tech-stack §3.1–3.2, ADR 0011 |
| `assembly-names` | A project is named after its root namespace, with an allowed adapter suffix | tech-stack §3.1, ADR 0011 |
| `business-folder-names` | No `Services`, `Repositories`, `Helpers`, `Managers`, `Utils`, `Dtos`, `Commands` or `Queries` namespace segment | mission principle 1, tech-stack §3.4 |
| `typed-ids-are-readonly-record-structs` | Every `ITypedId<T>` is a `readonly record struct` | tech-stack §7, ADR 0008 |
| `no-binary-floating-point` | No `double` or `float` (also nullable or as a generic argument) in cores, SharedKernel or `.PublishedLanguage` | quality bar, ADR 0009 |
| `test-namespaces` | Every type in a `*.Tests` assembly is in a namespace ending with `.Tests` | tech-stack §3.3 |

**Adding a context or project:**

1. Add its row to `ServiceDomainCatalogue.Solution` in the same PR as its BIAN mapping ADR ([ADR 0011](docs/adr/0011-namespace-rule.md)).
2. Add a `ProjectReference` to it from `tests/RimBank.Architecture.Tests/RimBank.Architecture.Tests.csproj`. The rules only see projects referenced there, and `The_solution_loads_every_project_and_never_the_fixtures` lists them.
3. Mark the CQRS roles: commands implement `ICommand`, queries `IQuery<TView>`, the write port `IWritePort` and the read port `IReadPort`. An unmarked type escapes the CQRS rules.

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
