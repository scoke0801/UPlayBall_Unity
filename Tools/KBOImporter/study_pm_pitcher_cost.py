"""Cost v12 관측을 인물별로 분할해 출전량·성적 상호작용 후보를 비교한다.

기준 Archive와 당시 Balance 스냅샷이 필요하다. SK 집중 표본은 후보 선택에 사용하며,
독립 검증에서는 그 인물 전체를 제외한다. K/9 탐색 축은 해당 지표 근거가 없는 현재
Archive에서는 결측 재정규화로 소거되며, 채택 프로필에도 K/9를 추가하지 않는다.
"""
import argparse
import bisect
import json
import random
from collections import defaultdict
from pathlib import Path
from report_pm_calibration import load_seasons
from study_pm_cost_shape import reference_labels,summarize


def feature(season):
    """표시 능력치 대신 베이크에 보존된 시즌 성적·출전량을 사용한다."""
    c=season['costDerivationTrace']['componentScores']; w=c['workload']
    metrics={m['metric']:m['adjustedZ'] for m in season['costMetricEvidence'] if m['isAvailable']}
    return (c['quality'],w['roleCurve'],w['absoluteCurve'],w['starterShare'],c['roleAdjustment'],
            metrics.get('NegativeEarnedRunAverage'),metrics.get('NegativeWalksPerNine'),w['ratio'],c['reliability'],metrics.get('StrikeoutsPerNine'))


def quality_value(features,weight,strikeout_weight=0):
    observations=[(features[5],weight*(1-strikeout_weight)),(features[6],(1-weight)*(1-strikeout_weight)),(features[9],strikeout_weight)]
    available=[(value,w) for value,w in observations if value is not None and w>0]
    return sum(v*w for v,w in available)/sum(w for _,w in available) if available else 0


def value(features,parameters):
    base,quality,scale,power,interaction,boundary,boundary10,era_weight,gate,strikeout_weight=parameters
    _,role,absolute,share,_,*_=features
    q=quality_value(features,era_weight,strikeout_weight)
    workload=(2*role**power+4*absolute**power+1.25*share)*scale
    return base+q*quality+workload+max(0,q)*min(1,absolute*absolute)*interaction


def predict(seasons,features,parameters,settings,full_population=True):
    """최종 후보는 전체 시즌 모집단의 역할 백분위까지 재계산한다."""
    thresholds=[r['upperExclusive'] for r in settings['valueTierThresholds']]
    thresholds[7],thresholds[8]=parameters[5:7]
    values={sid:(round(value(f,parameters),8) if seasons[sid]['costDerivationTrace']['componentScores']['roleGroup']=='Rotation'
                 else seasons[sid]['costDerivationTrace']['componentScores']['rawValue']) for sid,f in features.items()}
    adjusted={}
    if full_population:
        populations=defaultdict(list)
        for sid,v in values.items():
            s=seasons[sid]; role=s['costDerivationTrace']['componentScores']['roleGroup']
            populations[s['originYear']].append((sid,v,role))
        maximum=settings['roleNormalization']['maximumAdjustment']
        minimum=settings['roleNormalization']['minimumGroupCount']
        def rank(v,ordered):
            return (bisect.bisect_left(ordered,v)+bisect.bisect_right(ordered,v))/(2*len(ordered))
        for population in populations.values():
            all_values=sorted(v for _,v,_ in population); groups=defaultdict(list)
            for _,v,role in population: groups[role].append(v)
            for group in groups.values(): group.sort()
            for sid,v,role in population:
                shift=max(-maximum,min(maximum,(rank(v,groups[role])-rank(v,all_values))*2*maximum)) if len(groups[role])>=minimum else 0
                adjusted[sid]=v+shift
    else:
        adjusted={sid:v+features[sid][4] for sid,v in values.items()}
    predicted={}
    for sid,v in adjusted.items():
        f=features[sid];q=quality_value(f,parameters[7],parameters[9]); ratio,reliability=f[7:9]
        q9,w9,q10,w10=parameters[8]
        selected_thresholds=thresholds
        if seasons[sid]['costDerivationTrace']['componentScores']['roleGroup']!='Rotation':
            q=f[0];q9,w9,q10,w10=0,.75,.65,1
            selected_thresholds=[r['upperExclusive'] for r in settings['valueTierThresholds']]
        ceiling=10 if q>=q10 and ratio>=w10 and reliability>=.55 else (9 if q>=q9 and ratio>=w9 and reliability>=.45 else 8)
        predicted[sid]=min(1+bisect.bisect_right(selected_thresholds,v),ceiling)
    return predicted


