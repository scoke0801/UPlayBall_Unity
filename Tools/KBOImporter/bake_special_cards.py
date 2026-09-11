"""해시로 고정한 특수 카드 평가에서 Definition·Recipe를 발급한다. Gate 실패 시 발급 파일을 쓰지 않는다."""
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


class BakeValidationError(ValueError):
    """일부 카드만 조용히 발급하지 않고 전체 발급을 차단하는 검증 오류다."""

    def __init__(self, errors):
        self.errors = errors
        super().__init__('\n'.join(errors))


def verify_inputs(evaluation, root=ROOT):
    errors = []
    if not evaluation.get('inputFiles'):
        errors.append('MissingInputManifest')
    for entry in evaluation.get('inputFiles', []):
        path = (root / entry['path']).resolve()
        if not path.is_relative_to(root.resolve()):
            errors.append('InputOutsideRepository:' + entry['path'])
        elif not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != entry['sha256']:
            errors.append('StaleInput:' + entry['path'])
    if errors:
        raise BakeValidationError(errors)


def build_catalog(evaluation, policy, editions=('Ex', 'CareerHigh', 'Legend', 'Rare')):
    """평가 순위를 바꾸거나 Cost를 승격하지 않고 통과한 사전 발급 내용만 만든다."""
    errors, cards, recipes, cancelled_ex = [], [], [], []
    if not editions or len(set(editions)) != len(editions) or set(editions) - {'Ex', 'CareerHigh', 'Legend', 'Rare'}:
        raise BakeValidationError(['InvalidEditionScope'])
    if policy.get('version') is None:
        errors.append('MissingPolicyVersion')
    if policy.get('evaluationPolicyVersion') != evaluation.get('policy', {}).get('version'):
        errors.append('EvaluationPolicyMismatch')
    if evaluation.get('unmappedOrMissingTrace'):
        errors.append('MissingCanonicalTrace')
    supported_years = policy.get('supportedYears', [])
    if not supported_years or len(set(supported_years)) != len(supported_years):
        errors.append('InvalidSupportedYears')

    def emit(row, edition, modifier):
        wildcard = edition in ('CareerHigh', 'Legend')
        sid = row['playerSeasonId']
        lineage = row['lineage']
        card_id = f'{sid}:{edition}' + (f':{lineage}' if wildcard else '')
        # 이름·Source ID·Research 출처는 이 허용 목록에 포함하지 않는다.
        cards.append(dict(cardId=card_id, playerSeasonId=sid, edition=edition,
                          teamColorLineageId=lineage, editionStatModifiers=[modifier] * 12))
        return card_id

    rare_by_id = {row['playerSeasonId']: row for row in evaluation.get('rare', [])}
    if policy.get('rarePlayerSeasonIds') and policy.get('rareSelectionInputHash') != evaluation.get('inputHash'):
        errors.append('StaleRareCuration')
    for sid in policy.get('rarePlayerSeasonIds', []) if 'Rare' in editions else []:
        row = rare_by_id.get(sid)
        if row is None or row['cost'] not in (4, 5) or not row.get('qualified'):
            errors.append('RareEligibilityGate:' + sid)
        else:
            emit(row, 'Rare', 0)

    ex_by_key = {}
    for row in evaluation.get('ex', []):
        key = (row['year'], row['role'])
        if key in ex_by_key:
            errors.append(f'DuplicateEx:{key}')
        ex_by_key[key] = row
    for year in sorted(supported_years) if 'Ex' in editions else []:
        for role in ('Hitter', 'Pitcher'):
            row = ex_by_key.get((year, role))
            if row is None:
                errors.append(f'MissingEx:{year}:{role}')
            elif row.get('cost') in range(1, 10) and row.get('status') == 'BlockedCost':
                # 성과 1위·원본 10코스트 규약은 유지한다. 미달 연도는 발급을 취소하며
                # 차순위 승격이나 전체 발급 실패로 다른 정상 카드를 막지 않는다.
                cancelled_ex.append(dict(year=year, role=role,
                    playerSeasonId=row['playerSeasonId'], cost=row['cost'], reason='SourceCostBelowTen'))
            elif row.get('cost') != 10 or row.get('status') != 'Eligible':
                errors.append(f'ExCostGate:{year}:{role}:{row.get("cost")}:{row.get("playerSeasonId")}')
            else:
                emit(row, 'Ex', 0)

    for row in evaluation.get('careerHigh', []) if 'CareerHigh' in editions else []:
        if row.get('status') != 'Eligible':
            continue
        candidates = row.get('qualifiedNormalCardIds', [])
        years = row.get('qualifiedYears', [])
        if row['cost'] not in (9, 10) or len(set(years)) < 8 or len(set(candidates)) < 8:
            errors.append('CareerHighGate:' + row['playerSeasonId'])
            continue
        card_id = emit(row, 'CareerHigh', policy['careerHighAllBonus'])
        recipes.append(dict(targetCardId=card_id, materialGroups=[
            dict(groupId=f'year-slot-{index + 1}', candidateCardIds=sorted(set(candidates))) for index in range(8)]))

    # 과거 Research 관측 목록이 전체 역사를 대표하지 않으므로 명시적 큐레이션은 전체 Peak 목록을 조회한다.
    shortlist = {(row['playerPersonId'], row['lineage']): row for row in evaluation.get('legendShortlist', [])}
    shortlist.update({(row['playerPersonId'], row['lineage']): row for row in evaluation.get('careerHigh', [])})
    if policy.get('legendMaterialInputHash') is not None and policy['legendMaterialInputHash'] != evaluation.get('inputHash'):
        errors.append('StaleLegendMaterialPolicy')
    enabled_legends = [entry for entry in policy.get('legends', []) if entry.get('enabled')] if 'Legend' in editions else []
    if 'Legend' in editions and not enabled_legends:
        errors.append('MissingLegendCuration')
    seen = set()
    for entry in enabled_legends:
        key = (entry['playerPersonId'], entry['lineage'])
        row = shortlist.get(key)
        if entry.get('basePlayerSeasonId'):
            row = next((candidate for candidate in evaluation.get('seasons', [])
                if candidate['playerSeasonId'] == entry['basePlayerSeasonId']), None)
            if row is not None and ((row['playerPersonId'], row['lineage']) != key or not row.get('qualified')):
                row = None
        groups = entry.get('materialGroups', [])
        if key in seen or row is None or not entry.get('curatedReasonTags'):
            errors.append(f'InvalidLegendCuration:{key}')
            continue
        seen.add(key)
        if row['cost'] not in (9, 10):
            errors.append(f'LegendPeakCostGate:{key}')
            continue
        if len(groups) != 8 or any(not group.get('candidateCardIds') or not group.get('groupId') for group in groups) or len({group.get('groupId') for group in groups}) != 8:
            errors.append(f'LegendMaterialGroups:{key}')
            continue
        card_id = emit(row, 'Legend', policy['legendAllBonus'])
        recipes.append(dict(targetCardId=card_id, materialGroups=groups,
                            allowTargetPersonMaterials=entry.get('allowTargetPersonMaterials', True),
                            requireDistinctMaterialPersons=entry.get('requireDistinctMaterialPersons', False)))
    if errors:
        raise BakeValidationError(errors)
    cards.sort(key=lambda card: card['cardId'])
    recipes.sort(key=lambda recipe: recipe['targetCardId'])
    if len({card['cardId'] for card in cards}) != len(cards):
        raise BakeValidationError(['DuplicateCardId'])
    result = dict(schemaVersion=1, policyVersion=policy['version'], inputHash=evaluation.get('inputHash'),
                  editionScope=sorted(editions),
                  cancelledEx=cancelled_ex,
                  cards=cards, recipes=recipes,
                  policyHash=hashlib.sha256(json.dumps(policy, sort_keys=True, separators=(',', ':')).encode()).hexdigest())
    result['contentHash'] = hashlib.sha256(json.dumps(result, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    return result


def validate_canonical(content, evaluation, root=ROOT):
    """실제 정본 카드 존재·Person·계보·서로 다른 연도를 확인한 뒤 Runtime 계보 표를 부착한다."""
    seasons, normals, lineages, errors = {}, {}, {}, []
    for category in ('ex', 'rare', 'careerHigh', 'legendShortlist'):
        for row in evaluation.get(category, []):
            franchise = row.get('originFranchiseId')
            lineage = row.get('lineage')
            if franchise and lineage:
                if franchise in lineages and lineages[franchise] != lineage:
                    errors.append('ConflictingLineage:' + franchise)
                lineages[franchise] = lineage
    runtime_root = evaluation.get('runtimeArchiveRoot', 'Assets/10.Datas/HistoricalSimulation/1982-2025')
    archive_path = (root / runtime_root).resolve()
    if not archive_path.is_relative_to(root.resolve()):
        raise BakeValidationError(['RuntimeArchiveOutsideRepository'])
    for entry in evaluation['inputFiles']:
        if not entry['path'].startswith(runtime_root.rstrip('/') + '/Years/'):
            continue
        year = json.loads((root / entry['path']).read_text(encoding='utf-8-sig'))
        for season in year['playerSeasons']:
            seasons[season['playerSeasonId']] = season
        for card in year['normalCards']:
            if card['edition'] == 'Normal':
                normals[card['cardId']] = card['playerSeasonId']
    targets = {card['cardId']: card for card in content['cards']}
    for card in content['cards']:
        source = seasons.get(card['playerSeasonId'])
        if source is None or f'{card["playerSeasonId"]}:Normal' not in normals:
            errors.append('MissingCanonicalBase:' + card['cardId'])
        elif lineages.get(source['originFranchiseId']) != card['teamColorLineageId']:
            errors.append('BaseLineageMismatch:' + card['cardId'])
        elif (card['edition'] == 'Rare' and source['cost'] not in (4, 5)) or (card['edition'] == 'Ex' and source['cost'] != 10) or (
                card['edition'] in ('Legend', 'CareerHigh') and source['cost'] not in (9, 10)):
            errors.append('CanonicalCostMismatch:' + card['cardId'])
    for recipe in content['recipes']:
        target = targets[recipe['targetCardId']]
        peak = seasons.get(target['playerSeasonId'])
        if peak is None:
            continue
        years = set()
        used_people = set()
        for group in recipe['materialGroups']:
            group_people = set()
            for card_id in group['candidateCardIds']:
                season = seasons.get(normals.get(card_id))
                if season is None:
                    errors.append('MissingNormalMaterial:' + card_id)
                    continue
                if lineages.get(season['originFranchiseId']) != target['teamColorLineageId']:
                    errors.append('MaterialLineageMismatch:' + card_id)
                if target['edition'] == 'CareerHigh' and season['playerPersonId'] != peak['playerPersonId']:
                    errors.append('MaterialPersonMismatch:' + card_id)
                if target['edition'] == 'Legend' and not recipe.get('allowTargetPersonMaterials', True) and season['playerPersonId'] == peak['playerPersonId']:
                    errors.append('TargetPersonMaterialForbidden:' + card_id)
                group_people.add(season['playerPersonId'])
                years.add(season['originYear'])
            if recipe.get('requireDistinctMaterialPersons', False) and group_people & used_people:
                errors.append('RepeatedLegendMaterialPerson:' + target['cardId'])
            used_people.update(group_people)
        if target['edition'] == 'CareerHigh' and len(years) < 8:
            errors.append('CanonicalDistinctYears:' + target['cardId'])
    if errors:
        raise BakeValidationError(errors)
    # 실제 구단 이름을 담는 평가용 계보 키도 Runtime에서는 불투명한 안정 ID로 치환한다.
    opaque = {lineage: 'LINEAGE_' + hashlib.sha256(lineage.encode()).hexdigest()[:20]
              for lineage in set(lineages.values())}
    id_map = {}
    for card in content['cards']:
        old_id = card['cardId']
        old_lineage = card['teamColorLineageId']
        card['teamColorLineageId'] = opaque[old_lineage]
        if card['edition'] in ('CareerHigh', 'Legend'):
            card['cardId'] = f'{card["playerSeasonId"]}:{card["edition"]}:{opaque[old_lineage]}'
        id_map[old_id] = card['cardId']
    for recipe in content['recipes']:
        recipe['targetCardId'] = id_map[recipe['targetCardId']]
    content['lineages'] = [dict(franchiseId=franchise, lineageId=opaque[lineage])
                           for franchise, lineage in sorted(lineages.items())]
    manifest_path = archive_path / 'manifest.json'
    if manifest_path.exists():
        content['baseContentHash'] = json.loads(manifest_path.read_text(encoding='utf-8-sig'))['sourceManifest']['contentHash']
    content.pop('contentHash', None)
    content['contentHash'] = hashlib.sha256(json.dumps(content, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    return content


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evaluation', type=Path, required=True)
    parser.add_argument('--policy', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--editions', nargs='+', choices=('Ex', 'CareerHigh', 'Legend', 'Rare'),
                        default=['Ex', 'CareerHigh', 'Legend', 'Rare'], help='기본은 전체 발급. 부분 검증은 산출물 editionScope에 명시한다.')
    args = parser.parse_args()
    evaluation = json.loads(args.evaluation.read_text(encoding='utf-8-sig'))
    policy = json.loads(args.policy.read_text(encoding='utf-8-sig'))
    try:
        verify_inputs(evaluation)
        content = build_catalog(evaluation, policy, args.editions)
        validate_canonical(content, evaluation)
    except BakeValidationError as error:
        print(json.dumps(dict(status='Blocked', errors=error.errors), ensure_ascii=False, indent=2))
        return 1
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(content, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(f'발급 완료: {len(content["cards"])} cards, {len(content["recipes"])} recipes')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
