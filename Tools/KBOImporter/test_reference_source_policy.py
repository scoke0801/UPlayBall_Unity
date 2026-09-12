"""출처 게임 혼동이 로딩·재수집에 다시 들어오지 않는지 검증한다."""
import unittest
import copy
from reference_source_policy import rejection_reason, card_rejection_reason, validate_training_sources
import synthetic_bake as bake
from repair_rejected_reference_cost import repair_seasons
from test_elite_cost import season


class ReferenceSourcePolicyTests(unittest.TestCase):
    def test_article_aliases_are_rejected(self):
        for source in ('ArticleCost:https://www.inven.co.kr/webzine/news/?news=107123',
                       'http://m.inven.co.kr/webzine/wznews.php?site=bm&news=107123#card'):
            self.assertIsNotNone(rejection_reason(source))

    def test_unrelated_observations_remain_available(self):
        for source in ('Database:ta:224', 'ArchivedWebCard:Research/PyaMaeCardDb/2010-2016/archive-cards.csv:10637',
                       'https://www.inven.co.kr/webzine/news/?news=107124'):
            self.assertIsNone(rejection_reason(source))

    def test_loader_excludes_every_rejected_source(self):
        cards = bake.load_annual_reference_overrides()
        self.assertTrue(cards)
        self.assertFalse(any(card_rejection_reason(card) for card in cards.values()))
        self.assertNotIn('SEASON_5527f94e86e73e9f463e', cards)
        self.assertEqual(8, cards['SEASON_9b23a0bdabecd7f3bf3d']['values']['Cost'])

    def test_repair_uses_full_population_and_preserves_other_cards(self):
        leader = season('leader', 20, 10, starter=1)
        damaged = season('damaged', 10, 8, starter=1)
        damaged['annualReferenceOverride'] = dict(formulaCost=9,values={'Cost':8},
            sources={'Cost':'ArticleCost:https://www.inven.co.kr/webzine/news/?news=107123'})
        before_leader = copy.deepcopy(leader)
        attributes = list(damaged['baseAttributes'])
        repair_seasons([damaged,leader])
        self.assertEqual(9,damaged['cost'])
        self.assertEqual(before_leader,leader)
        self.assertEqual(attributes,damaged['baseAttributes'])
        self.assertNotIn('annualReferenceOverride',damaged)
        self.assertEqual(8,damaged['costDerivationTrace']['rejectedReference']['previousCost'])
        snapshot = copy.deepcopy(damaged)
        self.assertEqual([],repair_seasons([damaged,leader]))
        self.assertEqual(snapshot,damaged)

    def test_direct_application_cannot_bypass_source_rejection(self):
        with self.assertRaises(ValueError):
            bake.apply_annual_reference_overrides([{'playerSeasonId':'s'}], {'s':dict(
                sources={'Cost':'https://www.inven.co.kr/webzine/news/?news=107123'})})

    def test_old_training_cache_is_rejected(self):
        with self.assertRaises(ValueError):
            validate_training_sources([{'origin':'ArticleCost:https://www.inven.co.kr/webzine/news/?news=107123'}],[])
        validate_training_sources([{'origin':'Database:tu:10'}],[])