def loss(labels,predicted):
    groups=defaultdict(list)
    for row in labels: groups[row['corpus']].append(abs(row['expected']-predicted[row['id']]))
    weights={key:min(1,len(errors)/20) for key,errors in groups.items()}
    return sum(sum(errors)/len(errors)*weights[key] for key,errors in groups.items())/sum(weights.values())


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--focus-reference',type=Path,required=True)
    parser.add_argument('--balance',type=Path,required=True)
    args=parser.parse_args()
    all_seasons=load_seasons(args.archive)
    labels=[r for r in reference_labels(all_seasons,args.archive) if all_seasons[r['id']]['playerType']=='Pitcher']
    seasons={sid:s for sid,s in all_seasons.items() if s['playerType']=='Pitcher'}
    settings=json.loads(args.balance.read_text(encoding='utf-8'))['costValueModel']
    features={sid:feature(s) for sid,s in seasons.items()}
    baseline=(1.25,2,1,1,0,9.4,9.5,.75,(0,.75,.65,1),0)
    replay=predict(seasons,features,baseline,settings)
    assert all(replay[sid]==s['cost'] for sid,s in seasons.items())
    focus_doc=json.loads(args.focus_reference.read_text(encoding='utf-8'))
    focus=[]
    for r in focus_doc['rows']:
        matches=[sid for sid,s in seasons.items() if s['originYear']==r['year'] and s['originFranchiseId']==r['team'] and r['name'] in s['sourceReferenceNames']]
        assert len(matches)==1
        focus.append({'id':matches[0],'expected':r['cost'],'corpus':'UserFocus','split':'Focus'})
    focus_people={seasons[r['id']]['playerPersonId'] for r in focus}
    independent=[r for r in labels if seasons[r['id']]['playerPersonId'] not in focus_people]
    train=[r for r in independent if r['split']=='Train']; train_ids={r['id'] for r in train+focus}
    train_features={sid:f for sid,f in features.items() if sid in train_ids}
    candidates=[]
    grid=((1.75,2.25,2.75,3.25),(2,2.5,3,4,5),(.4,.5,.6,.7,.8),(1,1.5,2),(0,1,2),(7.5,8,8.25,8.5),(9,9.25,9.5),(.75,.9,1),((0,.6,.5,.8),(0,.6,.6,.8),(0,.5,.5,.75)),(0,.1,.2,.3))
    rng=random.Random(20260906)
    sampled=sorted({tuple(rng.choice(axis) for axis in grid) for _ in range(25000)})+[baseline]
    for p in sampled:
        distance=abs(p[0]-1.25)+abs(p[1]-2)*.2+abs(p[2]-1)+abs(p[3]-1)*.2+p[4]*.2+abs(p[5]-9.4)*.1
        predicted=predict(seasons,train_features,p,settings,False)
        score=.5*loss(train,predicted)+.5*loss(focus,predicted)+.01*distance
        candidates.append((score,distance,p))
    # 대량 후보 예선에서만 기존 역할 보정을 사용하고 결선은 모두 다시 계산한다.
    finalists=[]
    for _,distance,p in sorted(candidates)[:60]:
        predicted=predict(seasons,features,p,settings)
        finalists.append({'parameters':p,'trainLoss':.5*loss(train,predicted)+.5*loss(focus,predicted)+.01*distance,'focusMae':loss(focus,predicted),'scores':summarize(labels,predicted),
                           'independentScores':summarize(independent,predicted),'focusPredictions':[{**r,'before':seasons[r['id']]['cost'],'after':predicted[r['id']]} for r in focus],
                           'changed':sum(predicted[sid]!=s['cost'] for sid,s in seasons.items())})
    finalists.sort(key=lambda r:(r['trainLoss'],r['parameters']))
    chosen=finalists[0]
    result={'selection':'User focus 50% + non-focus Train 50%; exclude all focus persons from independent validation; corpus-balanced MAE + 0.01 regularization; final 60 use full population role ranks',
            'parameterNames':['base','qualityMultiplier','workloadScale','workloadExponent','qualityWorkloadInteraction','cost8Upper','cost9Upper','eraWeight','eliteGate','strikeoutWeight'],
            'candidateCount':len(candidates),'before':summarize(labels,replay),'chosen':chosen,'finalists':finalists}
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(chosen,ensure_ascii=False),flush=True)


if __name__=='__main__': main()
