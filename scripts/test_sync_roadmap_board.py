"""Tests for the roadmap parser in sync_roadmap_board.py. Run: python -m unittest discover -s scripts"""

import unittest
from pathlib import Path

from sync_roadmap_board import LABELS, ROADMAP, issue_body, issue_title, parse_roadmap, phase_labels

SAMPLE = """# Roadmap

## Definition of done (every phase)

- [ ] Not a phase

---

## Stage 0: Foundations

- [x] **0.1 Repository skeleton:** `RimBank.slnx`, `global.json`.
- [ ] **0.2 CI pipeline:** GitHub Actions.

## Stage 4: Current Account

`RimBank.LoansAndDeposits.CurrentAccount`. Control Record: *CurrentAccountFacility*.

- [ ] **4.13 Conformance + authorisation policies.**

## Stage 8+: Growth backlog (order to be decided)

Each item repeats the slice pattern.

- Savings Account, Term Deposit
- Consumer Loan, Loan
"""


class ParseRoadmapTests(unittest.TestCase):
    def setUp(self) -> None:
        self.stages = parse_roadmap(SAMPLE)

    def test_definition_of_done_is_not_a_stage(self) -> None:
        self.assertEqual([s.name for s in self.stages], [
            "Stage 0: Foundations",
            "Stage 4: Current Account",
            "Stage 8+: Growth backlog (order to be decided)",
        ])

    def test_phases_carry_number_title_scope_and_tick(self) -> None:
        first, second = self.stages[0].phases
        self.assertEqual((first.key, first.title, first.done), ("0.1", "0.1 Repository skeleton", True))
        self.assertEqual(first.scope, "`RimBank.slnx`, `global.json`.")
        self.assertFalse(second.done)

    def test_phase_without_description_uses_its_heading_as_scope(self) -> None:
        phase = self.stages[1].phases[0]
        self.assertEqual(phase.title, "4.13 Conformance + authorisation policies")
        self.assertEqual(phase.scope, "Conformance + authorisation policies")

    def test_stage_description_is_the_text_before_the_first_phase(self) -> None:
        self.assertIn("Control Record", self.stages[1].description)

    def test_backlog_bullets_are_keyed_by_their_text(self) -> None:
        keys = [p.key for p in self.stages[2].phases]
        self.assertEqual(keys, ["8+:savings-account-term-deposit", "8+:consumer-loan-loan"])

    def test_stage_is_done_only_when_every_phase_is(self) -> None:
        self.assertFalse(self.stages[0].done)
        self.assertFalse(self.stages[2].done)

    def test_body_carries_the_marker_and_title_drops_backticks(self) -> None:
        stage = self.stages[0]
        self.assertTrue(issue_body(stage.phases[0], stage, "o/r").startswith("<!-- roadmap-phase: 0.1 -->"))
        self.assertEqual(issue_title(stage.phases[0]), "0.1 Repository skeleton")


class PhaseLabelTests(unittest.TestCase):
    def labels(self, roadmap: str) -> list[str]:
        stage = parse_roadmap(roadmap)[0]
        return phase_labels(stage.phases[0], stage)

    def test_every_phase_has_exactly_one_type_and_known_labels(self) -> None:
        root = Path(__file__).resolve().parent.parent
        for stage in parse_roadmap((root / ROADMAP).read_text(encoding="utf-8")):
            for phase in stage.phases:
                labels = phase_labels(phase, stage)
                self.assertEqual(len([l for l in labels if l.startswith("type:")]), 1, phase.title)
                unknown = [l for l in labels if l not in LABELS and not l.startswith("stage:")]
                self.assertEqual(unknown, [], phase.title)

    def test_adr_phase_in_a_context_stage(self) -> None:
        labels = self.labels("## Stage 1: Party\n\n- [ ] **1.1 ADR:** BIAN to DDD mapping.\n")
        self.assertIn("type:adr", labels)
        self.assertIn("context:party", labels)

    def test_areas_come_from_the_text(self) -> None:
        labels = self.labels("## Stage 0: F\n\n- [ ] **0.10 Marten wiring:** one Marten store per context.\n")
        self.assertIn("area:persistence", labels)
        self.assertIn("type:feature", labels)
        self.assertFalse(any(l.startswith("context:") for l in labels))


class RealRoadmapTests(unittest.TestCase):
    def test_every_key_is_unique_and_every_stage_has_phases(self) -> None:
        root = Path(__file__).resolve().parent.parent
        stages = parse_roadmap((root / ROADMAP).read_text(encoding="utf-8"))
        keys = [p.key for s in stages for p in s.phases]
        self.assertEqual(len(keys), len(set(keys)))
        self.assertTrue(all(s.phases for s in stages))


if __name__ == "__main__":
    unittest.main()
