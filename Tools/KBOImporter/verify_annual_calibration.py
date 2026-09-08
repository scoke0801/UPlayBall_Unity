"""연구 예측과 실제 캐시 베이크 출력이 일치하는지 검증하고 전후 오차를 저장한다."""
import argparse
import csv
import json
import hashlib
from collections import Counter
from pathlib import Path

from study_pm_calibration import metrics
from synthetic_bake import ABILITY_INDEX
import synthetic_bake as bake


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fit',type=Path,required=True)
    parser.add_argument('--candidate',type=Path,required=True)
    parser.add_argument('--verify-seeds',action='store_true')
    args=parser.parse_args()
    seasons={s['playerSeasonId']:s for p in sorted((args.candidate/'Years').glob('*.json'))
             for s in json.loads(p.read_text(encoding='utf-8'))['playerSeasons']}
    with (args.fit/'comparison.csv').open(encoding='utf-8-sig',newline='') as stream:rows=list(csv.DictReader(stream))
    mismatches=[]
    for r in rows:
        s=seasons[r['id']]
        r['actual']=s['cost'] if r['target']=='Cost' else s['baseAttributes'][ABILITY_INDEX[r['target']]]
        if r['actual']!=int(r['after']):mismatches.append({k:r[k] for k in ('id','kind','target','expected','after','actual')})
    scores=[]
    for kind in ('Hitter','Pitcher'):
        for target in sorted({r['target'] for r in rows if r['kind']==kind}):
            for split in ('All','Train','Validation','Holdout'):
                part=[r for r in rows if r['kind']==kind and r['target']==target and (split=='All' or r['split']==split)]
                scores.append(dict(kind=kind,target=target,split=split,
                    before=metrics([(int(r['expected']),int(r['before'])) for r in part]),
                    after=metrics([(int(r['expected']),r['actual']) for r in part])))
    result={'evaluatedFields':len(rows),'cards':len({r['id'] for r in rows}),'mismatches':mismatches,
            'evaluationScope':'DeploymentReferenceReplay',
            'maximumReferenceYear':max(int(r['year']) for r in rows),'scores':scores,
            'generatedCostDistribution':dict(sorted(Counter(s['cost'] for s in seasons.values()).items()))}
    cost_rows=[r for r in rows if r['target']=='Cost']
    result['referenceCostTiers']=[dict(cost=cost,count=len(part),
        before=metrics([(cost,int(r['before'])) for r in part]),
        after=metrics([(cost,r['actual']) for r in part]),
        predictedDistribution=dict(sorted(Counter(r['actual'] for r in part).items())))
        for cost in range(1,11) if (part:=[r for r in cost_rows if int(r['expected'])==cost])]
    result['matchedCostDistribution']=dict(sorted(Counter(r['actual'] for r in cost_rows).items()))
    result['generatedThrough2013CostDistribution']=dict(sorted(Counter(s['cost'] for s in seasons.values() if s['originYear']<=2013).items()))
    if args.verify_seeds:
        hashes=[]
        for seed in (20260901,20260902):
            content=bake.bake(Path(__file__).parent/'.cache/KBOImport/Normalized',[2000,2013,2025],seed)
            hashes.append(hashlib.sha256(bake.canonical_json_bytes(content)).hexdigest())
        if hashes[0]!=hashes[1]:raise ValueError('Source 기반 출력이 World Seed에 따라 변경됐습니다.')
        result['seedVerification']={'years':[2000,2013,2025],'seeds':[20260901,20260902],'hashes':hashes}
    (args.fit/'actual_verification.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('FIELDS',len(rows),'MISMATCHES',len(mismatches),'MAX_REFERENCE_YEAR',result['maximumReferenceYear'])
    if mismatches:raise ValueError('연구 예측과 실제 베이크가 다릅니다. actual_verification.json을 확인하세요.')


if __name__=='__main__':main()
