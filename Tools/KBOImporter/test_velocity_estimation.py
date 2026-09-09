"""구속 실측·추정·참조 대체 경계와 재현성을 검증한다."""
import copy
import math
import unittest
from unittest.mock import patch

import synthetic_bake as bake
import velocity_estimation as velocity


class VelocityEstimationTests(unittest.TestCase):
    def setUp(self):
        self.model = velocity.load_model(bake.DERIVATION_BALANCE['missingVelocityEstimation'], bake.DERIVATION_BALANCE_PATH.parent)

    def test_measured_velocity_bypasses_estimation(self):
        evidence = {name: {'isAvailable': False} for name in bake.PITCHER_METRIC_NAMES}
        evidence['FastballVelocityKph'] = dict(isAvailable=True, rawValue=170, absoluteRating=100,
                                             reliability=1, adjustedZ=0, sampleSize=1)
        attributes, traces = bake.to_ratings_with_trace('Pitcher', (0,)*len(evidence), evidence)
        trace = next(t for t in traces if t['attribute']=='Velocity')
        self.assertEqual(attributes[7], 100)
        self.assertEqual(trace['evaluationMethod'], 'AbsoluteRecordAnchor')
        self.assertNotIn('velocityEstimation', trace)

    def test_records_change_estimate_without_changing_source_or_other_abilities(self):
        first = {c['metric']:c for c in bake.pitcher_metric_evidence({'pitcherStats':
            dict(inningsOuts=300, games=20, strikeouts=40, walks=30, sourceERA=3, saves=0, holds=0)})}
        second = copy.deepcopy(first)
        second['StrikeoutsPerNine']['rawValue'] = 10
        snapshot = copy.deepcopy(first)
        low, low_trace = velocity.estimate(first, self.model)
        high, high_trace = velocity.estimate(second, self.model)
        self.assertGreater(high, low)
        self.assertEqual((low, low_trace), velocity.estimate(first, self.model))
        self.assertEqual(first, snapshot)
        self.assertFalse(first['FastballVelocityKph']['isAvailable'])
        self.assertFalse(high_trace['measuredVelocity'])

    def test_empty_and_nonfinite_records_use_explicit_prior(self):
        empty, trace = velocity.estimate({}, self.model)
        bad = {'StrikeoutsPerNine': dict(isAvailable=True, rawValue=float('inf'), sampleSize=50)}
        self.assertEqual(velocity.estimate(bad, self.model)[0], empty)
        self.assertTrue(math.isfinite(empty))
        self.assertEqual(trace['method'], 'EstimatedVelocityPrior')

    def test_normal_card_observation_remains_authoritative(self):
        season = dict(playerSeasonId='fixture', playerType='Pitcher', originYear=2013,
                      baseAttributes=[60]*12, cost=5,
                      costDerivationTrace=dict(cost=5, costEligibility={}, eliteEligibility={}))
        card = dict(playerType='Pitcher', originYear=2013, values={'Cost':5, 'Velocity':82})
        bake.apply_annual_reference_overrides([season], {'fixture':card})
        self.assertEqual(season['baseAttributes'][7], 82)
        self.assertEqual(season['annualReferenceOverride']['formulaBaseAttributes'][7], 60)

    def test_model_rejects_identity_features_and_nan(self):
        for invalid in ('playerName.rawValue', 'playerSeasonId.rawValue', 'Cost.rawValue'):
            model = copy.deepcopy(self.model)
            model['features'][0]['source'] = invalid
            with self.assertRaises(ValueError):
                velocity.validate_model(model)
        model = copy.deepcopy(self.model)
        model['features'][0]['coefficient'] = float('nan')
        with self.assertRaises(ValueError):
            velocity.validate_model(model)

    def test_deployed_model_keeps_holdout_people_out_of_training(self):
        validation = self.model['validation']
        self.assertFalse(validation['personLeakage'])
        self.assertFalse(validation['specialCardsUsed'])
        self.assertLess(validation['holdout']['mae'], validation['baseline']['mae']*0.95)
        self.assertLess(validation['temporal']['estimated']['mae'], validation['temporal']['baseline']['mae'])

    def test_rating_version_does_not_reroll_person_identity(self):
        from source_backed_final_bake import _materialize_persons
        people = [dict(playerPersonId='PERSON_FIXTURE', careerStartYear=2000, careerEndYear=2015, primaryPosition='P')]
        before = _materialize_persons(people, {}, bake)
        with patch.object(bake, 'DERIVATION_BALANCE_VERSION', 'future-rating-version'):
            self.assertEqual(before, _materialize_persons(people, {}, bake))


if __name__ == '__main__':
    unittest.main()
