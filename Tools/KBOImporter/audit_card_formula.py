"""전체 원본 시즌을 메모리에서 재계산하고 카드 관측과 판본별로 대조한다."""
import collections
import copy
import csv
import hashlib
import json
from pathlib import Path

import synthetic_bake as bake
import calibrate_annual_reference as reference
from evaluate_special_cards import TEAM_ALIASES

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / 'Research/PyaMaeCardDb/CardFormulaAudit'


def read(path):
    """한글 자료의 BOM 유무를 모두 허용한다."""
    return json.loads(path.read_text(encoding='utf-8-sig'))


def metrics(rows):
    """부호는 현재 값에서 관측 값을 뺀 방향이다."""
    errors = [r['actual'] - r['expected'] for r in rows]
    return dict(count=len(errors), mae=sum(map(abs, errors)) / len(errors),
                bias=sum(errors) / len(errors), exact=sum(e == 0 for e in errors),
                maxAbs=max(map(abs, errors))) if errors else dict(count=0)


def main():
    """정본을 변경하지 않고 전체 재계산·참조 오차·특수 카드 충돌을 저장한다."""
    OUTPUT.mkdir(parents=True, exist_ok=True)
    paths = set(Path(__file__).parent.glob('*.py')) | set(Path(__file__).parent.glob('*.json'))
    paths |= set((ROOT / 'Tools/KBOImporter/.cache/KBOImport/Normalized').glob('*.json'))
    for base in ('Assets/Editor Default Resources/HistoricalSimulation/1982-2025',
                 'Assets/10.Datas/HistoricalSimulation/1982-2025'):
        paths |= set((ROOT / base).rglob('*.json'))
    research = ROOT / 'Research/PyaMaeCardDb/SpecialCardWebResearch'
    paths |= set(research.glob('*.json')) | set((research / 'evidence').glob('*'))
    paths.add(ROOT / 'Tools/PMReference/reports/COST_PLAYER_BASELINE.csv')
    for base in ('Research/PyaMaeCardDb', 'docs/reports/pm_threshold_review_20260906',
                 'docs/reports/pm_reference_review_20260906'):
        paths |= set((ROOT / base).glob('*.csv')) | set((ROOT / base).glob('*.json'))
    for base in ('1985-Samsung', '1989-1999', '2010-2016'):
        paths |= set((ROOT / 'Research/PyaMaeCardDb' / base).glob('*.csv'))
    hashes = {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
              for p in sorted(paths) if p.is_file()}
    with (ROOT / 'Tools/PMReference/reports/COST_PLAYER_BASELINE.csv').open(encoding='utf-8-sig') as stream:
        mapping = {r['EditorPlayerSeasonId']: r['PlayerSeasonId'] for r in csv.DictReader(stream)}
    editor, runtime, formula, differences, years = {}, {}, {}, [], []
    role_checks, neutral_counts = [], collections.Counter()
    counts = collections.Counter()
    for year in range(1982, 2026):
        current = read(ROOT / f'Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Years/{year}.json')
        public = read(ROOT / f'Assets/10.Datas/HistoricalSimulation/1982-2025/Years/{year}.json')
        rebuilt = bake.build_editor_original_content(ROOT / 'Tools/KBOImporter/.cache/KBOImport/Normalized', [year])
        regenerated = {s['playerSeasonId']: s for s in rebuilt['years'][0]['playerSeasons']}
        public_by_id = {s['playerSeasonId']: s for s in public['playerSeasons']}
        role_projection = copy.deepcopy(public['playerSeasons'])
        source_by_runtime = {mapping.get(s['playerSeasonId'], s['playerSeasonId']): s for s in current['playerSeasons']}
        role_groups = collections.defaultdict(list)
        for projected in role_projection:
            source = source_by_runtime.get(projected['playerSeasonId'])
            if source:
                projected['positionRoleDerivationTrace'] = copy.deepcopy(source['positionRoleDerivationTrace'])
                projected['pitcherRole'] = source['pitcherRole']
            role_groups[projected['originTeamSeasonKey']].append(projected)
        for group in role_groups.values():
            bake.limit_team_season_pitcher_roles(group)
        projected_by_id = {s['playerSeasonId']: s for s in role_projection}
        for r in public['playerSeasons']:
            counts['runtime:' + r.get('sourceDataKind', r['dataProvenance'])] += 1
            if not 1 <= r['cost'] <= 10 or len(r['baseAttributes']) != 12 or any(not 1 <= v <= 100 for v in r['baseAttributes']):
                differences.append(dict(id=r['playerSeasonId'], check='InvalidRuntimeValues'))
        counts.update({'sourceSeasons': len(current['playerSeasons']), 'runtimeSeasons': len(public_by_id),
                       'normalAndRareCards': len(public['normalCards'])})
        for s in current['playerSeasons']:
            sid = s['playerSeasonId']
            editor[sid] = s
            actual = regenerated.get(sid)
            if actual is None:
                differences.append(dict(id=sid, check='MissingRebuiltSeason'))
                continue
            for field in ('cost', 'baseAttributes', 'pitcherRole', 'position', 'costMetricEvidence',
                          'abilityDerivationTrace', 'costDerivationTrace'):
                if s.get(field) != actual.get(field):
                    differences.append(dict(id=sid, year=year, name=s['sourceReferenceNames'],
                                            check='RebuildMismatch', field=field))
            formula[sid] = dict(cost=actual.get('annualReferenceOverride', {}).get('formulaCost', actual['cost']),
                                attributes=actual.get('annualReferenceOverride', {}).get('formulaBaseAttributes', actual['baseAttributes']))
            rid = mapping.get(sid, sid)
            r = public_by_id.get(rid)
            if r is None:
                differences.append(dict(id=sid, check='MissingRuntime'))
                continue
            runtime[rid] = dict(source=s, public=r)
            if s['pitcherRole'] != r['pitcherRole']:
                projected = projected_by_id[rid]
                role_checks.append(dict(id=sid, runtimeId=rid, year=year, name=s['sourceReferenceNames'],
                    editorRole=s['pitcherRole'], runtimeRole=r['pitcherRole'], projectedRole=projected['pitcherRole'],
                    explainedByRuntimeIdRoleLimit=projected['pitcherRole'] == r['pitcherRole']))
            for field in ('cost', 'baseAttributes', 'playerType', 'position'):
                if s.get(field) != r.get(field):
                    differences.append(dict(id=sid, check='RuntimeMismatch', field=field))
            for t in s['abilityDerivationTrace']:
                counts['abilityFields'] += 1
                if t['evaluationMethod'] == 'NeutralWithoutEvidence':
                    neutral_counts[t['attribute']] += 1
                if t['attribute'] == 'Velocity' and not any(c['isAvailable'] for c in t['components']):
                    counts['velocityWithoutEvidence'] += 1
            counts['pitcherSeasons'] += s['playerType'] == 'Pitcher'
            counts['publishedVelocity55'] += s['playerType'] == 'Pitcher' and s['baseAttributes'][7] == 55
            counts['referenceOverrides'] += bool(s.get('annualReferenceOverride'))
        for card in public['normalCards']:
            s = public_by_id.get(card['playerSeasonId'])
            if s is None or len(card.get('editionStatModifiers', [])) != 12:
                differences.append(dict(id=card['cardId'], check='InvalidCardBaseOrModifiers'))
        years.append(dict(year=year, sourceCount=len(current['playerSeasons']), rebuiltCount=len(regenerated)))
        print(f'{year}: 원본 기록부터 {len(regenerated)}명 재계산 완료', flush=True)

    labels, rejected = reference.load_labels(editor, read(ROOT / 'Tools/KBOImporter/reference_calibration_policy.json'))
    comparisons = []
    for label in labels:
        s = editor[label['id']]
        target = label['target']
        value = s['cost'] if target == 'Cost' else s['baseAttributes'][bake.ABILITY_INDEX[target]]
        computed = formula[label['id']]['cost'] if target == 'Cost' else formula[label['id']]['attributes'][bake.ABILITY_INDEX[target]]
        comparisons.append(dict(**label, actual=value, formula=computed))
    grouped = collections.defaultdict(list)
    for r in comparisons:
        grouped[(r['kind'], r['target'])].append(r)
    reference_metrics = {':'.join(k): dict(published=metrics(v), formula=metrics([dict(r, actual=r['formula']) for r in v]))
                         for k, v in sorted(grouped.items())}

    observations = read(research / 'card-stats-observations.json')['observations']
    special_rows, unmatched = [], []
    index = collections.defaultdict(list)
    for rid, pair in runtime.items():
        s = pair['source']
        for name in s['sourceReferenceNames']:
            index[(s['originYear'], name, TEAM_ALIASES.get(s['originFranchiseId']), s['playerType'])].append(rid)
    observed_keys = set()
    for o in sorted(observations, key=lambda o: o.get('snapshot') or '', reverse=True):
        key = (o.get('playerSeasonId'), o['sourceCardKey'])
        if key in observed_keys:
            continue
        observed_keys.add(key)
        rid = o.get('playerSeasonId')
        if rid is None:
            candidates = index[(o['year'], o['name'], TEAM_ALIASES.get(o['team']), o['role'])]
            rid = candidates[0] if len(candidates) == 1 else None
        if rid not in runtime:
            unmatched.append(o)
            continue
        pair = runtime[rid]
        s, r = pair['source'], pair['public']
        # 원작 번트와 게임 Arm은 서로 다른 능력치다. 직접 비교에서 번트를 제외한다.
        names = reference.ARCHIVE_NAMES[o['role']]
        comparisons_by_stat = {target: dict(expected=o['stats'][name], actual=r['baseAttributes'][bake.ABILITY_INDEX[target]])
                               for name, target in names.items() if name in o['stats']}
        special_rows.append(dict(year=o['year'], name=o['name'], role=o['role'], edition=o['edition'],
                                 position=o['position'], currentRole=r['pitcherRole'],
                                 playerSeasonId=rid, currentCost=r['cost'], referenceCost=o['cost'],
                                 stats=comparisons_by_stat, sourceFile=o['sourceFile'],
                                 sourceCardKey=o['sourceCardKey'], sourceSnapshot=o['snapshot'],
                                 sourceStage=o['statsStage'], formula=formula[s['playerSeasonId']],
                                 costMethod=s['costDerivationTrace']['costMethod'],
                                 costPrediction=(s['costDerivationTrace'].get('referenceCalibration') or {}).get('prediction')))
    policy = read(ROOT / 'Tools/KBOImporter/special_card_evaluation_policy.json')
    candidates = collections.defaultdict(list)
    for rid, pair in runtime.items():
        s, r = pair['source'], pair['public']
        t = s['costDerivationTrace']
        c = t['componentScores']
        if c['reliability'] >= policy['minimumReliability'] and c['workload']['ratio'] >= policy['minimumWorkloadRatio']:
            candidates[(s['originYear'], r['playerType'])].append(dict(id=rid, name=s['sourceReferenceNames'],
                cost=r['cost'], performance=t['continuousValue'], reliability=c['reliability'], sample=c['workload']['sample'],
                method=t['costMethod']))
    ex = []
    for (year, role), rows in sorted(candidates.items()):
        rows.sort(key=lambda r: (-r['performance'], -r['reliability'], -r['sample'], r['id']))
        ex.append(dict(year=year, role=role, **rows[0], qualifiedCostTenCount=sum(r['cost'] == 10 for r in rows)))
    previous = read(ROOT / 'Research/PyaMaeCardDb/SpecialCardCurationEvaluation/evaluation.json')
    stale = [e['path'] for e in previous['inputFiles']
             if hashlib.sha256((ROOT / e['path']).read_bytes()).hexdigest() != e['sha256']]
    changed = [p for p, digest in hashes.items() if hashlib.sha256((ROOT / p).read_bytes()).hexdigest() != digest]
    summary = dict(counts, rebuiltYears=len(years), differences=len(differences),
                   referenceLabels=len(comparisons), specialVariants=len(special_rows), unmatchedSpecial=len(unmatched),
                   exWinners=len(ex), exBlocked=sum(r['cost'] != 10 for r in ex),
                   exBlockedMethods=dict(collections.Counter(r['method'] for r in ex if r['cost'] != 10)),
                   staleSpecialEvaluationFiles=len(stale), inputsChangedDuringAudit=changed)
    summary['editorRuntimeRoleDifferences'] = len(role_checks)
    summary['unexplainedRoleDifferences'] = sum(not r['explainedByRuntimeIdRoleLimit'] for r in role_checks)
    report = dict(summary=summary, years=years, differences=differences, referenceMetrics=reference_metrics,
                  neutralWithoutEvidence=dict(neutral_counts), roleChecks=role_checks,
                  referenceRejectedCounts=dict(collections.Counter(r['reason'] for r in rejected)),
                  specialVariants=special_rows, ex=ex, staleEvaluationFiles=stale, inputs=hashes,
                  balanceChanged=False, simulationExecuted=False)
    for name, payload in (('audit.json', report), ('normal-reference-comparisons.json', comparisons)):
        (OUTPUT / name).write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(summary, ensure_ascii=False, indent=2), flush=True)


if __name__ == '__main__':
    main()
