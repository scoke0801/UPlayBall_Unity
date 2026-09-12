"""선수 식별자 없이 기록의 비선형 관계를 학습하고 표준 C/Python 자료로 내보낸다."""
import argparse
import copy
import csv
import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from sklearn.ensemble import GradientBoostingRegressor
from reference_source_policy import validate_training_sources

from record_calibration import evaluate_model, read_feature, resolve_model_cost, validate_models
from record_tree_calibration import MODEL_TYPE, model_hash, write_balance_config
from study_pm_calibration import metrics
import synthetic_bake as bake


def prepare_features(rows):
    """식별자·구단·연도는 특징 목록에 들어가지 않는다."""
    sources=['baseline','value.quality','value.workload.ratio','value.workload.starterShare',
             'value.workloadScore','value.reliability','value.defensiveValue']
    sources += [f'{metric}.{field}' for metric in sorted(rows[0]['evidence'])
                for field in ('rawValue','adjustedZ','reliability','sampleSize')]
    sources += ['role.'+role for role in ('C','1B','2B','3B','SS','LF','CF','RF','DH','Rotation','Relief')]
    features=[]
    for source in sources:
        values=[read_feature({'source':source},r['evidence'],r['before'],r['value']) for r in rows]
        observed=[v for v in values if v is not None]
        if not observed or min(observed)==max(observed):
            continue
        features.append(dict(source=source,mean=float(np.median(observed)),scale=1.0,
            minimum=min(observed),maximum=max(observed),coefficient=0.0))
    return features


def matrix(rows, features):
    """결측과 학습 범위 외 값을 실행기와 동일하게 변환한다."""
    result=[]
    for row in rows:
        values=[]
        for feature in features:
            value=read_feature(feature,row['evidence'],row['before'],row['value'])
            value=feature['mean'] if value is None else value
            values.append(max(feature['minimum'],min(feature['maximum'],value)))
        result.append(values)
    return np.asarray(result,dtype=np.float32)


def export_model(estimator, features):
    """나무 분기와 잎 기여를 내보낸다. 학습 표본의 ID와 정답 목록은 포함하지 않는다."""
    trees=[]
    for stage in estimator.estimators_:
        tree=stage[0].tree_
        nodes=[]
        for index in range(tree.node_count):
            leaf=tree.children_left[index]==-1
            nodes.append([-1 if leaf else int(tree.feature[index]),float(tree.threshold[index]),
                int(tree.children_left[index]),int(tree.children_right[index]),
                float(tree.value[index,0,0])*estimator.learning_rate if leaf else 0.0])
        trees.append(nodes)
    model=dict(modelType=MODEL_TYPE,intercept=float(estimator.init_.constant_[0,0]),features=features,trees=trees)
    model['modelSha256']=model_hash(model)
    return model


