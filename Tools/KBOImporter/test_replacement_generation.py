from __future__ import annotations

import copy
import json
import math
from pathlib import Path
import unittest

from replacement_generation import (
    REPLACEMENT_GENERATED,
    ROSTER_SHORTAGE,
    SOURCE_BACKED,
    ReplacementGenerationSettings,
    ShortageSlotSpec,
    build_source_cost_thresholds,
    generate_replacements,
)


class ReplacementGenerationTests(unittest.TestCase):
    def setUp(self) -> None:
        self.settings = ReplacementGenerationSettings(
            percentile_lower_inclusive=0.05,
            percentile_upper_inclusive=0.30,
            covariance_scale=0.75,
            minimum_group_population=6,
            maximum_sampling_attempts=512,
            composite_profiles={
                "Hitter:Default": (1.0, 1.0, 1.0, 1.0, 1.0, 1.0),
                "Hitter:SS": (1.35, 1.0, 1.35, 2.0, 2.0, 1.35),
                "Hitter:C": (1.35, 1.0, 1.0, 2.0, 2.0, 2.0),
                "Pitcher:Default": (1.0, 2.0, 2.0, 1.35, 1.35, 1.35),
                "Pitcher:Starter": (2.0, 1.35, 1.35, 1.35, 2.0, 1.35),
            },
        )
        self.sources = self._source_hitters(80) + self._source_pitchers(40)

    def test_generation_is_deterministic_and_matches_exact_shortage(self) -> None:
        slots = [
            ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="SS", count=7),
            ShortageSlotSpec(
                1982,
                "SEOUL",
                "1982:SEOUL",
                "Pitcher",
                natural_pitcher_role="Starter",
                assigned_roster_role="StartingPitcher",
                count=3,
            ),
        ]
        before = copy.deepcopy(self.sources)

        first = generate_replacements(self.sources, slots, "fixture-seed", self.settings)
        second = generate_replacements(
            list(reversed(self.sources)),
            list(reversed(slots)),
            "fixture-seed",
            self.settings,
        )

        self.assertEqual(first.to_dict(), second.to_dict())
        self.assertEqual(len(first.replacements), 10)
        self.assertEqual(self.sources, before, "SourceBacked 입력을 변경하면 안 됩니다.")
        self.assertEqual(len({row["playerPersonId"] for row in first.replacements}), 10)
        self.assertEqual(len({row["playerSeasonId"] for row in first.replacements}), 10)

    def test_provenance_has_no_source_identity_or_reference_mix(self) -> None:
        result = generate_replacements(
            self.sources,
            [ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="SS", count=20)],
            142857,
            self.settings,
        )

        for row in result.replacements:
            self.assertEqual(row["dataProvenance"], REPLACEMENT_GENERATED)
            self.assertEqual(row["generationReason"], ROSTER_SHORTAGE)
            self.assertNotIn("sourcePlayerId", row)
            self.assertNotIn("sourcePlayerSeasonId", row)
            self.assertNotIn("sourceReferenceNames", row)
            trace = row["replacementGenerationTrace"]
            self.assertNotIn("sourcePlayerId", trace)
            self.assertNotIn("sourcePlayerSeasonIds", trace)

    def test_generated_vector_never_exactly_duplicates_source_vector(self) -> None:
        source_vectors = {tuple(row["baseAttributes"]) for row in self.sources}
        result = generate_replacements(
            self.sources,
            [ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="SS", count=100)],
            "collision-fixture",
            self.settings,
        )

        generated_vectors = [tuple(row["baseAttributes"]) for row in result.replacements]
        self.assertTrue(source_vectors.isdisjoint(generated_vectors))
        self.assertEqual(len(generated_vectors), len(set(generated_vectors)))

    def test_replacement_is_sampled_from_configured_group_low_tail(self) -> None:
        result = generate_replacements(
            self.sources,
            [ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="SS", count=50)],
            "low-tail-fixture",
            self.settings,
        )

        percentiles = [trace["groupInsertionPercentile"] for trace in result.generation_traces]
        self.assertTrue(all(trace["acceptedWithinBand"] for trace in result.generation_traces))
        self.assertGreaterEqual(min(percentiles), self.settings.percentile_lower_inclusive)
        self.assertLessEqual(max(percentiles), self.settings.percentile_upper_inclusive)
        self.assertLess(sum(percentiles) / len(percentiles), 0.25)

    def test_population_covariance_preserves_positive_contact_power_relation(self) -> None:
        result = generate_replacements(
            self.sources,
            [ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="SS", count=80)],
            "covariance-fixture",
            self.settings,
        )
        contacts = [row["baseAttributes"][0] for row in result.replacements]
        powers = [row["baseAttributes"][1] for row in result.replacements]

        self.assertGreater(self._correlation(contacts, powers), 0.20)
        covariance = result.generation_traces[0]["aggregateCovariance"]
        self.assertGreater(covariance[0][1], 0.0)

    def test_source_cost_thresholds_exclude_replacements_and_cost_is_monotonic(self) -> None:
        source_before = build_source_cost_thresholds(self.sources, self.settings)
        small = generate_replacements(
            self.sources,
            [ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="SS", count=5)],
            "cost-fixture",
            self.settings,
        )
        large = generate_replacements(
            self.sources,
            [ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="SS", count=35)],
            "cost-fixture",
            self.settings,
        )

        self.assertEqual(source_before, small.source_cost_thresholds)
        self.assertEqual(source_before, large.source_cost_thresholds)
        self.assertEqual(source_before[1982]["sourcePopulationSize"], len(self.sources))
        ordered = sorted(
            (
                row["costDerivationTrace"]["composite"],
                row["cost"],
            )
            for row in large.replacements
        )
        self.assertTrue(all(left[1] <= right[1] for left, right in zip(ordered, ordered[1:])))
        for row in large.replacements:
            trace = row["costDerivationTrace"]
            self.assertTrue(
                {
                    "dataProvenance",
                    "generationReason",
                    "sourcePopulationSize",
                    "replacementExcludedFromThresholdCalculation",
                    "thresholds",
                    "cost",
                }.issubset(trace)
            )
            self.assertEqual(trace["dataProvenance"], REPLACEMENT_GENERATED)
            self.assertEqual(trace["generationReason"], ROSTER_SHORTAGE)
            self.assertEqual(trace["costPopulationSource"], "OriginYearSourceBacked")
            self.assertEqual(trace["sourcePopulationSize"], len(self.sources))
            self.assertTrue(trace["replacementExcludedFromThresholdCalculation"])
            self.assertIsInstance(trace["thresholds"], list)
            self.assertEqual(trace["cost"], row["cost"])
            self.assertNotIn("DataProvenance", trace)
            self.assertNotIn("SourcePopulationSize", trace)
            self._assert_lower_camel_case_keys(row)

    def test_balance_file_exposes_explicit_replacement_generation_v1(self) -> None:
        balance_path = Path(__file__).with_name("derivation_balance.json")
        balance = json.loads(balance_path.read_text(encoding="utf-8"))
        replacement = balance["replacementGeneration"]

        self.assertEqual(replacement["version"], "replacement-generation-v1")
        self.assertEqual(replacement["percentileLowerInclusive"], 0.05)
        self.assertEqual(replacement["percentileUpperInclusive"], 0.30)
        self.assertEqual(replacement["covarianceScale"], 0.65)
        self.assertEqual(replacement["covarianceRegularization"], 1e-6)
        self.assertEqual(replacement["minimumGroupPopulation"], 6)
        self.assertEqual(replacement["maximumSamplingAttempts"], 256)

        settings = ReplacementGenerationSettings.from_balance(balance)
        self.assertEqual(settings.percentile_lower_inclusive, 0.05)
        self.assertEqual(settings.percentile_upper_inclusive, 0.30)
        self.assertEqual(settings.covariance_scale, 0.65)
        self.assertEqual(settings.covariance_regularization, 1e-6)
        self.assertEqual(settings.minimum_group_population, 6)
        self.assertEqual(settings.maximum_sampling_attempts, 256)

    def test_small_role_group_uses_explicit_year_player_type_fallback(self) -> None:
        sparse_catchers = copy.deepcopy(self.sources)
        sparse_catchers[0]["position"] = "C"
        sparse_catchers[1]["position"] = "C"
        result = generate_replacements(
            sparse_catchers,
            [ShortageSlotSpec(1982, "SEOUL", "1982:SEOUL", "Hitter", position="C")],
            "fallback-fixture",
            self.settings,
        )
        trace = result.generation_traces[0]

        self.assertEqual(trace["aggregateGroupKey"], "1982:Hitter:AllRoles")
        self.assertTrue(trace["aggregateFallbackReason"].startswith("ROLE_GROUP_TOO_SMALL:2<6"))
        self.assertEqual(trace["aggregatePopulationCount"], 80)

    def test_non_source_row_cannot_contaminate_threshold_population(self) -> None:
        contaminated = copy.deepcopy(self.sources)
        contaminated.append(
            {
                "playerSeasonId": "REPL",
                "originYear": 1982,
                "playerType": "Hitter",
                "position": "SS",
                "dataProvenance": REPLACEMENT_GENERATED,
                "baseAttributes": [50] * 12,
            }
        )
        with self.assertRaisesRegex(ValueError, "SourceBacked만"):
            build_source_cost_thresholds(contaminated, self.settings)

    @staticmethod
    def _source_hitters(count: int) -> list[dict]:
        rows = []
        for index in range(count):
            latent = (index - (count - 1) * 0.5) / 12.0
            ratings = [
                round(55 + latent * 4.5 + ((index * 7) % 5 - 2)),
                round(52 + latent * 4.0 + ((index * 11) % 7 - 3)),
                round(54 + latent * 3.0 + ((index * 13) % 5 - 2)),
                round(53 + latent * 2.5 + ((index * 17) % 5 - 2)),
                round(56 + latent * 3.5 + ((index * 19) % 7 - 3)),
                round(55 + latent * 3.0 + ((index * 23) % 5 - 2)),
            ]
            rows.append(
                {
                    "playerPersonId": f"SOURCE-PERSON-H-{index:03d}",
                    "playerSeasonId": f"SOURCE-SEASON-H-{index:03d}",
                    "sourcePlayerId": f"H{index:03d}",
                    "dataProvenance": SOURCE_BACKED,
                    "originYear": 1982,
                    "playerType": "Hitter",
                    "position": "SS",
                    "pitcherRole": "",
                    "baseAttributes": ratings + [50] * 6,
                }
            )
        return rows

    @staticmethod
    def _source_pitchers(count: int) -> list[dict]:
        rows = []
        for index in range(count):
            latent = (index - (count - 1) * 0.5) / 9.0
            ratings = [
                round(55 + latent * 3.0 + ((index * 5) % 3 - 1)),
                round(53 + latent * 2.5 + ((index * 7) % 5 - 2)),
                round(54 + latent * 3.5 + ((index * 11) % 5 - 2)),
                round(52 + latent * 2.5 + ((index * 13) % 3 - 1)),
                round(55 + latent * 3.0 + ((index * 17) % 5 - 2)),
                round(54 + latent * 2.0 + ((index * 19) % 3 - 1)),
            ]
            rows.append(
                {
                    "playerPersonId": f"SOURCE-PERSON-P-{index:03d}",
                    "playerSeasonId": f"SOURCE-SEASON-P-{index:03d}",
                    "sourcePlayerId": f"P{index:03d}",
                    "dataProvenance": SOURCE_BACKED,
                    "originYear": 1982,
                    "playerType": "Pitcher",
                    "position": "P",
                    "pitcherRole": "Starter",
                    "naturalPitcherRole": "Starter",
                    "baseAttributes": [50] * 6 + ratings,
                }
            )
        return rows

    @staticmethod
    def _correlation(left: list[int], right: list[int]) -> float:
        left_mean = sum(left) / len(left)
        right_mean = sum(right) / len(right)
        numerator = sum((x - left_mean) * (y - right_mean) for x, y in zip(left, right))
        left_sum = sum((x - left_mean) ** 2 for x in left)
        right_sum = sum((y - right_mean) ** 2 for y in right)
        return numerator / math.sqrt(left_sum * right_sum)

    def _assert_lower_camel_case_keys(self, value: object) -> None:
        if isinstance(value, dict):
            for key, child in value.items():
                if isinstance(key, str):
                    self.assertTrue(key and key[0].islower(), f"lowerCamelCase가 아닌 JSON field: {key}")
                self._assert_lower_camel_case_keys(child)
        elif isinstance(value, list):
            for child in value:
                self._assert_lower_camel_case_keys(child)


if __name__ == "__main__":
    unittest.main()
