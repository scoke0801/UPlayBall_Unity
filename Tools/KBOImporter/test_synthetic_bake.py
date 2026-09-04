from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path

from synthetic_bake import (
    COMMON_KOREAN_SURNAMES,
    DEFENSIVE_HITTER_POSITIONS,
    assign_source_team_roles,
    bake,
    build_adjusted_feature_pool,
    build_editor_original_content,
    create_runtime_safe_content,
    derive_pitcher_role_availability,
    derive_source_position,
    derive_source_pitcher_role,
    load_and_validate_editor_asset_archive,
    percentile_cost,
    select_defensive_starters,
    write_editor_asset_archive,
)


class SyntheticBakeTests(unittest.TestCase):
    def test_cost_percentile_boundaries(self) -> None:
        costs = [percentile_cost(rank, 1000) for rank in range(1000)]
        self.assertEqual(costs.count(1), 50)
        self.assertEqual(costs.count(10), 30)
        self.assertEqual(min(costs), 1)
        self.assertEqual(max(costs), 10)

    def test_same_seed_bakes_source_one_to_one_and_minimum_replacements(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fixture = self._reference_fixture(
                2099,
                [
                    self._hitter("실제이름가", 0.280, 12),
                    self._hitter("실제이름나", 0.310, 22),
                    self._pitcher("실제이름다", 3.10, 150),
                    self._pitcher("실제이름라", 4.20, 90),
                ],
            )
            (root / "2099.json").write_text(
                json.dumps(fixture, ensure_ascii=False), encoding="utf-8"
            )

            first = bake(root, [2099], 77)
            second = bake(root, [2099], 77)

            self.assertEqual(first, second)
            year = first["years"][0]
            self.assertEqual(len(year["teamSeasons"]), 10)
            self.assertEqual(len(year["playerSeasons"]), 250)
            self.assertEqual(len(first["playerPersons"]), 250)
            self.assertEqual(
                sum(season["dataProvenance"] == "SourceBacked" for season in year["playerSeasons"]),
                4,
            )
            self.assertEqual(
                sum(season["dataProvenance"] == "ReplacementGenerated" for season in year["playerSeasons"]),
                246,
            )
            self.assertTrue(all(len(team["core25CardIds"]) == 25 for team in year["teamSeasons"]))
            self.assertTrue(all(len(team["allNormalCardIds"]) == 25 for team in year["teamSeasons"]))
            self.assertEqual(year["originalAwardRecords"], [])
            serialized = json.dumps(first, ensure_ascii=False)
            self.assertNotIn("originalName", serialized)
            self.assertNotIn("sourceReferenceNames", serialized)
            self.assertNotIn("실제이름가", serialized)

            year["playerSeasons"][0]["positionRoleDerivationTrace"] = {"reason": "Editor only"}
            year["teamSeasons"][0]["rosterSelectionTrace"] = {"reason": "Editor only"}
            year["teamSeasons"][0]["validationWarnings"] = [{"code": "Editor only"}]
            runtime_content = create_runtime_safe_content(first)
            runtime_serialized = json.dumps(runtime_content, ensure_ascii=False)
            self.assertNotIn("sourcePlayerId", serialized)
            self.assertNotIn("sourceTeamId", serialized)
            self.assertNotIn("originalName", runtime_serialized)
            self.assertNotIn("sourceReferenceNames", runtime_serialized)
            self.assertNotIn("실제이름가", runtime_serialized)
            self.assertNotIn("positionRoleDerivationTrace", runtime_serialized)
            self.assertNotIn("rosterSelectionTrace", runtime_serialized)
            fictional_names = [
                person["fictionalName"] for person in runtime_content["playerPersons"]
            ]
            self.assertEqual(len(fictional_names), len(set(fictional_names)))
            self.assertTrue(all(len(name) == 3 for name in fictional_names))
            self.assertTrue(all(name[0] in COMMON_KOREAN_SURNAMES for name in fictional_names))
            self.assertTrue(all(len(set(name)) == 3 for name in fictional_names))

    def test_editor_asset_archive_is_split_and_deterministic(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            fixture = self._reference_fixture(
                2099,
                [
                    self._hitter("실제이름가", 0.280, 12),
                    self._hitter("실제이름나", 0.310, 22),
                    self._pitcher("실제이름다", 3.10, 150),
                    self._pitcher("실제이름라", 4.20, 90),
                ],
            )
            (root / "2099.json").write_text(
                json.dumps(fixture, ensure_ascii=False), encoding="utf-8"
            )
            content = build_editor_original_content(root, [2099])

            first_manifest = write_editor_asset_archive(content, root / "first")
            second_manifest = write_editor_asset_archive(content, root / "second")
            reloaded = load_and_validate_editor_asset_archive(root / "first")
            runtime_content = create_runtime_safe_content(bake(root, [2099], 77))
            write_editor_asset_archive(runtime_content, root / "runtime")
            reloaded_runtime = load_and_validate_editor_asset_archive(root / "runtime")

            self.assertEqual(first_manifest, second_manifest)
            self.assertEqual(reloaded, content)
            self.assertEqual(reloaded_runtime, runtime_content)
            self.assertTrue((root / "first" / "manifest.json").is_file())
            self.assertTrue((root / "first" / "player_persons.json").is_file())
            self.assertTrue((root / "first" / "Years" / "2099.json").is_file())
            self.assertEqual(len(content["playerPersons"]), 4)
            self.assertEqual(len(content["years"][0]["playerSeasons"]), 4)
            warning_codes = [
                warning["code"]
                for warning in content["years"][0]["teamSeasons"][0]["validationWarnings"]
                if warning["code"].endswith("_POOL_SHORTAGE")
            ]
            self.assertEqual(len(warning_codes), len(set(warning_codes)))
            self.assertIn("ROSTER_TOTAL_POOL_SHORTAGE", warning_codes)
            self.assertTrue(all("fictionalName" not in person for person in content["playerPersons"]))
            self.assertTrue(all(
                season["sourceReferenceNames"] == [
                    next(
                        person["originalName"]
                        for person in content["playerPersons"]
                        if person["playerPersonId"] == season["playerPersonId"]
                    )
                ]
                for season in content["years"][0]["playerSeasons"]
            ))

    def test_hitter_core25_assigns_defensive_eight_before_dh_and_bench(self) -> None:
        hitters = [
            self._hitter_row(position, 55 + index, position)
            for index, position in enumerate(DEFENSIVE_HITTER_POSITIONS)
        ]
        hitters.extend(
            [
                self._hitter_row("slugger-1b", 95, "1B"),
                self._hitter_row("slugger-rf", 94, "RF"),
                self._hitter_row("dh-best", 93, "DH"),
                self._hitter_row("bench-a", 60, "LF"),
                self._hitter_row("bench-b", 59, "CF"),
                self._hitter_row("bench-c", 58, "C"),
            ]
        )
        pitchers = self._pitcher_rows()
        sources = {
            row["playerSeasonId"]: row.pop("_source")
            for row in hitters + pitchers
        }

        core, trace = assign_source_team_roles(hitters + pitchers, sources)

        self.assertEqual(
            [row["rosterRole"] for row in core[:9]],
            [f"StartingHitter:{position}" for position in (*DEFENSIVE_HITTER_POSITIONS, "DH")],
        )
        self.assertEqual([row["position"] for row in core[:8]], list(DEFENSIVE_HITTER_POSITIONS))
        self.assertEqual(core[4]["playerSeasonId"], "SS")
        self.assertEqual(core[8]["playerSeasonId"], "dh-best")
        self.assertEqual(sum(row["rosterRole"].startswith("BenchHitter") for row in core), 5)
        self.assertEqual(len({row["playerSeasonId"] for row in core}), 25)
        self.assertFalse(any(
            warning["code"] == "ROSTER_POSITION_FALLBACK"
            for warning in trace["validationWarnings"]
        ))

    def test_multi_position_matching_is_deterministic_and_fallback_only_when_needed(self) -> None:
        rows = [
            self._hitter_row("catcher", 50, "C"),
            self._hitter_row("first", 50, "1B"),
            self._hitter_row("utility", 80, "2B", ("2B", "SS")),
            self._hitter_row("second", 50, "2B"),
            self._hitter_row("third", 50, "3B"),
            self._hitter_row("left", 50, "LF"),
            self._hitter_row("center", 50, "CF"),
            self._hitter_row("right", 50, "RF"),
        ]
        sources = {row["playerSeasonId"]: row.pop("_source") for row in rows}

        first, _, first_warnings = select_defensive_starters(rows, sources)
        second, _, second_warnings = select_defensive_starters(list(reversed(rows)), sources)

        self.assertEqual(
            [row["playerSeasonId"] for row in first],
            [row["playerSeasonId"] for row in second],
        )
        self.assertEqual(first[4]["playerSeasonId"], "utility")
        self.assertFalse(any(warning["code"] == "ROSTER_POSITION_FALLBACK" for warning in first_warnings))
        self.assertEqual(first_warnings, second_warnings)

        sources["utility"]["defenseRecords"] = [self._defense_record("2B")]
        _, _, fallback_warnings = select_defensive_starters(rows, sources)
        self.assertTrue(any(
            warning["code"] == "ROSTER_POSITION_FALLBACK"
            and warning["position"] == "SS"
            for warning in fallback_warnings
        ))

    def test_pitcher_role_uses_season_usage_and_roster_assignment_does_not_mutate_it(self) -> None:
        starter, starter_trace = derive_source_pitcher_role(
            self._pitcher_source(games=28, games_started=24, innings_outs=450)
        )
        reliever, _ = derive_source_pitcher_role(
            self._pitcher_source(games=45, games_started=2, innings_outs=240)
        )
        closer, _ = derive_source_pitcher_role(
            self._pitcher_source(games=50, games_started=0, innings_outs=150, saves=28, games_finished=36)
        )
        setup, _ = derive_source_pitcher_role(
            self._pitcher_source(games=55, games_started=0, innings_outs=165, holds=24)
        )
        no_sample, no_sample_trace = derive_source_pitcher_role(self._pitcher_source())

        self.assertEqual(starter, "Starter")
        self.assertEqual(starter_trace["pitcherRoleEvidence"]["gamesStarted"], 24)
        self.assertNotEqual(reliever, "Starter")
        self.assertEqual(closer, "Closer")
        self.assertEqual(setup, "Setup")
        self.assertEqual(no_sample, "MiddleRelief")
        self.assertTrue(any(
            warning["code"] == "PITCHER_ROLE_LOW_CONFIDENCE"
            for warning in no_sample_trace["warnings"]
        ))

        pitchers = self._pitcher_rows(include_role_shortage=True)
        original_roles = {row["playerSeasonId"]: row["pitcherRole"] for row in pitchers}
        hitters = [
            self._hitter_row(position, 55, position)
            for position in DEFENSIVE_HITTER_POSITIONS
        ] + [self._hitter_row(f"bench-{index}", 50, "DH") for index in range(6)]
        sources = {
            row["playerSeasonId"]: row.pop("_source")
            for row in hitters + pitchers
        }
        _, trace = assign_source_team_roles(hitters + pitchers, sources)

        self.assertEqual(
            {row["playerSeasonId"]: row["pitcherRole"] for row in pitchers},
            original_roles,
        )
        self.assertTrue(any(
            warning["code"] == "PITCHER_ROLE_FALLBACK"
            for warning in trace["validationWarnings"]
        ))

    def test_pitcher_role_uses_cg_and_innings_fallback_when_league_gs_is_unavailable(self) -> None:
        legacy_ace = self._pitcher_source(games=36, innings_outs=674, complete_games=15)
        legacy_ace["sourcePlayerId"] = "legacy-ace"
        legacy_reliever = self._pitcher_source(games=45, innings_outs=180, saves=12)
        legacy_reliever["sourcePlayerId"] = "legacy-reliever"
        availability = derive_pitcher_role_availability([legacy_ace, legacy_reliever])

        ace_role, ace_trace = derive_source_pitcher_role(legacy_ace, availability)
        reliever_role, reliever_trace = derive_source_pitcher_role(legacy_reliever, availability)

        self.assertFalse(availability["gamesStarted"])
        self.assertEqual(ace_role, "Starter")
        self.assertNotEqual(reliever_role, "Starter")
        self.assertFalse(ace_trace["pitcherRoleEvidence"]["gamesStartedAvailable"])
        self.assertEqual(
            ace_trace["pitcherRoleEvidence"]["starterEvidenceMode"],
            "CompleteGamesAndInningsPerGameProxy",
        )
        self.assertFalse(reliever_trace["pitcherRoleEvidence"]["gamesFinishedAvailable"])
        _, _, groups = build_adjusted_feature_pool(
            [legacy_ace, legacy_reliever],
            1982,
            "Pitcher",
            availability,
        )
        self.assertEqual(groups["legacy-ace"], "1982:Starter")
        self.assertEqual(
            groups["legacy-ace"].split(":")[1],
            ace_trace["selectedNaturalPitcherRole"],
        )

    def test_position_and_pitcher_role_are_derived_per_season_not_from_career_metadata(self) -> None:
        first_position, _ = derive_source_position(
            {"defenseRecords": [self._defense_record("SS")]},
            "DH",
        )
        second_position, _ = derive_source_position(
            {"defenseRecords": [self._defense_record("1B")]},
            "DH",
        )
        first_role, _ = derive_source_pitcher_role(
            self._pitcher_source(games=25, games_started=20, innings_outs=390)
        )
        second_source = self._pitcher_source(games=45, innings_outs=135)
        second_source["pitcherRole"] = "Starter"
        second_role, _ = derive_source_pitcher_role(second_source)

        self.assertEqual(first_position, "SS")
        self.assertEqual(second_position, "1B")
        self.assertEqual(first_role, "Starter")
        self.assertEqual(second_role, "MiddleRelief")

    @classmethod
    def _hitter_row(
        cls,
        player_season_id: str,
        rating: int,
        position: str,
        eligible_positions: tuple[str, ...] | None = None,
    ) -> dict:
        positions = eligible_positions
        if positions is None:
            positions = () if position == "DH" else (position,)
        source = {
            "sourcePlayerId": player_season_id,
            "hitterStats": {"plateAppearances": 400, "games": 100},
            "defenseRecords": [cls._defense_record(value) for value in positions],
        }
        return {
            "playerSeasonId": player_season_id,
            "playerPersonId": f"person-{player_season_id}",
            "playerType": "Hitter",
            "position": position,
            "pitcherRole": "",
            "baseAttributes": [rating] * 12,
            "rosterRole": "",
            "_source": source,
        }

    @classmethod
    def _pitcher_rows(cls, include_role_shortage: bool = False) -> list[dict]:
        sources: list[tuple[str, dict]] = []
        if include_role_shortage:
            sources = [
                (f"reliever-{index}", cls._pitcher_source(games=40, innings_outs=120))
                for index in range(11)
            ]
        else:
            sources.extend(
                (f"starter-{index}", cls._pitcher_source(games=25, games_started=20, innings_outs=390))
                for index in range(5)
            )
            sources.extend(
                (f"reliever-{index}", cls._pitcher_source(games=45, innings_outs=135))
                for index in range(4)
            )
            sources.append(("setup", cls._pitcher_source(games=50, innings_outs=150, holds=20)))
            sources.append(("closer", cls._pitcher_source(games=50, innings_outs=150, saves=25, games_finished=35)))

        rows: list[dict] = []
        for player_season_id, source in sources:
            natural_role, trace = derive_source_pitcher_role(source)
            rows.append(
                {
                    "playerSeasonId": player_season_id,
                    "playerPersonId": f"person-{player_season_id}",
                    "playerType": "Pitcher",
                    "position": "P",
                    "pitcherRole": natural_role,
                    "positionRoleDerivationTrace": trace,
                    "baseAttributes": [50] * 12,
                    "rosterRole": "",
                    "_source": source,
                }
            )
        return rows

    @staticmethod
    def _defense_record(position: str) -> dict:
        names = {
            "C": "포수",
            "1B": "1루수",
            "2B": "2루수",
            "3B": "3루수",
            "SS": "유격수",
            "LF": "좌익수",
            "CF": "중견수",
            "RF": "우익수",
        }
        return {
            "position": names[position],
            "inningsOuts": 300,
            "gamesStarted": 30,
            "games": 40,
        }

    @staticmethod
    def _pitcher_source(
        games: int = 0,
        games_started: int = 0,
        innings_outs: int = 0,
        saves: int = 0,
        holds: int = 0,
        games_finished: int = 0,
        complete_games: int = 0,
    ) -> dict:
        return {
            "pitcherStats": {
                "games": games,
                "gamesStarted": games_started,
                "inningsOuts": innings_outs,
                "saves": saves,
                "holds": holds,
                "gamesFinished": games_finished,
                "completeGames": complete_games,
            }
        }

    @staticmethod
    def _reference_fixture(year: int, players: list[dict]) -> dict:
        for player in players:
            player["year"] = year
        return {
            "schemaVersion": 3,
            "importerVersion": "1.2.0",
            "year": year,
            "isSeasonComplete": True,
            "sourceMetadata": {
                "schemaVersion": 3,
                "importerVersion": "1.2.0",
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
            "awardAvailabilityStatus": {
                "RegularSeasonMvp": "AvailableEmpty",
                "AllStarGameMvp": "Unavailable",
                "KoreanSeriesMvp": "Unavailable",
                "GoldenGlove": "AvailableEmpty",
                "AllStarSelection": "NotSelected",
            },
        }

    @staticmethod
    def _hitter(name: str, average: float, home_runs: int) -> dict:
        return {
            "sourcePlayerId": name,
            "playerName": name,
            "aggregateTeamName": "원본팀",
            "hitterStats": {
                "aggregateOrigin": "SingleTeamStint",
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
            "runningStats": {
                "aggregateOrigin": "SingleTeamStint",
                "stolenBases": 8,
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
    def _pitcher(name: str, earned_run_average: float, innings: int) -> dict:
        return {
            "sourcePlayerId": name,
            "playerName": name,
            "aggregateTeamName": "원본팀",
            "hitterStats": None,
            "pitcherStats": {
                "aggregateOrigin": "SingleTeamStint",
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
