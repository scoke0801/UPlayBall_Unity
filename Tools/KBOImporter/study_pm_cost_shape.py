"""선수 단위 분할을 유지하며 성적·출전량 계수 후보의 Cost 오차를 비교한다."""
from __future__ import annotations

import argparse
import bisect
import csv
import itertools
import json
from collections import defaultdict
from pathlib import Path

from report_pm_calibration import load_seasons
from study_pm_calibration import ROOT, metrics, read
from study_pm_expanded import join_cards, split_player


def reference_labels(seasons, editor):
    """2010 연결 카드와 2011 관측을 독립 집합으로 보존하고 중복 관측은 제외한다."""
    joined, _ = join_cards(editor, ROOT / 'docs/reports/pm_threshold_review_20260906')
    result = [{'id': r['season']['playerSeasonId'], 'expected': r['card'].get('articleCost', r['card'].get('cost')),
               'corpus': 'Article2010', 'split': r['split'], 'sourceUrl':r['card']['sourceUrl']} for r in joined]
    with (ROOT / 'Tools/PMReference/reports/COST_PLAYER_BASELINE.csv').open(encoding='utf-8-sig') as stream:
        identities = {r['PlayerSeasonId']: r['EditorPlayerSeasonId'] for r in csv.DictReader(stream)}
    seen = set()
    with (ROOT / 'Tools/PMReference/reports/COST_REFERENCE_VALIDATION.csv').open(encoding='utf-8-sig') as stream:
        for row in csv.DictReader(stream):
            sid = identities.get(row['PlayerSeasonId'])
            if row['DataVersion'] != 'OriginalObserved2011' or sid not in seasons:
                continue
            if row['HasCostConflict'] != 'False' or row['CardType'] != 'Unknown':
                continue
            key = (sid, int(row['ReferenceCost']))
            if key in seen:
                continue
            seen.add(key)
            result.append({'id': sid, 'expected': key[1], 'corpus': 'Observed2011',
                           'split': split_player(seasons[sid]['playerPersonId']), 'sourceUrl':row['SourceUrl']})
    identity = {(s['originYear'], s['originFranchiseId'], n): sid for sid,s in seasons.items() for n in s['sourceReferenceNames']}
    workbook = read(ROOT / 'docs/reports/pm_reference_review_20260906/workbook_extracted.json')
    for row in next(s['Rows'] for s in workbook['Sheets'] if s['Name'] == 'Cost_근거')[1:]:
        c = row['Cells']
        sid = identity.get((int(c['A']),c.get('B',''),c['C']))
        if sid:
            result.append({'id':sid,'expected':int(c['E']),'corpus':'Workbook'+c['G'], 'split':split_player(seasons[sid]['playerPersonId']), 'sourceUrl':c['K']})
    return result


def predict_costs(seasons, balance, kind, base, quality_multiplier, workload_weight, elite=None):
    """전체 Source 모집단의 역할 백분위를 다시 계산한다. 이름·팀은 입력하지 않는다."""
    settings = balance['costValueModel']
    populations = defaultdict(list)
    for sid, season in seasons.items():
        if season['playerType'] != kind:
            continue
        component = season['costDerivationTrace']['componentScores']
        workload = component['workload']
        workload_score = component['workloadScore'] * workload_weight / settings['hitterWorkload']['weight'] if kind == 'Hitter' else component['workloadScore'] * workload_weight
        raw = round(component['rawValue'] + base - component['baseScore'] + component['quality'] * (quality_multiplier-settings['qualityMultiplier']) + workload_score-component['workloadScore'],8)
        populations[season['originYear']].append((sid, raw, component['roleGroup']))
    predicted = {}
    role_settings = settings['roleNormalization']
    def percentile(value, ordered):
        return (bisect.bisect_left(ordered, value) + bisect.bisect_right(ordered, value)) / (2 * len(ordered))
    for population in populations.values():
        all_values = sorted(r[1] for r in population)
        groups = defaultdict(list)
        for _, value, role in population:
            groups[role].append(value)
        for values in groups.values():
            values.sort()
        for sid, value, role in population:
            if len(groups[role]) >= role_settings['minimumGroupCount']:
                maximum=role_settings['maximumAdjustment']
                value += max(-maximum,min(maximum,(percentile(value, groups[role]) - percentile(value, all_values)) * 2.0 * maximum))
            thresholds = settings.get('valueTierThresholdsByPlayerType', {}).get(kind,settings['valueTierThresholds'])
            ceiling = seasons[sid]['costDerivationTrace']['eliteEligibility']['maximumCost']
            if elite is not None:
                boundary9, boundary10, quality10, workload10 = elite
                thresholds = [dict(r) for r in thresholds]
                thresholds[7]['upperExclusive'], thresholds[8]['upperExclusive'] = boundary9, boundary10
                c = seasons[sid]['costDerivationTrace']['componentScores']
                if c['quality'] >= quality10 and c['workload']['ratio'] >= workload10 and c['reliability'] >= settings['eliteEligibility']['cost10']['minimumReliability']:
                    ceiling = 10
                elif ceiling == 10:
                    ceiling = 9
            tier = next(r['cost'] for r in thresholds if value < r['upperExclusive'])
            predicted[sid] = min(tier, ceiling)
    return predicted


