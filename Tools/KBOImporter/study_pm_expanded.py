"""기사 카드 표본을 선수 단위로 분할해 공통 중심·변환 폭과 Cost 후보를 비교한다."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
from collections import defaultdict, Counter
from pathlib import Path

from study_pm_calibration import metrics, read

ATTRIBUTES = ('Contact', 'Power', 'Speed', 'Arm', 'Defense', 'BatterMental',
              'Stamina', 'Velocity', 'Stuff', 'Breaking', 'Control', 'PitcherMental')


def split_player(person_id):
    """동일 인물의 여러 시즌이 학습·검증 사이에 섞이지 않게 한다."""
    bucket = int(hashlib.sha256(('pm-2010-player-split-v1|' + person_id).encode()).hexdigest()[:8], 16) % 10
    return 'Train' if bucket < 6 else 'Validation' if bucket < 8 else 'Holdout'


def join_cards(editor_dir, report_dir):
    """연도·이름·유형이 유일할 때만 연결하고 선수별 판본 중복은 제외한다."""
    identities = defaultdict(dict)
    for path in sorted((editor_dir / 'Years').glob('*.json')):
        for season in read(path)['playerSeasons']:
            for name in season['sourceReferenceNames']:
                identities[(season['originYear'], name, season['playerType'])][season['playerSeasonId']] = season
    cards = [(row, 'Hitter') for row in read(report_dir / 'hitter_card_readings.json')['readings']]
    cards += [(row, 'Pitcher') for row in read(report_dir / 'pitch_card_readings.json')]
    joined, rejected = [], []
    seen = set()
    for card, kind in cards:
        matches = list(identities[(card['year'], card['name'], kind)].values())
        if len(matches) != 1:
            rejected.append({'cardId': card['cardId'], 'year': card['year'], 'name': card['name'], 'sourceMatches': len(matches)})
            continue
        season = matches[0]
        if season['playerSeasonId'] in seen:
            rejected.append({'cardId': card['cardId'], 'reason': 'DuplicatePlayerSeason'})
            continue
        seen.add(season['playerSeasonId'])
        joined.append({'card': card, 'season': season, 'split': split_player(season['playerPersonId'])})
    return joined, rejected


def predict_rating(season, attribute, center, scale):
    """결측 중립값과 정수 반올림은 실제 Bake와 같은 계약이다."""
    trace = next(trace for trace in season['abilityDerivationTrace'] if trace['attribute'] == attribute)
    if trace['evaluationMethod'] == 'NeutralWithoutEvidence':
        return 55
    return max(25, min(100, round(center + scale * trace['combinedZ'])))


def compare(pairs, prediction):
    return metrics([(target, prediction(row)) for row, target in pairs])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before', type=Path, required=True)
    parser.add_argument('--balance', type=Path, required=True)
    parser.add_argument('--report-dir', type=Path, required=True)
    parser.add_argument('--after', type=Path)
    args = parser.parse_args()
    balance = read(args.balance)
    joined, rejected = join_cards(args.before, args.report_dir)
    results = []
    for index, attribute in enumerate(ATTRIBUTES):
        if attribute in ('Arm', 'Stamina', 'Velocity'):
            continue
        kind = 'Hitter' if index < 6 else 'Pitcher'
        part = [(row, row['card']['attributes'][index % 6]) for row in joined if row['season']['playerType'] == kind]
        config = balance['ratingProfiles'][kind][attribute]
        current_center, current_scale = config.get('center',55), config['scale']
        train = [(row,value) for row,value in part if row['split'] == 'Train']
        candidates = []
        for center in sorted(set([current_center] + list(range(55,80,2)))):
            for scale in sorted(set([current_scale,8,12,16,20,24])):
                result = compare(train, lambda row: predict_rating(row['season'],attribute,center,scale))
                penalty = .04 * abs(center-current_center) + .04 * abs(scale-current_scale)
                candidates.append((result['mae'] + penalty, abs(center-current_center)+abs(scale-current_scale),center,scale))
        _,_,center,scale = min(candidates)
        scores = []
        for split in ('Train','Validation','Holdout','All'):
            subset = [(row,val) for row,val in part if split == 'All' or row['split'] == split]
            scores.append({'split': split, 'before': compare(subset,lambda row: row['season']['baseAttributes'][index]),
                           'after': compare(subset,lambda row: predict_rating(row['season'],attribute,center,scale))})
        results.append({'attribute':attribute,'type':kind,'center':center,'scale':scale,'scores':scores})
    cost = []
    for kind in ('Hitter','Pitcher'):
        part = [(row,row['card'].get('articleCost',row['card'].get('cost'))) for row in joined if row['season']['playerType']==kind]
        def price(row,offset):
            trace=row['season']['costDerivationTrace']
            value=trace['continuousValue']+offset
            tier=next(t['cost'] for t in balance['costValueModel']['valueTierThresholds'] if value<t['upperExclusive'])
            return min(tier,trace['eliteEligibility']['maximumCost'])
        train=[(row,val) for row,val in part if row['split']=='Train']
        offset=min((compare(train,lambda row:price(row,step*.25))['mae']+.04*abs(step*.25),abs(step),step*.25)
                   for step in range(-8,9))[2]
        cost.append({'type':kind,'offset':offset,'scores':[{'split':split,
            'before':compare([(r,v) for r,v in part if split=='All' or r['split']==split],lambda r:r['season']['cost']),
            'after':compare([(r,v) for r,v in part if split=='All' or r['split']==split],lambda r:price(r,offset))}
            for split in ('Train','Validation','Holdout','All')]})
    output={'splitPolicy':'pm-2010-player-split-v1','joined':len(joined),'rejected':rejected,
            'splitCounts':dict(Counter(row['split'] for row in joined)),'abilities':results,'cost':cost}
    if args.after:
        after = {season['playerSeasonId']: season for path in sorted((args.after/'Years').glob('*.json'))
                 for season in read(path)['playerSeasons']}
        actual = []
        for row in joined:
            previous, card = row['season'], row['card']
            current = after[previous['playerSeasonId']]
            for field in ('Cost',) + (ATTRIBUTES[:6] if previous['playerType']=='Hitter' else ATTRIBUTES[6:]):
                if field == 'Arm':
                    continue
                index = ATTRIBUTES.index(field) if field != 'Cost' else None
                actual.append({'cardId':card['cardId'],'year':card['year'],'name':card['name'],
                               'playerType':previous['playerType'],'split':row['split'],'field':field,
                               'sourceUrl':card['sourceUrl'], 'sourceIdentity':previous['playerSeasonId'],
                               'reference':card.get('articleCost',card.get('cost')) if index is None else card['attributes'][index%6],
                               'before':previous['cost'] if index is None else previous['baseAttributes'][index],
                               'after':current['cost'] if index is None else current['baseAttributes'][index]})
        summary=[]
        for field in ('Cost',)+ATTRIBUTES:
            if field=='Arm':
                continue
            for split in ('All','Train','Validation','Holdout'):
                part=[row for row in actual if row['field']==field and (split=='All' or row['split']==split)]
                summary.append({'field':field,'split':split,**{phase:metrics([(r['reference'],r[phase]) for r in part])
                                                             for phase in ('before','after')}})
        output['actualComparison']=summary
        output['afterManifest']=read(args.after/'manifest.json')['sourceManifest']
        output['beforeManifest']=read(args.before/'manifest.json')['sourceManifest']
        with (args.report_dir/'actual_card_comparison.csv').open('w',encoding='utf-8-sig',newline='') as stream:
            writer=csv.DictWriter(stream,fieldnames=list(actual[0]))
            writer.writeheader()
            writer.writerows(actual)
    (args.report_dir/'expanded_study.json').write_text(json.dumps(output,ensure_ascii=False,indent=2),encoding='utf-8')
    print('JOIN',output['joined'],'REJECTED',rejected,'SPLITS',output['splitCounts'])
    for result in results+cost:
        print(result.get('attribute',result['type']),result.get('center'),result.get('scale'),result.get('offset'),
              [(r['split'],r['before']['count'],round(r['before']['mae'],3),round(r['after']['mae'],3)) for r in result['scores']])


if __name__ == '__main__':
    main()
