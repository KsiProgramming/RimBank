"""Mirror specs/roadmap.md on GitHub: labels, one milestone per roadmap stage, one issue per
phase, and a Projects board holding the issues. Uses the `gh` CLI and the standard library only.

The roadmap stays the source of truth. Running this again updates what changed: issue titles,
bodies and milestones follow the roadmap, a ticked phase (`- [x]`) closes its issue and moves its
card to Done, and a stage whose phases are all ticked closes its milestone. Cards a person moved to
"In Progress" stay there until the phase is ticked. Growth-backlog bullets (Stage 8+) become
issues too, keyed by their text.

Usage:
  python scripts/sync_roadmap_board.py            apply
  python scripts/sync_roadmap_board.py --dry-run  print what would change, write nothing

Needs `gh auth login` with the `project` scope (`gh auth refresh -s project`).
"""

from __future__ import annotations

import io
import json
import re
import subprocess
import sys
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

ROADMAP = "specs/roadmap.md"
PROJECT_TITLE = "RimBank roadmap"
PHASE_LABEL = "roadmap-phase"
MARKER = re.compile(r"<!-- roadmap-phase: (\S+) -->")
STAGE_HEADING = re.compile(r"^## Stage (\d+)(\+?): (.+?)\s*$")
# - [x] **0.1 Repository skeleton:** description   |   - [ ] **4.13 Conformance + policies.**
PHASE_LINE = re.compile(r"^- \[(?P<tick>[ xX])\] \*\*(?P<number>\d+\.\d+) (?P<head>.+?)\*\*\s*(?P<rest>.*)$")
BACKLOG_LINE = re.compile(r"^- (?!\[)(?P<text>.+?)\s*$")
TITLE_LIMIT = 100

# name -> (colour, description). Applied with --force, so colours and descriptions follow this.
LABELS: dict[str, tuple[str, str]] = {
    PHASE_LABEL: ("5319e7", "A phase of specs/roadmap.md (synced, edit the roadmap)"),
}
STAGE_LABEL_COLOUR = "1d76db"


@dataclass
class Phase:
    key: str
    title: str
    scope: str
    done: bool


@dataclass
class Stage:
    number: int
    title: str
    open_ended: bool = False
    description: str = ""
    phases: list[Phase] = field(default_factory=list)

    @property
    def name(self) -> str:
        return f"Stage {self.number}{'+' if self.open_ended else ''}: {self.title}"

    @property
    def label(self) -> str:
        return f"stage:{self.number}"

    @property
    def done(self) -> bool:
        return bool(self.phases) and all(p.done for p in self.phases)


def slug(text: str) -> str:
    return re.sub(r"[^a-z0-9]+", "-", text.lower()).strip("-")[:60]


def parse_roadmap(roadmap: str) -> list[Stage]:
    stages: list[Stage] = []
    current: Stage | None = None
    for line in roadmap.splitlines():
        if match := STAGE_HEADING.match(line):
            current = Stage(int(match.group(1)), match.group(3), open_ended=bool(match.group(2)))
            stages.append(current)
        elif line.startswith("## ") or line.startswith("---"):
            current = None
        elif current is None:
            continue
        elif match := PHASE_LINE.match(line):
            head = match.group("head").rstrip(".:").strip()
            rest = match.group("rest").strip()
            current.phases.append(
                Phase(
                    key=match.group("number"),
                    title=f"{match.group('number')} {head}",
                    scope=rest or head,
                    done=match.group("tick").lower() == "x",
                )
            )
        elif current.open_ended and (match := BACKLOG_LINE.match(line)):
            text = match.group("text")
            current.phases.append(
                Phase(key=f"{current.number}+:{slug(text)}", title=text, scope=text, done=False)
            )
        elif line.strip() and not line.startswith("-") and not current.phases:
            current.description = f"{current.description} {line.strip()}".strip()
    return stages


def issue_title(phase: Phase) -> str:
    title = phase.title.replace("`", "")
    return title if len(title) <= TITLE_LIMIT else title[: TITLE_LIMIT - 1].rstrip() + "…"


