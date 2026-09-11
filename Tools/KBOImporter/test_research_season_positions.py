"""시즌 통합 문서와 동명이인 때문에 다른 해의 수비 위치가 섞이지 않도록 검증한다."""
import unittest
import copy
from research_season_positions import parse_roster
from source_position_evidence import load_position_evidence
from prepare_position_revision import retain_valuations


class SeasonRosterParsingTests(unittest.TestCase):
    def test_position_revision_keeps_issued_value_and_new_position(self):
        before = {'manifest': {}, 'years': [{'year': 1994, 'playerSeasons': [{
            'playerSeasonId': 'season', 'position': 'DH', 'cost': 5,
            'costDerivationTrace': {'roleWeights': [1]}, 'costMetricEvidence': [2],
            'baseAttributes': [60], 'trainingCeiling': [65]}]}]}
        after = copy.deepcopy(before)
        row = after['years'][0]['playerSeasons'][0]
        row.update(position='C', cost=6, costDerivationTrace={'roleWeights': [3]})
        self.assertEqual(retain_valuations(before, after, editor=True), 1)
        self.assertEqual(row['position'], 'C')
        self.assertEqual(row['cost'], 5)
        self.assertEqual(row['costDerivationTrace'], {'roleWeights': [1]})
        row['baseAttributes'] = [61]
        with self.assertRaises(ValueError):
            retain_valuations(before, after, editor=True)

    def test_committed_evidence_has_unique_ids_and_complete_sources(self):
        rows = load_position_evidence()
        self.assertGreater(len(rows), 3000)
        for (year, source_id), row in rows.items():
            self.assertEqual(year, row['seasonYear'])
            self.assertEqual(source_id, str(row['sourcePlayerId']))
            self.assertTrue(row['sourceTeamName'])
            self.assertTrue(row['sources'])

    def test_single_season_excludes_awards_and_next_section(self):
        html = '<h1>1994년 LG 트윈스 시즌</h1><h2>타이틀</h2><ul><li>포수: 다른선수</li></ul>' \
               '<h2>선수단</h2><ul><li>포수: 가상포수, 예비포수</li><li>지명타자: 가상타자</li></ul>' \
               '<h2>여담</h2><ul><li>유격수: 다른선수</li></ul>'
        self.assertEqual(parse_roster(html, 1994), {'가상포수': ['C'], '예비포수': ['C'], '가상타자': ['DH']})
        self.assertEqual(parse_roster(html, 1993), {})

    def test_combined_season_redirect_uses_requested_year_only(self):
        html = '<h1>1982년~1989년 MBC 청룡 시즌</h1><h2>1982년</h2><h3>선수단</h3>' \
               '<ul><li>2루수: 가상선수</li></ul><h2>1983년</h2><h3>선수단</h3>' \
               '<ul><li>1루수: 가상선수</li></ul>'
        self.assertEqual(parse_roster(html, 1982), {'가상선수': ['2B']})
        self.assertEqual(parse_roster(html, 1983), {'가상선수': ['1B']})
        self.assertEqual(parse_roster(html, 1984), {})

    def test_ambiguous_positions_are_preserved_for_review(self):
        html = '<h1>1994년 가상 구단 시즌</h1><h2>선수단</h2>' \
               '<ul><li>포수: 동일선수</li><li>1루수: 동일선수</li></ul>'
        self.assertEqual(parse_roster(html, 1994)['동일선수'], ['C', '1B'])

    def test_position_table_reads_names_without_inventing_defensive_innings(self):
        html = '<h1>2000년 가상 구단 시즌</h1><h2>선수단</h2><h3>포수</h3>' \
               '<table><tr><th>이름</th><th>경기</th></tr><tr><td>가상포수</td><td>3</td></tr></table>' \
               '<h3>1루수</h3><table><tr><th>이름</th></tr><tr><td>가상타자</td></tr></table>'
        self.assertEqual(parse_roster(html, 2000), {'가상포수': ['C'], '가상타자': ['1B']})
        self.assertEqual(parse_roster(html, 2001), {})


if __name__ == '__main__':
    unittest.main()
