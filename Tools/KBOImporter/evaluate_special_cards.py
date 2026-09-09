"""Research 관측과 현재 정본을 대조해 특수카드 Bake 검토 입력만 출력한다."""
import argparse
import collections
import csv
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
LINEAGES = {
    'KiaLineage': ['HT', 'KIA', '해태'], 'LgLineage': ['MBC', 'LG'],
    'SsgLineage': ['SB', 'SK', 'SSG', '쌍방울'], 'DoosanLineage': ['OB', 'DOOSAN', '두산'],
    'LotteLineage': ['LOTTE', '롯데'], 'SamsungLineage': ['SAMSUNG', '삼성'],
    'HanwhaLineage': ['BINGGRAE', 'HANWHA', '빙그레', '한화'],
    'KiwoomLineage': ['SAMMI', 'CHUNGBO', 'TAEPYEONGYANG', 'HYUNDAI', 'HEROES', 'NEXEN', 'KIWOOM',
                      '삼미', '청보', '태평양', '현대', '우리', '히어로즈', '넥센', '키움'],
    'NcLineage': ['NC'], 'KtLineage': ['KT'],
}
TEAM_ALIASES = {v: k for k, values in LINEAGES.items() for v in values}
ABILITIES = ['Contact', 'Power', 'Speed', 'Arm', 'Defense', 'BatterMental',
             'Stamina', 'Velocity', 'Stuff', 'Breaking', 'Control', 'PitcherMental']


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True) + '\n', encoding='utf-8')


