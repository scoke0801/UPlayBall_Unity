"""로스터 선정의 수비 자격을 Runtime에 보존하는 계약을 검증한다."""
import copy
import unittest

import synthetic_bake as bake


class SecondaryPositionTests(unittest.TestCase):
    def test_repeated_fielding_is_qualified_but_one_inning_is_not(self):
        player = {"defenseRecords": [
            {"position": "좌익수", "inningsOuts": 854, "games": 50},
            {"position": "우익수", "inningsOuts": 754, "games": 53},
            {"position": "중견수", "inningsOuts": 517, "games": 28},
            {"position": "포수", "inningsOuts": 3, "games": 1},
        ]}
        self.assertEqual(bake.eligible_source_positions(player), {"LF", "RF", "CF"})
        reversed_player = copy.deepcopy(player)
        reversed_player["defenseRecords"].reverse()
        self.assertEqual(bake.eligible_source_positions(player), bake.eligible_source_positions(reversed_player))

    def test_missing_records_do_not_invent_secondary_positions(self):
        self.assertEqual(bake.eligible_source_positions({}), set())


if __name__ == "__main__":
    unittest.main()
