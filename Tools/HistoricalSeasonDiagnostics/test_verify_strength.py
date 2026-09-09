"""빈 검증·중복 시드·공동 선두를 통과 판정에서 구분한다."""
import copy
import unittest
from verify_strength import evaluate


class StrengthGateTests(unittest.TestCase):
    def setUp(self):
        self.reference = dict(teams=[dict(year=1985, team='대상', teamSeasonKey='a', actualWins=70, actualLosses=30)])
        self.simulation = dict(contentHash='test', games=3200, rows=[dict(year=1985, seed=i,
            teams=[dict(TeamSeasonKey='a', Wins=70, Losses=30), dict(TeamSeasonKey='b', Wins=30, Losses=70)]) for i in range(32)])

    def test_complete_pass(self):
        self.assertTrue(evaluate(self.simulation, self.reference)['passed'])

    def test_empty_targets_rejected(self):
        with self.assertRaises(ValueError):
            evaluate(self.simulation, dict(teams=[]))

    def test_duplicate_seed_rejected(self):
        self.simulation['rows'][1]['seed'] = 0
        with self.assertRaises(ValueError):
            evaluate(self.simulation, self.reference)

    def test_insufficient_samples_fail(self):
        self.simulation['rows'].pop()
        self.assertFalse(evaluate(self.simulation, self.reference)['passed'])

    def test_missing_year_fails(self):
        self.reference['teams'][0]['year'] = 1992
        self.assertFalse(evaluate(self.simulation, self.reference)['passed'])

    def test_simulated_tie_has_shared_rank(self):
        for row in self.simulation['rows']:
            row['teams'][1].update(Wins=70, Losses=30)
        peer = copy.deepcopy(self.reference['teams'][0])
        peer.update(team='공동 선두', teamSeasonKey='b')
        self.reference['teams'].append(peer)
        result = evaluate(self.simulation, self.reference)
        self.assertTrue(result['passed'])
        self.assertEqual([1, 1], [t['rank'] for t in result['teams']])


if __name__ == '__main__':
    unittest.main()