def write_csv(path, rows):
    if not rows:
        path.write_text('status\nno_rows\n', encoding='utf-8-sig')
        return
    keys = list(dict.fromkeys(k for row in rows for k in row))
    with path.open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=keys)
        writer.writeheader()
        for row in rows:
            writer.writerow({k: json.dumps(v, ensure_ascii=False) if isinstance(v, (list, dict)) else v
                             for k, v in row.items()})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'Research/PyaMaeCardDb/SpecialCardEvaluation')
    parser.add_argument('--editor-root', type=Path, default=ROOT / 'Assets/Editor Default Resources/HistoricalSimulation/1982-2025')
    parser.add_argument('--runtime-root', type=Path, default=ROOT / 'Assets/10.Datas/HistoricalSimulation/1982-2025')
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(ROOT / 'Research'):
        raise ValueError('평가 산출물은 Research 아래에만 작성합니다.')
    output.mkdir(parents=True, exist_ok=True)
    editor_root, runtime_root = args.editor_root.resolve(), args.runtime_root.resolve()
    if not editor_root.is_relative_to(ROOT) or not runtime_root.is_relative_to(ROOT):
        raise ValueError('평가 원본은 저장소 안의 Archive여야 합니다.')
    policy_path = Path(__file__).with_name('special_card_evaluation_policy.json')
    policy = read(policy_path)
    inputs = []
    for path in (policy_path, Path(__file__).resolve()):
        inputs.append({'path': path.relative_to(ROOT).as_posix(),
                       'sha256': hashlib.sha256(path.read_bytes()).hexdigest()})

    def tracked(path):
        raw = path.read_bytes()
        inputs.append({'path': path.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(raw).hexdigest()})
        return json.loads(raw.decode('utf-8-sig'))

    runtime_manifest = tracked(runtime_root / 'manifest.json')
    tracked(editor_root / 'manifest.json')
    observations, coverage = [], []
    # Editor와 Runtime은 서로 다른 ID 공간이다. 기존 1:1 정본 대조표의 ID 연결만 재사용한다.
    identity_path = ROOT / 'Tools/PMReference/reports/COST_PLAYER_BASELINE.csv'
    inputs.append({'path': identity_path.relative_to(ROOT).as_posix(),
                   'sha256': hashlib.sha256(identity_path.read_bytes()).hexdigest()})
    with identity_path.open(encoding='utf-8-sig') as stream:
        editor_to_runtime = {r['EditorPlayerSeasonId']: r['PlayerSeasonId'] for r in csv.DictReader(stream)
                             if r['EditorPlayerSeasonId'] and r['PlayerSeasonId']}
    for relative in ['card-index.csv', '1989-1999/archive-cards.csv', '2010-2016/archive-cards.csv']:
        path = ROOT / 'Research/PyaMaeCardDb' / relative
        raw = path.read_bytes()
        inputs.append({'path': path.relative_to(ROOT).as_posix(), 'sha256': hashlib.sha256(raw).hexdigest()})
        with path.open(encoding='utf-8-sig') as stream:
            rows = list(csv.DictReader(stream))
        coverage.append({'source': relative, 'rows': len(rows), 'editions': dict(collections.Counter(r['CardType'] for r in rows))})
        for row in rows:
            if row['CardType'] not in ('레어', 'EX', '레전드', '커리어하이', '커리어 하이'):
                continue
            observations.append({**row, 'researchSource': relative})

    seasons, identities, missing = {}, collections.defaultdict(set), []
    for year in range(1982, 2026):
        editor = tracked(editor_root / f'Years/{year}.json')
        runtime = tracked(runtime_root / f'Years/{year}.json')
        runtime_seasons = {s['playerSeasonId']: s for s in runtime['playerSeasons']}
        normal_cards = {c['playerSeasonId']: c['cardId'] for c in runtime['normalCards']
                        if c['edition'] == 'Normal'}
        for source in editor['playerSeasons']:
            editor_id = source['playerSeasonId']
            sid = editor_to_runtime.get(editor_id, editor_id)
            current = runtime_seasons.get(sid)
            if not current:
                missing.append({'editorPlayerSeasonId': editor_id, 'missingRuntimeIdentity': True})
                continue
            if current.get('dataProvenance') != 'SourceBacked':
                continue
            franchise = source['originFranchiseId']
            lineage = TEAM_ALIASES.get(franchise)
            if not lineage:
                missing.append({'playerSeasonId': sid, 'unmappedFranchise': franchise})
                continue
            trace = source.get('costDerivationTrace', {})
            components = trace.get('componentScores', {})
            weights = trace.get('roleWeights', [])
            if not weights or 'continuousValue' not in trace:
                missing.append({'playerSeasonId': sid, 'missingTrace': True})
                continue
            attributes = current['baseAttributes']
            denominator = sum(w['weight'] for w in weights)
            strength = sum(attributes[ABILITIES.index(w['ability'])] * w['weight'] for w in weights) / denominator
            names = source.get('sourceReferenceNames', [])
            row = dict(playerSeasonId=sid, editorPlayerSeasonId=editor_id, playerPersonId=current['playerPersonId'],
                       year=year, sourceFranchise=franchise, lineage=lineage, normalCardId=normal_cards.get(sid),
                       originFranchiseId=current['originFranchiseId'],
                       originTeamSeasonKey=current['originTeamSeasonKey'],
                       sourceNames=names, role=current['playerType'], position=current['position'],
                       cost=current['cost'], baseAttributes=attributes, issuedStrength=round(strength, 8),
                       sourcePerformance=trace['continuousValue'],
                       reliability=components.get('reliability', 0),
                       workloadRatio=components.get('workload', {}).get('ratio', 0),
                       sample=components.get('workload', {}).get('sample', 0),
                       editorRuntimeAttributesAgree=source['baseAttributes'] == attributes,
                       editorRuntimeCostAgree=source['cost'] == current['cost'], evidence=[])
            row['qualified'] = row['reliability'] >= policy['minimumReliability'] and row['workloadRatio'] >= policy['minimumWorkloadRatio']
            seasons[sid] = row
            for name in names:
                identities[(year, lineage, name.strip())].add(sid)
        print(f'{year}: 평가 입력 읽기 완료', flush=True)

    matches = []
    for obs in observations:
        year = int(obs['SeasonYear'])
        lineage = TEAM_ALIASES.get(obs['Team'].upper())
        ids = sorted(identities.get((year, lineage, obs['Name'].strip()), []))
        entry = dict(researchSource=obs['researchSource'], researchCardId=obs.get('CardId') or obs.get('SourceTable', '') + ':' + obs.get('SourceId', ''),
                     edition=obs['CardType'], name=obs['Name'], year=year, team=obs['Team'],
                     referenceCost=obs['Cost'], candidateSeasonIds=ids,
                     matchStatus='Matched' if len(ids) == 1 else 'Ambiguous' if ids else 'Unmatched',
                     sourceUrl=obs.get('SourceUrl', ''), snapshot=obs.get('SnapshotTimestamp', ''))
        if len(ids) == 1:
            season = seasons[ids[0]]
            entry.update(currentCost=season['cost'], issuedStrength=season['issuedStrength'])
            season['evidence'].append(entry)
        matches.append(entry)

    def peak_key(row):
        return (-row['issuedStrength'], -row['reliability'], -row['sample'], row['year'], row['playerSeasonId'])

    ex = []
    for year in range(1982, 2026):
        for role in ('Hitter', 'Pitcher'):
            candidates = sorted((s for s in seasons.values() if s['year'] == year and s['role'] == role and s['qualified']),
                                key=lambda s: (-s['sourcePerformance'], -s['reliability'], -s['sample'], s['playerSeasonId']))
            if not candidates:
                ex.append({'year': year, 'role': role, 'status': 'MissingQualifiedSource'})
                continue
            winner = candidates[0]
            ex.append({**winner, 'status': 'Eligible' if winner['cost'] == 10 else 'BlockedCost',
                       'runnerUpSeasonId': candidates[1]['playerSeasonId'] if len(candidates) > 1 else None,
                       'runnerUpPerformance': candidates[1]['sourcePerformance'] if len(candidates) > 1 else None,
                       'observedEx': any(e['edition'] == 'EX' for e in winner['evidence'])})

    rare, by_team = [], collections.defaultdict(list)
    for row in seasons.values():
        if row['cost'] in (4, 5) and row['qualified']:
            by_team[row['originTeamSeasonKey']].append(row)
    for key in sorted(by_team):
        candidates = sorted(by_team[key], key=lambda s: (not any(e['edition'] == '레어' for e in s['evidence']), *peak_key(s)))
        for index, row in enumerate(candidates):
            rare.append({**row, 'recommended': index < policy['rareTargetPerTeamSeason'],
                         'basis': 'ResearchRare' if any(e['edition'] == '레어' for e in row['evidence']) else 'CurrentDataFallback',
                         'status': 'PendingCuration'})

    groups = collections.defaultdict(list)
    for row in seasons.values():
        groups[(row['playerPersonId'], row['lineage'])].append(row)
    career, legend_pool = [], collections.defaultdict(list)
    for key in sorted(groups):
        group = groups[key]
        peak = sorted(group, key=peak_key)[0]
        qualified = sorted((s for s in group if s['qualified']), key=lambda s: (s['year'], s['playerSeasonId']))
        years = sorted({s['year'] for s in qualified})
        base = {**peak, 'qualifiedYears': years, 'qualifiedDistinctYears': len(years),
                'qualifiedNormalCardIds': [s['normalCardId'] for s in qualified if s['normalCardId']],
                'lineageSeasonCount': len({s['year'] for s in group}), 'requiredDistinctYears': 8,
                'cumulativeSourcePerformance': round(sum(s['sourcePerformance'] for s in qualified), 8)}
        career.append({**base, 'status': 'InsufficientSeasons' if len(years) < 8 else
                       'Eligible' if peak['cost'] in (9, 10) else 'BlockedPeakCost'})
        base['legendResearchEvidence'] = [e for s in group for e in s['evidence'] if e['edition'] == '레전드']
        if peak['cost'] in (9, 10) or base['legendResearchEvidence']:
            legend_pool[(peak['lineage'], peak['role'])].append(base)
    legends = []
    for key in sorted(legend_pool):
        ordered = sorted(legend_pool[key], key=lambda s: (not bool(s['legendResearchEvidence']), -s['qualifiedDistinctYears'], -s['cumulativeSourcePerformance'], *peak_key(s)))
        observed = [s for s in ordered if s['legendResearchEvidence']]
        fallback = [s for s in ordered if not s['legendResearchEvidence']]
        selected = observed or fallback[:policy['legendShortlistPerLineageRole']]
        for rank, row in enumerate(selected, 1):
            legends.append({**row, 'shortlistRank': rank,
                            'status': 'PendingHistoricalCuration' if row['cost'] in (9, 10) else 'BlockedPeakCost',
                            'enabled': False, 'basis': 'ResearchLegend' if row['legendResearchEvidence'] else 'CurrentDataTenureAndPerformance',
                            'curatedReasonTags': []})

    report = {'evaluationVersion': 1, 'policy': policy, 'productionBakeExecuted': False,
              'sourceNamesAreEditorOnly': True, 'coverage': coverage, 'inputFiles': sorted(inputs, key=lambda r: r['path']),
              'unmappedOrMissingTrace': missing, 'researchMatches': matches, 'ex': ex, 'rare': rare,
              'careerHigh': career, 'legendShortlist': legends,
              'seasons': [seasons[key] for key in sorted(seasons)]}
    report['runtimeArchiveRoot'] = runtime_root.relative_to(ROOT).as_posix()
    report['baseContentHash'] = runtime_manifest['sourceManifest']['contentHash']
    report['inputHash'] = hashlib.sha256(json.dumps(report['inputFiles'], sort_keys=True).encode()).hexdigest()
    write_json(output / 'evaluation.json', report)
    for name, rows in [('Research_Matches', matches), ('EX_By_Year', ex), ('Rare_Candidates', rare),
                       ('CareerHigh_By_Lineage', career), ('Legend_Shortlist', legends)]:
        write_csv(output / (name + '.csv'), rows)
    summary = {'sourceSeasons': len(seasons), 'researchObservations': len(matches),
               'researchMatchStatus': dict(collections.Counter(r['matchStatus'] for r in matches)),
               'ex': dict(collections.Counter(r['status'] for r in ex)),
               'rareQualified': len(rare), 'rareRecommended': sum(r['recommended'] for r in rare),
               'careerHigh': dict(collections.Counter(r['status'] for r in career)),
               'legendShortlist': len(legends), 'missingInputs': len(missing),
               'editorRuntimeAttributeMismatch': sum(not r['editorRuntimeAttributesAgree'] for r in seasons.values()),
               'editorRuntimeCostMismatch': sum(not r['editorRuntimeCostAgree'] for r in seasons.values())}
    write_json(output / 'summary.json', summary)
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
