from __future__ import annotations

import copy
import unittest
from pathlib import Path

from synthetic_bake import (
    ABILITY_NAMES,
    DERIVATION_BALANCE,
    assign_origin_year_costs,
    build_editor_original_content,
    build_adjusted_feature_pool,
    build_ability_validation_warnings,
    build_metric_influence_warnings,
    derive_source_pitcher_role,
    derive_player_value_components,
    metric_composite_influence_audit,
    role_adjusted_composite,
    to_ratings_with_trace,
    validate_derivation_balance,
    headroom_range,
)


class AbilityCostDerivationTests(unittest.TestCase):
    def test_cost_workload_uses_classifier_proxy_when_games_started_is_unavailable(self) -> None:
        season = self._pitcher_season("LEGACY_STARTER", 1982, "Starter", [60] * 6, 450.0)
        self._with_cost_quality(season, 0.5, 0.8)
        season["sourceSeasonGames"] = 80
        season["_costValueInputs"] = {
            "inningsOuts": 450,
            "games": 30,
            "gamesStarted": 0,
            "gamesStartedAvailable": False,
            "inferredStarterRate": 0.8,
            "starterEvidenceMode": "CompleteGamesAndInningsPerGameProxy",
        }

        components = derive_player_value_components(season, 60.0)

        workload = components["workload"]
        self.assertEqual(workload["starterShare"], 0.8)
        self.assertEqual(
            workload["starterShareOrigin"],
            "Inferred:CompleteGamesAndInningsPerGameProxy",
        )

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
            abs(traces["small"]["BattingAverage"]["adjustedZ"] - traces["small"]["BattingAverage"]["priorZ"]),
            abs(traces["larger"]["BattingAverage"]["adjustedZ"] - traces["larger"]["BattingAverage"]["priorZ"]),
        )

    def test_qualified_boundary_uses_one_continuous_baseline(self) -> None:
        players = [
            self._rate_hitter("below", 249, 0.340),
            self._rate_hitter("above", 250, 0.340),
            self._rate_hitter("peer_low", 400, 0.240),
            self._rate_hitter("peer_middle", 400, 0.280),
        ]
        vectors, traces, groups = build_adjusted_feature_pool(players, 2099, "Hitter")
        below_ratings, below_trace = to_ratings_with_trace(
            "Hitter", vectors["below"], traces["below"], "BELOW", 2099, groups["below"]
        )
        above_ratings, above_trace = to_ratings_with_trace(
            "Hitter", vectors["above"], traces["above"], "ABOVE", 2099, groups["above"]
        )

        self.assertEqual(groups["below"], "2099:1B")
        self.assertEqual(groups["below"], groups["above"])
        self.assertEqual(traces["below"]["BattingAverage"]["rawZ"], traces["above"]["BattingAverage"]["rawZ"])
        self.assertEqual(below_trace[0]["roleTier"], "Limited")
        self.assertEqual(above_trace[0]["roleTier"], "Qualified")
        self.assertLessEqual(abs(below_ratings[0] - above_ratings[0]), 1)

    def test_less_sample_never_strengthens_same_rate_deviation(self) -> None:
        players = [
            self._rate_hitter("small", 80, 0.340),
            self._rate_hitter("large", 640, 0.340),
            self._rate_hitter("peer_low", 400, 0.240),
            self._rate_hitter("peer_middle", 400, 0.280),
        ]
        _, traces, groups = build_adjusted_feature_pool(players, 2099, "Hitter")

        self.assertEqual(groups["small"], groups["large"])
        for metric in ("BattingAverage", "OnBasePercentage", "SluggingPercentage"):
            small = traces["small"][metric]
            large = traces["large"][metric]
            self.assertEqual(small["rawZ"], large["rawZ"])
            self.assertLess(abs(small["adjustedZ"] - small["priorZ"]), abs(large["adjustedZ"] - large["priorZ"]))

    def test_arm_and_defense_use_independent_evidence(self) -> None:
        arm_metrics = set(DERIVATION_BALANCE["ratingProfiles"]["Hitter"]["Arm"]["metrics"])
        defense_metrics = set(DERIVATION_BALANCE["ratingProfiles"]["Hitter"]["Defense"]["metrics"])
        self.assertNotIn("FieldingPercentage", arm_metrics)
        self.assertTrue(arm_metrics.isdisjoint(defense_metrics))

        players = [
            {**self._rate_hitter("no_defense", 400, 0.300), "defenseRecords": []},
            {**self._rate_hitter("peer", 400, 0.280), "defenseRecords": []},
        ]
        vectors, traces, groups = build_adjusted_feature_pool(players, 2099, "Hitter")
        ratings, ability_trace = to_ratings_with_trace(
            "Hitter", vectors["no_defense"], traces["no_defense"], "NO_DEFENSE", 2099, groups["no_defense"]
        )
        arm_trace = next(trace for trace in ability_trace if trace["attribute"] == "Arm")
        self.assertEqual(ratings[3], int(DERIVATION_BALANCE["rating"]["center"]))
        self.assertTrue(all(not component["isAvailable"] for component in arm_trace["components"]))

    def test_metric_influence_cap_catches_duplicated_raw_metric(self) -> None:
        for player_type, profiles in DERIVATION_BALANCE["roleCompositeProfiles"].items():
            for profile_name in profiles:
                audit = metric_composite_influence_audit(player_type, profile_name)
                self.assertFalse(audit["hasViolation"], f"{player_type}/{profile_name}")

        invalid = copy.deepcopy(DERIVATION_BALANCE)
        invalid["ratingProfiles"]["Hitter"]["Arm"]["metrics"] = {"FieldingPercentage": 1.0}
        invalid["ratingProfiles"]["Hitter"]["Defense"]["metrics"] = {"FieldingPercentage": 1.0}
        invalid["roleCompositeProfiles"]["Hitter"]["SS"] = [0.0, 0.0, 0.0, 0.5, 0.5, 0.0]
        invalid_audit = metric_composite_influence_audit("Hitter", "SS", invalid)
        self.assertEqual(
            [warning["code"] for warning in build_metric_influence_warnings(invalid_audit)],
            ["ABILITY_METRIC_INFLUENCE_CAP_EXCEEDED"],
        )
        with self.assertRaisesRegex(ValueError, "Raw metric"):
            validate_derivation_balance(invalid)

    def test_pitcher_role_confidence_distinguishes_proxy_and_direct_usage(self) -> None:
        legacy = self._pitcher(30, 0, 300)
        _, legacy_trace = derive_source_pitcher_role(
            legacy,
            {"gamesStarted": False, "gamesFinished": False, "holds": False},
        )
        modern_high = self._pitcher(20, 18, 300)
        _, high_trace = derive_source_pitcher_role(
            modern_high,
            {"gamesStarted": True, "gamesFinished": True, "holds": True},
        )
        modern_medium = self._pitcher(8, 6, 120)
        _, medium_trace = derive_source_pitcher_role(
            modern_medium,
            {"gamesStarted": True, "gamesFinished": True, "holds": True},
        )

        self.assertEqual(legacy_trace["pitcherRoleConfidence"], "Low")
        self.assertEqual(high_trace["pitcherRoleConfidence"], "High")
        self.assertEqual(medium_trace["pitcherRoleConfidence"], "Medium")
        self.assertLess(
            legacy_trace["roleMismatchPenaltyMultiplier"],
            medium_trace["roleMismatchPenaltyMultiplier"],
        )
        self.assertLess(
            medium_trace["roleMismatchPenaltyMultiplier"],
            high_trace["roleMismatchPenaltyMultiplier"],
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

    def test_tiny_sample_peers_do_not_inflate_reference_deviation(self) -> None:
        regulars = [
            self._rate_hitter(f"regular_{index}", 500, 0.260 + index * 0.005)
            for index in range(8)
        ]
        noise = [
            self._rate_hitter("noise_high", 2, 1.000),
            self._rate_hitter("noise_low", 2, 0.000),
        ]
        target = self._rate_hitter("target", 500, 0.330)

        clean_traces = build_adjusted_feature_pool(regulars + [target], 2099, "Hitter")[1]
        noisy_traces = build_adjusted_feature_pool(regulars + noise + [target], 2099, "Hitter")[1]

        clean = clean_traces["target"]["BattingAverage"]
        noisy = noisy_traces["target"]["BattingAverage"]
        self.assertAlmostEqual(clean["groupStdDev"], noisy["groupStdDev"], delta=0.01)
        self.assertAlmostEqual(clean["rawZ"], noisy["rawZ"], delta=0.2)
        self.assertGreater(noisy["rawZ"], 1.0)

    def test_tiny_sample_player_still_receives_own_ability(self) -> None:
        players = [
            self._rate_hitter(f"regular_{index}", 500, 0.250 + index * 0.005)
            for index in range(8)
        ] + [self._rate_hitter("tiny", 12, 0.500)]
        vectors, traces, groups = build_adjusted_feature_pool(players, 2099, "Hitter")

        tiny = traces["tiny"]["BattingAverage"]
        self.assertTrue(tiny["isAvailable"])
        self.assertGreater(tiny["rawZ"], 0.0)
        self.assertLess(tiny["referenceWeight"], 0.10)
        self.assertLess(abs(tiny["adjustedZ"]), abs(tiny["rawZ"]))

        ratings, _ = to_ratings_with_trace(
            "Hitter", vectors["tiny"], traces["tiny"], "TINY", 2099, groups["tiny"]
        )
        self.assertLess(ratings[0], int(DERIVATION_BALANCE["rating"]["center"]))

    def test_thin_group_blends_toward_position_family(self) -> None:
        players = [
            self._rate_hitter(f"infield_{index}", 500, 0.250 + index * 0.004)
            for index in range(10)
        ]
        for index, player in enumerate(players):
            player["defenseRecords"][0]["position"] = "1루수" if index else "유격수"
        vectors, traces, groups = build_adjusted_feature_pool(players, 2099, "Hitter")

        lone_shortstop = traces["infield_0"]["FieldingPercentage"]
        self.assertEqual(groups["infield_0"], "2099:SS")
        self.assertEqual(lone_shortstop["referenceFamilyKey"], "2099:Infield")
        self.assertLess(lone_shortstop["referenceGroupShare"], 1.0)
        self.assertGreater(lone_shortstop["groupStdDev"], 0.0)

    def test_workload_contributes_separately_from_ability(self) -> None:
        """기본 전력이 같아도 실제 시즌 workload가 큰 선수의 가격이 더 높다."""
        seasons = [
            self._pitcher_season("SHORT", 2099, "Starter", [60] * 6, 90.0),
            self._pitcher_season("FULL", 2099, "Starter", [60] * 6, 620.0),
        ]
        assign_origin_year_costs(seasons)
        self.assertLess(seasons[0]["cost"], seasons[1]["cost"])
        self.assertEqual(seasons[0]["costDerivationTrace"]["costEligibility"]["tier"], "Tiny")
        self.assertEqual(seasons[1]["costDerivationTrace"]["costEligibility"]["tier"], "Full")
        self.assertTrue(seasons[0]["costDerivationTrace"]["costEligibility"]["affectsCost"])

    def test_full_workload_closer_is_not_capped_by_starter_thresholds(self) -> None:
        """마무리 한 시즌은 선발보다 상대 타자가 적어도 온전한 시즌이다."""
        seasons = [
            self._pitcher_season(f"SP_{index:02d}", 2099, "Starter", [50] * 6, 620.0)
            for index in range(30)
        ]
        seasons.append(self._pitcher_season("CLOSER", 2099, "Closer", [75] * 6, 215.0))
        self._with_cost_quality(seasons[-1], 2.0, 0.70)
        assign_origin_year_costs(seasons)

        closer = next(season for season in seasons if season["playerSeasonId"] == "CLOSER")
        eligibility = closer["costDerivationTrace"]["costEligibility"]
        self.assertEqual(eligibility["scope"], "Relief")
        self.assertEqual(eligibility["tier"], "Full")
        self.assertGreaterEqual(eligibility["workloadRatio"], 0.75)
        self.assertGreaterEqual(closer["costEligibilitySample"], 190.0)
        self.assertGreaterEqual(closer["cost"], 9)

    def test_cost_depends_on_ability_not_peer_population(self) -> None:
        """주변에 약한 선수를 추가해도 이미 결정된 선수 가격은 바뀌지 않는다."""
        target = self._pitcher_season("TARGET", 2099, "Starter", [54] * 6, 110.0)
        assign_origin_year_costs([target])
        before = target["cost"]
        peers = [self._pitcher_season(f"PEER_{i}", 2099, "Starter", [30] * 6, 620.0) for i in range(40)]
        assign_origin_year_costs([target] + peers)
        self.assertEqual(before, target["cost"])
        self.assertEqual(target["costDerivationTrace"]["costMethod"], "SeasonValueOrdinalWithEliteGate")

    def test_training_headroom_does_not_favor_low_cost(self) -> None:
        """기본 전력이 낮다는 이유로 더 큰 능력치 증가를 주지 않는다."""
        ranges = [headroom_range(cost) for cost in range(1, 11)]
        self.assertEqual(len(set(ranges)), 1)

    def test_historical_full_season_uses_its_own_schedule(self) -> None:
        """80경기 시대의 298타석을 현대 400타석 기준으로 제한하지 않는다."""
        season = self._season("HISTORICAL", 1982, "DH", [75] * 6, 298.0)
        season["sourceSeasonGames"] = 80.0
        self._with_cost_quality(season, 2.0, 0.70)
        assign_origin_year_costs([season])
        self.assertEqual(season["costDerivationTrace"]["costEligibility"]["tier"], "Full")
        self.assertEqual(season["cost"], 10)

    def test_partial_starter_is_not_ranked_below_a_shorter_relief_season(self) -> None:
        """이닝이 더 많은 부분 선발이 더 짧은 구원 시즌보다 낮은 상한을 받지 않는다."""
        seasons = [
            self._pitcher_season(f"SP_{index:02d}", 2099, "Starter", [50] * 6, 620.0)
            for index in range(30)
        ]
        seasons.append(self._pitcher_season("SWINGMAN", 2099, "Starter", [80] * 6, 349.0))
        seasons.append(self._pitcher_season("SHORT_RELIEF", 2099, "Closer", [80] * 6, 102.0))
        assign_origin_year_costs(seasons)

        swingman = next(s for s in seasons if s["playerSeasonId"] == "SWINGMAN")
        relief = next(s for s in seasons if s["playerSeasonId"] == "SHORT_RELIEF")
        self.assertGreaterEqual(
            swingman["costDerivationTrace"]["costEligibility"]["maximumCost"],
            relief["costDerivationTrace"]["costEligibility"]["maximumCost"],
        )

    def test_cost_percentile_population_is_split_by_player_type(self) -> None:
        seasons = [
            self._season(f"HITTER_{index:02d}", 2099, "1B", [40 + index] * 6, 500.0)
            for index in range(40)
        ] + [
            self._pitcher_season(f"PITCHER_{index:02d}", 2099, "Starter", [40 + index] * 6, 600.0)
            for index in range(60)
        ]
        self._with_cost_quality(seasons[39], 2.0, 0.70)
        self._with_cost_quality(seasons[-1], 2.0, 0.70)
        assign_origin_year_costs(seasons)

        for season in seasons:
            trace = season["costDerivationTrace"]
            expected = 40 if season["playerType"] == "Hitter" else 60
            self.assertEqual(trace["populationCount"], expected)
            self.assertEqual(
                trace["costPopulationSource"],
                f"OriginYear{season['playerType']}SourceBacked",
            )
        top_hitter = max(
            (season for season in seasons if season["playerType"] == "Hitter"),
            key=lambda season: season["cost"],
        )
        top_pitcher = max(
            (season for season in seasons if season["playerType"] == "Pitcher"),
            key=lambda season: season["cost"],
        )
        self.assertEqual(top_hitter["cost"], 10)
        self.assertEqual(top_pitcher["cost"], 10)

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
            sum(season["playerType"] == "Hitter" for season in seasons_by_year[2020]),
        )
        self.assertEqual(
            lee_dae_ho["costDerivationTrace"]["costPopulationSource"],
            "OriginYearHitterSourceBacked",
        )

        audit_names = {"오지환", "안치용", "송은범", "박진만", "로페즈", "최정", "박희수"}
        audited = [
            season for season in seasons_by_year[2012]
            if person_names[season["playerPersonId"]] in audit_names
        ]
        self.assertEqual(len(audited), len(audit_names))
        population_by_type = {
            "Hitter": sum(season["playerType"] == "Hitter" for season in seasons_by_year[2012]),
            "Pitcher": sum(season["playerType"] == "Pitcher" for season in seasons_by_year[2012]),
        }
        self.assertTrue(
            all(
                season["costDerivationTrace"]["populationCount"]
                == population_by_type[season["playerType"]]
                for season in audited
            )
        )
        self.assertTrue(all(season["costDerivationTrace"]["rank"] > 0 for season in audited))
        by_name = {person_names[season["playerPersonId"]]: season for season in audited}
        oh_ji_hwan = by_name["오지환"]
        arm = next(trace for trace in oh_ji_hwan["abilityDerivationTrace"] if trace["attribute"] == "Arm")
        defense = next(trace for trace in oh_ji_hwan["abilityDerivationTrace"] if trace["attribute"] == "Defense")
        self.assertNotIn("FieldingPercentage", {component["metric"] for component in arm["components"]})
        self.assertIn("FieldingPercentage", {component["metric"] for component in defense["components"]})
        self.assertFalse(oh_ji_hwan["costDerivationTrace"]["metricInfluenceAudit"]["hasViolation"])

        ahn_chi_yong = by_name["안치용"]
        self.assertEqual(ahn_chi_yong["abilityDerivationTrace"][0]["groupKey"], "2012:DH")
        self.assertEqual(ahn_chi_yong["abilityDerivationTrace"][0]["roleTier"], "Limited")
        song_eun_beom = by_name["송은범"]
        self.assertEqual(song_eun_beom["abilityDerivationTrace"][0]["groupKey"], "2012:Starter")
        self.assertEqual(song_eun_beom["pitcherRoleConfidence"], "High")

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
    def _season(
        season_id: str,
        year: int,
        position: str,
        hitter_ratings: list[int],
        eligibility_sample: float | None = None,
    ) -> dict:
        season = {
            "playerSeasonId": season_id,
            "originYear": year,
            "position": position,
            "pitcherRole": "",
            "playerType": "Hitter",
            "baseAttributes": hitter_ratings + [50] * (len(ABILITY_NAMES) - 6),
            "cost": 0,
        }
        if eligibility_sample is not None:
            season["costEligibilitySample"] = eligibility_sample
        return season

    @staticmethod
    def _pitcher_season(
        season_id: str,
        year: int,
        pitcher_role: str,
        pitcher_ratings: list[int],
        eligibility_sample: float | None = None,
    ) -> dict:
        season = {
            "playerSeasonId": season_id,
            "originYear": year,
            "position": "P",
            "pitcherRole": pitcher_role,
            "playerType": "Pitcher",
            "baseAttributes": [50] * 6 + pitcher_ratings,
            "cost": 0,
        }
        if eligibility_sample is not None:
            season["costEligibilitySample"] = eligibility_sample
        return season

    @staticmethod
    def _with_cost_quality(season: dict, quality: float, reliability: float) -> None:
        profile = DERIVATION_BALANCE["costValueModel"]["qualityProfiles"][season["playerType"]]
        season["abilityDerivationTrace"] = [
            {
                "components": [
                    {
                        "metric": metric,
                        "adjustedZ": quality,
                        "reliability": reliability,
                    }
                    for metric in profile
                ]
            }
        ]

    @staticmethod
    def _rate_hitter(source_id: str, plate_appearances: int, average: float) -> dict:
        at_bats = max(1, int(plate_appearances * 0.9))
        return {
            "sourcePlayerId": source_id,
            "hitterStats": {
                "sourceAVG": average,
                "sourceOBP": average + 0.060,
                "sourceSLG": average + 0.150,
                "plateAppearances": plate_appearances,
                "atBats": at_bats,
                "hits": round(average * at_bats),
                "homeRuns": plate_appearances * 0.025,
                "walks": plate_appearances * 0.08,
                "strikeouts": plate_appearances * 0.18,
            },
            "runningStats": {
                "stolenBases": plate_appearances * 0.02,
                "caughtStealing": plate_appearances * 0.01,
                "stolenBaseAttempts": plate_appearances * 0.03,
            },
            "defenseRecords": [
                {
                    "position": "1루수",
                    "inningsOuts": plate_appearances * 2,
                    "putouts": plate_appearances * 0.5,
                    "assists": plate_appearances * 0.05,
                    "errors": plate_appearances * 0.005,
                }
            ],
        }

    @staticmethod
    def _pitcher(games: int, games_started: int, innings_outs: int) -> dict:
        return {
            "pitcherStats": {
                "games": games,
                "gamesStarted": games_started,
                "gamesFinished": 2,
                "completeGames": 0,
                "saves": 0,
                "holds": 1,
                "inningsOuts": innings_outs,
            }
        }


if __name__ == "__main__":
    unittest.main()
