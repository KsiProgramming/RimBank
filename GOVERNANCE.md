# Governance

RimBank is a young project with a single maintainer. This document says who decides what, and how that will change as the community grows.

## Roles

| Role | Who | Can |
|---|---|---|
| **Maintainer** | [@KsiProgramming](https://github.com/KsiProgramming) | Merge pull requests, cut releases, accept ADRs, change the constitution in [`specs/`](specs/), triage and moderate |
| **Contributor** | Anyone who opens an issue, discussion or pull request | Propose changes from a fork. Every change reaches `main` through a reviewed pull request |

The maintainer is the only person who can merge into `main`. The `main` branch is protected by a ruleset (see [CONTRIBUTING.md](CONTRIBUTING.md#branch-protection-maintainers)): no direct pushes, no force pushes, no deletion, required status checks, and code-owner approval for every pull request.

## How decisions are made

- **Day-to-day changes** are decided in pull request review, against the [mission](specs/mission.md), the [tech stack](specs/tech-stack.md) and the [Definition of done](specs/roadmap.md#definition-of-done-every-phase).
- **Significant or BIAN-interpretation decisions** are recorded as an [architecture decision record](docs/adr/). Anyone can propose one in a pull request. The maintainer accepts or rejects it, and gives the reasons in the ADR.
- **The roadmap** ([specs/roadmap.md](specs/roadmap.md)) is changed only by pull request. Proposals start as a [feature or roadmap proposal](https://github.com/KsiProgramming/RimBank/issues/new?template=feature_request.yml) or a [discussion](https://github.com/KsiProgramming/RimBank/discussions).
- When the maintainer and a contributor disagree, the constitution in `specs/` is the tie-breaker. If it is silent, the maintainer decides and records the decision in an ADR.

## Becoming a maintainer

Contributors who have landed several substantial pull requests, reviewed others' work and shown they follow the constitution may be invited to become maintainers. When that happens, this file, [CODEOWNERS](.github/CODEOWNERS) and the `main` ruleset are updated in the same pull request. Decisions will then need agreement between maintainers.

## Code of Conduct

Everyone in the project follows the [Code of Conduct](CODE_OF_CONDUCT.md). The maintainer enforces it.
