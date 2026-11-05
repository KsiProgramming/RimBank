# Validation: CI pipeline

## Done when (from roadmap)
> **0.2 CI pipeline:** GitHub Actions workflow for restore, build, test and `dotnet format --verify-no-changes`; Dependabot; CodeQL.
> + Definition of done (roadmap.md)

How the Definition of done applies to this phase: there is no C# code, so unit, integration, read-model, architecture, Problem Details and MCP parity items do not apply. "CI green", "ADR for significant decisions" and "roadmap tick" do apply. As the user chose, the proof is **a green run on this phase's own PR**.

## Automated checks
- [ ] Workflow `ci`, job `build`, is green on the 0.2 PR, and the log shows these steps ran: restore, build (Release), test, `dotnet format --verify-no-changes`
- [ ] Workflow `codeql` is green on the 0.2 PR for both matrix legs: `actions` analyses the workflows; `csharp` passes with its analysis steps skipped (no `.csproj` yet)
- [ ] Both workflows also run on `push` to `main` after merge (check the first run on `main`)
- [ ] The Dependabot config is accepted: no error under Insights → Dependency graph → Dependabot, and the `nuget`, `github-actions` and `dotnet-sdk` updaters are listed
- [ ] Every `uses:` in `.github/workflows/*.yml` points to a 40-character commit SHA with a version comment: `grep -nE "uses: [^@]+@" .github/workflows/*.yml | grep -vE "@[0-9a-f]{40} # v"` returns nothing
- [ ] Every workflow declares top-level `permissions:` and none uses `write-all`

## Manual / demo checks
- [ ] Locally, from the repo root, the commands listed in CONTRIBUTING give the same result as CI:
  `dotnet restore RimBank.slnx && dotnet build RimBank.slnx -c Release --no-restore && dotnet test RimBank.slnx -c Release --no-build && dotnet format RimBank.slnx --verify-no-changes --no-restore` → exit 0 (the "Nothing to restore" warning is expected until 0.3, as recorded in 0.1)
- [ ] While private: the `codeql-sarif-actions` artifact exists on the PR run, and the "Fail on error-level findings" step reports 0. Once public: Security → Code scanning shows a completed `actions` analysis with no open high-severity alerts
- [ ] The README badges render and link to the workflow runs
- [ ] Note for 0.3 (not a merge blocker): the first PR that adds a `.csproj` must show the csharp CodeQL leg actually analysing code, and the test step running tests

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox for 0.2 ticked in the same PR
- [ ] ADR 0005 (CI and supply-chain policy) written, with status Accepted
- [ ] No secrets in workflows (only `GITHUB_TOKEN` with least-privilege permissions); secret scanning enabled on the repository
- [ ] CONTRIBUTING documents the required checks and branch-protection settings; a maintainer has applied them (or a follow-up issue is open)
- [ ] Open questions in requirements.md answered (repository visibility for CodeQL, CODEOWNERS owner)
