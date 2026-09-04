"""source_backed_runtime_bake의 1:1 Source 계약 회귀 테스트."""

from __future__ import annotations

import json
import unittest
from typing import Any

from source_backed_runtime_bake import (
    CORE_HITTER_COUNT,
    CORE_PITCHER_COUNT,
    FRANCHISE_IDS,
    build_source_backed_runtime_plan,
    canonical_json_bytes,
    editor_source_person_id,
    editor_source_season_id,
    runtime_player_person_id,
    runtime_player_season_id,
    validate_source_backed_runtime_plan,
)


class SourceBackedRuntimeBakeTests(unittest.TestCase):
    def test_source_person_and_season_are_preserved_exactly_once(self) -> None:
        editor, normalized = _build_fixture({2012: (19, 13), 2013: (7, 5)}, repeat_person=True)

        plan = build_source_backed_runtime_plan(editor, normalized, allocation_seed=37)
        validate_source_backed_runtime_plan(plan)

        expected_person_ids = {
            runtime_player_person_id(player["sourcePlayerId"])
            for document in normalized
            for player in document["players"]
        }
        actual_person_ids = {
            person["playerPersonId"] for person in plan.runtime_content["playerPersons"]
        }
        self.assertEqual(expected_person_ids, actual_person_ids)

        expected_season_ids = {
            runtime_player_season_id(player["sourcePlayerId"], document["year"])
            for document in normalized
            for player in document["players"]
        }
        actual_seasons = [
            season
            for year_data in plan.runtime_content["years"]
            for season in year_data["playerSeasons"]
        ]
        self.assertEqual(expected_season_ids, {season["playerSeasonId"] for season in actual_seasons})
        self.assertEqual(len(expected_season_ids), len(actual_seasons))

        expected_costs = {
            runtime_player_season_id(player["sourcePlayerId"], document["year"]): player["cost"]
            for document in normalized
            for player in document["players"]
        }
        actual_costs = {season["playerSeasonId"]: season["cost"] for season in actual_seasons}
        self.assertEqual(expected_costs, actual_costs)

    def test_same_source_person_keeps_one_runtime_identity_and_name(self) -> None:
        editor, normalized = _build_fixture({2012: (2, 1), 2013: (2, 1)}, repeat_person=True)

        plan = build_source_backed_runtime_plan(editor, normalized)
        repeated_id = runtime_player_person_id("SRC_SHARED_H")
        person = next(
            item for item in plan.runtime_content["playerPersons"]
            if item["playerPersonId"] == repeated_id
        )
        linked_seasons = [
            season
            for year_data in plan.runtime_content["years"]
            for season in year_data["playerSeasons"]
            if season["playerPersonId"] == repeated_id
        ]

        self.assertEqual(2, len(linked_seasons))
        self.assertEqual({repeated_id}, {season["playerPersonId"] for season in linked_seasons})
        self.assertTrue(person["fictionalName"])
        self.assertNotEqual("실제공유타자", person["fictionalName"])

    def test_all_source_seasons_are_allocated_once_to_exactly_ten_variable_pools(self) -> None:
        editor, normalized = _build_fixture({2012: (201, 139)})

        plan = build_source_backed_runtime_plan(editor, normalized, allocation_seed=911)
        validate_source_backed_runtime_plan(plan)
        year_data = plan.runtime_content["years"][0]
        teams = year_data["teamAllocationPlans"]
        allocated = [
            season_id
            for team in teams
            for season_id in team["sourceBackedPlayerSeasonIds"]
        ]

        self.assertEqual(len(FRANCHISE_IDS), len(teams))
        self.assertEqual(
            {
                "SEOUL_COMETS",
                "BUSAN_TIDES",
                "INCHEON_HARBORS",
                "DAEGU_FORGE",
                "DAEJEON_PIONEERS",
                "GWANGJU_PHOENIX",
                "SUWON_GUARDIANS",
                "CHANGWON_MARINERS",
                "JEONJU_STARS",
                "GANGNEUNG_WAVES",
            },
            {team["franchiseId"] for team in teams},
        )
        self.assertEqual(340, len(allocated))
        self.assertEqual(340, len(set(allocated)))
        self.assertGreater(max(len(team["sourceBackedPlayerSeasonIds"]) for team in teams), 30)
        self.assertTrue(
            all(len(team["sourceBackedHitterSeasonIds"]) >= CORE_HITTER_COUNT for team in teams)
        )
        self.assertTrue(
            all(len(team["sourceBackedPitcherSeasonIds"]) >= CORE_PITCHER_COUNT for team in teams)
        )
        self.assertEqual((), plan.replacement_requests)

    def test_1982_shortage_math_requests_only_core25_gap(self) -> None:
        editor, normalized = _build_fixture({1982: (102, 39)})
        received = []

        plan = build_source_backed_runtime_plan(
            editor,
            normalized,
            allocation_seed=1982,
            replacement_request_sink=received.append,
        )
        validate_source_backed_runtime_plan(plan)
        year_report = plan.runtime_content["years"][0]["allocationReport"]
        hitter_requests = sum(
            request.count for request in plan.replacement_requests
            if request.player_type == "Hitter"
        )
        pitcher_requests = sum(
            request.count for request in plan.replacement_requests
            if request.player_type == "Pitcher"
        )

        self.assertEqual(38, hitter_requests)
        self.assertEqual(71, pitcher_requests)
        self.assertEqual(109, hitter_requests + pitcher_requests)
        self.assertEqual(38, year_report["requiredReplacementHitterCount"])
        self.assertEqual(71, year_report["requiredReplacementPitcherCount"])
        self.assertEqual(list(plan.replacement_requests), received)

    def test_runtime_content_has_no_actual_name_or_source_id_fields(self) -> None:
        editor, normalized = _build_fixture({2012: (4, 3)}, repeat_person=True)

        plan = build_source_backed_runtime_plan(editor, normalized)
        serialized = json.dumps(plan.runtime_content, ensure_ascii=False, sort_keys=True)
        actual_names = {
            player["playerName"]
            for document in normalized
            for player in document["players"]
        }

        for actual_name in actual_names:
            self.assertNotIn(actual_name, serialized)
        self.assertNotIn('"sourcePlayerId"', serialized)
        self.assertNotIn('"sourceReferenceNames"', serialized)
        self.assertNotIn('"originalName"', serialized)
        self.assertNotIn('"abilityDerivationTrace"', serialized)

    def test_same_input_and_seed_produce_identical_bytes(self) -> None:
        editor, normalized = _build_fixture({2012: (31, 17), 2013: (33, 19)}, repeat_person=True)

        first = build_source_backed_runtime_plan(editor, normalized, allocation_seed=44)
        second = build_source_backed_runtime_plan(editor, normalized, allocation_seed=44)

        self.assertEqual(canonical_json_bytes(first), canonical_json_bytes(second))

    def test_multi_reference_season_is_rejected(self) -> None:
        editor, normalized = _build_fixture({2012: (2, 1)})
        editor["years"][0]["playerSeasons"][0]["sourceReferenceNames"].append("다른실제선수")

        with self.assertRaisesRegex(ValueError, "하나의 normalized Source"):
            build_source_backed_runtime_plan(editor, normalized)

    def test_runtime_source_season_marks_provenance_and_preserves_role_confidence(self) -> None:
        editor, normalized = _build_fixture({1988: (2, 2)})
        source_pitcher = next(
            season
            for season in editor["years"][0]["playerSeasons"]
            if season["playerType"] == "Pitcher"
        )
        source_pitcher["pitcherRoleConfidence"] = 0.375

        plan = build_source_backed_runtime_plan(editor, normalized)
        runtime_pitcher = next(
            season
            for season in plan.runtime_content["years"][0]["playerSeasons"]
            if season["playerSeasonId"]
            == runtime_player_season_id("SRC_1988_Pitcher_0000", 1988)
        )

        self.assertEqual("SourceBacked", runtime_pitcher["dataProvenance"])
        self.assertEqual(0.375, runtime_pitcher["pitcherRoleConfidence"])


