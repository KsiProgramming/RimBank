# 0001. Record architecture decisions

- Status: Accepted
- Date: 2026-11-04
- Roadmap phase: 0.1

## Context

RimBank is an open-source core banking platform that turns the BIAN v14 specifications into running software. Many choices are not dictated by BIAN: how a Behavior Qualifier maps to an aggregate, which libraries we use, how errors are shaped, and how the constitution in `specs/` evolves. Contributors and adopters need to know *why* the code looks the way it does, and reviewers need a single place to challenge a decision.

The constitution already requires an ADR for changes to `specs/mission.md`, for adding or replacing a technology (`specs/tech-stack.md`), and for significant or BIAN-interpretation decisions (Definition of done in `specs/roadmap.md`).

## Options considered

1. **No formal records**: decisions live in PR descriptions and chat. Cheap, but the rationale is lost and hard to find.
2. **Wiki pages**: outside the repository, so they drift from the code and are not reviewed with it.
3. **Architecture Decision Records in the repository** (lightweight [MADR](https://adr.github.io/madr/) format): versioned with the code and reviewed in the same PR.

## Decision

We record architecture decisions as Markdown ADRs in `docs/adr/`, using [template.md](template.md).

- **File name:** `NNNN-kebab-case-title.md`, numbered sequentially from `0001` and never reused.
- **Status:** `Proposed` while in review, `Accepted` when merged. An ADR is never edited to reverse its decision; a new ADR supersedes it, and the old one's status becomes `Superseded by NNNN`.
- **An ADR is required for:**
  - any change to `specs/mission.md`;
  - adding, replacing or removing a technology listed in `specs/tech-stack.md`;
  - every BIAN → DDD mapping and every interpretation where BIAN is ambiguous or underspecified;
  - modelling a Behavior Qualifier as its own aggregate instead of an entity;
  - synchronous cross-context calls;
  - any other decision a reviewer considers significant.
- The ADR is written in the same PR as the change it justifies.

## Consequences

- Every significant decision has a short, reviewable rationale next to the code.
- The PR template's Definition of done checklist reminds contributors to add an ADR when needed.
- Writing ADRs adds a little work to each phase, which is the point: it keeps phases small and deliberate (mission principle 9).
