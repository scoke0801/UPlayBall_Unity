"""로테이션별 결과를 팀 기록과 혼동하거나 누락한 보고서를 거부한다."""
import unittest
from verify_rotation import summarize


class RotationReportTests(unittest.TestCase):
    def setUp(self):
        self.rotation = dict(teamSeasonKey='a', regularStarts=[2, 1, 1, 1, 1],
                             regularTeamWins=[1, 0, 1, 0, 0], regularTeamLosses=[1, 1, 0, 0, 1],
                             regularTeamDraws=[0, 0, 0, 1, 0])
        self.data = dict(rotationPolicy='FixedFive', contentHash='test', rows=[dict(year=1982, seed=1,
            teams=[dict(TeamSeasonKey='a', Games=6, Wins=2, Losses=3, Ties=1)], rotations=[self.rotation])])

    def test_draws_excluded_from_win_rate(self):
        team = summarize(self.data)['teams'][0]
        self.assertEqual([.5, 0, 1, None, 0], team['teamWinRatesByStarterSlot'])

    def test_missing_rotation_team_rejected(self):
        self.data['rows'][0]['rotations'] = []
        with self.assertRaises(ValueError):
            summarize(self.data)

    def test_duplicate_seed_rejected(self):
        self.data['rows'].append(self.data['rows'][0])
        with self.assertRaises(ValueError):
            summarize(self.data)

    def test_wrong_slot_rejected_even_when_team_totals_match(self):
        self.rotation['regularTeamWins'] = [0, 0, 2, 0, 0]
        with self.assertRaises(ValueError):
            summarize(self.data)

    def test_wrong_team_total_rejected(self):
        self.data['rows'][0]['teams'][0]['Wins'] = 3
        with self.assertRaises(ValueError):
            summarize(self.data)

    def test_unequal_fixed_rotation_rejected(self):
        self.rotation['regularStarts'] = [3, 1, 0, 1, 1]
        with self.assertRaises(ValueError):
            summarize(self.data)


if __name__ == '__main__':
    unittest.main()