def _build_fixture(
    year_counts: dict[int, tuple[int, int]],
    *,
    repeat_person: bool = False,
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    normalized: list[dict[str, Any]] = []
    editor_years: list[dict[str, Any]] = []
    source_persons: dict[str, dict[str, Any]] = {}

    for year_index, (year, counts) in enumerate(sorted(year_counts.items())):
        hitter_count, pitcher_count = counts
        players: list[dict[str, Any]] = []
        seasons: list[dict[str, Any]] = []
        records: list[dict[str, Any]] = []
        for player_type, count in (("Hitter", hitter_count), ("Pitcher", pitcher_count)):
            for index in range(count):
                if repeat_person and index == 0:
                    source_id = "SRC_SHARED_H" if player_type == "Hitter" else "SRC_SHARED_P"
                    actual_name = "실제공유타자" if player_type == "Hitter" else "실제공유투수"
                else:
                    source_id = f"SRC_{year}_{player_type}_{index:04d}"
                    actual_name = f"실제{year_index:02d}{player_type[0]}{index:04d}"
                cost = 1 + index % 10
                player = {
                    "sourcePlayerId": source_id,
                    "playerName": actual_name,
                    "cost": cost,
                }
                players.append(player)
                editor_person_id_value = editor_source_person_id(source_id)
                editor_season_id_value = editor_source_season_id(source_id, year)
                source_persons.setdefault(
                    source_id,
                    {
                        "playerPersonId": editor_person_id_value,
                        "originalName": actual_name,
                        "primaryPosition": "SS" if player_type == "Hitter" else "P",
                        "careerStartYear": year,
                        "careerEndYear": year,
                    },
                )
                source_persons[source_id]["careerEndYear"] = year
                seasons.append(
                    {
                        "playerSeasonId": editor_season_id_value,
                        "playerPersonId": editor_person_id_value,
                        "originYear": year,
                        "originFranchiseId": "실제구단",
                        "originTeamSeasonKey": f"실제구단_{year}",
                        "position": "SS" if player_type == "Hitter" else "P",
                        "pitcherRole": "" if player_type == "Hitter" else "Starter",
                        "playerType": player_type,
                        "registrationType": "Domestic",
                        "baseAttributes": {"contact": 50 + index % 7},
                        "cost": cost,
                        "trainingCeiling": 70,
                        "rosterRole": "Starter",
                        "sourceReferenceNames": [actual_name],
                        "isOriginalSourceSeason": True,
                        "abilityDerivationTrace": {"sourcePlayerId": source_id},
                    }
                )
                records.append(
                    {
                        "playerSeasonId": editor_season_id_value,
                        "seasonYear": year,
                        "teamSeasonKey": f"실제구단_{year}",
                        "games": 1,
                        "source": "fixture",
                    }
                )
        normalized.append({"year": year, "players": players})
        editor_years.append(
            {
                "year": year,
                "playerSeasons": seasons,
                "normalCards": [],
                "teamSeasons": [],
                "originalSeasonRecords": records,
                "originalAwardRecords": [],
            }
        )

    editor = {
        "schemaVersion": "fixture-v1",
        "playerPersons": sorted(
            source_persons.values(),
            key=lambda person: person["playerPersonId"],
        ),
        "years": editor_years,
        "manifest": {},
    }
    return editor, normalized


if __name__ == "__main__":
    unittest.main()