def issue_body(phase: Phase, stage: Stage, repo: str) -> str:
    roadmap_url = f"https://github.com/{repo}/blob/main/{ROADMAP}"
    context = f"\n\n{stage.description}" if stage.description else ""
    return (
        f"<!-- roadmap-phase: {phase.key} -->\n"
        f"Part of [{stage.name}]({roadmap_url}).{context}\n\n"
        f"### Scope\n\n{phase.scope}\n\n"
        f"### Done when\n\nThe [definition of done]({roadmap_url}#definition-of-done-every-phase) "
        "holds, and the roadmap checkbox is ticked in the same PR.\n\n"
        f"**Status:** {'Done' if phase.done else 'Not started'}\n\n"
        f"_Synced from `{ROADMAP}` by `scripts/sync_roadmap_board.py`: "
        "edit the roadmap, not this issue._\n"
    )


def origin_repo() -> str:
    url = subprocess.run(
        ["git", "remote", "get-url", "origin"], capture_output=True, encoding="utf-8", check=True
    ).stdout.strip()
    match = re.search(r"github\.com[:/](.+?/.+?)(?:\.git)?$", url)
    if not match:
        sys.exit(f"origin is not a GitHub remote: {url}")
    return match.group(1)


class GitHub:
    def __init__(self, repo: str, dry_run: bool) -> None:
        self.repo = repo
        self.owner = repo.split("/")[0]
        self.dry_run = dry_run

    def read(self, *args: str) -> Any:
        return self._run(args, payload=None)

    def write(self, summary: str, *args: str, payload: object = None, check: bool = True) -> Any:
        print(("would " if self.dry_run else "") + summary)
        if self.dry_run:
            return None
        return self._run(args, payload=payload, check=check)

    def _run(self, args: tuple[str, ...], payload: object, check: bool = True) -> Any:
        if payload is not None:
            args = (*args, "--input", "-")
        result = subprocess.run(
            ["gh", *args],
            input=None if payload is None else json.dumps(payload),
            capture_output=True,
            encoding="utf-8",
        )
        if result.returncode != 0:
            if not check:
                return None
            sys.exit(f"gh {' '.join(args)} failed:\n{result.stderr.strip()}")
        return json.loads(result.stdout) if result.stdout.strip() else None


def sync_labels(gh: GitHub, stages: list[Stage]) -> None:
    existing = gh.read("label", "list", "--limit", "500", "--json", "name,color,description")
    by_name = {label["name"]: label for label in existing}
    wanted = dict(LABELS)
    for stage in stages:
        wanted[stage.label] = (STAGE_LABEL_COLOUR, f"Roadmap {stage.name}"[:100])
    for name, (color, description) in wanted.items():
        have = by_name.get(name)
        if have and have["color"].lower() == color and have["description"] == description:
            continue
        gh.write(
            f"{'update' if have else 'create'} label {name}",
            "label", "create", name, "--color", color, "--description", description, "--force",
        )


def sync_milestones(gh: GitHub, stages: list[Stage]) -> dict[int, int]:
    """Return roadmap stage number -> GitHub milestone number."""
    existing = gh.read("api", f"repos/{gh.repo}/milestones?state=all&per_page=100")
    by_title = {m["title"]: m for m in existing}
    numbers: dict[int, int] = {}
    for stage in stages:
        state = "closed" if stage.done else "open"
        wanted = {"title": stage.name, "description": stage.description, "state": state}
        have = by_title.get(stage.name)
        if have is None:
            created = gh.write(
                f"create milestone {stage.name!r}",
                "api", "-X", "POST", f"repos/{gh.repo}/milestones", payload=wanted,
            )
            numbers[stage.number] = created["number"] if created else -1
            continue
        numbers[stage.number] = have["number"]
        if (have["description"] or "") != stage.description or have["state"] != state:
            gh.write(
                f"update milestone {stage.name!r} ({state})",
                "api", "-X", "PATCH", f"repos/{gh.repo}/milestones/{have['number']}", payload=wanted,
            )
    return numbers


