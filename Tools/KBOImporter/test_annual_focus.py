"""구단 총합 일치가 선수별 과소평가를 숨기지 못하게 한다."""
import unittest

from audit_annual_focus import audit_focus


class AnnualFocusTests(unittest.TestCase):
    def test_equal_total_does_not_hide_underpriced_card(self):
        seasons=[dict(playerSeasonId=str(i),originFranchiseId='TEAM',originYear=2012,
                      sourceReferenceNames=[str(i)],cost=cost) for i,cost in enumerate((6,8))]
        labels=[dict(id=str(i),target='Cost',expected=7,origin='fixture') for i in range(2)]
        result=audit_focus(seasons,labels,'TEAM',{2012})
        self.assertEqual(result['groups'][0]['bias'],0)
        self.assertEqual(result['groups'][0]['underCount'],1)
        self.assertFalse(result['passed'])

    def test_missing_requested_year_cannot_pass(self):
        season=dict(playerSeasonId='1',originFranchiseId='TEAM',originYear=2012,
                    sourceReferenceNames=['P'],cost=8)
        result=audit_focus([season],[dict(id='1',target='Cost',expected=8,origin='fixture')],'TEAM',{2012,2013})
        self.assertEqual(result['missingYears'],[2013])
        self.assertFalse(result['passed'])

    def test_overpriced_card_also_fails(self):
        season=dict(playerSeasonId='1',originFranchiseId='TEAM',originYear=2012,
                    sourceReferenceNames=['P'],cost=9)
        result=audit_focus([season],[dict(id='1',target='Cost',expected=8,origin='fixture')],'TEAM',{2012})
        self.assertFalse(result['passed'])
        self.assertEqual(result['groups'][0]['overCount'],1)


if __name__=='__main__':
    unittest.main()
