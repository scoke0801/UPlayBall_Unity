"""선발 가격 구분과 웹 포지션 근거의 시즌 경계를 검증한다."""
import copy
import unittest

import source_position_evidence as evidence
import synthetic_bake as bake
import test_ability_cost_derivation as fixtures


class PitcherPositionTests(unittest.TestCase):
    def test_position_evidence_is_scoped_to_source_id_and_season(self):
        row = {"sourceTeamName":"Test", "primaryPosition":"C", "positions":["C"]}
        source = {"sourcePlayerId":"1", "aggregateTeamName":"Test", "defenseRecords":[]}
        reference = {"year":2000,"players":[copy.deepcopy(source)]}
        evidence.attach_position_evidence(reference,{(2001,"1"):row,(2000,"2"):row})
        self.assertNotIn("_supplementalPositionEvidence",reference["players"][0])
        evidence.attach_position_evidence(reference,{(2000,"1"):row})
        position,trace=bake.derive_source_position(reference["players"][0],"DH")
        self.assertEqual(position,"C")
        self.assertEqual(trace["positionCandidates"],[])
        self.assertEqual(reference["players"][0]["defenseRecords"],[])
        self.assertEqual(bake.eligible_source_positions(reference["players"][0]),{"C"})

    def test_actual_defense_takes_priority_over_supplemental_position(self):
        source={"defenseRecords":[{"position":"1루수","inningsOuts":2700,"games":100}],
                "_supplementalPositionEvidence":{"primaryPosition":"C","positions":["C"]}}
        position,trace=bake.derive_source_position(source,"DH")
        self.assertEqual(position,"1B")
        self.assertFalse(trace["isSupplementalPositionApplied"])
        self.assertEqual(bake.eligible_source_positions(source),{"1B"})

    def test_position_evidence_rejects_different_team(self):
        with self.assertRaises(ValueError):
            evidence.attach_position_evidence({"year":2000,"players":[{"sourcePlayerId":"1","aggregateTeamName":"A"}]},
                                              {(2000,"1"):{"sourceTeamName":"B"}})

    def test_position_only_evidence_does_not_invent_dh_workload(self):
        row={"sourceSeasonGames":144,"costEligibilitySample":500,"positionRoleDerivationTrace":{
            "positionCandidates":[],"isSupplementalPositionApplied":True,"supplementalPositionEvidence":{"primaryPosition":"DH"}}}
        without=copy.deepcopy(row);without["positionRoleDerivationTrace"]={"positionCandidates":[]}
        self.assertEqual(bake.starter_usage_score(row,"DH"),bake.starter_usage_score(without,"DH"))

    def test_ace_quality_separates_from_average_rotation_with_same_workload(self):
        rows=[]
        for sid,quality in (("average",0),("ace",.9)):
            row=fixtures.AbilityCostDerivationTests._pitcher_season(sid,2008,"Starter",[60]*6,450)
            fixtures.AbilityCostDerivationTests._with_cost_quality(row,quality,.85)
            row["sourceSeasonGames"]=144
            row["_costValueInputs"]={"inningsOuts":450,"games":25,"gamesStarted":25,"gamesStartedAvailable":True}
            rows.append(row)
        bake.assign_origin_year_costs(rows)
        self.assertLessEqual(rows[0]["cost"],7)
        self.assertGreaterEqual(rows[1]["cost"]-rows[0]["cost"],2)
        self.assertEqual(rows[0]["baseAttributes"],rows[1]["baseAttributes"])

    def test_short_appearance_cannot_receive_elite_cost_from_quality_alone(self):
        components={"quality":2,"workload":{"ratio":.1},"reliability":.2,"roleGroup":"Rotation"}
        self.assertEqual(bake.elite_cost_ceiling(components,"Pitcher")[0],8)

    def test_rotation_gate_does_not_replace_relief_gate(self):
        components={"quality":.55,"workload":{"ratio":.8},"reliability":.7,"roleGroup":"Rotation"}
        self.assertEqual(bake.elite_cost_ceiling(components,"Pitcher")[0],10)
        components["roleGroup"]="Relief"
        self.assertEqual(bake.elite_cost_ceiling(components,"Pitcher")[0],9)

    def test_role_configuration_rejects_invalid_scope_and_coefficients(self):
        config=copy.deepcopy(bake.DERIVATION_BALANCE)
        config["costValueModel"]["pitcherRoleValueProfiles"]["PlayerName"]={}
        with self.assertRaises(ValueError): bake.validate_derivation_balance(config)
        for key,value in (("workloadScale",float('nan')),("workloadExponent",0),("positiveQualityWorkloadWeight",-1)):
            config=copy.deepcopy(bake.DERIVATION_BALANCE)
            config["costValueModel"]["pitcherRoleValueProfiles"]["Rotation"][key]=value
            with self.assertRaises(ValueError): bake.validate_derivation_balance(config)


if __name__=='__main__': unittest.main()
