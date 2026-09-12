"""명시적 인물 선정·Peak Gate·재료 인물 보호와 저작 결정론을 검증한다."""
import copy
import unittest

from bake_special_cards import BakeValidationError
from compile_legend_curation import compile_curation


class LegendCurationTests(unittest.TestCase):
    def fixture(self):
        peak = dict(playerPersonId='target', playerSeasonId='peak', lineage='lineage', cost=9, role='Hitter',
                    sourceNames=['검수 이름'], year=2020, qualifiedDistinctYears=10)
        profiles, seasons = [], []
        for index in range(8):
            role = 'Hitter' if index % 2 == 0 else 'Pitcher'
            cost = 5 + index // 2
            profiles.append(dict(groupId=f'g-{index}', role=role, cost=cost))
            for year in (2000, 2010, 2020):
                seasons.append(dict(playerPersonId=f'person-{index}', playerSeasonId=f's-{index}-{year}',
                                    lineage='lineage', cost=cost, role=role, sourceNames=[f'재료 {index}'],
                                    qualified=True, year=year, issuedStrength=50, position=role,
                                    normalCardId=f's-{index}-{year}:Normal'))
        evaluation = dict(careerHigh=[peak], seasons=seasons)
        curation = dict(materialGroups=profiles, maximumCandidatesPerGroup=3, allowTargetPersonMaterials=False,
                        legends=[dict(playerPersonId='target', lineage='lineage', role='Hitter',
                                      sourceReferenceName='검수 이름', curatedReasonTags=['LineageHitterRepresentative'])])
        return evaluation, curation

    def test_selection_is_explicit_and_materials_are_distinct_people(self):
        evaluation, curation = self.fixture()
        entries, report = compile_curation(evaluation, curation)
        self.assertEqual('target', entries[0]['playerPersonId'])
        self.assertEqual(8, len(entries[0]['materialGroups']))
        people = {group['playerPersonId'] for group in report[0]['groups']}
        self.assertEqual(8, len(people))
        self.assertNotIn('target', people)
        reversed_evaluation = copy.deepcopy(evaluation)
        reversed_evaluation['seasons'].reverse()
        self.assertEqual((entries, report), compile_curation(reversed_evaluation, curation))

    def test_missing_or_wrong_identity_cannot_be_substituted(self):
        evaluation, curation = self.fixture()
        curation['legends'][0]['playerPersonId'] = 'unknown'
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)
        curation['legends'][0]['playerPersonId'] = 'target'
        curation['legends'][0]['sourceReferenceName'] = '동명이인 확인 오류'
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)

    def test_low_peak_and_missing_material_fail_without_fallback(self):
        evaluation, curation = self.fixture()
        evaluation['careerHigh'][0]['cost'] = 8
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)
        evaluation['careerHigh'][0]['cost'] = 9
        evaluation['seasons'] = [row for row in evaluation['seasons'] if row['cost'] != 8]
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)

    def test_target_person_cannot_fill_its_own_recipe(self):
        evaluation, curation = self.fixture()
        for row in evaluation['seasons']:
            if row['playerPersonId'] == 'person-0':
                row['playerPersonId'] = 'target'
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)

    def test_eligible_peak_does_not_hide_identity_or_role_errors(self):
        evaluation, curation = self.fixture()
        evaluation['careerHigh'][0]['sourceNames'] = ['다른 이름']
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)
        evaluation['careerHigh'][0]['sourceNames'] = ['검수 이름']
        evaluation['careerHigh'][0]['role'] = 'Pitcher'
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)

    def test_research_tag_requires_actual_evidence(self):
        evaluation, curation = self.fixture()
        curation['legends'][0]['curatedReasonTags'] = ['ResearchLegend']
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)

    def test_explicit_representative_season_keeps_cost_and_identity_gates(self):
        evaluation, curation = self.fixture()
        selected = dict(evaluation['careerHigh'][0], playerSeasonId='representative', cost=10,
                        year=2000, qualified=True)
        evaluation['seasons'].append(selected)
        evaluation['careerHigh'][0]['cost'] = 7
        curation['legends'][0]['basePlayerSeasonId'] = 'representative'
        entries, report = compile_curation(evaluation, curation)
        self.assertEqual('representative', entries[0]['basePlayerSeasonId'])
        self.assertEqual(2000, report[0]['peakYear'])
        selected['cost'] = 8
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)
        selected['cost'] = 10
        selected['playerPersonId'] = 'different-person'
        with self.assertRaises(BakeValidationError):
            compile_curation(evaluation, curation)

    def test_price_fallback_returns_to_peak_only_when_eligible(self):
        evaluation, curation = self.fixture()
        fallback = dict(evaluation['careerHigh'][0], playerSeasonId='fallback', year=2000, qualified=True)
        evaluation['seasons'].append(fallback)
        entry = curation['legends'][0]
        entry['basePlayerSeasonId'] = 'fallback'
        entry['curatedReasonTags'].append('PeakCostEligibleSeason')
        evaluation['careerHigh'][0]['cost'] = 8
        self.assertEqual('fallback',compile_curation(evaluation,curation)[0][0]['basePlayerSeasonId'])
        evaluation['careerHigh'][0]['cost'] = 10
        rows, report = compile_curation(evaluation,curation)
        self.assertEqual('peak',rows[0]['basePlayerSeasonId'])
        self.assertNotIn('PeakCostEligibleSeason',report[0]['reasonTags'])
        entry['curatedReasonTags'].remove('PeakCostEligibleSeason')
        self.assertEqual('fallback',compile_curation(evaluation,curation)[0][0]['basePlayerSeasonId'])


if __name__ == '__main__':
    unittest.main()
