"""전체 구단의 현재 카드 평가와 선수 분리 교차검증 일치율을 산출한다."""
import argparse
import csv
import hashlib
import json
import math
from collections import defaultdict
from pathlib import Path

from synthetic_bake import ABILITY_INDEX


def evaluate(pairs):
    """정답·예측 쌍의 방향성 오차를 함께 집계한다."""
    errors=[predicted-expected for expected,predicted in pairs]
    count=len(errors)
    if not count:
        return dict(count=0)
    return dict(count=count,mae=sum(abs(e) for e in errors)/count,
        rmse=math.sqrt(sum(e*e for e in errors)/count),bias=sum(errors)/count,
        exact=sum(e==0 for e in errors)/count,within1=sum(abs(e)<=1 for e in errors)/count,
        within3=sum(abs(e)<=3 for e in errors)/count,within5=sum(abs(e)<=5 for e in errors)/count,
        underCount=sum(e<0 for e in errors),overCount=sum(e>0 for e in errors),maxAbs=max(abs(e) for e in errors))


def fold_for_person(person, split_salt, fold_count):
    """같은 선수의 모든 시즌을 한 교차검증 Fold에 둔다."""
    digest=hashlib.sha256(f'{split_salt}|AllClubAuditV1|{person}'.encode()).hexdigest()
    return int(digest[:8],16)%fold_count


def group_metrics(rows, expected_key, predicted_key, group_key):
    grouped=defaultdict(list)
    for row in rows:
        grouped[str(row[group_key])].append((int(row[expected_key]),int(row[predicted_key])))
    return {key:evaluate(pairs) for key,pairs in sorted(grouped.items())}


