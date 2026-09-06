"""주전 평가의 소표본·Cost 독립성·복수 보직 자격 회귀를 검증한다."""
import copy
import unittest

import synthetic_bake as bake
import test_synthetic_bake as fixtures


class FinalCalibrationTests(unittest.TestCase):
    def test_unverified_regular_can_compete_with_tiny_sample_position_candidate(self):
        tiny=self.with_usage(fixtures.SyntheticBakeTests._hitter_row('tiny',50,'1B'),5,'1B',0)
        regular=self.with_usage(fixtures.SyntheticBakeTests._hitter_row('regular',80,'DH'),500,'DH',0)
        regular['isPositionEvidenceMissing']=True
        rows=[tiny,regular,fixtures.SyntheticBakeTests._hitter_row('dh',90,'DH')]
        rows += [fixtures.SyntheticBakeTests._hitter_row(p,70,p) for p in bake.DEFENSIVE_HITTER_POSITIONS if p!='1B']
        sources={r['playerSeasonId']:r['_source'] for r in rows}
        selected,trace,warnings=bake.select_defensive_starters(rows,sources,True)
        self.assertEqual(selected[1]['playerSeasonId'],'regular')
        self.assertTrue(trace[1]['isFallback'])
        self.assertIn('ROSTER_POSITION_UNVERIFIED',[w['code'] for w in warnings])

    @staticmethod
    def with_usage(row, plate_appearances, position, outs):
        row['sourceSeasonGames'] = 144
        row['costEligibilitySample'] = plate_appearances
        row['positionRoleDerivationTrace'] = {'positionCandidates':[{'position':position,'inningsOuts':outs}]}
        return row

    def test_one_inning_neutral_catcher_does_not_displace_regular(self):
        rookie = self.with_usage(fixtures.SyntheticBakeTests._hitter_row('rookie',55,'C'),0,'C',3)
        rookie['baseAttributes'][:6] = [55,51,55,55,73,55]
        regular = self.with_usage(fixtures.SyntheticBakeTests._hitter_row('regular',55,'C'),369,'C',2456)
        regular['baseAttributes'][:6] = [50,58,57,45,77,55]
        rows = [rookie,regular,fixtures.SyntheticBakeTests._hitter_row('dh',80,'DH')] + [fixtures.SyntheticBakeTests._hitter_row(p,60,p) for p in bake.DEFENSIVE_HITTER_POSITIONS if p!='C']
        sources = {r['playerSeasonId']:r['_source'] for r in rows}
        selected,_,_ = bake.select_defensive_starters(rows,sources,True)
        self.assertEqual(selected[0]['playerSeasonId'],'regular')
        self.assertEqual(selected[-1]['playerSeasonId'],'dh')
        rookie['baseAttributes'] = [90]*12
        selected,_,_ = bake.select_defensive_starters(rows,sources,True)
        self.assertIn('rookie',[r['playerSeasonId'] for r in selected])

    def test_missing_defense_is_not_positive_designated_hitter_evidence(self):
        known = self.with_usage(fixtures.SyntheticBakeTests._hitter_row('known',60,'DH'),500,'C',30)
        unknown = copy.deepcopy(known)
        unknown['positionRoleDerivationTrace']['positionCandidates'] = []
        self.assertLess(bake.starter_usage_score(unknown,'DH'),bake.starter_usage_score(known,'DH'))
        unknown['sourceSeasonGames'] = 0
        self.assertEqual(bake.starter_usage_score(unknown,'DH'),0)

    def test_core_selection_is_independent_of_cost_and_input_order(self):
        starter = fixtures.SyntheticBakeTests._hitter_row('catcher',60,'C')
        reserve = fixtures.SyntheticBakeTests._hitter_row('reserve',65,'C')
        reserve['baseAttributes'][:6] = [65,65,100,55,55,65]
        rows = [starter,reserve,fixtures.SyntheticBakeTests._hitter_row('dh',100,'DH')] + [fixtures.SyntheticBakeTests._hitter_row(p,60,p) for p in bake.DEFENSIVE_HITTER_POSITIONS if p!='C']
        rows += [fixtures.SyntheticBakeTests._hitter_row('bench'+str(i),40,'LF') for i in range(4)] + fixtures.SyntheticBakeTests._pitcher_rows()
        sources = {r['playerSeasonId']:r['_source'] for r in rows}
        for r in rows:
            r['cost'] = 4
        reserve['cost']=8
        first,_ = bake.assign_source_team_roles(rows,sources)
        first_assignment = sorted((r['playerSeasonId'],r['rosterRole']) for r in first)
        for r in rows:
            r['cost'] = 11-r['cost']
        second,_ = bake.assign_source_team_roles(list(reversed(rows)),sources)
        self.assertEqual(first_assignment,sorted((r['playerSeasonId'],r['rosterRole']) for r in second))
        self.assertEqual(len(second),25)

    def test_save_leader_with_holds_can_compete_for_closer(self):
        def pitcher(sid,role,rating,saves):
            return {'playerSeasonId':sid,'pitcherRole':role,'baseAttributes':[rating]*12,
                    'positionRoleDerivationTrace':{'pitcherRoleEvidence':{'saves':saves,'holds':13}}}
        leader = pitcher('leader','Setup',80,10)
        closer = pitcher('closer','Closer',70,8)
        occasional = pitcher('occasional','Setup',90,3)
        remaining = [closer,occasional,leader]
        selected,trace = bake.select_pitcher_group(remaining,1,'Closer',{'Closer'})
        self.assertEqual(selected[0]['playerSeasonId'],'leader')
        self.assertEqual(leader['pitcherRole'],'Setup')
        self.assertFalse(next(r for r in trace['candidates'] if r['playerSeasonId']=='occasional')['isEligible'])

    def test_hitter_elite_calibration_does_not_change_pitcher_gate(self):
        components = {'quality':.55,'workload':{'ratio':.9},'reliability':.6}
        self.assertEqual(bake.elite_cost_ceiling(components,'Hitter')[0],10)
        self.assertEqual(bake.elite_cost_ceiling(components,'Pitcher')[0],9)

    def test_invalid_type_specific_cost_or_roster_settings_are_rejected(self):
        config = copy.deepcopy(bake.DERIVATION_BALANCE)
        config['costValueModel']['valueTierThresholdsByPlayerType']['PlayerName'] = []
        with self.assertRaises(ValueError):
            bake.validate_derivation_balance(config)
        for key,value in [('positionWeight',float('nan')),('plateAppearancesPerGame',0)]:
            config = copy.deepcopy(bake.DERIVATION_BALANCE)
            config['rosterSelection']['starterUsage'][key]=value
            with self.assertRaises(ValueError):
                bake.validate_derivation_balance(config)


if __name__=='__main__':
    unittest.main()
