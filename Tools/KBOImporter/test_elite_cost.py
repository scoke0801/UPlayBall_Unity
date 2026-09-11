"""고코스트 하한의 소표본 배제·일반 카드 독립성·결정론을 검증한다."""
import copy
import unittest
from elite_cost import apply_cost_floors


def season(sid, value, cost=5, quality=0, workload=1, reliability=.6, role='Pitcher', starter=0):
    return dict(playerSeasonId=sid, originYear=2025, playerType=role, cost=cost,
        baseAttributes=[55]*12, costDerivationTrace=dict(cost=cost, costMethod='Reference', continuousValue=value,
        roleWeights=[dict(ability='Contact' if role == 'Hitter' else 'Stuff', weight=1)],
        componentScores=dict(quality=quality, reliability=reliability,
        workload=dict(ratio=workload, starterShare=starter, sample=70))))


class EliteCostTests(unittest.TestCase):
    def test_final_ability_reprices_estimated_cost_without_changing_attributes(self):
        rows = [season('leader', 20, 10, role='Hitter'), season('recalibrated', 5, 7, role='Hitter')]
        rows[1]['baseAttributes'][0] = 82
        apply_cost_floors(rows)
        self.assertEqual(10, rows[1]['cost'])
        self.assertEqual(82, rows[1]['baseAttributes'][0])
        self.assertEqual('FinalIssuedAbility', rows[1]['costDerivationTrace']['eliteCostAdjustment']['reason'])

    def test_observed_cost_survives_every_floor_and_restores_previous_drift(self):
        rows = [season('observed', 20, 10, quality=1), season('runner-up', 5, 5, starter=1)]
        observed = rows[0]
        observed['baseAttributes'][8] = 99
        observed['annualReferenceOverride'] = dict(values={'Cost': 7}, sources={'Cost': 'fixture'})
        observed['costDerivationTrace']['eliteCostAdjustment'] = dict(previousCost=7)
        apply_cost_floors(rows)
        self.assertEqual([7, 5], [row['cost'] for row in rows])
        self.assertEqual('AnnualReferenceOverride', observed['costDerivationTrace']['costMethod'])
        self.assertNotIn('eliteCostAdjustment', observed['costDerivationTrace'])
        before = copy.deepcopy(rows)
        apply_cost_floors(rows)
        self.assertEqual(before, rows)

    def test_ability_tiers_are_monotonic_and_reject_small_samples(self):
        rows = [season('leader', 20, 10, starter=1)]
        for rating in (73, 74, 76, 79, 81):
            row = season(str(rating), 5, 5, starter=1)
            row['baseAttributes'][8] = rating
            rows.append(row)
        tiny = season('tiny', 5, 5, starter=1, workload=.1)
        tiny['baseAttributes'][8] = 99
        rows.append(tiny)
        apply_cost_floors(rows)
        self.assertEqual([10, 5, 7, 8, 9, 10, 5], [row['cost'] for row in rows])

    def test_leader_is_priced_without_any_special_card_input(self):
        rows = [season('a', 10, 7, starter=1), season('b', 5, 4, starter=1)]
        apply_cost_floors(rows)
        self.assertEqual([10, 4], [r['cost'] for r in rows])
        self.assertEqual([55]*12, rows[0]['baseAttributes'])

    def test_relief_quality_requires_workload_and_reliability(self):
        rows = [season('leader', 20, 10, starter=1), season('good', 8, quality=.7),
                season('tiny', 8, quality=.9, workload=.1), season('weak', 7, quality=-.2)]
        apply_cost_floors(rows)
        self.assertEqual([10, 10, 5, 5], [r['cost'] for r in rows])

    def test_order_does_not_change_prices_or_tie_break(self):
        rows = [season('a', 10), season('b', 10), season('strong', 5)]
        rows[-1]['baseAttributes'][8] = 85
        reverse = copy.deepcopy(rows[::-1])
        apply_cost_floors(rows)
        apply_cost_floors(reverse)
        self.assertEqual(rows, reverse[::-1])

    def test_existing_expensive_cards_never_fall(self):
        rows = [season('leader', 20, 10), season('old', 5, 10)]
        apply_cost_floors(rows)
        self.assertTrue(all(r['cost']==10 for r in rows))


if __name__ == '__main__':
    unittest.main()
