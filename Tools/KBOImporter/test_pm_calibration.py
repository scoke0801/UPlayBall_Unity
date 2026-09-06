"""참조 보정의 결측·가격 독립성·데이터 유출 경계를 검증한다."""
from __future__ import annotations

import copy
import unittest

import synthetic_bake as bake
from source_backed_runtime_bake import _sanitize_runtime_value


class ReferenceCalibrationTests(unittest.TestCase):
    def test_defensive_price_is_independent_of_displayed_rating_scale(self):
        season = {"costMetricEvidence": [{"metric": "FieldingPercentage", "isAvailable": True,
                                         "adjustedZ": 1.0, "reliability": 1.0}], "baseAttributes": [25] * 12}
        settings = bake.DERIVATION_BALANCE["costValueModel"]["hitterWorkload"]
        first = bake.derive_defensive_value_signal(season, settings)
        season["baseAttributes"] = [100] * 12
        self.assertEqual(first, bake.derive_defensive_value_signal(season, settings))
        self.assertAlmostEqual(first, .525)
        self.assertEqual(bake.derive_defensive_value_signal(season, {"defensiveQualityProfiles": []}), 0.0)
        season["costMetricEvidence"] = []
        self.assertEqual(bake.derive_defensive_value_signal(season, settings), 0.0)

    def test_defensive_evidence_is_collected_without_changing_attack_quality(self):
        components = {metric: {"metric": metric} for metric in bake.HITTER_METRIC_NAMES}
        evidence = bake.build_cost_metric_evidence("Hitter", components)
        names = [entry["metric"] for entry in evidence]
        self.assertEqual(names, sorted(set(names)))
        self.assertIn("StolenBases", names)
        self.assertIn("FieldingPercentage", names)
        self.assertIn("CaughtStealingRate", names)
        self.assertNotIn("FieldingPercentage", bake.DERIVATION_BALANCE["costValueModel"]["qualityProfiles"]["Hitter"])

    def test_invalid_defensive_price_weights_are_rejected(self):
        config = copy.deepcopy(bake.DERIVATION_BALANCE)
        config["costValueModel"]["hitterWorkload"]["defensiveQualityProfiles"][0]["weight"] = float('nan')
        with self.assertRaises(ValueError):
            bake.validate_derivation_balance(config)

    def test_cost_source_evidence_does_not_enter_runtime(self):
        value = {"seasons": [{"cost": 8, "costMetricEvidence": [{"rawValue": .301}],
                              "baseAttributes": [55] * 12}]}
        self.assertEqual(_sanitize_runtime_value(value), {"seasons": [{"cost": 8, "baseAttributes": [55] * 12}]})

    def test_cost_evidence_is_independent_of_visible_rating_profiles(self):
        season = {
            "costMetricEvidence": [{"metric": "StolenBases", "isAvailable": True, "adjustedZ": 1.2, "reliability": .7}],
            "abilityDerivationTrace": [{"components": [{"metric": "StolenBases", "isAvailable": True, "adjustedZ": -2, "reliability": .1}]}],
        }
        first = bake.cost_metric_evidence(season)
        season["abilityDerivationTrace"] = []
        self.assertEqual(first, bake.cost_metric_evidence(season))
        self.assertEqual(first, ({"StolenBases": 1.2}, {"StolenBases": .7}))

    def test_explicit_missing_cost_evidence_never_falls_back_to_abilities(self):
        season = {"costMetricEvidence": [], "abilityDerivationTrace": [{"components": [
            {"metric": "BattingAverage", "isAvailable": True, "adjustedZ": 3, "reliability": 1}]}]}
        self.assertEqual(bake.cost_metric_evidence(season), ({}, {}))

    def test_missing_ratings_remain_neutral_despite_calibrated_center(self):
        vector = (0.0,) * len(bake.HITTER_METRIC_NAMES)
        components = {metric: {"isAvailable": False} for metric in bake.HITTER_METRIC_NAMES}
        ratings, _ = bake.to_ratings_with_trace("Hitter", vector, components)
        self.assertEqual(ratings[:6], [55] * 6)

    def test_same_average_different_on_base_evidence_changes_mental_only(self):
        vector = [0.0] * len(bake.HITTER_METRIC_NAMES)
        first, _ = bake.to_ratings_with_trace("Hitter", tuple(vector))
        vector[bake.HITTER_METRIC_NAMES.index("OnBasePercentage")] = 1.0
        second, _ = bake.to_ratings_with_trace("Hitter", tuple(vector))
        self.assertEqual(first[:5], second[:5])
        self.assertGreater(second[5], first[5])

    def test_calibrated_centers_and_type_keys_are_validated(self):
        for key, value in (("center", float("nan")), ("center", 101)):
            config = copy.deepcopy(bake.DERIVATION_BALANCE)
            config["ratingProfiles"]["Hitter"]["Speed"][key] = value
            with self.assertRaises(ValueError):
                bake.validate_derivation_balance(config)
        config = copy.deepcopy(bake.DERIVATION_BALANCE)
        config["costValueModel"]["baseScoreByPlayerType"]["TeamName"] = 1
        with self.assertRaises(ValueError):
            bake.validate_derivation_balance(config)


if __name__ == "__main__":
    unittest.main()
