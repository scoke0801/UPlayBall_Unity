"""기록 기반 후보의 결측·신뢰도·결정론을 검증한다."""
import unittest
from hitter_record_calibration import apply_to_seasons, estimate_ratings, resolve_rating


class HitterRecordCalibrationTests(unittest.TestCase):
    def setUp(self):
        self.profile = dict(prior=100, center=65, scale=8,
                            denominator='plateAppearances', numerator={'walks': 1})

    def test_more_walks_raise_rating_at_equal_sample(self):
        result = estimate_ratings({'a': dict(plateAppearances=500, walks=20),
                                   'b': dict(plateAppearances=500, walks=60)}, self.profile)
        self.assertGreater(result['b']['rating'], result['a']['rating'])

    def test_small_sample_is_pulled_toward_league_rate(self):
        result = estimate_ratings({'small': dict(plateAppearances=10, walks=2),
            'large': dict(plateAppearances=500, walks=100),
            'average': dict(plateAppearances=5000, walks=300)}, self.profile)
        self.assertLess(result['small']['shrunkRate'], result['large']['shrunkRate'])

    def test_missing_walks_differ_from_zero_walks(self):
        result = estimate_ratings({'missing': dict(plateAppearances=100),
            'zero': dict(plateAppearances=100, walks=0)}, self.profile)
        self.assertNotIn('missing', result)
        self.assertEqual(65, result['zero']['rating'])

    def test_equal_rates_do_not_create_artificial_spread(self):
        result = estimate_ratings({'a': dict(plateAppearances=10, walks=1),
                                   'b': dict(plateAppearances=500, walks=50)}, self.profile)
        self.assertEqual([65, 65], [r['rating'] for r in result.values()])

    def test_record_order_does_not_change_results(self):
        rows = {'b': dict(plateAppearances=103, walks=9), 'a': dict(plateAppearances=551, walks=59)}
        self.assertEqual(estimate_ratings(rows, self.profile),
                         estimate_ratings(dict(reversed(list(rows.items()))), self.profile))

    def test_team_results_are_not_inputs(self):
        rows = {'a': dict(plateAppearances=500, walks=50), 'b': dict(plateAppearances=500, walks=30)}
        expected = estimate_ratings(rows, self.profile)
        rows['a'].update(teamRank=10, teamWins=1)
        rows['b'].update(teamRank=1, teamWins=100)
        self.assertEqual(expected, estimate_ratings(rows, self.profile))

    def test_invalid_numeric_evidence_rejected(self):
        for value in (-1, float('nan'), float('inf')):
            with self.assertRaises(ValueError):
                estimate_ratings({'a': dict(plateAppearances=100, walks=value)}, self.profile)

    def test_bake_trace_explains_the_applied_rating(self):
        profile = dict(self.profile, index=5)
        season = dict(playerSeasonId='small', playerType='Hitter', baseAttributes=[50]*12,
                      abilityDerivationTrace=[dict(attribute='BatterMental')])
        records = {'small': dict(plateAppearances=10, walks=3),
                   'large': dict(plateAppearances=500, walks=30)}
        apply_to_seasons([season], records, [profile], round)
        trace = season['abilityDerivationTrace'][0]
        component = trace['components'][0]
        self.assertEqual(season['baseAttributes'][5], trace['ratingAfterClamp'])
        self.assertAlmostEqual(component['adjustedZ'], component['priorContribution'] + component['observedContribution'])
        self.assertAlmostEqual(trace['ratingBeforeClamp'], profile['center'] + profile['scale'] * trace['combinedZ'])
        self.assertEqual([50]*5, season['baseAttributes'][:5])

    def test_small_sample_does_not_receive_average_rating_automatically(self):
        estimate = dict(rating=65, denominator=1, priorSample=100)
        self.assertLess(resolve_rating(estimate, 45, dict(blendWithExisting=True)), 46)

    def test_reliable_sample_approaches_record_estimate(self):
        small = dict(rating=80, denominator=10, priorSample=100)
        large = dict(small, denominator=500)
        policy = dict(blendWithExisting=True)
        self.assertGreater(resolve_rating(large, 65, policy), resolve_rating(small, 65, policy))

    def test_full_sample_policy_preserves_small_samples_and_stops_blending(self):
        policy = dict(blendWithExisting=True, fullSample=300)
        for sample, expected in ((0, 40), (30, 44), (150, 60), (300, 80), (600, 80)):
            self.assertEqual(expected, resolve_rating(dict(rating=80, denominator=sample,
                priorSample=100), 40, policy))

    def test_invalid_full_sample_is_rejected(self):
        for sample in (0, -1, float('nan'), float('inf')):
            with self.assertRaises(ValueError):
                resolve_rating(dict(rating=80, denominator=100, priorSample=100), 40,
                    dict(blendWithExisting=True, fullSample=sample))

    def test_trace_distinguishes_rate_shrinkage_from_rating_blend(self):
        policy = dict(self.profile, index=5, blendWithExisting=True, fullSample=300)
        season = dict(playerSeasonId='a', playerType='Hitter', baseAttributes=[40]*12,
                      abilityDerivationTrace=[dict(attribute='BatterMental')])
        apply_to_seasons([season], {'a': dict(plateAppearances=100, walks=10)}, [policy], round)
        trace = season['abilityDerivationTrace'][0]
        calibration = trace['recordCalibration']
        self.assertEqual(300, calibration['fullSample'])
        self.assertAlmostEqual(1/3, calibration['ratingReliability'])
        self.assertAlmostEqual(.5, trace['components'][0]['reliability'])
        self.assertAlmostEqual(40+(65-40)/3, trace['ratingBeforeClamp'])


if __name__ == '__main__':
    unittest.main()
