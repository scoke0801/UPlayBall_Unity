from __future__ import annotations

from copy import deepcopy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from kbo_importer import IMPORTER_VERSION, SCHEMA_VERSION
from kbo_importer.extractor import KboExtractor
from kbo_importer.validation import validate_saved_document
from synthetic_bake import (
    ABILITY_FORMULA_VERSION,
    CONTENT_SCHEMA_VERSION,
    build_editor_original_content,
    load_reference,
    validate_archive_content,
    validate_editor_original_content,
)


class SavedDocumentValidationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.document = {
            "schemaVersion": SCHEMA_VERSION,
            "importerVersion": IMPORTER_VERSION,
            "year": 2012,
            "isSeasonComplete": True,
            "sourceMetadata": {
                "schemaVersion": SCHEMA_VERSION,
                "importerVersion": IMPORTER_VERSION,
                "sourceSnapshotHash": "a" * 64,
                "overrideHash": "b" * 64,
            },
            "players": [{"sourcePlayerId": "fixture-player", "year": 2012}],
            "teams": [
                {
                    "sourceTeamId": "fixture-team",
                    "sourceTeamName": "Fixture",
                    "seasonYear": 2012,
                }
            ],
            "awardAvailabilityStatus": {
                "RegularSeasonMvp": "AvailableEmpty",
                "AllStarGameMvp": "Unavailable",
                "KoreanSeriesMvp": "Unavailable",
                "GoldenGlove": "AvailableEmpty",
                "AllStarSelection": "NotSelected",
            },
        }

    def test_current_version_and_same_year_links_are_valid(self) -> None:
        self.assertEqual([], validate_saved_document(self.document))

    def test_stale_importer_version_is_rejected(self) -> None:
        document = deepcopy(self.document)
        document["importerVersion"] = "0.0.0"

        issues = validate_saved_document(document)

        self.assertTrue(any("importerVersion" in issue for issue in issues))

    def test_source_metadata_version_mismatch_is_rejected(self) -> None:
        document = deepcopy(self.document)
        document["sourceMetadata"]["schemaVersion"] = SCHEMA_VERSION - 1

        issues = validate_saved_document(document)

        self.assertIn(
            "sourceMetadata.schemaVersion이 문서 schemaVersion과 다릅니다.",
            issues,
        )

    def test_cross_year_player_and_team_are_rejected(self) -> None:
        document = deepcopy(self.document)
        document["players"][0]["year"] = 2011
        document["teams"][0]["seasonYear"] = 2011

        issues = validate_saved_document(document)

        cross_year_issues = [
            issue for issue in issues if issue.startswith("SEASON_RECORD_CROSS_YEAR_REFERENCE")
        ]
        self.assertEqual(2, len(cross_year_issues))

    def test_unknown_aggregate_scope_is_rejected(self) -> None:
        document = deepcopy(self.document)
        document["players"][0]["hitterStats"] = {
            "aggregateOrigin": "UnverifiedStintSum",
            "plateAppearances": 10,
        }

        issues = validate_saved_document(document)

        self.assertTrue(any("aggregateOrigin" in issue for issue in issues))

    def test_changed_override_invalidates_normalized_cache(self) -> None:
        extractor = object.__new__(KboExtractor)
        extractor._override_hash = lambda: "c" * 64
        document = {
            "sourceMetadata": {
                "sourceSnapshots": [],
                "sourceSnapshotHash": hashlib.sha256().hexdigest(),
                "overrideHash": "b" * 64,
            }
        }

        issues = extractor._validate_source_snapshots(document)

        self.assertIn(
            "Normalized Cache의 overrideHash가 현재 Override와 다릅니다.",
            issues,
        )


