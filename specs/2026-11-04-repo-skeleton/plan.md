# Plan: Repository skeleton

Order: build configuration first, then the solution, then docs (user choice). The repository stays consistent after each group.

## 1. SDK pin
- [ ] `global.json`: `sdk.version = 10.0.301`, `rollForward = latestFeature`, `allowPrerelease = false`
- [ ] Check `dotnet --version` from the repository root resolves to 10.0.3xx

## 2. Shared build settings and editor rules
- [ ] `Directory.Build.props`: `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `ImplicitUsings=enable`, `EnforceCodeStyleInBuild=true`, `Deterministic=true`, `ContinuousIntegrationBuild` when `CI=true`, `Authors`/`Company`/`Copyright` = RimBank contributors, `PackageLicenseExpression=MIT`
- [ ] `Directory.Packages.props`: `ManagePackageVersionsCentrally=true`, `CentralPackageTransitivePinningEnabled=true`, empty `ItemGroup` with a comment pointing to tech-stack.md
- [ ] `.editorconfig`: root, UTF-8, LF, final newline, 4-space C# / 2-space JSON-YAML-XML; C# rules for file-scoped namespaces, `var` preferences, primary constructors, expression bodies; naming rules (PascalCase types, `_camelCase` private fields, `I` prefix for interfaces); severity `warning` for style rules enforced in build
- [ ] Review the existing `.gitignore` (from `dotnet new gitignore`); add `.idea/`, `.vscode/*` except shared settings, `*.DotSettings.user` and `TestResults/` if missing

## 3. Solution
- [ ] `src/.gitkeep`, `tests/.gitkeep`
- [ ] `RimBank.slnx` with `/src/` and `/tests/` folders and a `/Solution Items/` folder for `global.json`, `Directory.*.props`, `.editorconfig`, `README.md`
- [ ] Run `dotnet build RimBank.slnx` and `dotnet format RimBank.slnx --verify-no-changes`; if an empty solution is rejected, record the fallback in validation.md (see the open question)

## 4. License, README and ADRs
- [ ] `LICENSE`: MIT text, "Copyright (c) 2026 RimBank contributors"
- [ ] `README.md`: mission summary, BIAN → DDD mapping table (service domain → bounded context, Control Record → aggregate root, BQ → entity, operation → command), repository layout, prerequisites (.NET SDK from `global.json`), build commands, links to `specs/`, `docs/adr/`, `doc-0/release14.0.0/`, a roadmap status line, and the license
- [ ] `docs/adr/template.md` (MADR: Status, Context, Decision, Consequences)
- [ ] `docs/adr/0001-record-architecture-decisions.md` (Accepted): why ADRs, MADR format, numbering, file naming `NNNN-kebab-title.md`, when one is required (constitution changes, technology changes, BIAN interpretations, BQ-as-aggregate)
- [ ] `docs/adr/0002-bian-specs-location.md` (Accepted): BIAN v14 specs stay in `doc-0/release14.0.0/` as read-only input
- [ ] Update the paths: `specs/mission.md` (Source of truth, 2 lines), `specs/tech-stack.md` §3.3 layout tree, `.claude/skills/plan-feature/SKILL.md` step 1

## 5. Community files and GitHub templates
- [ ] `CONTRIBUTING.md`: workflow (one roadmap phase = one PR, `plan/<phase>-<slug>` planning branches, `specs/YYYY-MM-DD-*` packages), the Definition of done, the ADR rule, the business-language naming rule, and how to build and test
- [ ] `CODE_OF_CONDUCT.md`: Contributor Covenant 2.1, with a contact through GitHub private reporting or maintainers
- [ ] `SECURITY.md`: supported versions (none released yet), report through GitHub private vulnerability reporting, no public issues, statement that RimBank is not a licensed bank (mission non-goals)
- [ ] `.github/pull_request_template.md`: roadmap phase, summary, the Definition of done checklist copied from roadmap.md
- [ ] `.github/ISSUE_TEMPLATE/bug_report.yml`, `.github/ISSUE_TEMPLATE/feature_request.yml` (roadmap phase or BIAN service domain field), `.github/ISSUE_TEMPLATE/config.yml` (blank issues off, security link)

## 6. Close the phase
- [ ] Tick **0.1** in `specs/roadmap.md`
- [ ] Run the validation checks and the manual checklist in validation.md
