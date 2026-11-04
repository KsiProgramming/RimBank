# Repository skeleton

Roadmap phase: 0.1
Branch: plan/0.1-repo-skeleton

## Context
This is the first phase of Stage 0. Nothing comes before it, and every later phase builds on it: 0.2 CI needs a solution to build, and 0.3 onwards add projects that rely on the shared build settings and Central Package Management.

Relevant constitution:
- [tech-stack.md §1](../tech-stack.md): .NET 10 LTS / C# 14 pinned with `global.json`; `RimBank.slnx`; `Directory.Build.props` with `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `ImplicitUsings=enable`; Central Package Management with no versions in `.csproj` files.
- [tech-stack.md §3.3](../tech-stack.md): repository layout (`src/`, `tests/`, `docs/adr/`, BIAN specs as read-only input).
- [mission.md](../mission.md), "License and community": MIT, contributions follow `specs/`, the ADR process and the Definition of done. Principle 9 (small, verifiable steps).

The BIAN v14 specs are in `doc-0/release14.0.0/`, but mission.md and tech-stack.md §3.3 say `/release14.0.0/`. **The user decided to keep `doc-0/`** and correct the constitution. mission.md changes need an ADR.

## Scope
### In scope
- `global.json` pinning the .NET 10 SDK (installed: 10.0.301).
- `Directory.Build.props` with the four required settings, plus deterministic/CI-friendly defaults.
- `Directory.Packages.props` with `ManagePackageVersionsCentrally=true` and no packages yet.
- `.editorconfig` with C# style rules for file-scoped namespaces, `sealed`-friendly analyzers, LF and UTF-8, matching `.gitattributes`.
- `RimBank.slnx` with `src` and `tests` solution folders (no projects yet).
- Empty `src/` and `tests/` folders (`.gitkeep`).
- Keep the existing `.gitignore` and `.gitattributes`; check that they cover `.slnx`, `.props`, `.editorconfig` and the Aspire/Rider/VS Code artefacts.
- MIT `LICENSE`: "Copyright (c) 2026 RimBank contributors".
- `README.md`: mission in one paragraph, a BIAN → DDD summary, the layout, how to build, and links to `specs/`, `docs/adr/` and `doc-0/release14.0.0/`.
- `docs/adr/0001-record-architecture-decisions.md`, plus `docs/adr/template.md` (MADR).
- `docs/adr/0002-bian-specs-location.md`: keep the BIAN specs under `doc-0/release14.0.0/`, and update the paths in `specs/mission.md`, `specs/tech-stack.md` §3.3 and `.claude/skills/plan-feature/SKILL.md`.
- Community files (broader skeleton): `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md` (Contributor Covenant 2.1), `SECURITY.md` (private vulnerability reporting through GitHub), `.github/pull_request_template.md` (with the Definition of done checklist), and `.github/ISSUE_TEMPLATE/` (bug report, feature/phase request, `config.yml`).

### Out of scope
- Any C# project, including SharedKernel (0.3–0.6) and Architecture.Tests (0.7).
- GitHub Actions, Dependabot and CodeQL (0.2).
- Aspire AppHost/ServiceDefaults (0.8).
- `CODEOWNERS` and branch protection rules (to do with 0.2 if needed).

## Decisions
| Decision | Choice | Rationale |
|---|---|---|
| Scope breadth | Listed files + community files and templates | User choice; contribution rules from mission "License and community" are visible from day one |
| SDK pin | `10.0.301`, `rollForward: latestFeature`, `allowPrerelease: false` | tech-stack §1 (.NET 10 LTS); allows feature-band updates without breaking local builds |
| BIAN spec location | Stay in `doc-0/release14.0.0/`; fix the constitution paths | User choice; needs ADR 0002 because mission.md changes |
| ADR format | MADR (lightweight Markdown) | Common, short, and works well for reviews |
| License holder | "RimBank contributors" | User choice; fits an open-source community project |
| Projects in 0.1 | None; empty slnx with solution folders | Keeps the phase small; SharedKernel starts in 0.3 |
| Line endings | LF in repo (existing `.gitattributes`), `end_of_line = lf` in `.editorconfig` | Consistent with what is already committed |

## ADRs to write
- 0001: Record architecture decisions (MADR template, the process, and when an ADR is required: see the constitution file headers).
- 0002: Location of the BIAN v14 specifications (`doc-0/release14.0.0/`) and the constitution update.

## Open questions
- Should `doc-0/ideas.MD` stay in `doc-0/` or move to `docs/`? It is left as is for now.
- Does an empty `.slnx` (no projects) build cleanly with SDK 10.0.301? If not, `dotnet build` is replaced by `dotnet sln list` plus a format check until 0.3 (see validation.md).