def scores(rows, predictions):
    return metrics([(r['expected'],int(p)) for r,p in zip(rows,predictions)])


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--rows',type=Path,required=True)
    parser.add_argument('--base-fit',type=Path,required=True)
    parser.add_argument('--policy',type=Path,default=Path(__file__).with_name('reference_calibration_policy.json'))
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--reference-overrides',type=Path,required=True)
    args=parser.parse_args()
    read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
    policy=read(args.policy); settings=policy['recordTreeTraining']; rows=read(args.rows)
    override_payload=read(args.reference_overrides)
    validate_training_sources(rows, override_payload['cards'])
    config=read(args.base_fit/'derivation_balance.json')
    args.output.mkdir(parents=True,exist_ok=True)
    reports=[]; predicted={}
    for kind in ('Hitter','Pitcher'):
        part=[r for r in rows if r['kind']==kind]
        train=[r for r in part if r['split']=='Train']; validation=[r for r in part if r['split']=='Validation']
        holdout=[r for r in part if r['split']=='Holdout']
        features=prepare_features(train); x=matrix(train,features); y=[r['expected'] for r in train]
        candidates=[]
        for depth in settings['maximumDepthCandidates']:
            estimator=GradientBoostingRegressor(n_estimators=settings['estimatorCount'],max_depth=depth,
                min_samples_leaf=settings['minimumLeafSamples'],learning_rate=settings['learningRate'],random_state=settings['randomSeed'])
            estimator.fit(x,y)
            validation_scores=scores(validation,np.clip(np.rint(estimator.predict(matrix(validation,features))),1,10))
            training_scores=scores(train,np.clip(np.rint(estimator.predict(x)),1,10))
            candidates.append((depth,estimator,validation_scores,training_scores))
            print(kind,'depth',depth,'validation',validation_scores,'train',training_scores,flush=True)
        best=min(c[2]['mae'] for c in candidates)
        qualified=[c for c in candidates if c[2]['mae']<=best+settings['validationMaeTolerance']]
        if not qualified:
            raise ValueError('검증 오차 기준을 통과한 모델이 없습니다.')
        depth,evaluation_model,validation_scores,training_scores=min(qualified,key=lambda c:c[0])
        holdout_scores=scores(holdout,np.clip(np.rint(evaluation_model.predict(matrix(holdout,features))),1,10))
        # 구조 선택 후 전체 근거로 배포 모델을 재적합한다. 아래 일치율을 보류 검증 성적으로 부르지 않는다.
        features=prepare_features(part)
        estimator=copy.deepcopy(evaluation_model)
        estimator.fit(matrix(part,features),[r['expected'] for r in part])
        model=export_model(estimator,features)
        config['referenceRecordModels'][kind]['Cost']=model
        actual=[]
        for row in part:
            value,trace=evaluate_model(model,row['evidence'],row['before'],row['value'])
            cost=resolve_model_cost(value,model,10) if trace is not None else row['before']
            actual.append(cost);predicted[row['id']]=cost
        library=np.clip(np.rint(estimator.predict(matrix(part,features))),1,10).astype(int).tolist()
        if actual!=library:
            raise ValueError('표준 라이브러리 실행기와 학습기의 예측이 다릅니다.')
        deployment=scores(part,actual)
        reports.append(dict(kind=kind,selectedDepth=depth,train=training_scores,validation=validation_scores,
            holdoutBeforeRefit=holdout_scores,deploymentRefit=deployment,
            candidates=[dict(depth=c[0],validation=c[2],train=c[3]) for c in candidates]))
        print(kind,'selected',depth,'holdout',holdout_scores,'refit',deployment,flush=True)
    validate_models(config['referenceRecordModels'],{'Hitter':set(bake.HITTER_METRIC_NAMES),'Pitcher':set(bake.PITCHER_METRIC_NAMES)})
    config.update(version='historical-derivation-balance-v23',abilityFormulaVersion='historical-ability-v10',
                  costFormulaVersion='historical-season-value-v17')
    config['referenceCalibration']['policySha256']=hashlib.sha256(args.policy.read_bytes()).hexdigest()
    config['referenceCalibration']['costModel']='RecordGradientBoosting; 선택 전 선수 분리 검증, 선택 후 전체 근거 재적합'
    override_name='annual_reference_overrides.json'
    override_hash=hashlib.sha256(args.reference_overrides.read_bytes()).hexdigest()
    config['annualReferenceOverride']=dict(enabled=True,relativePath=override_name,
        version=override_payload['version'],maximumCardYear=override_payload['maximumCardYear'],
        cardCount=len(override_payload['cards']),contentSha256=override_hash)
    config['ratingCalibrationNote']='2013년 이하 일반 연도 카드의 검증된 수치는 기준 데이터로 동기화하고, 미확보 카드는 선수 식별자 없는 기록 회귀로 산출.'
    write_balance_config(args.output/'derivation_balance.json',config)
    shutil.copyfile(args.reference_overrides,args.output/override_name)
    report=dict(policy=policy,costScores=reports,trainingRowsSha256=hashlib.sha256(args.rows.read_bytes()).hexdigest(),
        referenceOverrideSha256=override_hash,
        note='모델 성적은 미확보 카드 fallback의 근사 성적이다. 확보한 일반 연도 카드는 검증된 기준 데이터로 동기화한다.')
    (args.output/'tree_fit_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    with (args.base_fit/'comparison.csv').open(encoding='utf-8-sig',newline='') as stream:
        reader=csv.DictReader(stream);fields=reader.fieldnames;comparison=list(reader)
    for row in comparison:
        if row['target']=='Cost':row['after']=predicted[row['id']]
    with (args.output/'comparison.csv').open('w',encoding='utf-8-sig',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=fields);writer.writeheader();writer.writerows(comparison)
    (args.output/'fit_report.json').write_text(json.dumps(read(args.base_fit/'fit_report.json'),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


if __name__=='__main__':
    main()
