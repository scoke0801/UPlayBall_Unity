"""캐시 기록으로 학습한 회귀 나무를 외부 학습 라이브러리 없이 실행한다."""
import math
import struct
import hashlib
import json


MODEL_TYPE = 'RecordGradientBoosting'


def model_hash(model):
    """정적 모델의 입력 정의와 분기 계수를 함께 식별한다."""
    payload={k:v for k,v in model.items() if k!='modelSha256'}
    return hashlib.sha256(json.dumps(payload,sort_keys=True,separators=(',',':')).encode()).hexdigest()


def write_balance_config(path, config):
    """일반 설정은 읽기 쉽게 두고 자동 생성 나무는 한 줄에 한 나무로 저장한다."""
    text=json.dumps(config,ensure_ascii=False,indent=2)
    for targets in config.get('referenceRecordModels',{}).values():
        for model in targets.values():
            if model.get('modelType')!=MODEL_TYPE:
                continue
            expanded=json.dumps(model['trees'],ensure_ascii=False,indent=2).replace('\n','\n'+' '*8)
            compact='[\n'+',\n'.join(' '*10+json.dumps(tree,separators=(',',':')) for tree in model['trees'])+'\n'+' '*8+']'
            if expanded not in text:
                raise ValueError('회귀 나무 설정의 직렬화 경로가 일치하지 않습니다.')
            text=text.replace(expanded,compact,1)
    path.write_text(text+'\n',encoding='utf-8')


def predict_trees(model, observed):
    """학습기의 float32 분기 계약을 재현하고 결측은 학습 중앙값으로 대체한다."""
    inputs=[]
    for feature,value in zip(model['features'],observed):
        actual=feature['mean'] if value is None else value
        bounded=max(feature['minimum'],min(feature['maximum'],actual))
        inputs.append(struct.unpack('<f',struct.pack('<f',bounded))[0])
    prediction=float(model['intercept'])
    for tree in model['trees']:
        index=0
        while tree[index][0]>=0:
            feature,threshold,left,right,_=tree[index]
            index=left if inputs[feature]<=threshold else right
        prediction+=tree[index][4]
    return prediction


def validate_trees(model):
    """유한 계수·입력 인덱스·순환 없는 트리 구조를 실행 전에 검사한다."""
    if set(model)!={'modelType','intercept','features','trees','modelSha256'}:
        raise ValueError('기록 회귀 나무에는 분기 계수 외의 조회표나 식별자를 저장할 수 없습니다.')
    if not model.get('trees'):
        raise ValueError('기록 회귀 나무가 비어 있습니다.')
    if model.get('modelSha256')!=model_hash(model):
        raise ValueError('기록 회귀 나무의 모델 해시가 일치하지 않습니다.')
    for feature in model['features']:
        if set(feature)-{'source','mean','scale','minimum','maximum','coefficient','transform','minimumCoefficient'}:
            raise ValueError('회귀 특징에 기록 변환 외의 조회 자료가 포함되어 있습니다.')
    for tree in model['trees']:
        if not tree:
            raise ValueError('기록 회귀 나무의 루트가 없습니다.')
        reached={0}
        for index,node in enumerate(tree):
            if len(node)!=5 or index not in reached:
                raise ValueError('기록 회귀 나무에 연결되지 않은 노드가 있습니다.')
            feature,threshold,left,right,contribution=node
            if not math.isfinite(threshold) or not math.isfinite(contribution):
                raise ValueError('기록 회귀 나무에 유한하지 않은 값이 있습니다.')
            if feature==-1:
                if left!=-1 or right!=-1:
                    raise ValueError('말단 노드가 자식을 가질 수 없습니다.')
                continue
            if not isinstance(feature,int) or not 0<=feature<len(model['features']):
                raise ValueError('기록 회귀 나무의 특징 인덱스가 잘못되었습니다.')
            if any(not isinstance(child,int) or not index<child<len(tree) for child in (left,right)) or left==right:
                raise ValueError('기록 회귀 나무가 순환하거나 자식 인덱스가 잘못되었습니다.')
            reached.update((left,right))
