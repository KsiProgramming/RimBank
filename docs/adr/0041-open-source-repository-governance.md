# 0041. Open-source repository governance

- Status: Accepted
- Date: 2026-11-10
- Roadmap phase: n/a

## Context

RimBank was always meant to be an MIT open-source project ([mission](../../specs/mission.md)), but until now the repository was private and owned by a user account. [ADR 0005](0005-ci-and-supply-chain-policy.md) designed the pipeline for that state: CodeQL kept its SARIF as an artifact, and the dependency-review action was left "for later". Making the repository public changes four things:

- **Anyone can open a pull request.** The single maintainer must stay the only person who can change `main`, and every change, the maintainer's included, must pass the same checks.
- **Public-only security features become free:** code scanning uploads, secret scanning with push protection, private vulnerability reporting (which [SECURITY.md](../../SECURITY.md) already points to), the dependency graph and dependency review.
- **Redistribution becomes visible.** `doc-0/release14.0.0/` copies the BIAN Semantic APIs, which BIAN publishes under the Apache License 2.0. Apache 2.0 allows redistribution, but the licence text and attribution must go with the files.
- **Contributors and adopters need to know** who decides, where to ask and what changed.

## Options considered

1. **Classic branch protection, maintainer can push directly.** Simple. But the maintainer would skip the checks that contributors face, and classic protection is less expressive than rulesets.
2. **A `main` ruleset that requires a pull request and code-owner approval, with the repository admin allowed to bypass only inside a pull request.** Contributors' PRs need the maintainer's approval. The maintainer cannot approve their own PR, so they can merge it without an approval, but only through a PR and only once the required checks are green. Nobody, admin included, can push directly, force-push or delete `main`.
3. **Require approval from a second person for every PR.** The strongest review guarantee, but impossible with one maintainer.

## Decision

Option 2, together with the public-repository set-up below.

- **Ruleset `main`** (target: the default branch): pull request required, 1 approval, stale approvals dismissed on push, code-owner review required, review threads resolved, linear history, no force push, no deletion. Required status checks, with branches up to date: `build`, `analyze (actions)`, `analyze (csharp)`, `dependency-review`. Bypass: the repository admin role, in pull requests only.
- **Merge settings:** squash merge only (the PR title becomes the commit message, matching CONTRIBUTING's imperative-mood rule), branches deleted after merge, "update branch" offered.
- **Security settings:** secret scanning with push protection, private vulnerability reporting, Dependabot alerts and security updates, and CodeQL results uploaded to code scanning (already automatic in `codeql.yml`).
- **Fork pull requests:** workflows from outside collaborators wait for the maintainer's approval before they run. The default `GITHUB_TOKEN` stays read-only, and no workflow uses `pull_request_target`.
- **New workflows**, under ADR 0005's rules (SHA-pinned, `contents: read` at the top, skipped while the repository is private):
  - `dependency-review` blocks PRs that add a dependency with a vulnerability of moderate severity or worse, or a GPL/AGPL licence.
  - `scorecard` runs OpenSSF Scorecard weekly and on every push to `main`. It publishes the score (README badge) and uploads findings to code scanning.
- **Community files:** [GOVERNANCE.md](../../GOVERNANCE.md) (single-maintainer model and how it grows), [SUPPORT.md](../../SUPPORT.md) (Discussions for questions), [CHANGELOG.md](../../CHANGELOG.md) (Keep a Changelog) with generated release notes grouped by `type:` label ([.github/release.yml](../../.github/release.yml)), [CITATION.cff](../../CITATION.cff), and Discussions enabled.
- **Third-party material:** BIAN's Apache 2.0 licence is copied to [doc-0/release14.0.0/LICENSE](../../doc-0/release14.0.0/LICENSE), and [THIRD-PARTY-NOTICES.md](../../THIRD-PARTY-NOTICES.md) records the source, the licence, that the files are unmodified, and that RimBank is not affiliated with BIAN.

Option 2 wins because it treats the maintainer's code exactly like a contributor's: the same checks, always through a PR. The only exception is the approval nobody else can give yet.

## Consequences

- When a second maintainer joins, remove the admin pull-request bypass, so every PR needs another person's approval. Update CODEOWNERS and GOVERNANCE.md in the same PR.
- A new required check means updating both the ruleset and the list in CONTRIBUTING.md ("Branch protection").
- Scorecard needs its workflow to keep its minimal shape (no top-level `env` or `defaults`, only the actions it allows), or publishing fails.
- Because of the GPL/AGPL rule, a dependency under those licences needs an ADR and an explicit allow entry in `dependency-review.yml`.
- Commit author emails in the existing history become public with the repository.
- SBOM generation, signed releases and container images stay in roadmap 7.5.
