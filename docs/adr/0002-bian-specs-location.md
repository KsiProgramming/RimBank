# 0002. Location of the BIAN v14 specifications

- Status: Accepted
- Date: 2026-11-04
- Roadmap phase: 0.1

## Context

The BIAN v14 specifications (ISO 20022 extended DDD OpenAPI/AsyncAPI files and the semantic APIs) are the source of truth for RimBank (`specs/mission.md`, "Source of truth"). The constitution referred to them at `release14.0.0/` in the repository root, but they were committed under `doc-0/release14.0.0/`, next to other input material (`doc-0/ideas.MD`).

Changing `specs/mission.md` requires an ADR (ADR 0001).

## Options considered

1. **Move the specs to `/release14.0.0/`** to match the constitution. Keeps the documents unchanged, but splits the input material across two root folders and rewrites a large tree in history.
2. **Keep the specs in `doc-0/release14.0.0/`** and update the constitution. One folder holds all read-only input material.

## Decision

The BIAN v14 specifications stay in `doc-0/release14.0.0/`. `doc-0/` is the home of read-only input material. Its content is never edited, only replaced when a new BIAN release is adopted (which needs its own ADR).

The paths are updated in:

- `specs/mission.md` ("Source of truth");
- `specs/tech-stack.md` §3.3 (repository layout);
- `.claude/skills/plan-feature/SKILL.md` (step 1);
- `README.md`.

## Consequences

- Conformance tests (roadmap 0.14) load the YAML files from `doc-0/release14.0.0/apis-iso20022_ext-ddd/`.
- All references to the specs use the `doc-0/release14.0.0/` prefix.
