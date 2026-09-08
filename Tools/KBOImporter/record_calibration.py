"""관측 카드에서 학습한 공통 기록 회귀를 적용한다. 선수·구단·연도 ID는 입력하지 않는다."""
from __future__ import annotations

import math
from typing import Any, Mapping
from record_tree_calibration import MODEL_TYPE, predict_trees, validate_trees


def read_feature(feature: Mapping[str, Any], evidence: Mapping[str, Any], baseline: float,
                 value: Mapping[str, Any] | None = None) -> float | None:
    """결측은 0과 구분하고 설정된 기록 변환만 수행한다."""
    source = feature["source"]
    if source == "baseline":
        result = baseline
    elif source.startswith("value."):
        result = value or {}
        for part in source.split(".")[1:]:
            result = result.get(part) if isinstance(result, Mapping) else None
    elif source.startswith("role."):
        result = float((value or {}).get("roleGroup") == source.split(".")[1])
    else:
        metric, field = source.split(".")
        component = evidence.get(metric) or {}
        result = component.get(field) if component.get("isAvailable", False) else None
    if result is None or not math.isfinite(float(result)):
        return None
    result = float(result)
    transform = feature.get("transform", "identity")
    if transform == "log1p":
        result = math.log1p(max(0.0, result))
    elif transform == "sqrt":
        result = math.sqrt(max(0.0, result))
    elif transform == "positiveSquare":
        result = max(0.0, result) ** 2
    elif transform != "identity":
        raise ValueError(f"알 수 없는 기록 변환입니다: {transform}")
    return result


def has_observed_sample(evidence: Mapping[str, Any]) -> bool:
    """출전 표본 0은 실제로 관측한 0개의 성공과 구분한다."""
    return any(c.get("isAvailable", False) and float(c.get("sampleSize", 1.0)) > 0.0
               for c in evidence.values())


def evaluate_model(model: Mapping[str, Any] | None, evidence: Mapping[str, Any], baseline: float,
                   value: Mapping[str, Any] | None = None) -> tuple[float, dict[str, Any] | None]:
    """학습 범위 밖의 외삽을 제한하고 항목별 기여를 추적한다."""
    if model and model.get('modelType')==MODEL_TYPE:
        if not any(c.get('isAvailable',False) and c.get('rawValue') is not None for c in evidence.values()):
            return baseline,None
        observed=[read_feature(f,evidence,baseline,value) for f in model['features']]
        prediction=predict_trees(model,observed)
        return prediction,dict(method=MODEL_TYPE,baseline=baseline,prediction=prediction,
            treeCount=len(model['trees']),modelSha256=model['modelSha256'],
            imputedSources=[f['source'] for f,v in zip(model['features'],observed) if v is None])
    if not model or not has_observed_sample(evidence):
        return baseline, None
    contributions = []
    prediction = float(model["intercept"])
    for feature in model["features"]:
        observed = read_feature(feature, evidence, baseline, value)
        imputed = observed is None
        actual = float(feature["mean"]) if imputed else observed
        bounded = max(float(feature["minimum"]), min(float(feature["maximum"]), actual))
        normalized = (bounded - float(feature["mean"])) / float(feature["scale"])
        contribution = normalized * float(feature["coefficient"])
        prediction += contribution
        contributions.append({"source": feature["source"], "value": observed, "isImputed": imputed,
                              "boundedValue": bounded, "contribution": contribution})
    return prediction, {"method": "ReferenceRecordRidge", "baseline": baseline,
                        "prediction": prediction, "intercept": model["intercept"],
                        "contributions": contributions}


def resolve_model_cost(prediction: float, model: Mapping[str, Any] | None, ceiling: int) -> int:
    """가격은 학습한 순서형 경계로 분류하며 숫자 회귀의 중앙 집중을 피한다."""
    boundaries = (model or {}).get("costBoundaries")
    # 나무 모델은 출전량·신뢰도를 함께 학습한다. 이전 고정 상한을 중복 적용하지 않는다.
    if (model or {}).get('modelType')==MODEL_TYPE:
        ceiling=10
    cost = 1 + sum(prediction >= boundary for boundary in boundaries) if boundaries is not None else round(prediction)
    return max(1, min(ceiling, cost))


def validate_models(models: Mapping[str, Any], metric_names: Mapping[str, set[str]]) -> None:
    """잘못된 계수·식별자 유입·지원하지 않는 입력을 베이크 전에 거부한다."""
    allowed_value = {"value.quality", "value.workload.ratio", "value.workload.starterShare",
                     "value.workloadScore", "value.reliability", "value.defensiveValue"}
    for kind, targets in models.items():
        if kind not in metric_names:
            raise ValueError("회귀 선수 유형이 유효하지 않습니다.")
        for target, model in targets.items():
            allowed_targets = {"Contact", "Power", "Speed", "Arm", "Defense", "BatterMental", "Cost"} if kind == "Hitter" else {
                "Stamina", "Stuff", "Breaking", "Control", "PitcherMental", "Cost"}
            if target not in allowed_targets:
                raise ValueError("회귀 대상 능력치가 유효하지 않습니다. 실측 구속은 보정 대상이 아닙니다.")
            if not model.get("features") or not math.isfinite(float(model["intercept"])):
                raise ValueError("회귀 절편 또는 입력 목록이 유효하지 않습니다.")
            if model.get('modelType')==MODEL_TYPE:
                validate_trees(model)
            if "costBoundaries" in model:
                boundaries = model["costBoundaries"]
                if target != "Cost" or len(boundaries) != 9 or any(not math.isfinite(float(x)) for x in boundaries) or list(boundaries) != sorted(boundaries):
                    raise ValueError("Cost 분류 경계는 유한한 오름차순 9개 값이어야 합니다.")
            for feature in model["features"]:
                source = feature["source"]
                if source.startswith("role."):
                    if target != "Cost" or source.split(".")[1] not in {"C","1B","2B","3B","SS","LF","CF","RF","DH","Rotation","Relief"}:
                        raise ValueError("역할 특징은 알려진 야구 포지션·보직이어야 합니다.")
                if source != "baseline" and source not in allowed_value:
                    parts = source.split(".")
                    if not source.startswith("role.") and (len(parts) != 2 or parts[0] not in metric_names[kind] or parts[1] not in {
                        "rawValue", "adjustedZ", "reliability", "sampleSize"}):
                        raise ValueError("회귀 입력은 허용된 야구 기록이어야 합니다.")
                if source.startswith("value.") and target != "Cost":
                    raise ValueError("능력치에 가격 평가값을 입력할 수 없습니다.")
                if feature.get("transform", "identity") not in {"identity", "sqrt", "log1p", "positiveSquare"}:
                    raise ValueError("회귀 변환이 유효하지 않습니다.")
                if any(not math.isfinite(float(feature[k])) for k in
                       ("mean", "scale", "minimum", "maximum", "coefficient")):
                    raise ValueError("회귀 계수는 유한해야 합니다.")
                if float(feature["scale"]) <= 0 or float(feature["minimum"]) > float(feature["maximum"]):
                    raise ValueError("회귀 입력 범위가 유효하지 않습니다.")
                if "minimumCoefficient" in feature:
                    minimum = float(feature["minimumCoefficient"])
                    if not math.isfinite(minimum) or float(feature["coefficient"]) < minimum:
                        raise ValueError("회귀 계수가 기록의 단조 증가 계약을 위반합니다.")
