# Plan: CI pipeline

Each group is one commit. The repository stays green after each one.

## 1. CI workflow (build, test, format)
- [ ] `.github/workflows/ci.yml`, named `ci`, with a job `build` on `ubuntu-24.04`
  - [ ] Triggers: `push` to `main`, `pull_request`, `workflow_dispatch`
  - [ ] Top-level `permissions: contents: read`; `concurrency: ci-${{ github.ref }}`, with `cancel-in-progress` for PRs only
  - [ ] Env: `DOTNET_NOLOGO`, `DOTNET_CLI_TELEMETRY_OPTOUT`, `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` set to `true`
  - [ ] `actions/checkout` (SHA-pinned, `persist-credentials: false`)
  - [ ] `actions/setup-dotnet` (SHA-pinned, `global-json-file: global.json`)
  - [ ] `actions/cache` on `~/.nuget/packages`, key `nuget-${{ runner.os }}-${{ hashFiles('Directory.Packages.props', '**/*.csproj') }}`
  - [ ] `dotnet restore RimBank.slnx`
  - [ ] `dotnet build RimBank.slnx -c Release --no-restore` (the `CI` env makes `ContinuousIntegrationBuild=true` through `Directory.Build.props`)
  - [ ] `dotnet test RimBank.slnx -c Release --no-build --logger trx --results-directory TestResults` (guard with `hashFiles('**/*.csproj')` only if it fails on the empty solution; see the open question)
  - [ ] `dotnet format RimBank.slnx --verify-no-changes --no-restore`
  - [ ] `actions/upload-artifact` (SHA-pinned) for `TestResults/`, `if: always()`, `if-no-files-found: ignore`
- [ ] Push the branch and check that the run is green

## 2. Dependabot
- [ ] `.github/dependabot.yml` (version 2)
  - [ ] `nuget`, directory `/`, weekly; group `nuget-minor-patch` (`update-types: [minor, patch]`); commit prefix `deps`
  - [ ] `github-actions`, directory `/`, weekly; group `actions` (all updates); commit prefix `ci`
  - [ ] `dotnet-sdk`, directory `/`, weekly (updates `global.json`); commit prefix `deps`
  - [ ] `open-pull-requests-limit: 5` each; `labels: [dependencies]`
- [ ] Check that GitHub accepts the config (no error under Insights → Dependency graph → Dependabot)

## 3. CodeQL
- [ ] `.github/workflows/codeql.yml`, named `codeql`
  - [ ] Triggers: `push` to `main`, `pull_request`, `schedule` (weekly cron), `workflow_dispatch`
  - [ ] Job `analyze` with matrix `language: [actions, csharp]`; permissions `contents: read`, `security-events: write`, `actions: read`
  - [ ] `github/codeql-action/init` (SHA-pinned), `queries: security-and-quality`
  - [ ] csharp leg: `build-mode: manual`, then `setup-dotnet` + `dotnet build RimBank.slnx -c Release`. The init, build and analyze steps are guarded by `if: matrix.language != 'csharp' || hashFiles('**/*.csproj') != ''`
  - [ ] actions leg: `build-mode: none`
  - [ ] `github/codeql-action/analyze` with `category: /language:${{ matrix.language }}`
- [ ] Check that both legs are green on the PR (csharp shows skipped steps) and that `actions` results appear under Security → Code scanning

## 4. Ownership, docs, ADR and roadmap
- [ ] `.github/CODEOWNERS`: `* @KsiProgramming` (confirm the owner; see the open question)
- [ ] `docs/adr/0005-ci-and-supply-chain-policy.md` (MADR, from `docs/adr/template.md`, status Accepted)
- [ ] `README.md`: CI and CodeQL status badges under the title
- [ ] `CONTRIBUTING.md`, "Building and testing": the exact commands CI runs; required checks `ci / build` and `codeql / analyze (actions)` and `(csharp)`; the branch-protection settings for maintainers (require PR, require the checks, dismiss stale reviews, no force-push to `main`); the rule that new actions must be SHA-pinned
- [ ] `specs/roadmap.md`: tick **0.2 CI pipeline**
