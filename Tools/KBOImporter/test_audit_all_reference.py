"""전체 구단 평가 감사의 오차·선수 Fold 계약을 검증한다."""
import unittest

from audit_all_reference import evaluate, fold_for_person


class AllReferenceAuditTests(unittest.TestCase):
    def test_evaluate_keeps_under_and_over_separate(self):
        result=evaluate([(10,9),(5,6),(3,3)])
        self.assertEqual(result['underCount'],1)
        self.assertEqual(result['overCount'],1)
        self.assertEqual(result['exact'],1/3)
        self.assertEqual(result['bias'],0)

    def test_same_person_always_uses_same_fold(self):
        first=fold_for_person('PERSON_A','salt',5)
        self.assertEqual(first,fold_for_person('PERSON_A','salt',5))
        self.assertTrue(0<=first<5)


if __name__=='__main__':
    unittest.main()
