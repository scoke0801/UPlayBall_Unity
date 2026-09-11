"""분석 메모리 최적화가 정규시즌 집계와 원시 기록 구분을 보존하는지 검사한다."""
import unittest
from simulation_report import summarize_row


class SimulationReportTests(unittest.TestCase):
    def test_only_full_regular_season_is_aggregated(self):
        regular = dict(IsFirstHalf=False, IsPostseason=False, IsAllStarGame=False,
                       HomeRuns=2, Walks=3, Strikeouts=4, FieldingErrors=1)
        rows = [regular, dict(regular), dict(regular, IsFirstHalf=True),
                dict(regular, IsPostseason=True), dict(regular, IsAllStarGame=True)]
        result = summarize_row(dict(year=2021, seed=7, checksum='unchanged', statistics=rows))
        self.assertEqual(dict(HomeRuns=4, Walks=6, Strikeouts=8, FieldingErrors=2), result['regularTotals'])
        self.assertEqual('unchanged', result['checksum'])
        self.assertNotIn('statistics', result)

    def test_inconsistent_precomputed_totals_are_rejected(self):
        with self.assertRaises(ValueError):
            summarize_row(dict(year=2021, seed=7, statistics=[], regularTotals=dict(Walks=999)))

    def test_other_data_is_not_changed(self):
        value = dict(year=2021, team='fixture', statistics=[1])
        self.assertIs(value, summarize_row(value))


if __name__ == '__main__':
    unittest.main()