class SeasonJoinAndCacheVersionTests(unittest.TestCase):
    def test_same_person_is_joined_to_distinct_year_specific_seasons(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_reference(root, 2011)
            self._write_reference(root, 2012)

            content = build_editor_original_content(root, [2011, 2012])

            self.assertEqual(CONTENT_SCHEMA_VERSION, content["schemaVersion"])
            self.assertEqual(4, len(content["playerPersons"]))
            self.assertEqual(8, sum(len(year["playerSeasons"]) for year in content["years"]))
            for person in content["playerPersons"]:
                seasons = [
                    season
                    for year in content["years"]
                    for season in year["playerSeasons"]
                    if season["playerPersonId"] == person["playerPersonId"]
                ]
                self.assertEqual([2011, 2012], sorted(season["originYear"] for season in seasons))
                self.assertEqual(2, len({season["playerSeasonId"] for season in seasons}))

    def test_expected_year_and_baked_record_year_are_enforced(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_reference(root, 2012)
            with self.assertRaisesRegex(ValueError, "SEASON_RECORD_CROSS_YEAR_REFERENCE"):
                load_reference(root / "2012.json", 2011)

            content = build_editor_original_content(root, [2012])
            content["years"][0]["originalSeasonRecords"][0]["seasonYear"] = 2011
            with self.assertRaisesRegex(ValueError, "SEASON_RECORD_CROSS_YEAR_REFERENCE"):
                validate_editor_original_content(content)

    def test_stale_derivation_manifest_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self._write_reference(root, 2012)
            content = build_editor_original_content(root, [2012])
            self.assertEqual(
                ABILITY_FORMULA_VERSION,
                content["manifest"]["abilityFormulaVersion"],
            )

            content["manifest"]["abilityFormulaVersion"] = "stale-formula"
            with self.assertRaisesRegex(ValueError, "DERIVED_CACHE_VERSION_MISMATCH"):
                validate_archive_content(content)

    @classmethod
    def _write_reference(cls, root: Path, year: int) -> None:
        players = [
            cls._hitter("hitter-a", year, 0.280),
            cls._hitter("hitter-b", year, 0.310),
            cls._pitcher("pitcher-a", year, 3.10),
            cls._pitcher("pitcher-b", year, 4.20),
        ]
        document = {
            "schemaVersion": SCHEMA_VERSION,
            "importerVersion": IMPORTER_VERSION,
            "year": year,
            "isSeasonComplete": True,
            "sourceMetadata": {
                "schemaVersion": SCHEMA_VERSION,
                "importerVersion": IMPORTER_VERSION,
                "sourceSnapshotHash": "a" * 64,
                "overrideHash": "b" * 64,
            },
            "players": players,
            "teams": [
                {
                    "sourceTeamId": "fixture-team",
                    "sourceTeamName": "원본팀",
                    "seasonYear": year,
                }
            ],
            "awards": [],
            "awardAvailabilityStatus": {
                "RegularSeasonMvp": "AvailableEmpty",
                "AllStarGameMvp": "Unavailable",
                "KoreanSeriesMvp": "Unavailable",
                "GoldenGlove": "AvailableEmpty",
                "AllStarSelection": "NotSelected",
            },
        }
        (root / f"{year}.json").write_text(
            json.dumps(document, ensure_ascii=False),
            encoding="utf-8",
        )

    @staticmethod
    def _hitter(source_id: str, year: int, average: float) -> dict:
        return {
            "sourcePlayerId": source_id,
            "playerName": source_id,
            "year": year,
            "aggregateTeamName": "원본팀",
            "hitterStats": {
                "aggregateOrigin": "SingleTeamStint",
                "sourceAVG": average,
                "sourceOBP": average + 0.07,
                "sourceSLG": average + 0.15,
                "plateAppearances": 400,
                "atBats": 360,
                "hits": round(average * 360),
                "homeRuns": 10,
                "walks": 40,
                "strikeouts": 70,
            },
            "pitcherStats": None,
            "runningStats": {
                "aggregateOrigin": "SingleTeamStint",
                "stolenBases": 8,
                "caughtStealing": 4,
                "stolenBaseAttempts": 12,
            },
            "defenseRecords": [
                {
                    "aggregateOrigin": "SingleTeamStint",
                    "position": "중견수",
                    "inningsOuts": 900,
                    "putouts": 200,
                    "assists": 4,
                    "errors": 3,
                }
            ],
        }

    @staticmethod
    def _pitcher(source_id: str, year: int, earned_run_average: float) -> dict:
        return {
            "sourcePlayerId": source_id,
            "playerName": source_id,
            "year": year,
            "aggregateTeamName": "원본팀",
            "hitterStats": None,
            "pitcherStats": {
                "aggregateOrigin": "SingleTeamStint",
                "sourceERA": earned_run_average,
                "sourceWHIP": 1.20,
                "inningsOuts": 360,
                "games": 30,
                "gamesStarted": 20,
                "strikeouts": 120,
                "walks": 40,
                "earnedRuns": round(earned_run_average * 120 / 9),
                "saves": 0,
                "holds": 0,
            },
            "runningStats": None,
            "defenseRecords": [],
        }


if __name__ == "__main__":
    unittest.main()
