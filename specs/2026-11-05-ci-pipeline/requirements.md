# CI pipeline

Roadmap phase: 0.2
Branch: plan/0.2-ci-pipeline

## Context
0.1 added the repository skeleton (an empty `RimBank.slnx`, shared build settings and Central Package Management) and moved the automated gates to this phase. Every later phase's Definition of done includes "GitHub Actions CI green (build, test, format, CodeQL)", so this phase has to come before 0.3 adds the first C# project.

Relevant constitution:
- [tech-stack.md §1](../tech-stack.md#1-platform): CI is **GitHub Actions** (build, test, format, CodeQL, Dependabot); the SDK is pinned by `global.json`; `TreatWarningsAsErrors=true`.
- [tech-stack.md §10](../tech-stack.md#10-security--supply-chain): CodeQL, Dependabot and secret scanning. SBOM and signed releases come later, in 7.5.
- [mission.md](../mission.md), principle 9 ("production-grade from the first commit") and principle 10 ("small, verifiable steps").

There is no BIAN service domain in this phase. Commands, queries and read models do not apply.

**Constraint:** the solution has no projects yet. `dotnet test` has nothing to run, and CodeQL's C# analysis fails with "no source code seen". The pipeline must pass on today's empty solution and start enforcing automatically once 0.3 adds a `.csproj`. A placeholder project would be speculative scaffolding (mission non-goals), so we don't add one.

## Scope
### In scope
- `.github/workflows/ci.yml`: restore, build (Release), test and `dotnet format --verify-no-changes` on `ubuntu-24.04`, with a NuGet cache and the test results uploaded as an artifact.
- `.github/dependabot.yml`: the `nuget`, `github-actions` and `dotnet-sdk` (`global.json`) ecosystems, weekly, with grouped minor/patch updates.
- `.github/workflows/codeql.yml`: the `actions` language now; `csharp` runs only once a `.csproj` exists and is skipped before that. Triggers: push, PR and a weekly schedule.
- Workflow hardening: every third-party action pinned to a full commit SHA (with a version comment, kept up to date by Dependabot), least-privilege `permissions:` (top-level `contents: read`, widened only per job), `concurrency` that cancels superseded PR runs, and `persist-credentials: false` on checkout.
- `.github/CODEOWNERS`.
- Docs: a CI badge in `README.md`, a "What CI runs" section in `CONTRIBUTING.md` (the same commands to run locally, the required checks, and the branch-protection settings for maintainers to apply by hand), and the roadmap tick.
- ADR 0005: CI and supply-chain policy.

### Out of scope
- OS matrix (Windows/macOS), coverage collection, the dependency-review action, and markdown or link lint. These are not on the roadmap and can be added later if needed.
- Automating branch-protection rules (this needs admin rights and is not code; we document it instead).
- Testcontainers and Docker setup on the runner (comes with the first integration tests in 0.10; `ubuntu-24.04` already has Docker).
- Architecture tests (0.7), BIAN conformance (0.14).
- Container images, SBOM and signed releases (7.5). Mutation testing (7.7).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Scope breadth | Core + supply-chain hardening | User choice; mission principle 9 |
| Runner | `ubuntu-24.04` (pinned, not `-latest`) | Reproducible builds; has Docker for later Testcontainers phases |
| SDK install | `actions/setup-dotnet` with `global-json-file: global.json` | One source of truth for the SDK version (tech-stack §1) |
| NuGet cache | `actions/cache` on `~/.nuget/packages`, keyed on `Directory.Packages.props` + `**/*.csproj` | `setup-dotnet`'s built-in cache needs lock files, which we don't have |
| Workflow split | Separate `ci.yml` and `codeql.yml` | User choice (approach); keeps CodeQL's schedule and `security-events: write` permission apart from CI |
| CodeQL C# before 0.3 | Analysis steps guarded by `hashFiles('**/*.csproj') != ''` | No placeholder project; turns on automatically when 0.3 lands |
| CodeQL C# build mode | `manual` with `dotnet build RimBank.slnx -c Release` | Same build as CI, including source generators (Wolverine/Marten later) |
| CodeQL `actions` language | On from now | Scans the workflows themselves, so there is real code to analyse today |
| Action pinning | Full commit SHA + `# vX.Y.Z` comment | Supply-chain safety (tech-stack §10); Dependabot `github-actions` keeps the pins fresh |
| Dependabot cadence | Weekly; minor/patch grouped per ecosystem; majors as separate PRs | Fewer PRs to review; a major version can trigger an ADR |
| Test results | `--logger trx` into `TestResults/`, uploaded with `if: always()` | Failures can be diagnosed from the artifact; the logger flag is confirmed when the xUnit v3 runner arrives in 0.3 |
| Branch protection | Documented in CONTRIBUTING, applied by hand | Admin-only setting, not code |
| ADR | 0005, CI and supply-chain policy | SHA pinning, least privilege and required checks are lasting policies that contributors must follow |

## ADRs to write
- 0005: CI and supply-chain policy: workflows are pinned to SHAs and kept current by Dependabot, run with least-privilege permissions, and define the required status checks (`ci / build`, `codeql`); the runner and SDK are pinned.

## Open questions
Answered during implementation:
- **CodeQL availability:** the repository is **private and user-owned**, so code scanning is not available. User decision: CodeQL still runs on every PR. While the repository is private, it uses `upload: never`, keeps the SARIF as an artifact and fails the job on error-level findings. Once the repository is public, results are uploaded automatically (ADR 0005).
- **CODEOWNERS owner:** `@KsiProgramming` is a user account, not an org, so it is the single owner.
- **`dotnet test` on an empty solution:** verified locally with SDK 10.0.301. Restore, build, test and format all exit 0, so no guard is needed.

Still open:
- **Test runner mode:** xUnit v3 on .NET 10 may run on Microsoft.Testing.Platform (`global.json` `"test": { "runner": … }`), which changes the `dotnet test` arguments. This is decided in 0.3; the CI test step is updated then.
