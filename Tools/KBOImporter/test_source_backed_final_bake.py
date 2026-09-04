from __future__ import annotations

import json
import unittest
from pathlib import Path

import synthetic_bake


class SourceBackedFinalBakeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.normalized = Path(__file__).parent / ".cache" / "KBOImport" / "Normalized"
        if not (cls.normalized / "1982.json").is_file():
            raise unittest.SkipTest("실제 Normalized 1982 fixture가 없습니다.")

    def test_1982_shortage_is_explicit_and_source_costs_do_not_move(self) -> None:
        source = synthetic_bake.build_editor_original_content(self.normalized, [1982])
        runtime, report = synthetic_bake.bake_with_report(self.normalized, [1982], 20260901)
        year = runtime["years"][0]
        year_report = report["years"][0]

        self.assertEqual(year_report["sourceHitterCount"], 102)
        self.assertEqual(year_report["sourcePitcherCount"], 39)
        self.assertEqual(year_report["replacementHitterCount"], 38)
        self.assertEqual(year_report["replacementPitcherCount"], 71)
        self.assertEqual(year_report["replacementRatio"], 0.436)
        self.assertEqual(len(year["playerSeasons"]), 250)
        self.assertEqual(len(year["teamSeasons"]), 10)
        self.assertEqual(report["replacementGeneratedPlayerPersonCount"], 109)
        self.assertEqual(report["replacementGeneratedPlayerSeasonCount"], 109)
        replacement_person_ids = {
            row["playerPersonId"]
            for row in year["playerSeasons"]
            if row["dataProvenance"] == "ReplacementGenerated"
        }
        self.assertEqual(len(replacement_person_ids), 109)

        source_costs = sorted(
            int(row["cost"])
            for row in source["years"][0]["playerSeasons"]
        )
        runtime_source_costs = sorted(
            int(row["cost"])
            for row in year["playerSeasons"]
            if row["dataProvenance"] == "SourceBacked"
        )
        self.assertEqual(runtime_source_costs, source_costs)
        self.assertEqual(
            year_report["sourceCostThresholds"]["sourcePopulationSize"],
            141,
        )
        self.assertTrue(
            year_report["sourceCostThresholds"]["replacementExcludedFromThresholdCalculation"]
        )

    def test_runtime_is_byte_deterministic_and_contains_no_source_identity(self) -> None:
        first = synthetic_bake.create_runtime_safe_content(
            synthetic_bake.bake(self.normalized, [1982], 20260901)
        )
        second = synthetic_bake.create_runtime_safe_content(
            synthetic_bake.bake(self.normalized, [1982], 20260901)
        )
        first_bytes = json.dumps(
            first, ensure_ascii=False, sort_keys=True, separators=(",", ":")
        ).encode("utf-8")
        second_bytes = json.dumps(
            second, ensure_ascii=False, sort_keys=True, separators=(",", ":")
        ).encode("utf-8")
        self.assertEqual(first_bytes, second_bytes)
        self.assertNotIn(b"sourcePlayerId", first_bytes)
        self.assertNotIn(b"sourceReferenceNames", first_bytes)
        self.assertNotIn(b"originalName", first_bytes)
        self.assertNotIn(b"referenceSimilarityDistance", first_bytes)

    def test_every_runtime_team_meets_core25_roles(self) -> None:
        runtime = synthetic_bake.create_runtime_safe_content(
            synthetic_bake.bake(self.normalized, [1982, 1988], 20260901)
        )
        synthetic_bake.validate_bake(runtime)
        for year in runtime["years"]:
            seasons = {row["playerSeasonId"]: row for row in year["playerSeasons"]}
            for team in year["teamSeasons"]:
                core = [
                    seasons[card_id.removesuffix(":Normal")]
                    for card_id in team["core25CardIds"]
                ]
                roles = [row["rosterRole"] for row in core]
                self.assertEqual(sum(row["playerType"] == "Hitter" for row in core), 14)
                self.assertEqual(sum(row["playerType"] == "Pitcher" for row in core), 11)
                self.assertEqual(sum(role.startswith("StartingHitter:") for role in roles), 9)
                self.assertEqual(sum(role.startswith("StartingPitcher:") for role in roles), 5)
                self.assertEqual(sum(role.startswith("Bullpen") for role in roles), 4)
                self.assertEqual(roles.count("Setup"), 1)
                self.assertEqual(roles.count("Closer"), 1)


if __name__ == "__main__":
    unittest.main()
