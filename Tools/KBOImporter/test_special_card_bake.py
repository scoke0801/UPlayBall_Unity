"""특수 카드 발급 Gate·결정론·Runtime 식별 정보 경계를 검증한다."""
import copy
import hashlib
import json
import tempfile
import unittest
from pathlib import Path

from bake_special_cards import BakeValidationError, build_catalog, verify_inputs, validate_canonical


class SpecialCardBakeTests(unittest.TestCase):
    def fixture(self):
        row = dict(playerSeasonId='ps-peak', playerPersonId='person-1', lineage='lineage-1',
                   year=2000, role='Hitter', cost=10, status='Eligible', sourceNames=['출처 이름'],
                   originFranchiseId='runtime-franchise',
                   qualifiedYears=list(range(2000, 2008)),
                   qualifiedNormalCardIds=[f'ps-{year}:Normal' for year in range(2000, 2008)])
        pitcher = dict(row, role='Pitcher', playerSeasonId='ps-pitcher')
        evaluation = dict(policy=dict(version='evaluation-v1'), ex=[row, pitcher],
                          careerHigh=[row], legendShortlist=[dict(row, playerPersonId='person-2',
                                                                 playerSeasonId='ps-legend')], inputHash='fixed')
        policy = dict(version='bake-v1', evaluationPolicyVersion='evaluation-v1', supportedYears=[2000],
                      careerHighAllBonus=2, legendAllBonus=2,
                      legends=[dict(playerPersonId='person-2', lineage='lineage-1', enabled=True,
                                    curatedReasonTags=['Fixture'], materialGroups=[
                                        dict(groupId=f'g-{i}', candidateCardIds=[f'legend-{i}:Normal'])
                                        for i in range(8)])])
        return evaluation, policy

    def test_career_high_excludes_legend_across_years_lineages_and_partial_scope(self):
        for lineage in ('lineage-1', 'transferred-lineage'):
            for editions in (['CareerHigh', 'Legend'], ['Legend']):
                with self.subTest(lineage=lineage, editions=editions):
                    evaluation, policy = self.fixture()
                    policy['legends'][0].update(playerPersonId='person-1', lineage=lineage,
                                                basePlayerSeasonId='another-year')
                    result = build_catalog(evaluation, policy, editions)
                    self.assertFalse(any(card['edition'] == 'Legend' for card in result['cards']))
                    self.assertEqual(1, len(result['excludedLegends']))
                    self.assertEqual(len(result['cards']), len(result['recipes']))

    def test_ineligible_career_high_does_not_exclude_legend(self):
        evaluation, policy = self.fixture()
        evaluation['careerHigh'][0]['status'] = 'InsufficientYears'
        policy['legends'][0]['playerPersonId'] = 'person-1'
        result = build_catalog(evaluation, policy, ['CareerHigh', 'Legend'])
        self.assertEqual(['Legend'], [card['edition'] for card in result['cards']])
        self.assertEqual([], result['excludedLegends'])

    def test_bake_preserves_ids_and_is_deterministic(self):
        evaluation, policy = self.fixture()
        result = build_catalog(evaluation, policy)
        reordered = copy.deepcopy(evaluation)
        reordered['ex'].reverse()
        self.assertEqual(result, build_catalog(reordered, policy))
        self.assertEqual(4, len(result['cards']))
        self.assertEqual(2, len(result['recipes']))
        self.assertNotIn('출처 이름', str(result))
        self.assertEqual(8, len(result['recipes'][0]['materialGroups']))

    def test_explicit_partial_scope_does_not_claim_ex_is_issued(self):
        evaluation, policy = self.fixture()
        evaluation['ex'][0] = dict(evaluation['ex'][0], cost=9)
        result = build_catalog(evaluation, policy, ['Legend', 'CareerHigh'])
        self.assertEqual(['CareerHigh', 'Legend'], result['editionScope'])
        self.assertNotIn('Ex', [card['edition'] for card in result['cards']])
        with self.assertRaises(BakeValidationError):
            build_catalog(evaluation, policy)

    def test_changed_material_input_requires_recompiling_curation(self):
        evaluation, policy = self.fixture()
        policy['legendMaterialInputHash'] = 'old-input'
        with self.assertRaises(BakeValidationError):
            build_catalog(evaluation, policy)

    def test_ex_winner_cost_gate_never_substitutes_runner_up(self):
        evaluation, policy = self.fixture()
        evaluation['ex'][0] = dict(evaluation['ex'][0], cost=9, runnerUpCost=10)
        with self.assertRaises(BakeValidationError) as error:
            build_catalog(evaluation, policy)
        self.assertTrue(any('ExCostGate' in entry for entry in error.exception.errors))

    def test_ex_below_ten_is_cancelled_without_promoting_runner_up(self):
        evaluation, policy = self.fixture()
        evaluation['ex'][0] = dict(evaluation['ex'][0], cost=8, status='BlockedCost', runnerUpCost=10)
        result = build_catalog(evaluation, policy)
        self.assertEqual(1, len(result['cancelledEx']))
        self.assertEqual(8, result['cancelledEx'][0]['cost'])
        ex = [card for card in result['cards'] if card['edition'] == 'Ex']
        self.assertEqual(['ps-pitcher'], [card['playerSeasonId'] for card in ex])

    def test_missing_ex_and_duplicate_role_fail(self):
        evaluation, policy = self.fixture()
        evaluation['ex'][1] = dict(evaluation['ex'][0])
        with self.assertRaises(BakeValidationError) as error:
            build_catalog(evaluation, policy)
        self.assertTrue(any('DuplicateEx' in entry for entry in error.exception.errors))
        self.assertTrue(any('MissingEx' in entry for entry in error.exception.errors))

    def test_legend_requires_curation_and_eight_nonempty_groups(self):
        evaluation, policy = self.fixture()
        policy['legends'][0]['materialGroups'][1]['candidateCardIds'] = []
        with self.assertRaises(BakeValidationError):
            build_catalog(evaluation, policy)
        policy['legends'] = []
        with self.assertRaises(BakeValidationError):
            build_catalog(evaluation, policy)

    def test_career_high_rejects_duplicate_years_and_peak_cost(self):
        evaluation, policy = self.fixture()
        evaluation['careerHigh'][0] = dict(evaluation['careerHigh'][0], qualifiedYears=[2000] * 8)
        with self.assertRaises(BakeValidationError):
            build_catalog(evaluation, policy)
        evaluation['careerHigh'][0] = dict(evaluation['careerHigh'][0], qualifiedYears=list(range(2000, 2008)), cost=8)
        with self.assertRaises(BakeValidationError):
            build_catalog(evaluation, policy)

    def test_changed_source_cannot_reuse_old_evaluation(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / 'source.json'
            source.write_bytes(b'old')
            evaluation = dict(inputFiles=[dict(path='source.json', sha256=hashlib.sha256(b'old').hexdigest())])
            verify_inputs(evaluation, root)
            source.write_bytes(b'new')
            with self.assertRaises(BakeValidationError):
                verify_inputs(evaluation, root)

    def test_runtime_export_checks_actual_materials_and_uses_opaque_lineages(self):
        evaluation, policy = self.fixture()
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            relative = 'Assets/Editor Default Resources/HistoricalSimulation/Test/Runtime/Years/2000.json'
            evaluation['runtimeArchiveRoot'] = 'Assets/Editor Default Resources/HistoricalSimulation/Test/Runtime'
            path = root / relative
            path.parent.mkdir(parents=True)
            ids = ['ps-peak', 'ps-pitcher', 'ps-legend'] + [f'ps-{year}' for year in range(2000, 2008)] + [f'legend-{i}' for i in range(8)]
            seasons = [dict(playerSeasonId=sid, playerPersonId='person-2' if sid == 'ps-legend' else 'person-1', originFranchiseId='runtime-franchise',
                            originYear=2000 + index, cost=10) for index, sid in enumerate(ids)]
            normals = [dict(cardId=sid + ':Normal', playerSeasonId=sid, edition='Normal') for sid in ids]
            path.write_text(json.dumps(dict(playerSeasons=seasons, normalCards=normals)), encoding='utf-8')
            evaluation['inputFiles'] = [dict(path=relative)]
            result = validate_canonical(build_catalog(evaluation, policy), evaluation, root)
            self.assertTrue(all(card['teamColorLineageId'].startswith('LINEAGE_') for card in result['cards']))
            self.assertEqual('runtime-franchise', result['lineages'][0]['franchiseId'])
            normals.pop()
            path.write_text(json.dumps(dict(playerSeasons=seasons, normalCards=normals)), encoding='utf-8')
            with self.assertRaises(BakeValidationError):
                validate_canonical(build_catalog(evaluation, policy), evaluation, root)


if __name__ == '__main__':
    unittest.main()
