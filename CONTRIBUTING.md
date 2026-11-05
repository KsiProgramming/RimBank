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
- **Hexagonal dependencies.** The core depends on nothing technical. Adapters depend on the core, never the other way round (enforced by architecture tests).
- **Pure decisions, failures as values.** `decide(command, state) → events`, `evolve(state, event) → state`, and `Result`/`Error` for business rules.
- **Commands and queries are separate.** Commands go through a decision and return an acknowledgement; queries read only from read models. Money rules are never checked against a read model ([ADR 0003](docs/adr/0003-separate-commands-and-queries.md)).
- **Loosely coupled contexts.** Read models are private. Contexts share only `.PublishedLanguage` integration events, consumed through their own `.Subscriptions` adapter; cross-context requests are events, not commands ([ADR 0004](docs/adr/0004-loosely-coupled-contexts.md)).
- **Money correctness.** `decimal` with ISO 4217 currency and minor units, never `double` or `float`.
- **Problem Details everywhere.** Every error returns RFC 9457 Problem Details, over HTTP and MCP.
- **HTTP and MCP parity.** Every BIAN operation exposed over HTTP is also an MCP tool, with a parity test.
- **No secrets or PII** in source, logs or test fixtures.
- **ADRs.** Significant decisions, BIAN interpretations, technology changes and any change to `specs/mission.md` need an ADR in [`docs/adr/`](docs/adr/) (see [ADR 0001](docs/adr/0001-record-architecture-decisions.md)).

## Building and testing

You need the .NET SDK pinned in [global.json](global.json).

```bash
dotnet build RimBank.slnx
dotnet test RimBank.slnx
dotnet format RimBank.slnx --verify-no-changes
```

Warnings are errors, and package versions live only in `Directory.Packages.props`.

### What CI runs

Every push to `main` and every pull request runs two workflows (policy: [ADR 0005](docs/adr/0005-ci-and-supply-chain-policy.md)):

- **`ci` / `build`**: the same commands you can run locally:

  ```bash
  dotnet restore RimBank.slnx
  dotnet build RimBank.slnx -c Release --no-restore
  dotnet test RimBank.slnx -c Release --no-build
  dotnet format RimBank.slnx --verify-no-changes --no-restore
  ```

- **`codeql` / `analyze (actions)` and `analyze (csharp)`**: static analysis of the workflows and the C# code (the `security-and-quality` suite). It also runs weekly.

Dependabot opens weekly PRs for NuGet packages, GitHub Actions and the .NET SDK.

When you change a workflow:

- Pin every action to a full commit SHA with a version comment, for example `uses: actions/checkout@<40-char sha> # v7.0.1`.
- Keep top-level `permissions: contents: read` and widen permissions only for the job that needs it.
- Never add secrets to workflows.

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
