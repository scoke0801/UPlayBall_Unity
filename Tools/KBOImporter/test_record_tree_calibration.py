"""기록 나무의 학습기 수치 계약과 잘못된 모델 차단을 검증한다."""
import copy
import unittest

from record_calibration import evaluate_model,resolve_model_cost
from record_tree_calibration import MODEL_TYPE,model_hash,predict_trees,validate_trees


def example_model():
    model=dict(modelType=MODEL_TYPE,intercept=5.0,
        features=[dict(source='Wins.rawValue',mean=5.0,scale=1.0,minimum=0.0,maximum=20.0,coefficient=0.0)],
        trees=[[[0,10.0,1,2,0.0],[-1,-2.0,-1,-1,-1.0],[-1,-2.0,-1,-1,5.0]]])
    model['modelSha256']=model_hash(model)
    return model


class RecordTreeTests(unittest.TestCase):
    def test_float32_branching_matches_training_precision(self):
        model=example_model()
        self.assertEqual(predict_trees(model,[10+1e-8]),4)
        self.assertEqual(predict_trees(model,[10+1e-4]),10)
        self.assertEqual(predict_trees(model,[None]),4)

    def test_zero_games_is_evidence_and_missing_records_keep_baseline(self):
        model=example_model()
        self.assertEqual(evaluate_model(model,{},3),(3,None))
        value,trace=evaluate_model(model,{'Wins':dict(isAvailable=True,rawValue=0,sampleSize=0)},3)
        self.assertEqual(value,4)
        self.assertEqual(trace['method'],MODEL_TYPE)

    def test_learned_price_has_no_duplicate_legacy_ceiling(self):
        self.assertEqual(resolve_model_cost(10,example_model(),8),10)
        self.assertEqual(resolve_model_cost(100,example_model(),8),10)
        self.assertEqual(resolve_model_cost(-100,example_model(),8),1)

    def test_corrupt_cycle_index_and_checksum_are_rejected(self):
        model=example_model();validate_trees(model)
        for index,value in ((2,0),(3,99),(0,99)):
            corrupt=copy.deepcopy(model);corrupt['trees'][0][0][index]=value
            corrupt['modelSha256']=model_hash(corrupt)
            with self.assertRaises(ValueError):validate_trees(corrupt)
        model['intercept']=6
        with self.assertRaises(ValueError):validate_trees(model)


if __name__=='__main__':
    unittest.main()
