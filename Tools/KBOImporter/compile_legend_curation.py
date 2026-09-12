"""명시적 레전드 선정과 데이터로 저작한 재료 규칙을 재현 가능한 Bake 정책으로 확정한다."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

from bake_special_cards import BakeValidationError, verify_inputs


def compile_curation(evaluation, curation):
    """ID로 선정한 인물의 실제 Peak와 동일 계보의 서로 다른 재료 인물 8명을 검증한다."""
    peaks = {(row['playerPersonId'], row['lineage']): row for row in evaluation['careerHigh']}
    seasons = evaluation.get('seasons', [])
    if not seasons:
        raise BakeValidationError(['MissingEvaluationSeasons'])
    profiles = curation['materialGroups']
    if len(profiles) != 8 or len({group['groupId'] for group in profiles}) != 8:
        raise BakeValidationError(['InvalidLegendGroupProfiles'])
    maximum = curation['maximumCandidatesPerGroup']
    if not isinstance(maximum, int) or maximum < 1:
        raise BakeValidationError(['InvalidMaximumCandidatesPerGroup'])
    tenure = collections.defaultdict(set)
    for row in seasons:
        if row['qualified']:
            tenure[(row['playerPersonId'], row['lineage'])].add(row['year'])
    results, report, seen = [], [], set()
    for entry in sorted(curation['legends'], key=lambda value: (value['lineage'], value['playerPersonId'])):
        key = (entry['playerPersonId'], entry['lineage'])
        if key in seen or key not in peaks or not entry.get('curatedReasonTags'):
            raise BakeValidationError([f'InvalidCuratedPerson:{key}'])
        seen.add(key)
        peak = peaks[key]
        selected_id = entry.get('basePlayerSeasonId')
        reason_tags = list(entry['curatedReasonTags'])
        # 가격 미달 때문에 고른 대체 시즌은 실제 Peak가 자격을 회복하면 필요하지 않다.
        # 별도로 저작한 대표 시즌(예: 재평가 확정 시즌)은 이 태그가 없으므로 유지한다.
        if 'PeakCostEligibleSeason' in reason_tags and peak['cost'] in (9, 10):
            selected_id = None
            reason_tags.remove('PeakCostEligibleSeason')
        if selected_id:
            selected = next((row for row in seasons if row['playerSeasonId'] == selected_id), None)
            if (selected is None or (selected['playerPersonId'], selected['lineage']) != key
                    or not selected['qualified']):
                raise BakeValidationError([f'InvalidCuratedBase:{key}:{selected_id}'])
            peak = dict(selected, qualifiedDistinctYears=peak['qualifiedDistinctYears'])
        if peak['cost'] not in (9, 10) or peak['role'] != entry['role']:
            raise BakeValidationError([f'CuratedPeakGate:{key}:{peak["cost"]}'])
        if entry['sourceReferenceName'] not in peak['sourceNames']:
            raise BakeValidationError([f'CuratedIdentityMismatch:{key}'])
        if 'ResearchLegend' in entry['curatedReasonTags']:
            evidence = [e for row in seasons if (row['playerPersonId'], row['lineage']) == key
                        for e in row.get('evidence', []) if e['edition'] == '레전드']
            if not evidence:
                raise BakeValidationError([f'MissingCuratedResearchEvidence:{key}'])
        used_people, used_decades, used_positions = set(), set(), set()
        if not curation['allowTargetPersonMaterials']:
            used_people.add(entry['playerPersonId'])
        groups, group_report = [], []
        for profile in profiles:
            candidates = [row for row in seasons if row['lineage'] == entry['lineage'] and row['qualified']
                          and row['role'] == profile['role'] and row['cost'] == profile['cost']
                          and row['playerPersonId'] not in used_people and row.get('normalCardId')]
            # 인물 선택을 자동 큐레이션으로 대신하지 않는다. 이 순서는 이미 선정한 Legend의 재료 저작에만 사용한다.
            candidates.sort(key=lambda row: (row['year'] // 10 in used_decades,
                            row['position'] in used_positions, -len(tenure[(row['playerPersonId'], row['lineage'])]),
                            -row['issuedStrength'], row['year'], row['playerSeasonId']))
            if not candidates:
                raise BakeValidationError([f'MissingLegendMaterial:{key}:{profile["groupId"]}'])
            representative = candidates[0]
            person = representative['playerPersonId']
            selected = [row for row in candidates if row['playerPersonId'] == person][:maximum]
            used_people.add(person)
            used_decades.add(representative['year'] // 10)
            used_positions.add(representative['position'])
            groups.append(dict(groupId=profile['groupId'], candidateCardIds=sorted(row['normalCardId'] for row in selected)))
            group_report.append(dict(groupId=profile['groupId'], playerPersonId=person,
                                     sourceNames=representative['sourceNames'], role=profile['role'], cost=profile['cost'],
                                     years=sorted(row['year'] for row in selected)))
        results.append(dict(playerPersonId=key[0], lineage=key[1], enabled=True,
                            basePlayerSeasonId=peak['playerSeasonId'],
                            curatedReasonTags=reason_tags,
                            allowTargetPersonMaterials=curation['allowTargetPersonMaterials'],
                            requireDistinctMaterialPersons=True, materialGroups=groups))
        report.append(dict(lineage=key[1], playerPersonId=key[0], sourceNames=peak['sourceNames'],
                           peakYear=peak['year'], peakCost=peak['cost'], peakSeasonId=peak['playerSeasonId'],
                           qualifiedYears=peak['qualifiedDistinctYears'], role=peak['role'],
                           reasonTags=reason_tags, groups=group_report))
    return results, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evaluation', type=Path, required=True)
    parser.add_argument('--curation', type=Path, required=True)
    parser.add_argument('--policy', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    evaluation = json.loads(args.evaluation.read_text(encoding='utf-8-sig'))
    curation = json.loads(args.curation.read_text(encoding='utf-8-sig'))
    policy = json.loads(args.policy.read_text(encoding='utf-8-sig'))
    verify_inputs(evaluation)
    legends, report = compile_curation(evaluation, curation)
    policy['legends'] = legends
    policy['legendCurationVersion'] = curation['version']
    policy['legendCurationSha256'] = hashlib.sha256(args.curation.read_bytes()).hexdigest()
    policy['legendCompilerSha256'] = hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    policy['legendMaterialInputHash'] = evaluation['inputHash']
    policy['version'] = 'special-card-bake-v2'
    args.policy.write_text(json.dumps(policy, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'레전드 {len(legends)}명, 재료 {sum(len(row["materialGroups"]) for row in legends)}그룹 확정')


if __name__ == '__main__':
    main()