def sync_issues(gh: GitHub, stages: list[Stage], numbers: dict[int, int]) -> dict[str, tuple[str, bool]]:
    """Return phase key -> (issue URL, done)."""
    existing = gh.read(
        "issue", "list", "--label", PHASE_LABEL, "--state", "all", "--limit", "1000",
        "--json", "number,title,body,state,milestone,labels,url",
    )
    by_key: dict[str, dict[str, Any]] = {}
    for issue in existing:
        if match := MARKER.search(issue["body"] or ""):
            by_key[match.group(1)] = issue

    urls: dict[str, tuple[str, bool]] = {}
    for stage in stages:
        labels = sorted({PHASE_LABEL, stage.label})
        for phase in stage.phases:
            title, body = issue_title(phase), issue_body(phase, stage, gh.repo)
            state = "closed" if phase.done else "open"
            payload: dict[str, object] = {
                "title": title,
                "body": body,
                "labels": labels,
                "milestone": numbers[stage.number],
                "state": state,
            }
            if phase.done:
                payload["state_reason"] = "completed"
            have = by_key.get(phase.key)
            if have is None:
                created = gh.write(
                    f"create issue {title!r} ({state})",
                    "api", "-X", "POST", f"repos/{gh.repo}/issues", payload={**payload, "state": "open"},
                )
                if created and phase.done:
                    gh.write(
                        f"close issue #{created['number']}",
                        "api", "-X", "PATCH", f"repos/{gh.repo}/issues/{created['number']}", payload=payload,
                    )
                urls[phase.key] = (created["html_url"] if created else f"<new {phase.key}>", phase.done)
                continue
            urls[phase.key] = (have["url"], phase.done)
            have_labels = sorted(label["name"] for label in have["labels"])
            have_milestone = (have["milestone"] or {}).get("number")
            # Extra labels people add are kept; only the synced ones are required.
            if (
                have["title"] == title
                and (have["body"] or "").strip() == body.strip()
                and set(labels) <= set(have_labels)
                and have_milestone == numbers[stage.number]
                and have["state"].lower() == state
            ):
                continue
            payload["labels"] = sorted(set(labels) | set(have_labels))
            gh.write(
                f"update issue #{have['number']} {title!r} ({state})",
                "api", "-X", "PATCH", f"repos/{gh.repo}/issues/{have['number']}", payload=payload,
            )
    return urls


def sync_project(gh: GitHub, issues: dict[str, tuple[str, bool]]) -> None:
    projects = gh.read("project", "list", "--owner", gh.owner, "--format", "json", "--limit", "100")
    project = next((p for p in projects["projects"] if p["title"] == PROJECT_TITLE), None)
    if project is None:
        project = gh.write(
            f"create project {PROJECT_TITLE!r}",
            "project", "create", "--owner", gh.owner, "--title", PROJECT_TITLE, "--format", "json",
        )
        if project is None:  # dry run
            print(f"would add {len(issues)} issues to the project")
            return
        gh.write(
            "describe the project",
            "project", "edit", str(project["number"]), "--owner", gh.owner,
            "--description", f"Roadmap phases of {gh.repo}, synced from {ROADMAP}",
        )
    number = str(project["number"])
    # Already linked is an error from gh; either way the project shows on the repo.
    gh.write(
        f"link project to {gh.repo}",
        "project", "link", number, "--owner", gh.owner, "--repo", gh.repo, check=False,
    )

    fields = gh.read("project", "field-list", number, "--owner", gh.owner, "--format", "json")
    status = next(f for f in fields["fields"] if f["name"] == "Status")
    options = {o["name"]: o["id"] for o in status["options"]}

    items = gh.read(
        "project", "item-list", number, "--owner", gh.owner, "--format", "json", "--limit", "1000",
    )
    by_url = {item["content"].get("url"): item for item in items["items"] if item.get("content")}

    for key, (url, done) in issues.items():
        item = by_url.get(url)
        current = item.get("status") if item else None
        if item is None:
            added = gh.write(
                f"add {key} to the project",
                "project", "item-add", number, "--owner", gh.owner, "--url", url, "--format", "json",
            )
            item_id = added["id"] if added else None
        else:
            item_id = item["id"]
        wanted = "Done" if done else ("Todo" if current in (None, "Done") else current)
        if wanted == current:
            continue
        gh.write(
            f"set {key} to {wanted}",
            "project", "item-edit", "--id", str(item_id), "--project-id", project["id"],
            "--field-id", status["id"], "--single-select-option-id", options[wanted],
        )
    print(f"Project: {project.get('url', PROJECT_TITLE)}")


def main(dry_run: bool) -> int:
    stages = parse_roadmap(Path(ROADMAP).read_text(encoding="utf-8"))
    gh = GitHub(origin_repo(), dry_run)
    sync_labels(gh, stages)
    numbers = sync_milestones(gh, stages)
    issues = sync_issues(gh, stages, numbers)
    sync_project(gh, issues)
    return 0


if __name__ == "__main__":
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", line_buffering=True)
    args = sys.argv[1:]
    if args not in ([], ["--dry-run"]):
        sys.exit(__doc__)
    sys.exit(main(dry_run=args == ["--dry-run"]))
