# 0013. Warning, package and roadmap-board governance

- Status: Accepted
- Date: 2026-11-05
- Roadmap phase: n/a (cross-cutting, after 0.7)

## Context

Three gaps showed up once the foundations (0.1–0.7) were in place.

- **Warnings.** `TreatWarningsAsErrors` is on, but nothing stopped a warning from being switched off quietly. The repository already had nine `[SuppressMessage]` attributes and `#pragma warning disable` blocks spread through the code, plus a solution-wide `<NoWarn>CS1591</NoWarn>`. In-code suppressions are hard to review as a set, easy to copy and paste, and invisible when you read the build configuration.
- **Packages.** Central Package Management was on, but restores were not reproducible: transitive versions could change between two restores, machine-level NuGet sources could take part in a restore, and nothing failed the build on a package with a known vulnerability. For a core banking platform these are supply-chain risks (mission principle 9, ADR 0005).
- **Roadmap visibility.** `specs/roadmap.md` is the source of truth, but it gives no view of what is in progress and cannot be linked from issues and pull requests.

## Options considered

For warnings:

1. **Keep in-code suppressions, with justifications.** This is the status quo, and it is what drifts.
2. **A root `.globalconfig`.** Central, but one file for the whole solution pushes towards broad exceptions.
3. **Nested `.editorconfig` files.** Each project (or `tests/` as a whole) carries an `.editorconfig` next to its code, with severity entries scoped to the exact files they cover and the reason beside each entry. A build guard forbids the other mechanisms.

For packages:

1. **CPM only** (the status quo).
2. **CPM plus lock files, locked restore in CI, NuGet audit, a cleared source list with package source mapping, and no `VersionOverride`.**
3. **Option 2 plus signed-package verification** (`signatureValidationMode=require`). This needs a list of trusted signers that has to be maintained for every new package author. It is deferred to the release work in 7.5.

For the roadmap board:

1. **Draft items on a Projects board.** Lightweight, but pull requests cannot link or close them.
2. **One issue per phase, one milestone per stage, and a Projects board, all generated from the roadmap by a script.** This is the convention already used in the maintainers' other repositories.

## Decision

Option 3 for warnings, option 2 for packages, option 2 for the board.

**Warnings**

- Every warning is an error, including MSBuild warnings (`MSBuildTreatWarningsAsErrors`).
- A diagnostic's severity is changed only by a `dotnet_diagnostic.<ID>.severity` entry in an `.editorconfig`. Each entry is scoped as narrowly as possible (a file, or a set of files) and has a comment giving the reason. The root `.editorconfig` holds the rules for the whole solution, `tests/.editorconfig` holds the exceptions shared by all test projects (CA1707, CS1591), and a project's own `.editorconfig` holds that project's exceptions.
- `Directory.Build.targets` fails the build with **RB0001** for `#pragma warning disable`, `[SuppressMessage]` or `[UnconditionalSuppressMessage]` in hand-written code (generated files under `obj/` are skipped), and with **RB0002** for `<NoWarn>` or `<WarningsNotAsErrors>` in a project file.
- CS1591 (a missing XML doc comment) is now enforced for production code under `src/`, and relaxed only for tests.

**Packages**

- Versions live only in `Directory.Packages.props`. Packages are grouped by concern, and packages of the same family share a version property. `CentralPackageVersionOverrideEnabled=false`, so NuGet rejects a `VersionOverride` (NU1013). Transitive pinning stays on.
- Every project commits a `packages.lock.json` (`RestorePackagesWithLockFile`). CI restores with `--locked-mode`, and any build where `CI` or `GITHUB_ACTIONS` is `true` sets `RestoreLockedMode`.
- NuGet audit covers direct and transitive packages (`NuGetAuditMode=all`) at every severity (`NuGetAuditLevel=low`). Its warnings are errors, so a newly published advisory fails the build until the package is raised in `Directory.Packages.props`.
- `NuGet.config` clears every inherited source, uses only nuget.org, and maps every package to it (package source mapping).
- The product version is set once, in `Directory.Build.props` (`VersionPrefix` 0.1.0, `VersionSuffix` alpha until roadmap 7.10).
- Settings shared by all test projects live in `tests/Directory.Build.props`, which imports the root one.

**Roadmap board**

- `scripts/sync_roadmap_board.py` (standard library only, plus the `gh` CLI) mirrors `specs/roadmap.md`. It creates one milestone per stage, one issue per phase (keyed by a `<!-- roadmap-phase: N.M -->` marker; growth-backlog bullets are keyed by their text), the `roadmap-phase` and `stage:N` labels, and the "RimBank roadmap" Projects board linked to the repository. A ticked phase closes its issue and moves its card to Done. A card moved to In Progress by a person stays there.
- The roadmap remains the source of truth: issue titles and bodies are overwritten on every sync. Discussion belongs in comments.
- The `roadmap-board` workflow runs the sync on every push to `main` that changes the roadmap. **This amends ADR 0005** ("workflows use no secrets other than `GITHUB_TOKEN`") for this one job only. The default token cannot write to a user-owned Projects board, so the job reads a `ROADMAP_BOARD_TOKEN` secret with the `repo` and `project` scopes. It runs only on `main` (never on pull requests from forks), checks out without persisting credentials, and does nothing if the secret is missing.

## Consequences

- To silence a diagnostic, a contributor edits an `.editorconfig`, and the change shows up in review as configuration with a reason, not hidden in a code diff. Silencing a diagnostic for a whole project or for `src/` deserves the same scrutiny as an ADR.
- Adding or upgrading a package changes `Directory.Packages.props` and the affected `packages.lock.json` files in the same PR. Run `dotnet restore` locally and commit the lock files, or CI fails in locked mode. Dependabot updates the lock files too.
- A new vulnerability advisory can turn `main` red without any code change. That is intended: the fix is a version raise in `Directory.Packages.props`.
- Contributors who tick a roadmap phase see the board follow after the merge (or after `python scripts/sync_roadmap_board.py` if the secret is not configured). Board views (for example grouping by milestone) are configured once in the GitHub UI; the API cannot create them.
- Signed-package verification, SBOMs and signed releases stay in 7.5.
