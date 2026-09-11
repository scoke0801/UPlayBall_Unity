"""개선 채택이 빈 표본·다른 입력·평균 통계 악화를 숨기지 않도록 검사한다."""
import copy
import unittest
from verify_improvement import evaluate


class ImprovementGateTests(unittest.TestCase):
    def setUp(self):
        metadata = dict(engineVersion=7, balanceHash='same', regularSeasonGamesPerTeam=144,
                        rotationPolicy='FixedFive', contentHash='after')
        statistics = dict(AVG=.27, ERA=4, runsPerGame=8, homeRunsPerGame=2, walksToStrikeouts=.5)
        self.comparison = dict(metadata=dict(before=dict(metadata, contentHash='before'), after=metadata),
            teams=[dict(year=2000, key='a')],
            agreement=dict(before=dict(winRateMae=.06), after=dict(winRateMae=.04)),
            leagues=dict(before=statistics, after=copy.deepcopy(statistics)))
        self.leaders = dict(contentHash='after', passed=False, passedCount=0,
            teams=[dict(year=2000, teamSeasonKey='a', difference=.035, rankPassed=True, samplePassed=True, repeats=32)])

    def test_individual_failure_remains_visible(self):
        result = evaluate(self.comparison, self.leaders)
        self.assertTrue(result['statisticalSelectionPassed'])
        self.assertFalse(result['individualYearTargetsPassed'])

    def test_empty_results_rejected(self):
        self.leaders['teams'] = []
        with self.assertRaises(ValueError):
            evaluate(self.comparison, self.leaders)

    def test_different_balance_rejected(self):
        self.comparison['metadata']['after']['balanceHash'] = 'changed'
        with self.assertRaises(ValueError):
            evaluate(self.comparison, self.leaders)

    def test_worse_all_team_error_fails(self):
        self.comparison['agreement']['after']['winRateMae'] = .07
        self.assertFalse(evaluate(self.comparison, self.leaders)['statisticalSelectionPassed'])

    def test_declared_balance_change_keeps_statistical_checks(self):
        self.comparison['metadata']['after']['balanceHash'] = 'changed'
        result = evaluate(self.comparison, self.leaders, allow_balance_change=True)
        self.assertTrue(result['balanceChanged'])
        self.comparison['leagues']['after']['ERA'] = 5
        self.assertFalse(evaluate(self.comparison, self.leaders, allow_balance_change=True)['statisticalSelectionPassed'])

    def test_league_drift_fails(self):
        self.comparison['leagues']['after']['ERA'] = 5
        self.assertFalse(evaluate(self.comparison, self.leaders)['statisticalSelectionPassed'])


if __name__ == '__main__':
    unittest.main()
