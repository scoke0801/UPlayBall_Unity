from __future__ import annotations

import unittest
from pathlib import Path

from synthetic_bake import (
    ABILITY_NAMES,
    assign_origin_year_costs,
    build_editor_original_content,
    build_adjusted_feature_pool,
    build_ability_validation_warnings,
    role_adjusted_composite,
    to_ratings_with_trace,
)


class AbilityCostDerivationTests(unittest.TestCase):
    def test_adjusted_features_and_cost_are_deterministic(self) -> None:
        players = [
            self._hitter("first", 400, 1, 0),
            self._hitter("second", 400, 12, 4, average=0.280),
        ]
        first = build_adjusted_feature_pool(players, 2099, "Hitter")
        second = build_adjusted_feature_pool(players, 2099, "Hitter")
        self.assertEqual(first, second)

        first_seasons = [
            self._season("A", 2099, "1B", [50, 80, 80, 50, 50, 50]),
            self._season("B", 2099, "SS", [50, 50, 80, 80, 80, 50]),
        ]
        second_seasons = [dict(season) for season in first_seasons]
        assign_origin_year_costs(first_seasons)
        assign_origin_year_costs(second_seasons)
        self.assertEqual(first_seasons, second_seasons)

    def test_low_sample_warning_requires_actual_component_dominance(self) -> None:
        trace = [
            {
                "attribute": "Speed",
                "components": [
                    {"metric": "Small", "reliability": 0.10, "contribution": 0.30},
                    {"metric": "Reliable", "reliability": 0.80, "contribution": 0.10},
                ],
            }
        ]
        warnings = build_ability_validation_warnings(trace)
        self.assertEqual([warning["code"] for warning in warnings], ["ABILITY_LOW_SAMPLE_DOMINANCE"])

        trace[0]["components"][0]["contribution"] = 0.05
        self.assertEqual(build_ability_validation_warnings(trace), [])

    def test_one_successful_steal_does_not_outrank_twenty_of_twenty_five(self) -> None:
        players = [
            self._hitter("one", 611, 1, 0),
            self._hitter("twenty", 611, 20, 5),
            self._hitter("zero", 611, 0, 0),
            self._hitter("middle", 611, 8, 4),
        ]
        vectors, traces, groups = build_adjusted_feature_pool(players, 2020, "Hitter")

        one_ratings, _ = to_ratings_with_trace(
            "Hitter", vectors["one"], traces["one"], "SEASON_ONE", 2020, groups["one"]
        )
        twenty_ratings, _ = to_ratings_with_trace(
            "Hitter", vectors["twenty"], traces["twenty"], "SEASON_TWENTY", 2020, groups["twenty"]
        )

        self.assertLess(one_ratings[2], twenty_ratings[2])
        self.assertLess(
            traces["one"]["StolenBaseSuccessRate"]["reliability"],
            traces["twenty"]["StolenBaseSuccessRate"]["reliability"],
        )
        self.assertAlmostEqual(
            traces["one"]["StolenBaseSuccessRate"]["reliability"],
            1.0 / 21.0,
            places=7,
        )

    def test_caught_stealing_null_and_zero_are_distinct_evidence(self) -> None:
        players = [
            self._hitter("known", 400, 1, 0),
            self._hitter("unknown", 400, 1, None),
        ]
        _, traces, _ = build_adjusted_feature_pool(players, 2020, "Hitter")

        known = traces["known"]["StolenBaseSuccessRate"]
        unknown = traces["unknown"]["StolenBaseSuccessRate"]
        self.assertTrue(known["isAvailable"])
        self.assertGreater(known["reliability"], 0.0)
        self.assertFalse(unknown["isAvailable"])
        self.assertEqual(unknown["reliability"], 0.0)

    def test_plate_appearance_reliability_shrinks_same_extreme_rate(self) -> None:
        players = [
            self._hitter("small", 20, 1, 0, average=0.400),
            self._hitter("larger", 200, 10, 0, average=0.400),
            self._hitter("baseline_a", 100, 3, 1, average=0.250),
            self._hitter("baseline_b", 150, 4, 2, average=0.280),
        ]
        _, traces, groups = build_adjusted_feature_pool(players, 2099, "Hitter")

        self.assertEqual(groups["small"], groups["larger"])
        self.assertLess(
            abs(traces["small"]["BattingAverage"]["adjustedZ"]),
            abs(traces["larger"]["BattingAverage"]["adjustedZ"]),
        )

    def test_cost_uses_origin_year_population_and_role_weights(self) -> None:
        seasons = [
            self._season("A", 2099, "1B", [50, 80, 80, 50, 50, 50]),
            self._season("B", 2099, "SS", [50, 50, 80, 80, 80, 50]),
            self._season("C", 2100, "1B", [60, 60, 60, 60, 60, 60]),
        ]
        assign_origin_year_costs(seasons)

        self.assertEqual(seasons[0]["costDerivationTrace"]["populationCount"], 2)
        self.assertEqual(seasons[1]["costDerivationTrace"]["populationCount"], 2)
        self.assertEqual(seasons[2]["costDerivationTrace"]["populationCount"], 1)
        first_base_trace = seasons[0]["costDerivationTrace"]
        power = next(x for x in first_base_trace["abilityContribution"] if x["ability"] == "Power")
        speed = next(x for x in first_base_trace["abilityContribution"] if x["ability"] == "Speed")
        self.assertGreater(power["weight"], speed["weight"])

    def test_award_and_display_name_do_not_change_cost_composite(self) -> None:
        season = self._season("SAME", 2099, "1B", [60, 70, 40, 50, 55, 65])
        before, _ = role_adjusted_composite(season)
        season["playerName"] = "유명 선수"
        season["awards"] = ["Mvp", "GoldenGlove"]
        after, _ = role_adjusted_composite(season)
        self.assertEqual(before, after)

    def test_ability_trace_contains_explainable_components(self) -> None:
        players = [
            self._hitter("target", 400, 12, 4),
            self._hitter("peer", 400, 5, 3, average=0.250),
        ]
        vectors, traces, groups = build_adjusted_feature_pool(players, 2099, "Hitter")
        _, ability_trace = to_ratings_with_trace(
            "Hitter", vectors["target"], traces["target"], "SEASON_TARGET", 2099, groups["target"]
        )
        speed = next(item for item in ability_trace if item["attribute"] == "Speed")
        self.assertEqual(speed["playerSeasonId"], "SEASON_TARGET")
        self.assertEqual(
            [component["metric"] for component in speed["components"]],
            ["StolenBaseAttemptRate", "StolenBaseSuccessRate"],
        )
        for component in speed["components"]:
            for key in (
                "rawValue", "numerator", "denominator", "sampleSize", "groupMean",
                "groupStdDev", "rawZ", "reliability", "adjustedZ", "weight", "contribution",
            ):
                self.assertIn(key, component)

    def test_actual_2020_and_2012_audit_fixtures(self) -> None:
        normalized = Path(__file__).parent / ".cache" / "KBOImport" / "Normalized"
        if not (normalized / "2012.json").is_file() or not (normalized / "2020.json").is_file():
            self.skipTest("로컬 KBO Normalized audit fixture가 없습니다.")

        content = build_editor_original_content(normalized, [2012, 2020])
        person_names = {
            person["playerPersonId"]: person["originalName"]
            for person in content["playerPersons"]
        }
        seasons_by_year = {
            year["year"]: year["playerSeasons"]
            for year in content["years"]
        }
        lee_dae_ho = next(
            season for season in seasons_by_year[2020]
            if person_names[season["playerPersonId"]] == "이대호"
        )
        speed_trace = next(
            trace for trace in lee_dae_ho["abilityDerivationTrace"]
            if trace["attribute"] == "Speed"
        )
        success = next(
            component for component in speed_trace["components"]
            if component["metric"] == "StolenBaseSuccessRate"
        )
        self.assertEqual((success["numerator"], success["denominator"]), (1.0, 1.0))
        self.assertLess(success["reliability"], 0.05)
        self.assertLess(lee_dae_ho["baseAttributes"][2], 60)
        self.assertNotIn(
            "ABILITY_LOW_SAMPLE_DOMINANCE",
            [warning["code"] for warning in lee_dae_ho["derivationWarnings"]],
        )
        self.assertEqual(
            lee_dae_ho["costDerivationTrace"]["populationCount"],
            len(seasons_by_year[2020]),
        )

        audit_names = {"안치용", "박진만", "로페즈", "최정", "박희수"}
        audited = [
            season for season in seasons_by_year[2012]
            if person_names[season["playerPersonId"]] in audit_names
        ]
        self.assertEqual(len(audited), len(audit_names))
        self.assertTrue(all(season["costDerivationTrace"]["populationCount"] == len(seasons_by_year[2012]) for season in audited))
        self.assertTrue(all(season["costDerivationTrace"]["rank"] > 0 for season in audited))

    @staticmethod
    def _hitter(
        source_id: str,
        plate_appearances: int,
        stolen_bases: int,
        caught_stealing: int | None,
        average: float = 0.300,
    ) -> dict:
        at_bats = max(1, int(plate_appearances * 0.9))
        attempts = stolen_bases + (caught_stealing or 0)
        return {
            "sourcePlayerId": source_id,
            "hitterStats": {
                "sourceAVG": average,
                "sourceOBP": average + 0.060,
                "sourceSLG": average + 0.150,
                "plateAppearances": plate_appearances,
                "atBats": at_bats,
                "hits": round(average * at_bats),
                "homeRuns": 10,
                "walks": 30,
                "strikeouts": 60,
            },
            "runningStats": {
                "stolenBases": stolen_bases,
                "caughtStealing": caught_stealing,
                "stolenBaseAttempts": attempts,
            },
            "defenseRecords": [
                {"position": "1루수", "inningsOuts": 600, "putouts": 200, "assists": 20, "errors": 2}
            ],
        }

    @staticmethod
    def _season(season_id: str, year: int, position: str, hitter_ratings: list[int]) -> dict:
        return {
            "playerSeasonId": season_id,
            "originYear": year,
            "position": position,
            "pitcherRole": "",
            "playerType": "Hitter",
            "baseAttributes": hitter_ratings + [50] * (len(ABILITY_NAMES) - 6),
            "cost": 0,
        }


if __name__ == "__main__":
    unittest.main()
