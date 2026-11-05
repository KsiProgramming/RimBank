# 0005. CI and supply-chain policy

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: 0.2

## Context

Every roadmap phase must leave GitHub Actions CI green (build, test, format, CodeQL), and `specs/tech-stack.md` §1 and §10 require CodeQL, Dependabot and secret scanning. The workflows themselves are part of the supply chain. A third-party action referenced by a movable tag (`@v4`) can change under us, and a workflow token with broad permissions widens the blast radius of any compromised step. A core banking platform has to treat its pipeline with the same care as its code (mission principle 9).

Two facts shape the first version of the pipeline:

- The solution has no projects until 0.3, so CodeQL's C# analysis has nothing to scan, and adding a placeholder project would be speculative scaffolding (mission non-goals).
- The repository is currently private and owned by a user account, so GitHub code scanning (uploading CodeQL results) is not available.

## Options considered

1. **Tag-pinned actions, default permissions, CodeQL only when public.** Simplest. But tags are mutable, the default token can write, and there is no static analysis until the repository is public.
2. **SHA-pinned actions, least privilege, CodeQL always run.** Actions are pinned to full commit SHAs, and Dependabot keeps the pins current. CodeQL runs on every PR. Where results cannot be uploaded, the job keeps the SARIF as an artifact and fails on error-level findings.
3. **Reusable workflows and an OS matrix now.** More structure. Nothing on the roadmap needs it before the release work in 7.5.

## Decision

Option 2.

- **Pinning:** every `uses:` refers to a full 40-character commit SHA followed by a `# vX.Y.Z` comment. Dependabot's `github-actions` updater moves the pins. The runner image (`ubuntu-24.04`) and the SDK (`global.json`, also updated by Dependabot's `dotnet-sdk` updater) are pinned too.
- **Least privilege:** each workflow declares top-level `permissions: contents: read` and widens it only per job (CodeQL: `security-events: write`, `actions: read`). Checkout uses `persist-credentials: false`. Workflows use no secrets other than the default `GITHUB_TOKEN`.
- **Workflows:** `ci` (job `build`: restore, Release build, test, `dotnet format --verify-no-changes`) and `codeql` (job `analyze` for `actions` and `csharp`, with the `security-and-quality` suite). C# analysis is skipped until the solution contains a `.csproj`. While the repository is private, results are kept as a SARIF artifact and error-level findings fail the job. Once it is public, results are uploaded to code scanning with no workflow change.
- **Dependencies:** Dependabot checks `nuget`, `github-actions` and `dotnet-sdk` weekly. Minor and patch updates are grouped, and major updates arrive as separate PRs, so a major version of a stack technology can be weighed (and get an ADR if needed).
- **Required checks:** `build` (from `ci`), `analyze (actions)` and `analyze (csharp)` (from `codeql`) must pass before merging to `main`. Branch protection is configured by a maintainer as described in `CONTRIBUTING.md`.

## Consequences

- Contributors who add an action must pin it to a SHA. Review checks this, and `validation.md` for 0.2 gives a grep for it.
- From 0.3 onwards the C# CodeQL leg and the test step do real work automatically. The first PR with a `.csproj` should confirm this.
- The test step uses `--logger trx`. If 0.3 moves `dotnet test` to Microsoft.Testing.Platform, that step is updated in 0.3.
- While the repository is private, CodeQL findings are only visible in the job log and the SARIF artifact, not in the Security tab. Making the repository public (as an MIT open-source project) turns on the full code-scanning experience. Check that CodeQL's licence terms allow its use on this repository while it is private.
- SBOM generation and signed releases stay in 7.5. Coverage, an OS matrix and the dependency-review action can be added later without changing this policy.
