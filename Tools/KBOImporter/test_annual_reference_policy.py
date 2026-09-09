"""월별·연도 제한과 선수 단위 분리 정책을 검증한다."""
import unittest
from pathlib import Path
from calibrate_annual_reference import (is_annual_reference, read, split_person, match_record_candidates,
                                        archive_kind, filter_position_candidates, bake)


class AnnualReferencePolicyTests(unittest.TestCase):
    def setUp(self):
        self.policy=read(Path(__file__).with_name('reference_calibration_policy.json'))

    def test_monthly_and_after_2013_are_excluded(self):
        self.assertTrue(is_annual_reference(2013,'Normal','시즌',self.policy))
        for year,edition,text in ((2014,'Normal',''),(2013,'Normal','5월 카드'),(2012,'Normal','Monthly'),(2013,'AllStar',''),(2011,'Unknown','')):
            self.assertFalse(is_annual_reference(year,edition,text,self.policy))

    def test_person_split_is_stable_across_all_seasons(self):
        first=[split_person('PERSON_'+str(i),self.policy) for i in range(100)]
        self.assertEqual(first,[split_person('PERSON_'+str(i),self.policy) for i in range(100)])
        self.assertEqual(set(first),{'Train','Validation','Holdout'})

    def test_record_identity_resolves_names_without_using_cost(self):
        counts=dict(games=20,wins=5,losses=2,saves=0,strikeouts=30)
        players=[dict(sourcePlayerId='a',pitcherStats=counts),dict(sourcePlayerId='b',pitcherStats=dict(counts,wins=6))]
        seasons=[dict(playerSeasonId=bake.pitch_source_identity.editor_source_season_id(p['sourcePlayerId'],2009),originYear=2009,originFranchiseId='SK',playerType='Pitcher') for p in players]
        self.assertEqual(match_record_candidates(seasons,seasons,players,2009,'SK','Pitcher',counts),seasons[:1])
        self.assertEqual(match_record_candidates([],seasons,players,2009,'SK','Pitcher',counts),seasons[:1])
        players[1]['pitcherStats']=counts
        self.assertEqual(len(match_record_candidates([],seasons,players,2009,'SK','Pitcher',counts)),2)

    def test_empty_record_cannot_join_a_renamed_player(self):
        counts=dict(games=0,wins=0,losses=0,saves=0,strikeouts=0)
        player=dict(sourcePlayerId='a',pitcherStats=counts)
        season=dict(playerSeasonId=bake.pitch_source_identity.editor_source_season_id('a',2009),originYear=2009,originFranchiseId='SK',playerType='Pitcher')
        self.assertEqual(match_record_candidates([],[season],[player],2009,'SK','Pitcher',counts),[])

    def test_archive_position_distinguishes_same_name_pitchers(self):
        candidates=[dict(pitcherRole='Starter'),dict(pitcherRole='Closer')]
        self.assertEqual(filter_position_candidates(candidates,'Pitcher','마무리'),candidates[1:])
        self.assertEqual(archive_kind('선발'),'Pitcher')
        self.assertEqual(archive_kind('외야수'),'Hitter')

    def test_reference_override_preserves_formula_trace(self):
        season=dict(playerSeasonId='S',playerType='Pitcher',originYear=1994,cost=9,
            baseAttributes=[50]*12,costDerivationTrace=dict(cost=9,costMethod='RecordGradientBoosting',
                costEligibility={'affectsCost':True},eliteEligibility={'affectsCost':False}))
        override=dict(playerSeasonId='S',playerType='Pitcher',originYear=1994,
            values={'Cost':10,'Velocity':85},sources={'Cost':'archive','Velocity':'archive'})
        bake.apply_annual_reference_overrides([season],{'S':override})
        self.assertEqual(season['cost'],10)
        self.assertEqual(season['baseAttributes'][bake.ABILITY_INDEX['Velocity']],85)
        self.assertEqual(season['costDerivationTrace']['formulaCost'],9)
        self.assertFalse(season['costDerivationTrace']['costEligibility']['affectsCost'])


if __name__=='__main__':unittest.main()
