"""상승 필요치가 공통 기본 능력치로 역산 가능한지 관측된 카드 숫자와 대조한다."""
from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path


def analyze(root):
    """복수 포지션은 억지로 펼치지 않고 일대일 대응하는 행만 수치 제약을 검사한다."""
    def read(name):
        return json.loads((root/name).read_text(encoding='utf-8'))
    observations=read('article_observations.json')
    defenses={row['cardId']:row for row in read('hitter_card_readings.json')['readings']}
    pitchers={row['cardId']:row for row in read('pitch_card_readings.json')}
    defense_pairs, pitch_pairs=[],[]
    defense_thresholds=defaultdict(list)
    pitch_thresholds=defaultdict(list)
    for row in observations['86864']['rows']:
        if row['cardId'] not in defenses:
            continue
        by_position=defaultdict(list)
        for target in row['targets']:
            if len(target['grades'])==len(target['additional'])==1:
                label,delta=target['grades'][0],target['additional'][0]
                by_position[target['positions']].append((label,delta))
                defense_thresholds[label].append(defenses[row['cardId']]['attributes'][4]+delta)
        for values in by_position.values():
            for first,second in zip(values,values[1:]):
                defense_pairs.append(second[1]-first[1])
    for row in observations['86866']['rows']:
        by_pitch=defaultdict(list)
        for target in row['thresholds']:
            if len(target['targetGrades'])!=1:
                continue
            label,delta=target['targetGrades'][0],target['additional']
            by_pitch[target['pitch']].append((label,delta))
            pitch_thresholds[label].append(pitchers[row['cardId']]['attributes'][3]+delta)
        for values in by_pitch.values():
            for first,second in zip(values,values[1:]):
                pitch_pairs.append(second[1]-first[1])
    return {'defenseRows':len(observations['86864']['rows']),
            'defenseVariants':dict(Counter(row['variant'] for row in observations['86864']['rows'])),
            'pitchCards':len(observations['86866']['rows']),
            'pitchThresholdRows':sum(len(row['thresholds']) for row in observations['86866']['rows']),
            'defenseStepDifferences':dict(Counter(defense_pairs)), 'pitchStepDifferences':dict(Counter(pitch_pairs)),
            'observedDefensePlusDeltaByTarget':{grade:{'minimum':min(values),'maximum':max(values),'count':len(values)}
                                              for grade,values in sorted(defense_thresholds.items())},
            'observedBreakingPlusDeltaByTarget':{grade:{'minimum':min(values),'maximum':max(values),'count':len(values)}
                                               for grade,values in sorted(pitch_thresholds.items())},
            'interpretation':'DifferentPlayerThresholds_DoNotInferBaseRatingFromDeltaOrUniformGradeBoundary',
            'snapshotCaveat':'LinkedLiveImagesAreNotProofOfOriginalPublicationBytes'}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report-dir',type=Path,required=True)
    args=parser.parse_args()
    result=analyze(args.report_dir)
    (args.report_dir/'threshold_audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(result,ensure_ascii=False,indent=2))


if __name__=='__main__':
    main()
