"""실측 구속이 없는 투수의 일반 카드 표시 구속을 기록으로 추정한다."""
import hashlib
import json
import math
from functools import lru_cache
from pathlib import Path

from record_calibration import read_feature


def load_model(settings, root):
    """검증된 추정 모델만 사용하고 실측 데이터와 혼동하지 않는다."""
    if not settings or not settings.get('enabled'):
        return None
    return _read_model(str(Path(root) / settings['relativePath']), settings['sha256'])


@lru_cache(maxsize=4)
def _read_model(filename, expected_hash):
    """한 Bake에서 같은 해시의 불변 모델을 반복 파싱하지 않는다."""
    path = Path(filename)
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != expected_hash:
        raise ValueError('표시 구속 추정 모델의 해시가 다릅니다.')
    model = json.loads(raw.decode('utf-8'))
    validate_model(model)
    return model


def validate_model(model):
    """구속 추정에 식별자·가격·임의 조회표가 유입되지 않도록 제한한다."""
    allowed = {'StrikeoutsPerNine', 'NegativeWalksPerNine', 'NegativeEarnedRunAverage',
               'InningsPerGame', 'SaveRate', 'HoldRate', 'SeasonInnings', 'PitchingGames'}
    if model.get('target') != 'NormalCardVelocityRating' or model.get('measuredVelocity') is not False:
        raise ValueError('구속 추정의 대상은 실측 km/h가 아닌 일반 카드 표시값이어야 합니다.')
    if not model.get('features') or not math.isfinite(model['intercept']):
        raise ValueError('구속 추정 모델이 비어 있거나 절편이 유효하지 않습니다.')
    if not 1 <= model['minimumRating'] < model['maximumRating'] <= 100:
        raise ValueError('구속 추정 범위가 유효하지 않습니다.')
    seen = set()
    for feature in model['features']:
        parts = feature['source'].split('.')
        if len(parts) != 2 or parts[0] not in allowed or parts[1] != 'rawValue' or feature['source'] in seen:
            raise ValueError('구속 추정에는 중복 없는 허용 투구 기록만 사용할 수 있습니다.')
        seen.add(feature['source'])
        if feature.get('transform', 'identity') not in ('identity', 'log1p'):
            raise ValueError('구속 추정의 기록 변환이 유효하지 않습니다.')
        if any(not math.isfinite(feature[k]) for k in ('mean', 'scale', 'minimum', 'maximum', 'coefficient')):
            raise ValueError('구속 추정 계수는 유한해야 합니다.')
        if feature['scale'] <= 0 or feature['minimum'] > feature['maximum']:
            raise ValueError('구속 추정 입력 범위가 유효하지 않습니다.')


def estimate(evidence, model):
    """관측이 없는 입력은 학습 평균으로 대체하고 추정 출처를 반환한다."""
    prediction = model['intercept']
    available, imputed = [], []
    for feature in model['features']:
        metric = evidence.get(feature['source'].split('.')[0]) or {}
        observed = read_feature(feature, evidence, 0) if metric.get('sampleSize', 0) > 0 else None
        if observed is None:
            imputed.append(feature['source'])
            actual = feature['mean']
        else:
            available.append(feature['source'])
            actual = max(feature['minimum'], min(feature['maximum'], observed))
        prediction += (actual - feature['mean']) / feature['scale'] * feature['coefficient']
    prediction = max(model['minimumRating'], min(model['maximumRating'], prediction))
    return prediction, dict(method='EstimatedNormalCardVelocity' if available else 'EstimatedVelocityPrior',
                            modelVersion=model['version'], measuredVelocity=False,
                            prediction=round(prediction, 8), availableSources=available, imputedSources=imputed)
