"""번트 관측·결측·독립 성장 상한과 어깨 오인 방지를 검증한다."""
import unittest
import bunt_primary_stat as bunt


class BuntPrimaryStatTests(unittest.TestCase):
    def test_shoulder_is_not_bunt_observation(self):
        value, status, sources = bunt.resolve([60, 50, 50, 99, 20, 80],
            {'values': {'Arm': 99}, 'sources': {'Arm': 'Database:ta:1'}}, {})
        self.assertEqual((68, 'EstimatedWithoutBuntObservation', []), (value, status, sources))

    def test_direct_bunt_source_wins_over_estimate(self):
        self.assertEqual((91, 'Observed', ['archive:1']), bunt.resolve([50]*12,
            {'sources': {'Contact': 'archive:1'}}, {'archive:1': 91}))

    def test_conflicting_sources_fail(self):
        with self.assertRaises(ValueError):
            bunt.resolve([50]*12, {'sources': {'Contact': 'a', 'Power': 'b'}}, {'a': 30, 'b': 70})

    def test_growth_ceiling_preserves_headroom(self):
        season = dict(playerSeasonId='s', playerType='Hitter', baseAttributes=[60,50,50,90,30,80],
            trainingCeiling=[100]*6)
        bunt.apply([season], {}, {})
        self.assertEqual(68, season['baseAttributes'][3])
        self.assertEqual(78, season['trainingCeiling'][3])
        self.assertEqual(30, season['baseAttributes'][4])


if __name__ == '__main__':
    unittest.main()
