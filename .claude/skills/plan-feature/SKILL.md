---
name: plan-feature
description: Plan the next roadmap feature for this repo. Finds the next unplanned phase in specs/roadmap.md, creates a planning branch, interviews the user about the feature spec, then writes specs/YYYY-MM-DD-feature-name/ with plan.md, requirements.md and validation.md, guided by specs/mission.md and specs/tech-stack.md. Use this whenever the user wants to plan, spec out, scope or start the "next feature", "next phase" or "next roadmap item", or asks what to work on next and wants a plan written, even if they don't say "plan-feature".
---

# Plan the next roadmap feature

The goal is a small, reviewable planning package for one roadmap phase, written only after the user has shaped it. The files in `specs/` are the source of truth for RimBank. The plan must fit them, not invent its own direction.

## 1. Load context

Read these in full before doing anything else:

- `specs/roadmap.md`: the ordered stages and phases (for example **1.2 Aggregate**), plus the shared **Definition of done** at the top that applies to every phase.
- `specs/mission.md`: mission, BIAN source of truth, guiding principles, quality bar and non-goals. Use them to keep scope honest: small verifiable steps, business language only, money correctness, Problem Details everywhere, HTTP and MCP parity, and money-moving MCP tools off by default.
- `specs/tech-stack.md`: the chosen libraries, naming and namespace rules, dependency and CQRS rules (§4, §8), and the API/MCP/error contracts. Every plan must say which parts are commands (write side) and which are queries (read side), and for each read model whether its projection is inline or async. The plan must use these choices, not alternatives. Any deviation needs an ADR, and that ADR becomes part of the plan.
- For a phase that touches a BIAN service domain, also read the relevant spec in `doc-0/release14.0.0/apis-iso20022_ext-ddd/` (the OpenAPI under `oas3/yamls/` and the AsyncAPI under `asyncapi-3.x/yamls/`). It shows the Control Record, Behavior Qualifiers, operations and events in scope.

## 2. Find the next feature

Stages are worked in order (Stage 0, 1, 2, …), and phases within a stage in the order they are listed. Watch out: phases are not always listed in numeric order. If the roadmap has a "**Next up: Stage N**" line, the next feature is the **first unplanned phase of Stage N**. Once all of Stage N is planned, or if there is no such line, it is the **first unplanned phase in the file**.

A phase counts as planned when an existing `specs/YYYY-MM-DD-*/requirements.md` names it in its `Roadmap phase:` line, or when its checkbox in `roadmap.md` is already ticked (`- [x]`). Check those folders, and also look at `git branch -a` for `plan/<phase>-*` branches in case planning started elsewhere.

If an earlier phase is unplanned but a later one is planned, still pick the earliest one and mention the gap to the user. Note any phases the chosen one depends on that are not done yet, because that affects scope.

The roadmap has no per-phase "Done when" line. Treat the phase's own description, plus the shared **Definition of done**, as its "Done when".

Tell the user in one or two lines which phase you picked (number, title, its description) and why.

## 3. Create the branch

Pick a short kebab-case slug from the phase title (3 to 5 words, for example `repo-skeleton`, `sharedkernel-money`, `party-register-flow`). Then:

```bash
git status --porcelain        # if not clean, ask the user before switching
git switch main && git pull --ff-only   # skip the pull if there is no remote
git switch -c plan/<phase>-<slug>       # e.g. plan/0.1-repo-skeleton
```

Branching before any file is written keeps the planning work isolated and easy to review as a PR.

## 4. Interview the user (before writing anything)

Do not write any spec files until the user has answered. The interview is the point of the skill: the roadmap line is only a one-row summary, and the user owns the decisions that turn it into a real scope.

Make **one** `AskUserQuestion` call with **three questions**, one per output file. Draft the options yourself from the roadmap row, the mission, the tech stack and the BIAN spec, so the user mostly picks rather than types. Put your recommended option first and mark it "(Recommended)". Use `multiSelect` where choices are not exclusive.

