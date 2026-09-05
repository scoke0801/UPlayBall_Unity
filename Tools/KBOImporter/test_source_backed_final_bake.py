from __future__ import annotations

import json
import unittest
from pathlib import Path

import synthetic_bake
import source_backed_runtime_bake as source_plan


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
        self.assertEqual(year_report["replacementHitterCount"], 0)
        self.assertEqual(year_report["replacementPitcherCount"], 27)
        self.assertEqual(year_report["replacementRatio"], 0.18)
        self.assertEqual(len(year["playerSeasons"]), 168)
        self.assertEqual(len(year["teamSeasons"]), 6)
        self.assertEqual(report["replacementGeneratedPlayerPersonCount"], 27)
        self.assertEqual(report["replacementGeneratedPlayerSeasonCount"], 27)
        self.assertEqual(len(report["worldIdentityNameSample"]["players"]), 168)
        self.assertEqual(len(report["worldIdentityNameSample"]["franchises"]), 6)
        self.assertEqual(year_report["sourceTeamSeasonCount"], 6)
        self.assertEqual(year_report["canonicalTeamSeasonCount"], 6)
        self.assertEqual(
            year_report["teamCountDisposition"],
            "UnderTargetPreservedWithoutSyntheticTeams",
        )
        replacement_person_ids = {
            row["playerPersonId"]
            for row in year["playerSeasons"]
            if row["dataProvenance"] == "ReplacementGenerated"
        }
        self.assertEqual(len(replacement_person_ids), 27)

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
        thresholds_by_type = year_report["sourceCostThresholds"]
        self.assertEqual(
            sum(row["sourcePopulationSize"] for row in thresholds_by_type.values()),
            141,
        )
        self.assertEqual(set(thresholds_by_type), {"Hitter", "Pitcher"})
        self.assertTrue(
            all(
                row["replacementExcludedFromThresholdCalculation"]
                for row in thresholds_by_type.values()
            )
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
        self.assertNotIn(b"fictionalName", first_bytes)
        pool = first["worldIdentityNamePool"]
        self.assertGreaterEqual(
            len(pool["domesticPlayerNames"]),
            len(first["playerPersons"]),
        )
        reference = json.loads((self.normalized / "1982.json").read_text(encoding="utf-8"))
        source_player_names = {player["playerName"] for player in reference["players"]}
        source_team_names = {team["sourceTeamName"] for team in reference["teams"]}
        generated_players = pool["domesticPlayerNames"] + pool["foreignPlayerNames"]
        generated_franchises = pool["franchiseNames"]
        self.assertEqual(len(generated_players), len(set(generated_players)))
        self.assertEqual(len(generated_franchises), len(set(generated_franchises)))
        self.assertTrue(source_player_names.isdisjoint(generated_players))
        self.assertTrue(source_team_names.isdisjoint(generated_franchises))
        self.assertTrue(all(name and len(name) <= 30 for name in generated_players))
        self.assertTrue(all(not any(character.isdigit() or ord(character) < 32 for character in name)
                            for name in generated_players))
        self.assertTrue(all(token not in name
                            for name in generated_players
                            for token in ("블레이즈", "썬더", "파워", "베이스볼", "스타")))

    def test_generation_seed_does_not_change_canonical_bake(self) -> None:
        first = synthetic_bake.bake(self.normalized, [1982], 1)
        second = synthetic_bake.bake(self.normalized, [1982], 999999)

        self.assertEqual(first, second)
        self.assertEqual(0, first["manifest"]["generationSeed"])
        self.assertFalse(first["manifest"]["generationSeedAffectsCanonicalBake"])

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

    def test_1982_source_players_and_teams_keep_one_to_one_team_provenance(self) -> None:
        normalized = json.loads(
            (self.normalized / "1982.json").read_text(encoding="utf-8")
        )
        runtime = synthetic_bake.bake(self.normalized, [1982], 20260901)
        year = runtime["years"][0]
        source_franchise_by_team = {
            team["sourceTeamId"]: team["sourceFranchiseId"]
            for team in normalized["teams"]
        }
        runtime_seasons = {
            row["playerSeasonId"]: row
            for row in year["playerSeasons"]
            if row["dataProvenance"] == "SourceBacked"
        }

        for player in normalized["players"]:
            runtime_season = runtime_seasons[
                source_plan.runtime_player_season_id(
                    player["sourcePlayerId"],
                    1982,
                )
            ]
            source_franchise = source_franchise_by_team[player["aggregateTeamId"]]
            self.assertEqual(
                source_plan.runtime_franchise_id(source_franchise),
                runtime_season["originFranchiseId"],
            )
            self.assertEqual(
                source_plan.runtime_team_season_key(source_franchise, 1982),
                runtime_season["originTeamSeasonKey"],
            )


if __name__ == "__main__":
    unittest.main()