def main():
    import numpy as np
    from sklearn.ensemble import GradientBoostingRegressor

    from fit_record_trees import matrix, prepare_features

    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--comparison',type=Path,required=True)
    parser.add_argument('--cost-rows',type=Path,required=True)
    parser.add_argument('--archive',type=Path,required=True)
    parser.add_argument('--policy',type=Path,default=Path(__file__).with_name('reference_calibration_policy.json'))
    parser.add_argument('--tree-report',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
    policy=read(args.policy);tree_report=read(args.tree_report);settings=policy['recordTreeTraining']
    seasons={s['playerSeasonId']:s for path in sorted((args.archive/'Years').glob('*.json'))
             for s in read(path)['playerSeasons']}
    source_counts=defaultdict(int)
    for season in seasons.values():
        if int(season['originYear'])<=policy['maximumCardYear']:
            source_counts[season['originFranchiseId']]+=1
    with args.comparison.open(encoding='utf-8-sig',newline='') as stream:
        comparison=list(csv.DictReader(stream))
    cost_rows=read(args.cost_rows)
    for row in comparison:
        season=seasons[row['id']]
        row['team']=season['originFranchiseId']
        row['current']=season['cost'] if row['target']=='Cost' else season['baseAttributes'][ABILITY_INDEX[row['target']]]
        row['formula']=int(row['after'])
    cost_by_id={row['id']:row for row in cost_rows}
    for row in cost_rows:
        season=seasons[row['id']];row['team']=season['originFranchiseId']
    selected_depth={row['kind']:row['selectedDepth'] for row in tree_report['costScores']}
    for kind in ('Hitter','Pitcher'):
        part=[row for row in cost_rows if row['kind']==kind]
        train=[row for row in part if row['split']=='Train']
        evaluation=[row for row in part if row['split']!='Train']
        features=prepare_features(train)
        estimator=GradientBoostingRegressor(n_estimators=settings['estimatorCount'],
            max_depth=selected_depth[kind],min_samples_leaf=settings['minimumLeafSamples'],
            learning_rate=settings['learningRate'],random_state=settings['randomSeed'])
        estimator.fit(matrix(train,features),[row['expected'] for row in train])
        predicted=np.clip(np.rint(estimator.predict(matrix(evaluation,features))),1,10).astype(int)
        for row,value in zip(evaluation,predicted):
            row['evaluationPrediction']=int(value)
        print('COST_EVALUATION_READY',kind,len(train),len(evaluation),flush=True)
    cost_comparison=[]
    for row in comparison:
        if row['target']!='Cost':
            continue
        source=cost_by_id[row['id']]
        cost_comparison.append(dict(id=row['id'],person=row['person'],year=int(row['year']),team=row['team'],
            kind=row['kind'],split=row['split'],expected=int(row['expected']),current=int(row['current']),
            evaluationPrediction=source.get('evaluationPrediction')))
    cost_evaluation=[row for row in cost_comparison if row['evaluationPrediction'] is not None]
    abilities=[dict(id=row['id'],person=row['person'],year=int(row['year']),team=row['team'],kind=row['kind'],
        target=row['target'],split=row['split'],expected=int(row['expected']),current=int(row['current']),
        formula=int(row['formula']))
        for row in comparison if row['target']!='Cost']
    modeled=[row for row in abilities if row['target']!='Velocity']
    holdout=[row for row in abilities if row['split']=='Holdout']
    holdout_modeled=[row for row in holdout if row['target']!='Velocity']
    teams={}
    all_team_names=sorted({row['team'] for row in cost_comparison}|{row['team'] for row in abilities})
    for team in all_team_names:
        costs=[row for row in cost_comparison if row['team']==team]
        cost_test=[row for row in cost_evaluation if row['team']==team]
        stats=[row for row in abilities if row['team']==team]
        stats_holdout=[row for row in holdout_modeled if row['team']==team]
        teams[team]=dict(sourceCardsThrough2013=source_counts[team],referenceCostCards=len(costs),
            referenceCoverage=(len(costs)/source_counts[team] if source_counts[team] else None),
            currentCost=evaluate([(r['expected'],r['current']) for r in costs]),
            formulaCostEvaluation=evaluate([(r['expected'],r['evaluationPrediction']) for r in cost_test]),
            currentAbility=evaluate([(r['expected'],r['current']) for r in stats]),
            holdoutFormulaAbility=evaluate([(r['expected'],r['formula']) for r in stats_holdout]))
    result=dict(scope=dict(maximumReferenceYear=policy['maximumCardYear'],monthlyCardsExcluded=True,
        evaluationSplit='Train 대 Validation+Holdout 선수 분리',costReferenceCards=len(cost_comparison),
        costEvaluationCards=len(cost_evaluation),abilityReferenceFields=len(abilities),
        teamLabels=len(teams),note='구단명은 해당 시즌의 원본 명칭이며 인수·개명 전후 명칭을 합치지 않는다.'),
        currentBake=dict(cost=evaluate([(r['expected'],r['current']) for r in cost_comparison]),
            ability=evaluate([(r['expected'],r['current']) for r in abilities]),
            modeledAbilityExcludingUnmeasuredVelocity=evaluate([(r['expected'],r['current']) for r in modeled])),
        personGroupedFormulaEvaluation=dict(cost=evaluate([(r['expected'],r['evaluationPrediction']) for r in cost_evaluation]),
            byKind=group_metrics(cost_evaluation,'expected','evaluationPrediction','kind'),
            byTeam=group_metrics(cost_evaluation,'expected','evaluationPrediction','team'),
            byYear=group_metrics(cost_evaluation,'expected','evaluationPrediction','year'),
            byExpectedCost=group_metrics(cost_evaluation,'expected','evaluationPrediction','expected')),
        abilityFormulaHoldout=dict(all=evaluate([(r['expected'],r['formula']) for r in holdout]),
            modeledExcludingUnmeasuredVelocity=evaluate([(r['expected'],r['formula']) for r in holdout_modeled]),
            byTarget=group_metrics(holdout,'expected','formula','target'),
            byTeam=group_metrics(holdout_modeled,'expected','formula','team')),
        teams=teams,costRows=cost_comparison)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('CURRENT_COST',result['currentBake']['cost'])
    print('FORMULA_COST_EVALUATION',result['personGroupedFormulaEvaluation']['cost'])
    print('HOLDOUT_ABILITY_FORMULA',result['abilityFormulaHoldout']['modeledExcludingUnmeasuredVelocity'])
    for team,values in teams.items():
        print(team,values['referenceCostCards'],values['formulaCostEvaluation'],values['holdoutFormulaAbility'])


if __name__=='__main__':
    main()