1. **Requirements (scope)**: header like `Scope`. What is in and out of this phase. For example: a minimal slice that only meets the phase description vs. something slightly broader; which BIAN operations or Behavior Qualifiers to include; key open decisions such as a library choice not fixed in the tech stack, an API or MCP tool shape, an aggregate boundary, or an event or data model detail.
2. **Plan (approach)**: header like `Approach`. How to build it. For example: the order of work (domain first, then core flow, then the PostgreSQL adapter, then the API and MCP adapters; or contract first; or test first), how to split the work into task groups, and how much tooling or scaffolding to add now vs. later.
3. **Validation (done)**: header like `Validation`. How we prove it works and can merge. For example: which tests (unit with Shouldly, FsCheck properties, Testcontainers integration, Alba HTTP, MCP client parity, ArchUnitNET, BIAN conformance), the demo (an `.http` request or MCP tool call), the CI checks that must be green, and manual checks.

Make the options concrete for this specific phase, not generic. If the answers leave something important ambiguous, ask a short follow-up; otherwise proceed.

## 5. Write the planning package

Create `specs/<YYYY-MM-DD>-<slug>/` using today's date (`date +%F`). Write the three files below. Keep them short and concrete; a reviewer should be able to read all three in a few minutes. Reflect the user's answers explicitly, and cite mission principles or tech-stack sections where they drive a decision. Use the exact project and namespace names the tech stack prescribes (for example `RimBank.Core`, `RimBank.Api` and namespaces such as `RimBank.Party.PartyReferenceDataDirectory.Api.Registration`; never a per-context `.Api` or `.Mcp` project, ADR 0025).

### requirements.md

```markdown
# <Phase title>

Roadmap phase: <number>
Branch: plan/<phase>-<slug>

## Context
Why this phase now, what it depends on, and what it unblocks. Link the relevant mission principles and tech-stack choices (and the BIAN spec file, if any).

## Scope
### In scope
- ...
### Out of scope
- ... (and which later phase covers it, if any)

## Decisions
| Decision | Choice | Rationale |
|---|---|---|

## ADRs to write
- ... (or "None")

## Open questions
- ... (or "None")
```

### plan.md

A series of numbered task groups. Each group is a coherent chunk that could be one commit or small PR, ordered so the solution builds and the tests pass after each one. Under each group, list concrete tasks (projects, files, types, commands).

```markdown
# Plan: <Phase title>

## 1. <Group name>
- [ ] ...
- [ ] ...

## 2. <Group name>
- [ ] ...
```

### validation.md

How we know the implementation succeeded and can be merged. Start from the phase description and the roadmap's Definition of done, and make both checkable.

```markdown
# Validation: <Phase title>

## Done when (from roadmap)
> <phase description copied from roadmap.md>
> + Definition of done (roadmap.md)

## Automated checks
- [ ] ... (test projects/names, architecture tests, conformance, `dotnet format --verify-no-changes`, CI job)

## Manual / demo checks
- [ ] ... (exact commands, `.http` requests or MCP tool calls, and the expected result incl. Problem Details on errors)

## Merge criteria
- [ ] All checks above pass in CI
- [ ] Roadmap checkbox ticked in the same PR
- [ ] ADRs written for significant decisions
- [ ] Documentation updated with the `update-docs` skill: every affected `docs/architecture/` and `docs/business/` document, all Mermaid diagrams render
- [ ] ... (no secrets or PII in logs, etc.)
```

**Documentation tasks.** Before writing `plan.md`, use the `update-docs` skill's mapping table (`.claude/skills/update-docs/SKILL.md`, step 2) on the phase's scope. List the documents the phase will change as the last task group of `plan.md`, "Documentation". Name each document and what it gains, for example "Biz 02: glossary section for ProductDirectoryEntry" or "Arch 05: decision tree for Launch". If the phase has no documentation impact, say so there with the reason.

## 6. Wrap up

Summarise for the user: the phase, the branch name, the folder path, and one line per file on what it contains. Do not commit unless the user asks; offer to commit the three files on the planning branch.
