# Validation: Repository skeleton

## Done when (from roadmap)
> **0.1 Repository skeleton:** `RimBank.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`, `.gitignore`, MIT `LICENSE`, `README.md`, `docs/adr/0001-record-architecture-decisions.md`.
> + Definition of done (roadmap.md)

Applying the Definition of done to this phase: there is no C# code, so unit, integration, architecture, Problem Details and MCP parity items do not apply. ADRs and the roadmap tick do apply. CI does not exist until 0.2, so the **user chose to defer automated gates to 0.2**. 0.2 will run build and format over everything that 0.1 added.

## Automated checks (run locally)
- [ ] `dotnet --version` from the repository root prints `10.0.3xx` (matches `global.json`)
- [ ] `dotnet build RimBank.slnx` exits 0 with 0 errors. *Finding during implementation:* with no projects, NuGet restore prints one warning ("Nothing to restore"). This is expected until 0.3 adds the first project. From then on the build must have 0 warnings.
- [ ] `dotnet format RimBank.slnx --verify-no-changes` exits 0
- [ ] `dotnet sln RimBank.slnx list` succeeds (reports no projects)
- [ ] `git ls-files --eol` shows `i/lf` for all new text files

## Manual / demo checks (reviewer checklist)
- [ ] `LICENSE` is the unmodified MIT text with "Copyright (c) 2026 RimBank contributors"
- [ ] `Directory.Build.props` contains `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `ImplicitUsings=enable` (tech-stack §1)
- [ ] `Directory.Packages.props` enables Central Package Management and lists no packages
- [ ] `RimBank.slnx` has `src` and `tests` folders; `src/` and `tests/` exist on disk
- [ ] `README.md` links to `specs/mission.md`, `specs/tech-stack.md`, `specs/roadmap.md`, `docs/adr/`, `doc-0/release14.0.0/`, and every link resolves on GitHub
- [ ] ADR 0001 and ADR 0002 follow `docs/adr/template.md` and have status Accepted
- [ ] `grep -rn "release14.0.0" specs .claude README.md` shows only `doc-0/release14.0.0` paths
- [ ] `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md` and the `.github/` templates show up correctly in the GitHub "Community standards" page (Insights → Community)
- [ ] The PR template contains the full Definition of done checklist from roadmap.md
- [ ] No secrets, tokens or personal data in any file

## Merge criteria
- [ ] All local checks above pass (CI gate deferred to 0.2)
- [ ] Roadmap checkbox **0.1** ticked in the same PR
- [ ] ADR 0001 (record architecture decisions) and ADR 0002 (BIAN specs location) written
- [ ] Constitution paths updated consistently (mission.md, tech-stack.md, plan-feature skill)
- [ ] PR reviewed and approved by a maintainer
