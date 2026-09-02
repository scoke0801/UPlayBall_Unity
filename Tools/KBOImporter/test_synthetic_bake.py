from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from synthetic_bake import (
    bake,
    load_and_validate_editor_asset_archive,
    percentile_cost,
    write_editor_asset_archive,
)


class SyntheticBakeTests(unittest.TestCase):
    def test_cost_percentile_boundaries(self) -> None:
        costs = [percentile_cost(rank, 1000) for rank in range(1000)]
        self.assertEqual(costs.count(1), 50)
        self.assertEqual(costs.count(10), 30)
        self.assertEqual(min(costs), 1)
        self.assertEqual(max(costs), 10)

    def test_same_seed_bakes_same_runtime_safe_content(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fixture = {
                "year": 2099,
                "isSeasonComplete": True,
                "players": [
                    self._hitter("실제이름가", 0.280, 12),
                    self._hitter("실제이름나", 0.310, 22),
                    self._pitcher("실제이름다", 3.10, 150),
                    self._pitcher("실제이름라", 4.20, 90),
                ],
            }
            (root / "2099.json").write_text(
                json.dumps(fixture, ensure_ascii=False), encoding="utf-8"
            )

            first = bake(root, [2099], 77)
            second = bake(root, [2099], 77)

            self.assertEqual(first, second)
            year = first["years"][0]
            self.assertEqual(len(year["teamSeasons"]), 10)
            self.assertEqual(len(year["playerSeasons"]), 300)
            self.assertEqual(len(first["playerPersons"]), 300)
            self.assertTrue(all(len(team["core25CardIds"]) == 25 for team in year["teamSeasons"]))
            self.assertTrue(all(len(team["allNormalCardIds"]) == 30 for team in year["teamSeasons"]))
            self.assertTrue(all(
                season["referenceSimilarityDistance"] >= 0.12
                for season in year["playerSeasons"]
            ))
            self.assertEqual(
                sum(award["awardType"] == "GoldenGlove" for award in year["originalAwardRecords"]),
                10,
            )
            self.assertEqual(
                sum(award["awardType"] == "AllStar" for award in year["originalAwardRecords"]),
                25,
            )
            serialized = json.dumps(first, ensure_ascii=False)
            self.assertNotIn("sourcePlayerId", serialized)
            self.assertNotIn("sourceTeamId", serialized)
            self.assertNotIn("실제이름가", serialized)

    def test_editor_asset_archive_is_split_and_deterministic(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fixture = {
                "year": 2099,
                "isSeasonComplete": True,
                "players": [
                    self._hitter("실제이름가", 0.280, 12),
                    self._hitter("실제이름나", 0.310, 22),
                    self._pitcher("실제이름다", 3.10, 150),
                    self._pitcher("실제이름라", 4.20, 90),
                ],
            }
            (root / "2099.json").write_text(
                json.dumps(fixture, ensure_ascii=False), encoding="utf-8"
            )
            content = bake(root, [2099], 77)

            first_manifest = write_editor_asset_archive(content, root / "first")
            second_manifest = write_editor_asset_archive(content, root / "second")
            reloaded = load_and_validate_editor_asset_archive(root / "first")

            self.assertEqual(first_manifest, second_manifest)
            self.assertEqual(reloaded, content)
            self.assertTrue((root / "first" / "manifest.json").is_file())
            self.assertTrue((root / "first" / "player_persons.json").is_file())
            self.assertTrue((root / "first" / "Years" / "2099.json").is_file())

    @staticmethod
    def _hitter(name: str, average: float, home_runs: int) -> dict:
        return {
            "playerName": name,
            "hitterStats": {
                "sourceAVG": average,
                "sourceOBP": average + 0.07,
                "sourceSLG": average + 0.15,
                "plateAppearances": 400,
                "hits": round(average * 360),
                "homeRuns": home_runs,
                "walks": 40,
                "strikeouts": 70,
            },
            "pitcherStats": None,
            "runningStats": {"stolenBases": 8, "stolenBaseAttempts": 12},
            "defenseRecords": [
                {"position": "중견수", "inningsOuts": 900, "putouts": 200, "assists": 4, "errors": 3}
            ],
        }

    @staticmethod
    def _pitcher(name: str, earned_run_average: float, innings: int) -> dict:
        return {
            "playerName": name,
            "hitterStats": None,
            "pitcherStats": {
                "sourceERA": earned_run_average,
                "sourceWHIP": 1.20,
                "inningsOuts": innings * 3,
                "games": 30,
                "strikeouts": innings,
                "walks": innings // 3,
                "earnedRuns": round(earned_run_average * innings / 9),
                "saves": 0,
                "holds": 0,
            },
            "runningStats": None,
            "defenseRecords": [],
        }


if __name__ == "__main__":
    unittest.main()
