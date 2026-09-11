"""타격 후보 생성이 포지션·가격·투수·원기록을 함께 바꾸지 않도록 검증한다."""
import copy
import unittest
from prepare_hitting_bake import retain_issued_values


class HittingBakeScopeTests(unittest.TestCase):
    def setUp(self):
        player = dict(playerSeasonId='a', playerPersonId='p', originTeamSeasonKey='t', playerType='Hitter',
            position='1B', secondaryPositions=[], isPositionEvidenceMissing=False, pitcherRole='',
            cost=3, costDerivationTrace=dict(method='issued'), costMetricEvidence=[1],
            baseAttributes=[50]*12, trainingCeiling=[60]*12, rosterRole='Starter')
        self.before = dict(manifest={}, years=[dict(year=2021, playerSeasons=[player],
            originalSeasonRecords=[dict(runs=10)], originalAwardRecords=[], normalCards=[])])
        self.after = copy.deepcopy(self.before)
        self.player = self.after['years'][0]['playerSeasons'][0]

    def test_hitting_change_retains_issued_cost_and_trace(self):
        self.player.update(cost=4, costDerivationTrace=dict(method='revalued'), rosterRole='Bench')
        self.player['baseAttributes'][0] = 65
        self.player['trainingCeiling'][0] = 75
        self.assertEqual(1, retain_issued_values(self.before, self.after, True))
        self.assertEqual(3, self.player['cost'])
        self.assertEqual(dict(method='issued'), self.player['costDerivationTrace'])
        self.assertEqual(65, self.player['baseAttributes'][0])

    def test_position_change_rejected(self):
        self.player['position'] = 'DH'
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, True)

    def test_other_attribute_change_rejected(self):
        self.player['baseAttributes'][8] = 70
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, True)

    def test_pitcher_hitting_change_rejected(self):
        self.before['years'][0]['playerSeasons'][0]['playerType'] = 'Pitcher'
        self.player['playerType'] = 'Pitcher'
        self.player['baseAttributes'][0] = 70
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, True)

    def test_original_statistics_change_rejected(self):
        self.after['years'][0]['originalSeasonRecords'][0]['runs'] = 20
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, True)

    def test_team_metadata_cannot_be_changed_to_tune_results(self):
        self.before['years'][0]['teamSeasons'] = [dict(teamSeasonKey='t', core25CardIds=['a'])]
        self.after['years'][0]['teamSeasons'] = [dict(teamSeasonKey='t', core25CardIds=['a'], wins=100)]
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, True)

    def test_roster_selection_can_change_without_team_bonus(self):
        for content in (self.before, self.after):
            extra = copy.deepcopy(content['years'][0]['playerSeasons'][0])
            extra['playerSeasonId'] = 'b'
            content['years'][0]['playerSeasons'].append(extra)
        self.before['years'][0]['teamSeasons'] = [dict(teamSeasonKey='t', core25CardIds=['a'])]
        self.after['years'][0]['teamSeasons'] = [dict(teamSeasonKey='t', core25CardIds=['b'])]
        self.assertEqual(0, retain_issued_values(self.before, self.after, True))

    def test_reference_strength_must_equal_roster_attributes(self):
        team = dict(teamSeasonKey='t', core25CardIds=['a:Normal'], referenceStrength=50)
        self.before['years'][0]['teamSeasons'] = [copy.deepcopy(team)]
        self.after['years'][0]['teamSeasons'] = [team]
        team['referenceStrength'] = 90
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, True)
        self.player['baseAttributes'][0] = 65
        team['referenceStrength'] = 52.5
        self.assertEqual(0, retain_issued_values(self.before, self.after, True))

    def test_pitching_roster_is_preserved(self):
        team = dict(teamSeasonKey='t', core25CardIds=[str(i) for i in range(25)])
        self.before['years'][0]['teamSeasons'] = [copy.deepcopy(team)]
        self.after['years'][0]['teamSeasons'] = [team]
        team['core25CardIds'][18] = 'replacement-fifth-starter'
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, False)

    def test_editor_short_pitching_roster_allows_late_hitter_change(self):
        for content in (self.before, self.after):
            players = content['years'][0]['playerSeasons']
            for identifier, kind in (('b', 'Hitter'), ('p1', 'Pitcher'), ('p2', 'Pitcher')):
                extra = copy.deepcopy(players[0])
                extra.update(playerSeasonId=identifier, playerType=kind)
                players.append(extra)
            content['years'][0]['teamSeasons'] = [dict(teamSeasonKey='t', core25CardIds=['a', 'p1', 'b'])]
        team = self.after['years'][0]['teamSeasons'][0]
        team['core25CardIds'] = ['b', 'p1', 'a']
        self.assertEqual(0, retain_issued_values(self.before, self.after, True))
        team['core25CardIds'] = ['b', 'p2', 'a']
        with self.assertRaises(ValueError):
            retain_issued_values(self.before, self.after, True)


if __name__ == '__main__':
    unittest.main()
