"""월별·연도 제한과 선수 단위 분리 정책을 검증한다."""
import unittest
from pathlib import Path
from calibrate_annual_reference import is_annual_reference, read, split_person


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


if __name__=='__main__':unittest.main()
