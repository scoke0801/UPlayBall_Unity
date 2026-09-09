"""연도 카드 보정의 데이터 범위·결측·외삽·가격 독립성을 검증한다."""
import copy
import unittest

import synthetic_bake as bake
from record_calibration import evaluate_model, resolve_model_cost, validate_models


class RecordCalibrationTests(unittest.TestCase):
    def test_learned_cost_boundaries_preserve_top_tier_and_eligibility(self):
        model={'costBoundaries':[2,3,4,5,6,7,8,8.5,9]}
        self.assertEqual(resolve_model_cost(9,model,10),10)
        self.assertEqual(resolve_model_cost(9,model,8),8)
        self.assertEqual(resolve_model_cost(-100,model,10),1)
        prices=[resolve_model_cost(score/10,model,10) for score in range(120)]
        self.assertEqual(prices,sorted(prices))

    def test_invalid_cost_boundaries_are_rejected(self):
        for boundaries in ([1]*8,[1,2,3,4,5,6,7,9,8],[1,2,3,4,5,6,7,8,float('inf')]):
            models=copy.deepcopy(bake.DERIVATION_BALANCE['referenceRecordModels'])
            models['Hitter']['Cost']['costBoundaries']=boundaries
            with self.assertRaises(ValueError):
                validate_models(models,{'Hitter':set(bake.HITTER_METRIC_NAMES),'Pitcher':set(bake.PITCHER_METRIC_NAMES)})

    def test_count_features_distinguish_missing_and_observed_zero(self):
        missing={c['metric']:c for c in bake.pitcher_metric_evidence({'pitcherStats':{'inningsOuts':30}})}
        zero={c['metric']:c for c in bake.pitcher_metric_evidence({'pitcherStats':{'inningsOuts':30,'wins':0}})}
        self.assertFalse(missing['Wins']['isAvailable'])
        self.assertTrue(zero['Wins']['isAvailable'])
        self.assertEqual(zero['Wins']['rawValue'],0)

    def test_missing_evidence_preserves_baseline_and_zero_is_observed(self):
        model={'intercept':60,'features':[{'source':'HomeRuns.rawValue','mean':10,'scale':10,
                                         'minimum':0,'maximum':50,'coefficient':5}]}
        self.assertEqual(evaluate_model(model,{},55),(55,None))
        observed,trace=evaluate_model(model,{'HomeRuns':{'isAvailable':True,'rawValue':0}},55)
        self.assertEqual(observed,55)
        self.assertFalse(trace['contributions'][0]['isImputed'])
        self.assertEqual(evaluate_model(model,{'HomeRuns':{'isAvailable':True,'rawValue':0,'sampleSize':0}},40),(40,None))

    def test_extreme_out_of_sample_input_is_bounded(self):
        model={'intercept':50,'features':[{'source':'SeasonInnings.rawValue','mean':100,'scale':100,
                                         'minimum':0,'maximum':250,'coefficient':20}]}
        predictions=[evaluate_model(model,{'SeasonInnings':{'isAvailable':True,'rawValue':v}},55)[0] for v in (0,100,250,100000)]
        self.assertEqual(predictions,[30,50,80,80])

    def test_identity_and_nonfinite_coefficients_are_rejected(self):
        base=copy.deepcopy(bake.DERIVATION_BALANCE['referenceRecordModels'])
        for source in ('PlayerName.rawValue','TeamId.rawValue','SeasonYear.rawValue'):
            model=copy.deepcopy(base);model['Hitter']['Cost']['features'][0]['source']=source
            with self.assertRaises(ValueError):validate_models(model,{'Hitter':set(bake.HITTER_METRIC_NAMES),'Pitcher':set(bake.PITCHER_METRIC_NAMES)})
        base['Hitter']['Contact']['features'][0]['coefficient']=float('nan')
        with self.assertRaises(ValueError):bake.validate_derivation_balance({**bake.DERIVATION_BALANCE,'referenceRecordModels':base})
        base=copy.deepcopy(bake.DERIVATION_BALANCE['referenceRecordModels'])
        base['Hitter']['Contact']['features'][0]['coefficient']=-1
        with self.assertRaises(ValueError):bake.validate_derivation_balance({**bake.DERIVATION_BALANCE,'referenceRecordModels':base})

    def test_missing_velocity_is_an_explicit_estimate_not_a_measured_record(self):
        components={metric:{'isAvailable':False} for metric in bake.PITCHER_METRIC_NAMES}
        values,traces=bake.to_ratings_with_trace('Pitcher',(0,)*len(components),components)
        self.assertEqual([values[i] for i in (6,8,9,10,11)],[55]*5)
        velocity=next(t for t in traces if t['attribute']=='Velocity')
        self.assertEqual(velocity['evaluationMethod'],'EstimatedVelocityPrior')
        self.assertFalse(velocity['velocityEstimation']['measuredVelocity'])
        components['SeasonInnings']={'isAvailable':True,'rawValue':200,'adjustedZ':0,'reliability':1}
        values,traces=bake.to_ratings_with_trace('Pitcher',(0,)*len(components),components)
        self.assertFalse(components['FastballVelocityKph']['isAvailable'])

    def test_cost_prediction_ignores_displayed_attributes(self):
        model=bake.DERIVATION_BALANCE['referenceRecordModels']['Hitter']['Cost']
        evidence={'BattingAverage':{'isAvailable':True,'adjustedZ':1,'reliability':.7}}
        value={'quality':1,'workloadScore':3,'workload':{'ratio':.8},'defensiveValue':0}
        first=evaluate_model(model,evidence,6,value)
        value['baseAttributes']=[100]*12;value['PlayerName']='임의 선수'
        self.assertEqual(first,evaluate_model(model,evidence,6,value))
        predictions=[evaluate_model(model,evidence,6,{**value,'quality':quality})[0] for quality in (-1,0,1,2)]
        self.assertEqual(predictions,sorted(predictions))


if __name__=='__main__':unittest.main()
