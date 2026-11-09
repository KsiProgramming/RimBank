---
name: open-phase-pr
description: Open the pull request for a finished roadmap phase in this repo, with the RimBank conventions done for you. Verifies the branch locally, pushes it, writes the PR body from the template and the planning package, closes the phase issue, sets the labels, milestone, assignee and "RimBank roadmap" project, and syncs the board (cards to In Progress). Use this whenever the user asks to open, create or raise the PR for a phase or a plan/<phase>-* branch, to "ship", "push and open the PR", or to sync the board and labels for a phase, even if they don't say "open-phase-pr".
---

# Open the PR for a roadmap phase

A phase PR in RimBank is more than `gh pr create`. It closes the synced phase issue, carries the label taxonomy, sits in the stage milestone and on the **RimBank roadmap** board, and its body follows `.github/pull_request_template.md`. All of that comes from repo conventions (CONTRIBUTING "Roadmap board" and "Labels"), so get it right every time without asking the user.

## 1. Identify the phase

- The branch is `plan/<phase>-<slug>` (for example `plan/1.2-party-entry-aggregate`). Take `<phase>` from it, or ask if the branch does not match.
- Find the planning package: `specs/*/requirements.md` whose `Roadmap phase:` line is `<phase>`. Read its `requirements.md`, `plan.md` and `validation.md`. The PR body is built from them.
- Find the phase issue. Synced issues carry the marker `<!-- roadmap-phase: <phase> -->`:

  ```bash
  gh issue list --search "roadmap-phase: <phase> in:body" --state all --json number,title,labels,milestone
  ```

  Note its number, labels and milestone. If no issue exists, the board was never synced: tell the user and continue without `Closes #`.

## 2. Check the branch is ready

Stop and report if any check fails. Never open a PR on a red local build.

- `git status --porcelain` is clean (or commit what belongs to the phase first, if the user asked for it).
- The roadmap checkbox of `<phase>` is ticked in `specs/roadmap.md` on this branch (Definition of done).
- Lock files. A package change in a test or src project must update every lock file that depends on it, including the per-runtime ones of `RimBank.Architecture.Tests`, `RimBank.AppHost` and `RimBank.AppHost.Tests` (`packages.win-x64.lock.json` *and* `packages.linux-x64.lock.json`; CONTRIBUTING "Packages"). On Windows, a restore can rewrite unrelated lock files with CRLF only: `git diff -w` shows nothing, so revert those with `git checkout`.
- Run, and keep the numbers for the PR body:

  ```bash
  dotnet restore RimBank.slnx --locked-mode
  dotnet build RimBank.slnx -c Release                 # 0 warnings, 0 errors
  dotnet test --solution RimBank.slnx -c Release --no-build   # long: Testcontainers; run in the background
  dotnet format RimBank.slnx --verify-no-changes; echo $?    # check the real exit code, not a pipe's
  ```

  If `scripts/` changed, also run `python -m unittest discover -s scripts`.

- **Documentation.** Invoke the `update-docs` skill on the branch, every time, before pushing. It maps the diff to `docs/architecture/*` and `docs/business/*`, updates them, renders every Mermaid diagram and checks the links. It then commits the documentation changes on the branch. Keep its report for the PR body. A phase is not ready if a diagram fails to render or a document still describes the old behaviour.

## 3. Push and write the body

- `git push -u origin <branch>`.
- Write the body to a file in the scratchpad, following `.github/pull_request_template.md` section by section:
  - **Roadmap phase:** `Phase: <number> <title>`, `Planning package: specs/<folder>/`, then `Closes #<issue>`.
  - **Summary:** what changed and why, in business language, grouped by type or concern. Name the commands vs queries (CQRS) and any ADR written or amended.
  - **Definition of done:** copy every item from the template. Tick what is met, write `N/A: <reason>` for items that do not apply, and leave **CI green** unticked (CI has not run yet).
  - **Validation:** the commands from step 2 with their results (test counts), and the manual or break-on-purpose checks from `validation.md` that were run. Add a **Documentation** line from the `update-docs` report: the documents updated and the diagram render count, or "No documentation impact" with the reason.
  - **Review focus:** the decisions a reviewer should challenge, and the open questions from `requirements.md`.
  - End with the attribution line from the session's system reminder, if there is one.
- Title: imperative, describes the change, ends with `(roadmap <phase>)`. Look at `gh pr list --state merged --limit 5` for the house style.

## 4. Create the PR with its metadata

Labels: take the phase issue's labels **minus `roadmap-phase`** (that one is for synced issues only). The result must have exactly one `type:`, the relevant `area:` labels, at most one `context:` and the `stage:N` label. Add an `area:` the work clearly touched that the issue lacks (for example `area:architecture` if architecture rules changed). Only use labels from `LABELS` in `scripts/sync_roadmap_board.py`; never create one by hand.

```bash
gh pr create --base main --head <branch> --title "<title>" --body-file <body file> \
  --label "stage:N" --label "type:..." --label "area:..." --label "context:..." \
  --milestone "<the issue's milestone title>" \
  --assignee <repo owner login> --project "RimBank roadmap"
```

## 5. Sync the board

The roadmap is mirrored by `scripts/sync_roadmap_board.py`, and the `roadmap-board` workflow runs it after the merge. Before the merge, the board only needs both cards in **In Progress**:

```bash
gh project list --owner <owner> --format json                       # the "RimBank roadmap" number and id
gh project field-list <number> --owner <owner> --format json        # the Status field id and its "In Progress" option id
gh project item-list <number> --owner <owner> --limit 500 --format json \
  -q '.items[] | select(.content.number==<issue> or .content.number==<pr>) | "\(.id) \(.content.type) \(.status)"'
gh project item-edit --project-id <project id> --id <item id> \
  --field-id <status field id> --single-select-option-id <in progress option id>
```

Move both the phase issue's card and the PR's card to In Progress if they are not there already.

**Never run `sync_roadmap_board.py` without `--dry-run` from a phase branch.** The phase is already ticked on the branch, so the sync would close the issue and move its card to Done before the PR is merged. Run `python scripts/sync_roadmap_board.py --dry-run` only, to confirm it would set `<phase>` to Done after the merge and nothing else unexpected. If the dry run shows unrelated drift, mention it to the user.

## 6. Verify and report

```bash
gh pr view <pr> --json labels,milestone,assignees,projectItems,closingIssuesReferences
```

Check that the labels, milestone, assignee, `RimBank roadmap=In Progress` and `closes: <issue>` are all set. Then give the user the PR URL and one line each on labels, milestone, board state and the issue it closes. Say that the CI item stays unticked until GitHub Actions is green, and offer to watch the CI run.
