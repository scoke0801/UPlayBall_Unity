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

    def test_actual_leader_passes_at_third_but_fails_at_fourth(self):
        for row in self.simulation['rows']:
            row['teams'] = [
                dict(TeamSeasonKey='a', Wins=65, Losses=35),
                dict(TeamSeasonKey='b', Wins=68, Losses=32),
                dict(TeamSeasonKey='c', Wins=67, Losses=33),
                dict(TeamSeasonKey='d', Wins=64, Losses=36),
            ]
        self.assertTrue(evaluate(self.simulation, self.reference, tolerance=.05)['passed'])
        for row in self.simulation['rows']:
            row['teams'][3].update(Wins=66, Losses=34)
        self.assertFalse(evaluate(self.simulation, self.reference, tolerance=.05)['passed'])

    def test_official_leader_is_not_lost_when_tie_rules_differ(self):
        self.reference['teams'][0].update(actualRegularLeader=True, actualWins=69, actualLosses=31)
        self.reference['teams'].append(dict(year=1985, team='승패 기준 선두',
            teamSeasonKey='b', actualWins=70, actualLosses=30, actualRegularLeader=False))
        for row in self.simulation['rows']:
            row['teams'][1].update(Wins=69, Losses=31)
        result = evaluate(self.simulation, self.reference)
        self.assertTrue(result['passed'])
        self.assertTrue(all(team['requiresTopThree'] for team in result['teams']))


if __name__ == '__main__':
    unittest.main()