def summarize(labels, predicted):
    """학습·검증·홀드아웃과 기사 시점별 오차를 분리한다."""
    return {corpus: {split: metrics([(r['expected'], predicted[r['id']]) for r in labels
                                    if r['corpus'] == corpus and (split == 'All' or r['split'] == split)])
                     for split in ('Train', 'Validation', 'Holdout', 'All')}
            for corpus in sorted({r['corpus'] for r in labels})}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before', type=Path, required=True)
    parser.add_argument('--balance', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    seasons, balance = load_seasons(args.before), read(args.balance)
    labels = reference_labels(seasons, args.before)
    result = {'splitPolicy': 'pm-2010-player-split-v1', 'selection': 'Train only; corpus weights min(1, train count / 20); coefficient regularization 0.02', 'profiles': []}
    for elite in ((9.4,9.5,.65,1.0),(8.5,9.5,.5,.85),(8.0,9.0,.4,.75)):
      profile={'elite':elite,'types':[],'trainLoss':0.0}
      for kind in ('Hitter', 'Pitcher'):
        part = [r for r in labels if seasons[r['id']]['playerType'] == kind]
        original = (balance['costValueModel']['baseScoreByPlayerType'][kind], balance['costValueModel']['qualityMultiplier'],
                    balance['costValueModel']['hitterWorkload']['weight'] if kind == 'Hitter' else 1.0)
        replay = predict_costs(seasons, balance, kind, *original)
        mismatches = [sid for sid, cost in replay.items() if cost != seasons[sid]['cost']]
        if mismatches:
            raise ValueError(f'기준 Cost 재연산 불일치: {[(sid,replay[sid],seasons[sid]["cost"],seasons[sid]["costDerivationTrace"]["componentScores"]) for sid in mismatches[:5]]}')
        before = summarize(part, replay)
        grid = itertools.product([x * .25 for x in range(4, 13)], [x * .25 for x in range(6, 15)],
                                 [4, 4.5, 5, 5.5, 6] if kind == 'Hitter' else [.9, 1, 1.1])
        candidates = []
        for parameters in grid:
            scores = summarize([r for r in part if r['split'] == 'Train'], predict_costs(seasons, balance, kind, *parameters,elite=elite))
            weights = [min(1,c['Train']['count']/20) for c in scores.values()]
            loss = sum(c['Train']['mae']*w for c,w in zip(scores.values(),weights)) / sum(weights)
            distance = sum(abs(a-b) for a,b in zip(parameters,original))
            candidates.append((loss + .02 * distance, distance, parameters))
        chosen = min(candidates)[2]
        after = summarize(part, predict_costs(seasons, balance, kind, *chosen,elite=elite))
        entry = {'type':kind, 'beforeParameters':original, 'afterParameters':chosen, 'before':before, 'after':after}
        profile['types'].append(entry)
        profile['trainLoss'] += min(candidates)[0]/2
        print('CANDIDATE',elite,kind,chosen,min(candidates)[0],flush=True)
      result['profiles'].append(profile)
    result['chosen']=min(result['profiles'],key=lambda p:p['trainLoss'])
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print('CHOSEN',json.dumps(result['chosen'],ensure_ascii=False))


if __name__ == '__main__':
    main()
