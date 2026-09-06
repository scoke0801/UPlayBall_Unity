"""원문 등급·판본 보존과 선수 단위 분할의 회귀 테스트."""
import unittest
from bs4 import BeautifulSoup

from extract_pm_thresholds import parse_defense, parse_pitchers, player_label
from study_pm_expanded import split_player


def icon(grade):
    return f'<img src="https://img.inven.co.kr/image/bm/fonticon/positionrank1_{grade}1.gif"/>'


class ThresholdExtractionTests(unittest.TestCase):
    def test_defense_keeps_variant_card_and_target_grade(self):
        html = f'''<table><tr><td class="as1">9</td><td><div onmouseover="BM.Db.PlayerLayer.show(123);">08 검증선수</div></td>
            <td>3루수{icon('b')}</td><td></td><td>3루수{icon('a')}</td><td>수비력 +1</td>
            <td>3루수{icon('s')}</td><td>수비력 +11</td></tr></table>'''
        row = parse_defense(BeautifulSoup(html, 'lxml'))[0]
        self.assertEqual((row['variant'], row['cardId'], row['year']), ('AllStar', '123', 2008))
        self.assertEqual(row['baseGrades'], ['B'])
        self.assertEqual(row['targets'][1]['additional'], [11])
        self.assertEqual(row['targets'][1]['grades'], ['S'])
        self.assertNotIn('baseDefense', row)

    def test_pitch_threshold_keeps_pitch_specific_target(self):
        html = f'''<table><tr><th>09 검증선수</th><td></td><th>필요 변화구 수치</th></tr><tr><td>
            <img src="https://img.inven.co.kr/playercard/123f.jpg"/><img src="https://img.inven.co.kr/playercard/123b.jpg"/>
            </td><td><div class="subTable"><table><tr><td>슬라이더{icon('a')}</td><td>+3</td></tr>
            <tr><td>슬라이더{icon('s')}</td><td>+13</td></tr></table></div></td></tr></table>'''
        row = parse_pitchers(BeautifulSoup(html, 'lxml'))[0]
        self.assertEqual(row['thresholds'][0], {'pitch': '슬라이더', 'targetGrades': ['A'], 'additional': 3})
        self.assertEqual(row['thresholds'][1]['additional'] - row['thresholds'][0]['additional'], 10)
        self.assertNotIn('baseBreaking', row)

    def test_missing_back_image_is_rejected(self):
        html = '<table><tr><th>09 검증선수</th><th>필요 변화구 수치</th></tr><tr><td></td></tr></table>'
        with self.assertRaises(ValueError):
            parse_pitchers(BeautifulSoup(html, 'lxml'))

    def test_year_and_non_player_rows_are_not_guessed(self):
        self.assertEqual(player_label('99 검증선수'), (1999, '검증선수'))
        self.assertEqual(player_label('00 검증선수'), (2000, '검증선수'))
        with self.assertRaises(ValueError):
            player_label('선수 카드')

    def test_person_split_is_stable_without_season_or_card_variant(self):
        first = [split_player(f'PERSON_{i}') for i in range(200)]
        self.assertEqual(first, [split_player(f'PERSON_{i}') for i in range(200)])
        self.assertEqual(set(first), {'Train', 'Validation', 'Holdout'})


if __name__ == '__main__':
    unittest.main()
