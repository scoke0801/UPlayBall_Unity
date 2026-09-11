"""일반 카드 근거 연결과 출처 충돌 방지 회귀 테스트."""

import copy
import json
import unittest
from pathlib import Path
from unittest.mock import patch

import synthetic_bake as bake
from compile_archived_reference import compile_cards

ROOT = Path(__file__).resolve().parents[2]


class ArchivedReferenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.data = json.loads((ROOT / 'Tools/KBOImporter/annual_reference_1985_samsung.json').read_text(encoding='utf-8'))

    def test_nineteen_cards_and_only_observed_values(self):
        self.assertEqual(19, len(self.data['cards']))
        for card in self.data['cards']:
            self.assertEqual(1985, card['originYear'])
            self.assertNotIn('Arm', card['values'])
            self.assertNotIn('Bunt', card['values'])
        corrected = next(c for c in self.data['cards'] if 'supersedes' in c)
        self.assertEqual({'Cost': 10}, corrected['values'])
        self.assertEqual({'Cost': 8}, corrected['supersedes']['values'])

    def test_unobserved_attribute_is_preserved(self):
        season = dict(playerSeasonId='S', playerType='Hitter', originYear=1985,
            baseAttributes=[55]*12, cost=4,
            costDerivationTrace=dict(cost=4, costEligibility={}, eliteEligibility={}))
        override = dict(playerType='Hitter', originYear=1985, values={'Cost': 8, 'Contact': 74}, sources={})
        bake.apply_annual_reference_overrides([season], {'S': override})
        self.assertEqual(74, season['baseAttributes'][0])
        self.assertEqual([55]*11, season['baseAttributes'][1:])

    def test_unknown_edition_cannot_become_normal(self):
        cards = [c for c in self.data['researchCards'] if c['CardType'] != '일반']
        accepted, rejected = compile_cards(cards, [])
        self.assertFalse(accepted)
        self.assertEqual(3, len(rejected))
        self.assertTrue(all(r['reason'] == 'SpecialOrUnknownEdition' for r in rejected))

    def test_duplicate_requires_exact_reviewed_previous_card(self):
        config = copy.deepcopy(bake.DERIVATION_BALANCE['annualReferenceOverride'])
        old = dict(playerSeasonId='S', values={'Cost': 8})
        base = {r['card']['playerSeasonId']: r['card'] for r in config['rejectedCards']}
        base['S'] = old
        with patch.object(bake, 'load_annual_reference_file', side_effect=[copy.deepcopy(base), {'S': {'values': {'Cost': 10}}}]):
            with self.assertRaises(ValueError):
                bake.load_annual_reference_overrides()
        new = dict(values={'Cost': 10}, supersedes=old)
        with patch.object(bake, 'load_annual_reference_file', side_effect=[copy.deepcopy(base), {'S': new}]
                          + [{} for _ in config['additionalSources'][1:]]):
            self.assertEqual(new, bake.load_annual_reference_overrides()['S'])
        self.assertEqual(config, bake.DERIVATION_BALANCE['annualReferenceOverride'])

    def test_source_hash_and_count_validate(self):
        cards = bake.load_annual_reference_overrides()
        self.assertEqual(7543, len(cards))
        self.assertNotIn('SEASON_6840de0aeb942aea0991', cards)
        for card in self.data['cards']:
            self.assertEqual(card, cards[card['playerSeasonId']])


if __name__ == '__main__':
    unittest.main()
