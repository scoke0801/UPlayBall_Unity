from __future__ import annotations

import argparse
import bisect
import hashlib
import json
import math
import statistics
from pathlib import Path
from typing import Any, Iterable

from derivation_cost import composite_cost, resolve_value_cost

from kbo_importer import IMPORTER_VERSION as NORMALIZED_IMPORTER_VERSION
from kbo_importer import SCHEMA_VERSION as NORMALIZED_SCHEMA_VERSION
from kbo_importer.validation import validate_saved_document


REFERENCE_DATA_VERSION = f"kbo-normalized-v{NORMALIZED_SCHEMA_VERSION}"
CONTENT_SCHEMA_VERSION = 5
EDITOR_ORIGINAL_NAME_POLICY = "editor-original-source-v2"
RUNTIME_NAME_POLICY = "runtime-world-identity-pool-v3"
EDITOR_ASSET_FORMAT_VERSION = 1
DERIVATION_BALANCE_PATH = Path(__file__).with_name("derivation_balance.json")
DERIVATION_BALANCE = json.loads(DERIVATION_BALANCE_PATH.read_text(encoding="utf-8"))
ABILITY_FORMULA_VERSION = str(DERIVATION_BALANCE["abilityFormulaVersion"])
COST_FORMULA_VERSION = str(DERIVATION_BALANCE["costFormulaVersion"])
POSITION_ROLE_CLASSIFIER_VERSION = str(DERIVATION_BALANCE["positionRoleClassifierVersion"])
ROSTER_BUILDER_VERSION = str(DERIVATION_BALANCE["rosterBuilderVersion"])
DERIVATION_BALANCE_VERSION = str(DERIVATION_BALANCE["version"])
HITTER_POSITIONS = ("C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH")
DEFENSIVE_HITTER_POSITIONS = HITTER_POSITIONS[:-1]
ABILITY_NAMES = (
    "Contact",
    "Power",
    "Speed",
    "Arm",
    "Defense",
    "BatterMental",
    "Stamina",
    "Velocity",
    "Stuff",
    "Breaking",
    "Control",
    "PitcherMental",
)
HITTER_METRIC_NAMES = (
    "BattingAverage",
    "OnBasePercentage",
    "SluggingPercentage",
    "IsolatedPower",
    "HomeRunRate",
    "WalkRate",
    "NegativeStrikeoutRate",
    "StolenBaseAttemptRate",
    "StolenBaseSuccessRate",
    "FieldingPercentage",
    "NegativeErrorsPerNine",
    "DefensiveOpportunitiesPerNine",
    "AssistsPerNine",
    "CaughtStealingRate",
    "HomeRuns",
    "StolenBases",
)
PITCHER_METRIC_NAMES = (
    "NegativeEarnedRunAverage",
    "NegativeWhip",
    "StrikeoutsPerNine",
    "NegativeWalksPerNine",
    "NegativeHomeRunsPerNine",
    "InningsPerGame",
    "SaveRate",
    "HoldRate",
    "SeasonInnings",
    "FastballVelocityKph",
)
ABILITY_INDEX = {name: index for index, name in enumerate(ABILITY_NAMES)}
SOURCE_POSITION_MAP = {
    "포수": "C",
    "1루수": "1B",
    "2루수": "2B",
    "3루수": "3B",
    "유격수": "SS",
    "좌익수": "LF",
    "중견수": "CF",
    "우익수": "RF",
    "외야수": "CF",
    "지명타자": "DH",
}
PITCHER_ROLE_CLASSIFIER_CONFIG = DERIVATION_BALANCE["pitcherRoleClassifier"]
ROSTER_SELECTION_CONFIG = DERIVATION_BALANCE["rosterSelection"]
POSITION_STARTER_ATTRIBUTE_WEIGHTS = ROSTER_SELECTION_CONFIG["positionStarterAttributeWeights"]
DH_ATTRIBUTE_WEIGHTS = ROSTER_SELECTION_CONFIG["designatedHitterAttributeWeights"]
BENCH_ATTRIBUTE_WEIGHTS = ROSTER_SELECTION_CONFIG["benchAttributeWeights"]
PITCHER_ASSIGNMENT_ATTRIBUTE_WEIGHTS = ROSTER_SELECTION_CONFIG["pitcherAssignmentAttributeWeights"]
def metric_composite_influence_audit(
    player_type: str,
    profile_name: str,
    config: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Raw metric 하나가 역할 Composite를 중복 지배하는지 설정 단계에서 계산한다."""
    balance = config or DERIVATION_BALANCE
    profiles = balance["roleCompositeProfiles"][player_type]
    resolved_profile = profile_name if profile_name in profiles else "Default"
    ability_names = ABILITY_NAMES[:6] if player_type == "Hitter" else ABILITY_NAMES[6:]
    role_weights = [float(weight) for weight in profiles[resolved_profile]]
    total_role_weight = sum(role_weights)
    raw_influences: dict[str, float] = {}
    for ability_name, role_weight in zip(ability_names, role_weights):
        rating_profile = balance["ratingProfiles"][player_type][ability_name]
        normalized_role_weight = role_weight / total_role_weight
        scale = abs(float(rating_profile["scale"]))
        for metric_name, metric_weight in rating_profile["metrics"].items():
            raw_influences[metric_name] = raw_influences.get(metric_name, 0.0) + (
                normalized_role_weight * scale * abs(float(metric_weight))
            )

    total_influence = sum(raw_influences.values())
    maximum = float(balance["validation"]["maximumRawMetricCompositeInfluence"])
    metrics = []
    for metric_name in sorted(raw_influences):
        normalized = raw_influences[metric_name] / total_influence if total_influence > 0.0 else 0.0
        metrics.append(
            {
                "metric": metric_name,
                "absoluteInfluence": round(raw_influences[metric_name], 8),
                "normalizedInfluence": round(normalized, 8),
                "maximumAllowed": maximum,
                "exceedsMaximum": normalized > maximum + 1e-12,
            }
        )
    return {
        "playerType": player_type,
        "roleProfile": resolved_profile,
        "maximumAllowed": maximum,
        "metrics": metrics,
        "hasViolation": any(metric["exceedsMaximum"] for metric in metrics),
    }


def validate_derivation_balance(config: dict[str, Any]) -> None:
    maximum_metric_influence = float(
        config["validation"]["maximumRawMetricCompositeInfluence"]
    )
    if not 0.0 < maximum_metric_influence <= 1.0:
        raise ValueError("Raw metric 역할 Composite 영향력 상한은 0 초과 1 이하여야 합니다.")

    thresholds = config["costPercentileThresholds"]
    if not thresholds or float(thresholds[-1]["upperExclusive"]) <= 1.0:
        raise ValueError("Cost 백분위 설정이 전체 모집단을 덮지 않습니다.")
    previous = 0.0
    for threshold in thresholds:
        upper = float(threshold["upperExclusive"])
        cost = int(threshold["cost"])
        if upper <= previous or cost < 1 or cost > 10:
            raise ValueError("Cost 백분위 설정의 경계 또는 Cost가 유효하지 않습니다.")
        previous = upper

    metric_names_by_type = {
        "Hitter": set(HITTER_METRIC_NAMES),
        "Pitcher": set(PITCHER_METRIC_NAMES),
    }
    for player_type, profiles in config["ratingProfiles"].items():
        for attribute, profile in profiles.items():
            weights = profile["metrics"]
            if attribute not in ABILITY_NAMES or not set(weights).issubset(metric_names_by_type[player_type]):
                raise ValueError(f"알 수 없는 Ability/Metric 설정입니다: {player_type}/{attribute}")
            if abs(sum(float(weight) for weight in weights.values()) - 1.0) > 1e-9:
                raise ValueError(f"Ability metric weight 합은 1이어야 합니다: {player_type}/{attribute}")

    reference = config["referencePopulation"]
    if float(reference["minimumEffectiveSampleCount"]) <= 0.0:
        raise ValueError("Reference 모집단의 최소 유효 표본은 0보다 커야 합니다.")
    if not 0.0 <= float(reference["winsorizeTailQuantile"]) < 0.5:
        raise ValueError("Reference Winsorize 분위는 0 이상 0.5 미만이어야 합니다.")
    if set(reference["positionFamilies"]) != set(HITTER_POSITIONS):
        raise ValueError("Reference PositionFamily 설정이 모든 타자 포지션을 덮지 않습니다.")

    eligibility = config["costEligibility"]
    boundaries = config["costCompositeThresholds"]
    if len(boundaries) != 10 or float(boundaries[-1]["upperExclusive"]) <= 100.0:
        raise ValueError("능력치 Cost 경계는 0~100과 Cost 1~10을 덮어야 합니다.")
    previous = 0.0
    for expected_cost, boundary in enumerate(boundaries, 1):
        upper = float(boundary["upperExclusive"])
        if not math.isfinite(upper) or upper <= previous or boundary["cost"] != expected_cost:
            raise ValueError("능력치 Cost 경계는 유한한 순증가 값이어야 합니다.")
        previous = upper
    if float(eligibility["referenceSeasonGames"]) <= 0.0:
        raise ValueError("기준 시즌 경기 수는 양수여야 합니다.")
    prior = config["samplePrior"]
    if not -float(prior["maximumAbsoluteZ"]) <= float(prior["performanceZ"]) < 0.0:
        raise ValueError("소표본 사전 편차는 음수이며 Z 경계 안이어야 합니다.")
    headroom = config["trainingHeadroom"]
    if not 0 <= int(headroom["minimum"]) <= int(headroom["maximum"]):
        raise ValueError("훈련 여유는 음수가 아닌 순서쌍이어야 합니다.")

    threshold_sets = [eligibility["hitterSampleThresholds"]] + list(
        eligibility["pitcherSampleThresholds"].values()
    )
    for thresholds in threshold_sets:
        if set(thresholds) != {"Full", "Regular", "Limited"}:
            raise ValueError("Cost 자격 출전량 기준이 Full/Regular/Limited를 덮지 않습니다.")
        if not float(thresholds["Limited"]) < float(thresholds["Regular"]) < float(thresholds["Full"]):
            raise ValueError("Cost 자격 출전량 기준은 순증가해야 합니다.")
    if set(eligibility["pitcherSampleThresholds"]) != set(reference["pitcherRoleFamilies"].values()):
        raise ValueError("Cost 자격 투수 기준이 투수 역할군 전체를 덮지 않습니다.")

    for player_type, profiles in config["roleCompositeProfiles"].items():
        for profile_name, profile in profiles.items():
            if (len(profile) != 6 or any(not math.isfinite(float(weight)) or float(weight) < 0.0 for weight in profile)
                    or abs(sum(profile) - 1.0) > 1e-9):
                raise ValueError(f"Cost 역할 가중치 설정이 유효하지 않습니다: {player_type}/{profile_name}")
            audit = metric_composite_influence_audit(player_type, profile_name, config)
            if audit["hasViolation"]:
                violations = ", ".join(
                    f"{metric['metric']}={metric['normalizedInfluence']:.4f}"
                    for metric in audit["metrics"]
                    if metric["exceedsMaximum"]
                )
                raise ValueError(
                    "Raw metric의 역할 Composite 총 영향력이 상한을 초과합니다: "
                    f"{player_type}/{profile_name}/{violations}"
                )

    value_model = config["costValueModel"]
    if float(value_model["qualityMultiplier"]) <= 0.0:
        raise ValueError("Cost Quality 배율은 양수여야 합니다.")
    for player_type, profile in value_model["qualityProfiles"].items():
        if set(profile).difference(metric_names_by_type[player_type]):
            raise ValueError(f"Cost Quality에 알 수 없는 지표가 있습니다: {player_type}")
        if abs(sum(float(weight) for weight in profile.values()) - 1.0) > 1e-9:
            raise ValueError(f"Cost Quality weight 합은 1이어야 합니다: {player_type}")
    value_thresholds = value_model["valueTierThresholds"]
    if len(value_thresholds) != 10 or float(value_thresholds[-1]["upperExclusive"]) <= 10.0:
        raise ValueError("Season Value Cost 경계가 Cost 1~10을 덮지 않습니다.")
    previous = float("-inf")
    for expected_cost, boundary in enumerate(value_thresholds, 1):
        upper = float(boundary["upperExclusive"])
        if not math.isfinite(upper) or upper <= previous or int(boundary["cost"]) != expected_cost:
            raise ValueError("Season Value Cost 경계는 유한한 순증가 값이어야 합니다.")
        previous = upper
    for cost_key in ("cost9", "cost10"):
        gate = value_model["eliteEligibility"][cost_key]
        if not 0.0 <= float(gate["minimumReliability"]) <= 1.0:
            raise ValueError("Elite Reliability 기준은 0~1이어야 합니다.")
        if float(gate["minimumWorkloadRatio"]) < 0.0:
            raise ValueError("Elite Workload 기준은 음수일 수 없습니다.")


validate_derivation_balance(DERIVATION_BALANCE)


def stable_digest(*parts: object, length: int = 20) -> str:
    payload = "\0".join(str(part) for part in parts).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()[:length]


def stable_seed(*parts: object) -> int:
    return int(stable_digest(*parts, length=16), 16)


def safe_number(value: object, default: float = 0.0) -> float:
    if value is None:
        return default
    try:
        number = float(value)
    except (TypeError, ValueError):
        return default
    return number if math.isfinite(number) else default


def ratio(numerator: object, denominator: object) -> float:
    denominator_value = safe_number(denominator)
    return safe_number(numerator) / denominator_value if denominator_value > 0 else 0.0


def percentile_cost(rank: int, count: int) -> int:
    if count <= 0 or rank < 0 or rank >= count:
        raise ValueError("Cost 백분위 rank/count가 유효하지 않습니다.")
    percentile = (rank + 0.5) / count
    for threshold in DERIVATION_BALANCE["costPercentileThresholds"]:
        if percentile < float(threshold["upperExclusive"]):
            return int(threshold["cost"])
    raise ValueError("Cost 백분위 구간이 100%를 덮지 않습니다.")


def headroom_range(cost: int) -> tuple[int, int]:
    """저Cost 추가 성장으로 가격과 기본 전력의 관계를 뒤집지 않도록 같은 여유를 쓴다."""
    if not 1 <= cost <= 10:
        raise ValueError("훈련 대상 Cost는 1~10이어야 합니다.")
    settings = DERIVATION_BALANCE["trainingHeadroom"]
    return int(settings["minimum"]), int(settings["maximum"])


def clamp_rating(value: float) -> int:
    rating = DERIVATION_BALANCE["rating"]
    return max(int(rating["minimum"]), min(int(rating["maximum"]), int(round(value))))


def mean(values: Iterable[float]) -> float:
    materialized = tuple(values)
    return sum(materialized) / len(materialized) if materialized else 0.0


def optional_number(value: object) -> tuple[bool, float]:
    if value is None:
        return False, 0.0
    try:
        number = float(value)
    except (TypeError, ValueError):
        return False, 0.0
    return (True, number) if math.isfinite(number) else (False, 0.0)


def reliability(sample_size: float, constant: float) -> float:
    if sample_size <= 0.0:
        return 0.0
    return sample_size / (sample_size + constant)


def metric_evidence(
    metric: str,
    raw_value: float,
    numerator: float,
    denominator: float,
    sample_size: float,
    reliability_constant: float,
    is_available: bool,
) -> dict[str, Any]:
    return {
        "metric": metric,
        "rawValue": raw_value if is_available else None,
        "numerator": numerator if is_available else None,
        "denominator": denominator if is_available else None,
        "sampleSize": sample_size,
        "reliabilityConstant": reliability_constant,
        "isAvailable": is_available,
    }


def hitter_metric_evidence(player: dict[str, Any]) -> list[dict[str, Any]]:
    stats = player.get("hitterStats") or {}
    running = player.get("runningStats")
    defenses = player.get("defenseRecords") or []
    reliability_config = DERIVATION_BALANCE["reliability"]
    plate_appearances = max(0.0, safe_number(stats.get("plateAppearances")))
    at_bats = max(0.0, safe_number(stats.get("atBats")))
    hits = max(0.0, safe_number(stats.get("hits")))

    has_average, average = optional_number(stats.get("sourceAVG"))
    if not has_average and at_bats > 0.0 and stats.get("hits") is not None:
        has_average, average = True, hits / at_bats
    has_on_base, on_base = optional_number(stats.get("sourceOBP"))
    has_slugging, slugging = optional_number(stats.get("sourceSLG"))
    if not has_slugging and at_bats > 0.0 and stats.get("totalBases") is not None:
        has_slugging, slugging = True, safe_number(stats["totalBases"]) / at_bats
    # 타석 상세가 없는 시즌도 확인된 타수까지는 표본으로 인정한다.
    if stats.get("plateAppearances") is None:
        plate_appearances = at_bats

    pa_constant = float(reliability_config["plateAppearances"])
    result = [
        metric_evidence("BattingAverage", average, hits, at_bats, plate_appearances, pa_constant, has_average),
        metric_evidence("OnBasePercentage", on_base, on_base * plate_appearances, plate_appearances, plate_appearances, pa_constant, has_on_base),
        metric_evidence("SluggingPercentage", slugging, slugging * at_bats, at_bats, plate_appearances, pa_constant, has_slugging),
        metric_evidence("IsolatedPower", slugging - average, (slugging - average) * at_bats, at_bats, plate_appearances, pa_constant, has_slugging and has_average),
        metric_evidence("HomeRunRate", ratio(stats.get("homeRuns"), plate_appearances), safe_number(stats.get("homeRuns")), plate_appearances, plate_appearances, pa_constant, plate_appearances > 0.0 and stats.get("homeRuns") is not None),
        metric_evidence("WalkRate", ratio(stats.get("walks"), plate_appearances), safe_number(stats.get("walks")), plate_appearances, plate_appearances, pa_constant, plate_appearances > 0.0 and stats.get("walks") is not None),
        metric_evidence("NegativeStrikeoutRate", -ratio(stats.get("strikeouts"), plate_appearances), -safe_number(stats.get("strikeouts")), plate_appearances, plate_appearances, pa_constant, plate_appearances > 0.0 and stats.get("strikeouts") is not None),
        metric_evidence("HomeRuns", safe_number(stats.get("homeRuns")), safe_number(stats.get("homeRuns")), 1.0, plate_appearances, pa_constant, stats.get("homeRuns") is not None),
    ]

    has_attempts = False
    attempts = 0.0
    has_stolen_bases = False
    stolen_bases = 0.0
    has_caught_stealing = False
    if isinstance(running, dict):
        has_attempts, attempts = optional_number(running.get("stolenBaseAttempts"))
        has_stolen_bases, stolen_bases = optional_number(running.get("stolenBases"))
        has_caught_stealing, caught_stealing = optional_number(running.get("caughtStealing"))
        if not has_attempts and has_stolen_bases and has_caught_stealing:
            has_attempts = True
            attempts = stolen_bases + caught_stealing
    if not has_stolen_bases:
        has_stolen_bases, stolen_bases = optional_number(stats.get("stolenBases"))
    result.append(metric_evidence("StolenBases", stolen_bases, stolen_bases, 1.0,
                                  plate_appearances, pa_constant, has_stolen_bases))
    attempts = max(0.0, attempts)
    attempt_rate_available = has_attempts and plate_appearances > 0.0
    result.append(
        metric_evidence(
            "StolenBaseAttemptRate",
            attempts / plate_appearances if attempt_rate_available else 0.0,
            attempts,
            plate_appearances,
            plate_appearances,
            pa_constant,
            attempt_rate_available,
        )
    )
    success_available = has_stolen_bases and has_caught_stealing and attempts > 0.0
    result.append(
        metric_evidence(
            "StolenBaseSuccessRate",
            stolen_bases / attempts if success_available else 0.0,
            stolen_bases,
            attempts,
            attempts,
            float(reliability_config["stolenBaseAttempts"]),
            success_available,
        )
    )

    _, position_trace = derive_source_position(player, "DH")
    natural_position = position_trace["primaryDefensivePosition"]
    primary_defenses = [
        record
        for record in defenses
        if SOURCE_POSITION_MAP.get(str(record.get("position") or "")) == natural_position
    ]
    chances = sum(
        safe_number(record.get("putouts"))
        + safe_number(record.get("assists"))
        + safe_number(record.get("errors"))
        for record in primary_defenses
    )
    errors = sum(safe_number(record.get("errors")) for record in primary_defenses)
    assists = sum(safe_number(record.get("assists")) for record in primary_defenses)
    innings_outs = sum(safe_number(record.get("inningsOuts")) for record in primary_defenses)
    has_fielding_counts = bool(primary_defenses) and all(
        record.get(field) is not None
        for record in primary_defenses for field in ("putouts", "assists", "errors")
    )
    has_errors_and_innings = bool(primary_defenses) and all(
        record.get(field) is not None
        for record in primary_defenses for field in ("errors", "inningsOuts")
    )
    result.append(
        metric_evidence(
            "FieldingPercentage",
            1.0 - errors / chances if chances > 0.0 else 0.0,
            chances - errors,
            chances,
            chances,
            float(reliability_config["defensiveChances"]),
            has_fielding_counts and chances > 0.0,
        )
    )
    result.append(
        metric_evidence(
            "NegativeErrorsPerNine",
            -errors * 27.0 / innings_outs if innings_outs > 0.0 else 0.0,
            -errors,
            innings_outs / 27.0,
            innings_outs,
            float(reliability_config["defensiveInningsOuts"]),
            has_errors_and_innings and innings_outs > 0.0,
        )
    )
    result.append(
        metric_evidence(
            "DefensiveOpportunitiesPerNine",
            chances * 27.0 / innings_outs if innings_outs > 0.0 else 0.0,
            chances,
            innings_outs / 27.0,
            innings_outs,
            float(reliability_config["defensiveInningsOuts"]),
            chances > 0.0 and innings_outs > 0.0,
        )
    )
    assists_available = innings_outs > 0.0 and any(
        record.get("assists") is not None for record in primary_defenses
    )
    result.append(
        metric_evidence(
            "AssistsPerNine",
            assists * 27.0 / innings_outs if assists_available else 0.0,
            assists,
            innings_outs / 27.0,
            innings_outs,
            float(reliability_config["armInningsOuts"]),
            assists_available,
        )
    )

    stolen_bases_allowed = sum(
        safe_number(record.get("stolenBasesAllowed")) for record in primary_defenses
    )
    caught_stealing = sum(
        safe_number(record.get("caughtStealing")) for record in primary_defenses
    )
    catcher_attempts = stolen_bases_allowed + caught_stealing
    catcher_arm_available = natural_position == "C" and catcher_attempts > 0.0
    result.append(
        metric_evidence(
            "CaughtStealingRate",
            caught_stealing / catcher_attempts if catcher_arm_available else 0.0,
            caught_stealing,
            catcher_attempts,
            catcher_attempts,
            float(reliability_config["catcherStealAttempts"]),
            catcher_arm_available,
        )
    )
    return result


def pitcher_batters_faced(stats: dict[str, Any]) -> float:
    has_batters_faced, batters_faced = optional_number(stats.get("battersFaced"))
    if has_batters_faced:
        return max(0.0, batters_faced)
    return max(
        0.0,
        safe_number(stats.get("inningsOuts"))
        + safe_number(stats.get("hitsAllowed"))
        + safe_number(stats.get("walks"))
        + safe_number(stats.get("hitBatters")),
    )


def pitcher_metric_evidence(
    player: dict[str, Any],
    availability: dict[str, bool] | None = None,
) -> list[dict[str, Any]]:
    stats = player.get("pitcherStats") or {}
    outs = max(0.0, safe_number(stats.get("inningsOuts")))
    innings = outs / 3.0
    games = max(0.0, safe_number(stats.get("games")))
    batters_faced = pitcher_batters_faced(stats)
    tbf_constant = float(DERIVATION_BALANCE["reliability"]["battersFaced"])
    holds_available = bool((availability or {}).get("holds", True))

    has_era, earned_run_average = optional_number(stats.get("sourceERA"))
    if not has_era and innings > 0.0 and stats.get("earnedRuns") is not None:
        has_era = True
        earned_run_average = ratio(stats.get("earnedRuns"), innings) * 9.0
    has_whip, whip = optional_number(stats.get("sourceWHIP"))
    has_velocity, velocity = optional_number(stats.get("fastballVelocityKph"))
    has_velocity = has_velocity and velocity > 0.0
    return [
        metric_evidence("NegativeEarnedRunAverage", -earned_run_average, -safe_number(stats.get("earnedRuns")), innings, batters_faced, tbf_constant, has_era),
        metric_evidence("NegativeWhip", -whip, -(safe_number(stats.get("hitsAllowed")) + safe_number(stats.get("walks"))), innings, batters_faced, tbf_constant, has_whip),
        metric_evidence("StrikeoutsPerNine", ratio(stats.get("strikeouts"), innings) * 9.0, safe_number(stats.get("strikeouts")), innings, batters_faced, tbf_constant, innings > 0.0 and stats.get("strikeouts") is not None),
        metric_evidence("NegativeWalksPerNine", -ratio(stats.get("walks"), innings) * 9.0, -safe_number(stats.get("walks")), innings, batters_faced, tbf_constant, innings > 0.0 and stats.get("walks") is not None),
        metric_evidence("NegativeHomeRunsPerNine", -ratio(stats.get("homeRunsAllowed"), innings) * 9.0, -safe_number(stats.get("homeRunsAllowed")), innings, batters_faced, tbf_constant, innings > 0.0 and stats.get("homeRunsAllowed") is not None),
        metric_evidence("InningsPerGame", innings / games if games > 0.0 else 0.0, innings, games, batters_faced, tbf_constant, games > 0.0),
        metric_evidence("SaveRate", ratio(stats.get("saves"), games), safe_number(stats.get("saves")), games, batters_faced, tbf_constant, games > 0.0),
        metric_evidence("HoldRate", ratio(stats.get("holds"), games), safe_number(stats.get("holds")), games, batters_faced, tbf_constant, holds_available and games > 0.0),
        metric_evidence("SeasonInnings", innings, outs, 3.0, outs, 0.0, stats.get("inningsOuts") is not None),
        metric_evidence("FastballVelocityKph", velocity, velocity, 1.0, 1.0, 0.0, has_velocity),
    ]


def derivation_group_key(
    player: dict[str, Any],
    year: int,
    player_type: str,
    pitcher_role_availability: dict[str, bool] | None = None,
) -> str:
    if player_type == "Hitter":
        group = source_position(player, player_type)
    else:
        group, _ = derive_source_pitcher_role(player, pitcher_role_availability)
    return f"{year}:{group}"


def reference_family_key(group_key: str, player_type: str) -> str:
    """표본이 얇은 집단이 기댈 상위 비교 집단(포지션군·투수 역할군) 키를 만든다."""
    reference_config = DERIVATION_BALANCE["referencePopulation"]
    year, _, group = group_key.partition(":")
    families = (
        reference_config["positionFamilies"]
        if player_type == "Hitter"
        else reference_config["pitcherRoleFamilies"]
    )
    return f"{year}:{families.get(group, 'Default')}"


def weighted_reference_statistics(
    values: list[float],
    weights: list[float],
) -> tuple[float, float, float]:
    """표본 신뢰도를 Reference Weight로 쓴 Winsorized 가중 평균·표준편차를 만든다.

    소표본 선수를 모집단에서 제거하면 희소 포지션과 초기 연도의 비교 기준이 무너지므로
    제거하지 않고 기여도만 줄인다. Reference Weight는 개인 Shrinkage와 같은 곡선
    ``n / (n + k)``을 쓴다. 한 값이 자기 자신에 대해 갖는 증거력과 모집단에 대해 갖는
    증거력을 같은 척도로 두기 위해서다.
    """
    total_weight = sum(weights)
    if not values or total_weight <= 1e-9:
        return 0.0, 1.0, 0.0

    tail_quantile = float(DERIVATION_BALANCE["referencePopulation"]["winsorizeTailQuantile"])
    ordered = sorted(zip(values, weights), key=lambda pair: pair[0])
    lower_bound = ordered[0][0]
    upper_bound = ordered[-1][0]
    cumulative = 0.0
    for value, weight in ordered:
        cumulative += weight
        if cumulative >= tail_quantile * total_weight:
            lower_bound = value
            break
    cumulative = 0.0
    for value, weight in ordered:
        cumulative += weight
        if cumulative >= (1.0 - tail_quantile) * total_weight:
            upper_bound = value
            break
    if upper_bound < lower_bound:
        lower_bound, upper_bound = upper_bound, lower_bound

    clamped = [min(max(value, lower_bound), upper_bound) for value, _ in ordered]
    ordered_weights = [weight for _, weight in ordered]
    center = sum(value * weight for value, weight in zip(clamped, ordered_weights)) / total_weight
    variance = sum(
        weight * (value - center) ** 2 for value, weight in zip(clamped, ordered_weights)
    ) / total_weight
    deviation = math.sqrt(variance)
    return center, deviation if deviation > 1e-9 else 1.0, total_weight


def blend_reference_statistics(
    narrow: tuple[float, float, float],
    wide: tuple[float, float, float],
) -> tuple[float, float, float, float]:
    """유효 표본이 모자란 좁은 집단을 상위 집단 통계 쪽으로 연속적으로 섞는다."""
    minimum_effective = float(
        DERIVATION_BALANCE["referencePopulation"]["minimumEffectiveSampleCount"]
    )
    narrow_center, narrow_deviation, narrow_effective = narrow
    wide_center, wide_deviation, wide_effective = wide
    if wide_effective <= 1e-9:
        return narrow_center, narrow_deviation, narrow_effective, 1.0
    share = min(1.0, narrow_effective / minimum_effective) if minimum_effective > 0.0 else 1.0
    center = share * narrow_center + (1.0 - share) * wide_center
    variance = share * narrow_deviation**2 + (1.0 - share) * wide_deviation**2
    deviation = math.sqrt(variance)
    return center, deviation if deviation > 1e-9 else 1.0, narrow_effective, share


def derivation_role_tier(player: dict[str, Any], player_type: str) -> str:
    """Qualified/Limited는 비교 모집단이 아니라 표본 진단 metadata로만 남긴다."""
    role_tier = DERIVATION_BALANCE["roleTier"]
    if player_type == "Hitter":
        sample_size = safe_number((player.get("hitterStats") or {}).get("plateAppearances"))
        threshold = float(role_tier["qualifiedPlateAppearances"])
    else:
        sample_size = pitcher_batters_faced(player.get("pitcherStats") or {})
        threshold = float(role_tier["qualifiedBattersFaced"])
    return "Qualified" if sample_size >= threshold else "Limited"


def build_adjusted_feature_pool(
    players: list[dict[str, Any]],
    year: int,
    player_type: str,
    pitcher_role_availability: dict[str, bool] | None = None,
    season_games: float | None = None,
) -> tuple[dict[str, tuple[float, ...]], dict[str, dict[str, dict[str, Any]]], dict[str, str]]:
    """시대·포지션/역할 집단 Z-score에 지표별 표본 신뢰도를 적용한다."""
    metric_names = HITTER_METRIC_NAMES if player_type == "Hitter" else PITCHER_METRIC_NAMES
    evidence_by_id: dict[str, list[dict[str, Any]]] = {}
    group_by_id: dict[str, str] = {}
    role_tier_by_id: dict[str, str] = {}
    reference_group_by_id: dict[str, str] = {}
    reference_season_games = float(DERIVATION_BALANCE["costEligibility"]["referenceSeasonGames"])
    season_scale = (season_games if season_games is not None else reference_season_games) / reference_season_games
    if not math.isfinite(season_scale) or season_scale <= 0.0:
        raise ValueError("능력치 신뢰도 기준 시즌 길이는 양수여야 합니다.")
    for player in players:
        source_id = str(player.get("sourcePlayerId") or "")
        if not source_id or source_id in evidence_by_id:
            raise ValueError(f"PlayerSeason 능력치 파생 Source ID가 비었거나 중복되었습니다: {source_id}")
        evidence_by_id[source_id] = (
            hitter_metric_evidence(player)
            if player_type == "Hitter"
            else pitcher_metric_evidence(player, pitcher_role_availability)
        )
        group_by_id[source_id] = derivation_group_key(
            player,
            year,
            player_type,
            pitcher_role_availability,
        )
        role_tier_by_id[source_id] = derivation_role_tier(player, player_type)
        reference_group_by_id[source_id] = group_by_id[source_id]
        if player_type == "Hitter":
            _, position_trace = derive_source_position(player, "DH")
            defensive_position = position_trace["primaryDefensivePosition"]
            if defensive_position:
                reference_group_by_id[source_id] = f"{year}:{defensive_position}"
        # 짧은 역사 시즌에서도 같은 출전 비율은 같은 신뢰도로 평가한다.
        for component in evidence_by_id[source_id]:
            component["reliabilityConstant"] *= season_scale

    family_by_id = {
        source_id: reference_family_key(group_key, player_type)
        for source_id, group_key in reference_group_by_id.items()
    }
    type_key = f"{year}:{player_type}"

    def reference_weight(component: dict[str, Any]) -> float:
        if not component["isAvailable"]:
            return 0.0
        return reliability(
            float(component["sampleSize"]),
            float(component["reliabilityConstant"]),
        )

    def statistics_for(member_ids: list[str], metric_name: str) -> tuple[float, float, float]:
        values: list[float] = []
        weights: list[float] = []
        for source_id in member_ids:
            for component in evidence_by_id[source_id]:
                if component["metric"] != metric_name or not component["isAvailable"]:
                    continue
                values.append(float(component["rawValue"]))
                weights.append(reference_weight(component))
        return weighted_reference_statistics(values, weights)

    def members_of(scope_by_id: dict[str, str], scope_key: str) -> list[str]:
        return sorted(source_id for source_id, value in scope_by_id.items() if value == scope_key)

    all_member_ids = sorted(evidence_by_id)
    type_statistics = {
        metric_name: statistics_for(all_member_ids, metric_name)
        for metric_name in metric_names
    }
    family_statistics: dict[tuple[str, str], tuple[float, float, float]] = {}
    for family_key in sorted(set(family_by_id.values())):
        member_ids = members_of(family_by_id, family_key)
        for metric_name in metric_names:
            family_statistics[(family_key, metric_name)] = statistics_for(member_ids, metric_name)

    group_statistics: dict[tuple[str, str], tuple[float, float, float, float, float]] = {}
    for group_key in sorted(set(reference_group_by_id.values())):
        member_ids = members_of(reference_group_by_id, group_key)
        family_key = family_by_id[member_ids[0]]
        for metric_name in metric_names:
            family_center, family_deviation, family_effective, family_share = (
                blend_reference_statistics(
                    family_statistics[(family_key, metric_name)],
                    type_statistics[metric_name],
                )
            )
            center, deviation, group_effective, group_share = blend_reference_statistics(
                statistics_for(member_ids, metric_name),
                (family_center, family_deviation, family_effective),
            )
            group_statistics[(group_key, metric_name)] = (
                center,
                deviation,
                group_effective,
                group_share,
                family_share,
            )

    vectors: dict[str, tuple[float, ...]] = {}
    traces: dict[str, dict[str, dict[str, Any]]] = {}
    for source_id in sorted(evidence_by_id):
        group_key = reference_group_by_id[source_id]
        adjusted_values: list[float] = []
        component_traces: dict[str, dict[str, Any]] = {}
        by_metric = {component["metric"]: component for component in evidence_by_id[source_id]}
        for metric_name in metric_names:
            evidence = by_metric[metric_name]
            (
                center,
                deviation,
                group_effective,
                group_share,
                family_share,
            ) = group_statistics[(group_key, metric_name)]
            reference_key = group_key
            reference_config = DERIVATION_BALANCE["referencePopulation"]
            if player_type == "Pitcher" and metric_name in reference_config["pitcherFamilyMetrics"]:
                reference_key = family_by_id[source_id]
                center, deviation, group_effective, group_share = blend_reference_statistics(
                    family_statistics[(reference_key, metric_name)], type_statistics[metric_name]
                )
                family_share = group_share
            elif player_type == "Pitcher" or metric_name not in reference_config["positionMetrics"]:
                # 타격·실점 억제는 포지션과 보직이 달라도 같은 경기 척도로 비교한다.
                reference_key = type_key
                center, deviation, group_effective = type_statistics[metric_name]
                group_share = family_share = 1.0
            raw_z = (
                (float(evidence["rawValue"]) - center) / deviation
                if evidence["isAvailable"]
                else 0.0
            )
            sample_reliability = reliability(
                float(evidence["sampleSize"]),
                float(evidence["reliabilityConstant"]),
            ) if evidence["isAvailable"] else 0.0
            prior = DERIVATION_BALANCE["samplePrior"]
            prior_z = 0.0 if not evidence["isAvailable"] or metric_name in prior["neutralMetrics"] else float(prior["performanceZ"])
            limit = float(prior["maximumAbsoluteZ"])
            bounded_z = max(-limit, min(limit, raw_z))
            # 소표본은 평균 전력을 보장하지 않는다. 관측 증거가 쌓이면 보수적 사전값에서 벗어난다.
            # 시대별 수비 결측은 실제 능력 부족과 구분해 중립 사전값을 쓴다.
            adjusted_z = bounded_z * sample_reliability + prior_z * (1.0 - sample_reliability)
            anchor = DERIVATION_BALANCE["absoluteRatingAnchors"].get(metric_name)
            absolute_rating = None
            if anchor and evidence["isAvailable"]:
                # 누적 이닝과 실측 구속은 시대 Z나 소표본 보정으로 절대 기준점을 움직이지 않는다.
                fraction = (float(evidence["rawValue"]) - float(anchor["minimumValue"])) / (
                    float(anchor["maximumValue"]) - float(anchor["minimumValue"])
                )
                absolute_rating = float(anchor["minimumRating"]) + max(0.0, min(1.0, fraction)) * (
                    float(anchor["maximumRating"]) - float(anchor["minimumRating"])
                )
            adjusted_values.append(adjusted_z)
            component_traces[metric_name] = {
                **evidence,
                "roleTier": role_tier_by_id[source_id],
                "referenceGroupKey": reference_key,
                "seasonLengthScale": round(season_scale, 8),
                "priorZ": prior_z,
                "boundedZ": round(bounded_z, 8),
                "referenceWeight": round(reference_weight(evidence), 8),
                "referenceFamilyKey": family_by_id[source_id],
                "referenceEffectiveSampleCount": round(group_effective, 8),
                "referenceGroupShare": round(group_share, 8),
                "referenceFamilyShare": round(family_share, 8),
                "groupMean": round(center, 8),
                "groupStdDev": round(deviation, 8),
                "rawZ": round(raw_z, 8),
                "reliability": round(sample_reliability, 8),
                "adjustedZ": round(adjusted_z, 8),
                "absoluteRating": absolute_rating,
                "absoluteAnchor": anchor,
            }
        vectors[source_id] = tuple(adjusted_values)
        traces[source_id] = component_traces
    return vectors, traces, group_by_id


def position_from_source(player: dict[str, Any], fallback: str) -> str:
    position, _ = derive_source_position(player, fallback)
    return position


def derive_source_position(
    player: dict[str, Any],
    fallback: str,
) -> tuple[str, dict[str, Any]]:
    """해당 시즌 수비 기록만 사용해 Natural Position과 근거를 만든다."""
    candidates: list[dict[str, Any]] = []
    for record in player.get("defenseRecords") or []:
        source_position_name = str(record.get("position") or "")
        position = SOURCE_POSITION_MAP.get(source_position_name)
        if position is None:
            continue
        candidates.append(
            {
                "position": position,
                "sourcePosition": source_position_name,
                "inningsOuts": safe_number(record.get("inningsOuts")),
                "gamesStarted": safe_number(record.get("gamesStarted")),
                "games": safe_number(record.get("games")),
            }
        )

    candidates.sort(
        key=lambda candidate: (
            -candidate["inningsOuts"],
            -candidate["gamesStarted"],
            -candidate["games"],
            HITTER_POSITIONS.index(candidate["position"]),
        )
    )
    primary_defensive_position = next(
        (candidate["position"] for candidate in candidates if candidate["position"] != "DH"), ""
    )
    selected = candidates[0]["position"] if candidates else fallback
    reason = (
        "해당 SeasonYear의 수비 이닝, 선발 경기, 출전 경기 순으로 선택"
        if candidates
        else f"해당 SeasonYear의 수비 기록이 없어 {fallback} fallback"
    )
    stats = player.get("hitterStats") or {}
    games = safe_number(stats.get("games"))
    defensive_outs = sum(candidate["inningsOuts"] for candidate in candidates if candidate["position"] != "DH")
    settings = DERIVATION_BALANCE["positionClassifier"]
    is_inferred_dh = (
        bool(primary_defensive_position)
        and safe_number(stats.get("plateAppearances")) >= float(settings["minimumDesignatedHitterPlateAppearances"])
        and games > 0.0
        and defensive_outs / (3.0 * games) < float(settings["maximumDefensiveInningsPerGame"])
    )
    if is_inferred_dh:
        selected = "DH"
        reason = "충분한 타격 출전 대비 수비 이닝 비율이 낮아 DH 중심 시즌으로 추정; 주수비 위치 근거는 별도 보존"
    return selected, {
        "classifierVersion": POSITION_ROLE_CLASSIFIER_VERSION,
        "positionCandidates": candidates,
        "selectedNaturalPosition": selected,
        "primaryDefensivePosition": primary_defensive_position,
        "isDesignatedHitterInferred": is_inferred_dh,
        "defensiveInningsPerGame": round(defensive_outs / (3.0 * games), 8) if games > 0.0 else None,
        "reason": reason,
    }


def hitter_features(player: dict[str, Any]) -> tuple[float, ...]:
    return tuple(
        safe_number(component["rawValue"])
        for component in hitter_metric_evidence(player)
    )


def pitcher_features(player: dict[str, Any]) -> tuple[float, ...]:
    return tuple(
        safe_number(component["rawValue"])
        for component in pitcher_metric_evidence(player)
    )


def normalized_pool(players: list[dict[str, Any]], feature_fn) -> tuple[list[tuple[float, ...]], list[float], list[float]]:
    features = [feature_fn(player) for player in players]
    width = len(features[0])
    centers = [mean(row[index] for row in features) for index in range(width)]
    deviations = []
    for index in range(width):
        values = [row[index] for row in features]
        deviation = statistics.pstdev(values) if len(values) > 1 else 1.0
        deviations.append(deviation if deviation > 1e-9 else 1.0)
    normalized = [
        tuple((row[index] - centers[index]) / deviations[index] for index in range(width))
        for row in features
    ]
    return normalized, centers, deviations


def to_ratings(player_type: str, vector: tuple[float, ...]) -> list[int]:
    values, _ = to_ratings_with_trace(player_type, vector)
    return values


def to_ratings_with_trace(
    player_type: str,
    vector: tuple[float, ...],
    components: dict[str, dict[str, Any]] | None = None,
    player_season_id: str = "",
    season_year: int = 0,
    group_key: str = "",
) -> tuple[list[int], list[dict[str, Any]]]:
    """Adjusted Z component를 BaseAttributes와 Editor 파생 근거로 함께 변환한다."""
    values = [50] * len(ABILITY_NAMES)
    metric_names = HITTER_METRIC_NAMES if player_type == "Hitter" else PITCHER_METRIC_NAMES
    metric_values = dict(zip(metric_names, vector))
    profiles = DERIVATION_BALANCE["ratingProfiles"][player_type]
    rating_center = float(DERIVATION_BALANCE["rating"]["center"])
    traces: list[dict[str, Any]] = []
    role_tier = next(
        (
            str(component.get("roleTier"))
            for component in (components or {}).values()
            if component.get("roleTier")
        ),
        "Unknown",
    )
    for attribute, profile in profiles.items():
        attribute_components = []
        combined_z = 0.0
        available_weight = sum(
            float(weight) for metric, weight in profile["metrics"].items()
            if components is None or (components.get(metric) or {}).get("isAvailable", False)
        )
        for metric_name, weight_value in profile["metrics"].items():
            is_available = components is None or (components.get(metric_name) or {}).get("isAvailable", False)
            weight = float(weight_value) / available_weight if is_available and available_weight > 0.0 else 0.0
            contribution = metric_values[metric_name] * weight
            combined_z += contribution
            component = dict((components or {}).get(metric_name) or {
                "metric": metric_name,
                "rawValue": None,
                "numerator": None,
                "denominator": None,
                "sampleSize": 0.0,
                "groupMean": 0.0,
                "groupStdDev": 1.0,
                "rawZ": 0.0,
                "reliability": 0.0,
                "adjustedZ": round(metric_values[metric_name], 8),
                "isAvailable": False,
            })
            component["weight"] = weight
            component["configuredWeight"] = float(weight_value)
            component["contribution"] = round(contribution, 8)
            component["priorContribution"] = round(
                float(component.get("priorZ", 0.0)) * (1.0 - float(component.get("reliability", 0.0))) * weight, 8
            )
            component["observedContribution"] = round(contribution - component["priorContribution"], 8)
            attribute_components.append(component)
        rating_before_clamp = rating_center + float(profile["scale"]) * combined_z
        absolute_components = [component for component in attribute_components
                               if component.get("absoluteRating") is not None and component["weight"] > 0.0]
        if absolute_components:
            rating_before_clamp = sum(float(component["absoluteRating"]) * component["weight"]
                                     for component in absolute_components)
        rating_after_clamp = clamp_rating(rating_before_clamp)
        values[ABILITY_NAMES.index(attribute)] = rating_after_clamp
        traces.append(
            {
                "playerSeasonId": player_season_id,
                "seasonYear": season_year,
                "attribute": attribute,
                "groupKey": group_key,
                "roleTier": role_tier,
                "components": attribute_components,
                "combinedZ": round(combined_z, 8),
                "ratingBeforeClamp": round(rating_before_clamp, 8),
                "ratingAfterClamp": rating_after_clamp,
                "evaluationMethod": "AbsoluteRecordAnchor" if absolute_components else (
                    "AvailableMetrics" if available_weight > 0.0 else "NeutralWithoutEvidence"
                ),
            }
        )
    return values, traces


def build_ability_validation_warnings(
    ability_trace: list[dict[str, Any]],
) -> list[dict[str, Any]]:
    """낮은 표본 component가 Ability 결론을 실제로 지배할 때만 경고한다."""
    validation = DERIVATION_BALANCE["validation"]
    reliability_maximum = float(validation["abilityLowSampleReliabilityMaximum"])
    share_minimum = float(validation["abilityDominantContributionShareMinimum"])
    contribution_minimum = float(validation["abilityDominantContributionAbsoluteMinimum"])
    warnings: list[dict[str, Any]] = []
    for trace in ability_trace:
        components = trace.get("components") or []
        absolute_total = sum(abs(float(component.get("observedContribution", component["contribution"]))) for component in components)
        if absolute_total <= 1e-9:
            continue
        for component in components:
            contribution = abs(float(component.get("observedContribution", component["contribution"])))
            contribution_share = contribution / absolute_total
            if (
                float(component["reliability"]) <= reliability_maximum
                and contribution >= contribution_minimum
                and contribution_share >= share_minimum
            ):
                warnings.append(
                    {
                        "code": "ABILITY_LOW_SAMPLE_DOMINANCE",
                        "attribute": trace["attribute"],
                        "metric": component["metric"],
                        "reliability": component["reliability"],
                        "contribution": component.get("observedContribution", component["contribution"]),
                        "contributionShare": round(contribution_share, 8),
                    }
                )
    return warnings


def build_metric_influence_warnings(audit: dict[str, Any]) -> list[dict[str, Any]]:
    """설정 오류가 허용된 진단 경로에서도 Raw metric 중복 지배를 명시적으로 남긴다."""
    return [
        {
            "code": "ABILITY_METRIC_INFLUENCE_CAP_EXCEEDED",
            "playerType": audit["playerType"],
            "roleProfile": audit["roleProfile"],
            "metric": metric["metric"],
            "normalizedInfluence": metric["normalizedInfluence"],
            "maximumAllowed": metric["maximumAllowed"],
        }
        for metric in audit["metrics"]
        if metric["exceedsMaximum"]
    ]


def role_composite_weights(season: dict[str, Any]) -> tuple[str, list[float]]:
    player_type = str(season["playerType"])
    profiles = DERIVATION_BALANCE["roleCompositeProfiles"][player_type]
    if player_type == "Hitter":
        profile_name = str(season.get("position") or "Default")
    else:
        profile_name = str(season.get("pitcherRole") or "Default")
    if profile_name not in profiles:
        profile_name = "Default"
    return profile_name, [float(weight) for weight in profiles[profile_name]]


def role_adjusted_composite(season: dict[str, Any]) -> tuple[float, dict[str, Any]]:
    """오프라인 밸런스의 역할별 수치 가중치로 가격 기준 종합 능력치를 계산한다."""
    player_type = str(season["playerType"])
    if player_type == "Hitter":
        ability_names = ABILITY_NAMES[:6]
        ratings = season["baseAttributes"][:6]
        role = str(season.get("position") or "Default")
    else:
        ability_names = ABILITY_NAMES[6:]
        ratings = season["baseAttributes"][6:]
        role = str(season.get("pitcherRole") or "Default")
    profile_name, weights = role_composite_weights(season)
    total_weight = sum(weights)
    contributions = [
        {
            "ability": ability_name,
            "rating": int(rating),
            "weight": weight,
            "normalizedWeight": round(weight / total_weight, 8),
            "contribution": round(float(rating) * weight / total_weight, 8),
        }
        for ability_name, rating, weight in zip(ability_names, ratings, weights)
    ]
    composite = sum(component["contribution"] for component in contributions)
    metric_influence_audit = metric_composite_influence_audit(player_type, profile_name)
    return composite, {
        "baseAttributes": list(season["baseAttributes"]),
        "role": role,
        "roleProfile": profile_name,
        "roleWeights": [
            {"ability": ability_name, "weight": weight}
            for ability_name, weight in zip(ability_names, weights)
        ],
        "abilityContribution": contributions,
        "metricInfluenceAudit": metric_influence_audit,
        "composite": round(composite, 8),
        "originYear": int(season["originYear"]),
        "populationCount": 0,
        "rank": 0,
        "percentile": 0.0,
        "cost": 0,
    }


def cost_eligibility_sample_thresholds(season: dict[str, Any]) -> tuple[str, dict[str, float]]:
    """시즌 길이와 역할에 맞춘 출전량 진단 기준을 만든다. 가격 상한으로 쓰지 않는다."""
    settings = DERIVATION_BALANCE["costEligibility"]
    reference_games = float(settings["referenceSeasonGames"])
    season_games = float(season.get("sourceSeasonGames", reference_games))
    if not math.isfinite(season_games) or season_games <= 0.0:
        raise ValueError("출전량 진단의 시즌 경기 수가 유효하지 않습니다.")
    scale = season_games / reference_games
    if str(season["playerType"]) == "Hitter":
        scope, thresholds = "Hitter", settings["hitterSampleThresholds"]
    else:
        families = DERIVATION_BALANCE["referencePopulation"]["pitcherRoleFamilies"]
        scope = families.get(str(season.get("pitcherRole") or "Default"), "Relief")
        thresholds = settings["pitcherSampleThresholds"][scope]
    return scope, {key: float(value) * scale for key, value in thresholds.items()}


def cost_eligibility_tier(season: dict[str, Any]) -> dict[str, Any]:
    """출전량 설명용 metadata다. 능력치의 신뢰도 보정을 가격에서 중복 할인하지 않는다."""
    scope, thresholds = cost_eligibility_sample_thresholds(season)
    has_sample, sample = optional_number(season.get("costEligibilitySample"))
    if not has_sample:
        sample = -1.0
    tier = "Full" if sample >= thresholds["Full"] else "Regular" if sample >= thresholds["Regular"] else "Limited" if sample >= thresholds["Limited"] else "Tiny"
    workload_ratio = min(1.0, max(0.0, sample / thresholds["Full"]))
    return {
        "tier": tier,
        "scope": scope,
        "sample": round(sample, 8),
        "workloadRatio": round(workload_ratio, 8),
        "maximumCost": 10,
        "affectsCost": False,
        "fullSeasonSample": thresholds["Full"],
        "reason": (
            f"{scope} 출전량 {sample:.0f}은 해당 시즌 기준의 {workload_ratio * 100:.0f}%입니다. "
            "신뢰도는 능력치에 반영했으며 Cost는 별도로 할인하지 않습니다."
            if has_sample else "출전량 표본이 없습니다. Cost는 기본 전력으로 계산합니다."
        ),
    }


def cost_metric_evidence(season: dict[str, Any]) -> tuple[dict[str, float], dict[str, float]]:
    """Ability Trace에서 중복 없이 시대·표본 보정된 Cost 지표를 읽는다."""
    values: dict[str, float] = {}
    reliabilities: dict[str, float] = {}
    for attribute in season.get("abilityDerivationTrace") or []:
        for component in attribute.get("components") or []:
            metric = str(component.get("metric") or "")
            if not metric or not component.get("isAvailable", False):
                continue
            value = safe_number(component.get("adjustedZ"), float("nan"))
            reliability = safe_number(component.get("reliability"), float("nan"))
            if not math.isfinite(value) or not math.isfinite(reliability):
                raise ValueError("Cost Quality 지표가 유한하지 않습니다.")
            if metric in values and abs(values[metric] - value) > 1e-8:
                raise ValueError(f"같은 Cost 지표의 adjustedZ가 일치하지 않습니다: {metric}")
            values[metric] = value
            reliabilities[metric] = reliability
    return values, reliabilities


def workload_curve(sample: float, target: float) -> float:
    """표본 목표를 넘긴 내구성도 제한적으로 인정하는 완만한 workload 곡선이다."""
    if not math.isfinite(sample) or not math.isfinite(target) or target <= 0.0:
        raise ValueError("Cost Workload 입력이 유효하지 않습니다.")
    return math.sqrt(min(1.25, max(0.0, sample / target)))


def derive_player_value_components(
    season: dict[str, Any],
    legacy_composite: float,
) -> dict[str, Any]:
    """Source season의 quality·workload·수비를 Cost 단위로 분리한다."""
    settings = DERIVATION_BALANCE["costValueModel"]
    player_type = str(season["playerType"])
    profile = settings["qualityProfiles"][player_type]
    metrics, reliabilities = cost_metric_evidence(season)
    available_profile = {metric: float(weight) for metric, weight in profile.items() if metric in metrics}
    total_weight = sum(available_profile.values())
    available_profile = {metric: weight / total_weight for metric, weight in available_profile.items()} if total_weight else {}
    has_metric_evidence = bool(available_profile)
    if has_metric_evidence:
        quality_contributions = [
            {
                "metric": metric,
                "adjustedZ": round(metrics[metric], 8),
                "reliability": round(reliabilities[metric], 8),
                "weight": float(weight),
                "contribution": round(metrics[metric] * float(weight), 8),
            }
            for metric, weight in available_profile.items()
        ]
        quality = sum(row["contribution"] for row in quality_contributions)
        reliability = sum(
            reliabilities[metric] * float(weight) for metric, weight in available_profile.items()
        )
        quality_origin = "AvailableAdjustedSourceMetrics"
    else:
        # Source가 아닌 작은 단위 fixture와 명시적 대체 데이터만 쓰는 보수적 경로다.
        quality = 0.0 if season.get("abilityDerivationTrace") else (
            legacy_composite - float(DERIVATION_BALANCE["rating"]["center"])
        ) / 20.0
        quality_contributions = []
        reliability = 0.0
        quality_origin = "AbilityProxyWithoutSourceMetrics"

    inputs = season.get("_costValueInputs") or {}
    season_games = float(season.get("sourceSeasonGames", settings.get("referenceSeasonGames", 144.0)))
    season_scale = season_games / float(DERIVATION_BALANCE["costEligibility"]["referenceSeasonGames"])
    base_score = float(settings["baseScore"])
    quality_score = quality * float(settings["qualityMultiplier"])
    defensive_value = 0.0
    if player_type == "Hitter":
        hitter = settings["hitterWorkload"]
        plate_appearances = max(
            0.0,
            safe_number(inputs.get("plateAppearances"), safe_number(season.get("costEligibilitySample"))),
        )
        workload_target = float(hitter["plateAppearanceTarget"]) * season_scale
        workload_ratio = plate_appearances / workload_target if workload_target > 0.0 else 0.0
        primary_workload = workload_curve(plate_appearances, workload_target)
        workload_score = primary_workload * float(hitter["weight"])
        defensive_outs = max(0.0, safe_number(inputs.get("defensiveInningsOuts")))
        defensive_target = season_games * float(hitter["defensiveInningsTargetPerGame"]) * 3.0
        defensive_ratio = min(1.0, defensive_outs / defensive_target) if defensive_target > 0.0 else 0.0
        defense = float(season["baseAttributes"][ABILITY_INDEX["Defense"]])
        arm = float(season["baseAttributes"][ABILITY_INDEX["Arm"]])
        defense_signal = max(-1.0, min(1.0, (0.75 * defense + 0.25 * arm - 55.0) / 20.0))
        defensive_value = (
            float(hitter["defensiveValueMaximum"])
            * math.sqrt(defensive_ratio)
            * defense_signal
        )
        workload_trace = {
            "kind": "PlateAppearances",
            "sample": round(plate_appearances, 8),
            "target": round(workload_target, 8),
            "ratio": round(workload_ratio, 8),
            "curve": round(primary_workload, 8),
            "defensiveInningsOuts": round(defensive_outs, 8),
            "defensiveWorkloadRatio": round(defensive_ratio, 8),
        }
        role_group = str(season.get("position") or "Default")
    else:
        pitcher = settings["pitcherWorkload"]
        innings_outs = max(0.0, safe_number(inputs.get("inningsOuts"), safe_number(season.get("costEligibilitySample"))))
        innings = innings_outs / 3.0
        games = max(0.0, safe_number(inputs.get("games")))
        games_started = max(0.0, safe_number(inputs.get("gamesStarted")))
        games_started_available = bool(inputs.get("gamesStartedAvailable"))
        inferred_starter_rate = max(0.0, min(1.0, safe_number(inputs.get("inferredStarterRate"))))
        if games_started_available and games > 0.0:
            starter_share = min(1.0, games_started / games)
            starter_share_origin = "SourceGamesStartedRate"
        elif "inferredStarterRate" in inputs:
            starter_share = inferred_starter_rate
            evidence_mode = str(inputs.get("starterEvidenceMode") or "RoleClassifierProxy")
            starter_share_origin = f"Inferred:{evidence_mode}"
        else:
            starter_share = 1.0 if str(season.get("pitcherRole")) == "Starter" else 0.0
            starter_share_origin = "NaturalRoleFallback"
        role_target = (
            float(pitcher["reliefInningsTarget"])
            + float(pitcher["starterInningsIncrement"]) * starter_share
        ) * season_scale
        absolute_target = float(pitcher["absoluteInningsTarget"]) * season_scale
        workload_ratio = innings / role_target if role_target > 0.0 else 0.0
        role_curve = workload_curve(innings, role_target)
        absolute_curve = workload_curve(innings, absolute_target)
        workload_score = (
            role_curve * float(pitcher["roleTargetWeight"])
            + absolute_curve * float(pitcher["absoluteTargetWeight"])
            + starter_share * float(pitcher["starterShareWeight"])
        )
        workload_trace = {
            "kind": "Innings",
            "sample": round(innings, 8),
            "roleTarget": round(role_target, 8),
            "absoluteTarget": round(absolute_target, 8),
            "ratio": round(workload_ratio, 8),
            "roleCurve": round(role_curve, 8),
            "absoluteCurve": round(absolute_curve, 8),
            "starterShare": round(starter_share, 8),
            "starterShareOrigin": starter_share_origin,
            "starterShareScore": round(
                starter_share * float(pitcher["starterShareWeight"]), 8
            ),
        }
        role_group = "Rotation" if starter_share >= 0.35 else "Relief"

    raw_value = base_score + workload_score + quality_score + defensive_value
    return {
        "quality": round(quality, 8),
        "qualityOrigin": quality_origin,
        "excludedMissingMetrics": [metric for metric in profile if metric not in metrics],
        "qualityContributions": quality_contributions,
        "reliability": round(reliability, 8),
        "baseScore": round(base_score, 8),
        "qualityScore": round(quality_score, 8),
        "workloadScore": round(workload_score, 8),
        "defensiveValue": round(defensive_value, 8),
        "workload": workload_trace,
        "roleGroup": role_group,
        "rawValue": round(raw_value, 8),
    }


def midpoint_percentile(value: float, ordered_values: list[float]) -> float:
    """동률을 Stable ID가 아닌 같은 midpoint rank로 처리한다."""
    if not ordered_values:
        raise ValueError("Cost percentile 모집단이 비어 있습니다.")
    left = bisect.bisect_left(ordered_values, value)
    right = bisect.bisect_right(ordered_values, value)
    return (left + right) / (2.0 * len(ordered_values))


def elite_cost_ceiling(components: dict[str, Any]) -> tuple[int, dict[str, Any]]:
    """일반 Cost 1~8과 분리해 9/10의 quality·workload·reliability를 검사한다."""
    settings = DERIVATION_BALANCE["costValueModel"]["eliteEligibility"]
    quality = float(components["quality"])
    workload_ratio = float(components["workload"]["ratio"])
    reliability = float(components["reliability"])
    checks: dict[str, Any] = {}
    ceiling = 8
    for cost, key in ((9, "cost9"), (10, "cost10")):
        gate = settings[key]
        passed = (
            quality >= float(gate["minimumQuality"])
            and workload_ratio >= float(gate["minimumWorkloadRatio"])
            and reliability >= float(gate["minimumReliability"])
        )
        checks[key] = {
            "passed": passed,
            "minimumQuality": float(gate["minimumQuality"]),
            "minimumWorkloadRatio": float(gate["minimumWorkloadRatio"]),
            "minimumReliability": float(gate["minimumReliability"]),
        }
        if passed:
            ceiling = cost
    return ceiling, {
        "maximumCost": ceiling,
        "quality": quality,
        "workloadRatio": workload_ratio,
        "reliability": reliability,
        "checks": checks,
    }


def assign_origin_year_costs(seasons: list[dict[str, Any]]) -> None:
    """시즌 quality·workload·역할 맥락과 별도 elite gate로 Canonical Cost를 정한다."""
    by_population: dict[
        tuple[int, str],
        list[tuple[dict[str, Any], float, dict[str, Any], dict[str, Any]]],
    ] = {}
    for season in seasons:
        legacy_composite, trace = role_adjusted_composite(season)
        components = derive_player_value_components(season, legacy_composite)
        key = (int(season["originYear"]), str(season["playerType"]))
        by_population.setdefault(key, []).append(
            (season, float(components["rawValue"]), components, trace)
        )

    role_settings = DERIVATION_BALANCE["costValueModel"]["roleNormalization"]
    minimum_role_count = int(role_settings["minimumGroupCount"])
    maximum_role_adjustment = float(role_settings["maximumAdjustment"])
    value_thresholds = DERIVATION_BALANCE["costValueModel"]["valueTierThresholds"]

    for year, player_type in sorted(by_population):
        population = by_population[(year, player_type)]
        raw_values = sorted(entry[1] for entry in population)
        role_values: dict[str, list[float]] = {}
        for _, raw_value, components, _ in population:
            role_values.setdefault(str(components["roleGroup"]), []).append(raw_value)
        for values in role_values.values():
            values.sort()

        adjusted_population = []
        for season, raw_value, components, trace in population:
            type_percentile = midpoint_percentile(raw_value, raw_values)
            group_values = role_values[str(components["roleGroup"])]
            if len(group_values) >= minimum_role_count:
                role_percentile = midpoint_percentile(raw_value, group_values)
                role_adjustment = max(
                    -maximum_role_adjustment,
                    min(
                        maximum_role_adjustment,
                        (role_percentile - type_percentile) * 2.0 * maximum_role_adjustment,
                    ),
                )
                role_origin = "RoleMidpointPercentile"
            else:
                role_percentile = type_percentile
                role_adjustment = 0.0
                role_origin = "TypeFallbackSmallRolePopulation"
            continuous_value = raw_value + role_adjustment
            components["typePercentile"] = round(type_percentile, 8)
            components["rolePercentile"] = round(role_percentile, 8)
            components["rolePopulationCount"] = len(group_values)
            components["roleAdjustment"] = round(role_adjustment, 8)
            components["roleAdjustmentOrigin"] = role_origin
            components["continuousValue"] = round(continuous_value, 8)
            adjusted_population.append((season, continuous_value, components, trace))

        ranked = sorted(
            adjusted_population,
            key=lambda entry: (entry[1], str(entry[0]["playerSeasonId"])),
        )
        count = len(ranked)
        threshold_rows = []
        for threshold in DERIVATION_BALANCE["costPercentileThresholds"]:
            upper_exclusive = float(threshold["upperExclusive"])
            boundary_index = min(
                count - 1,
                max(0, math.ceil(min(1.0, upper_exclusive) * count) - 1),
            )
            threshold_rows.append(
                {
                    "upperExclusive": upper_exclusive,
                    "cost": int(threshold["cost"]),
                    "sourceValueAtBoundary": round(ranked[boundary_index][1], 8),
                }
            )
        for zero_based_rank, (season, continuous_value, components, trace) in enumerate(ranked):
            percentile = (zero_based_rank + 0.5) / count
            raw_percentile_cost = percentile_cost(zero_based_rank, count)
            elite_ceiling, elite_trace = elite_cost_ceiling(components)
            cost = resolve_value_cost(
                continuous_value,
                ((float(row["upperExclusive"]), int(row["cost"])) for row in value_thresholds),
                elite_ceiling,
            )
            eligibility_trace = cost_eligibility_tier(season)
            eligibility_trace["maximumCost"] = elite_ceiling
            eligibility_trace["affectsCost"] = True
            eligibility_trace["workloadRatio"] = components["workload"]["ratio"]
            eligibility_trace["reason"] = "시즌 workload는 연속 SeasonValue와 9/10 자격에 반영됩니다."
            season["cost"] = cost
            trace["legacyAbilityComposite"] = trace.pop("composite")
            trace["componentScores"] = components
            trace["composite"] = round(continuous_value, 8)
            trace["continuousValue"] = round(continuous_value, 8)
            trace["populationCount"] = count
            trace["dataProvenance"] = "SourceBacked"
            trace["costPopulationSource"] = f"OriginYear{player_type}SourceBacked"
            trace["sourcePopulationSize"] = count
            trace["replacementExcludedFromThresholdCalculation"] = True
            trace["thresholds"] = threshold_rows
            trace["rank"] = zero_based_rank + 1
            trace["percentile"] = round(percentile, 8)
            trace["rawPercentileCost"] = raw_percentile_cost
            trace["costMethod"] = "SeasonValueOrdinalWithEliteGate"
            trace["compositeThresholds"] = value_thresholds
            trace["costEligibility"] = eligibility_trace
            trace["eliteEligibility"] = elite_trace
            trace["balanceVersion"] = DERIVATION_BALANCE_VERSION
            trace["cost"] = cost
            season["costDerivationTrace"] = trace
            season.pop("_costValueInputs", None)
            metric_warnings = build_metric_influence_warnings(trace["metricInfluenceAudit"])
            if metric_warnings:
                existing = [
                    warning
                    for warning in season.get("derivationWarnings") or []
                    if warning.get("code") != "ABILITY_METRIC_INFLUENCE_CAP_EXCEEDED"
                ]
                season["derivationWarnings"] = existing + metric_warnings


def source_player_type(player: dict[str, Any]) -> str:
    """Normalized 원본에서 실제 출전량이 존재하는 선수 유형을 결정한다."""
    pitcher_outs = safe_number((player.get("pitcherStats") or {}).get("inningsOuts"))
    plate_appearances = safe_number((player.get("hitterStats") or {}).get("plateAppearances"))
    if pitcher_outs > 0 and (plate_appearances <= 0 or pitcher_outs >= plate_appearances):
        return "Pitcher"
    return "Hitter"


def source_position(player: dict[str, Any], player_type: str) -> str:
    return "P" if player_type == "Pitcher" else position_from_source(player, "DH")


def source_pitcher_role(player: dict[str, Any]) -> str:
    role, _ = derive_source_pitcher_role(player)
    return role


def derive_pitcher_role_availability(players: list[dict[str, Any]]) -> dict[str, bool]:
    """리그 합계의 야구 규칙 모순으로 시즌별 역할 세부 기록의 가용성을 판정한다."""
    pitcher_stats = [
        player.get("pitcherStats") or {}
        for player in players
        if source_player_type(player) == "Pitcher"
    ]
    games_started = sum(safe_number(stats.get("gamesStarted")) for stats in pitcher_stats)
    complete_games = sum(safe_number(stats.get("completeGames")) for stats in pitcher_stats)
    games_finished = sum(safe_number(stats.get("gamesFinished")) for stats in pitcher_stats)
    saves = sum(safe_number(stats.get("saves")) for stats in pitcher_stats)
    holds = sum(safe_number(stats.get("holds")) for stats in pitcher_stats)
    return {
        # CG > 0인데 리그 GS 합계가 0인 시즌은 KBO Detail의 0이 실제 0이 아니라 미제공값이다.
        "gamesStarted": games_started > 0 or complete_games <= 0,
        # SV > 0인데 GF 합계가 0인 경우도 같은 방식으로 미제공값을 구분한다.
        "gamesFinished": games_finished > 0 or saves <= 0,
        # HLD가 리그 전체 0이면 Setup 판정 근거로 사용하지 않고 평균 방향으로 둔다.
        "holds": holds > 0,
    }


def derive_source_pitcher_role(
    player: dict[str, Any],
    availability: dict[str, bool] | None = None,
) -> tuple[str, dict[str, Any]]:
    """해당 시즌의 실제 등판 패턴으로 Natural PitcherRole과 근거를 만든다."""
    stats = player.get("pitcherStats") or {}
    games = safe_number(stats.get("games"))
    availability = availability or {
        "gamesStarted": True,
        "gamesFinished": True,
        "holds": True,
    }
    games_started_available = bool(availability.get("gamesStarted", True))
    games_finished_available = bool(availability.get("gamesFinished", True))
    holds_available = bool(availability.get("holds", True))
    games_started = min(games, safe_number(stats.get("gamesStarted"))) if games > 0 else 0.0
    complete_games = safe_number(stats.get("completeGames"))
    games_finished = safe_number(stats.get("gamesFinished"))
    saves = safe_number(stats.get("saves"))
    holds = safe_number(stats.get("holds"))
    innings = safe_number(stats.get("inningsOuts")) / 3.0
    config = PITCHER_ROLE_CLASSIFIER_CONFIG
    innings_per_game = ratio(innings, games)
    if games_started_available:
        game_started_rate = ratio(games_started, games)
        relief_appearances = max(0.0, games - games_started)
        relief_rate = ratio(relief_appearances, games)
        innings_per_start = ratio(innings, games_started)
        inferred_starter_rate = game_started_rate
        starter_evidence_mode = "GamesStarted"
    else:
        inferred_starter_rate = min(
            1.0,
            innings_per_game / float(config["legacyStarterTargetInningsPerGame"]),
        )
        game_started_rate = 0.0
        relief_appearances = games
        relief_rate = max(0.0, 1.0 - inferred_starter_rate)
        innings_per_start = innings_per_game
        starter_evidence_mode = "CompleteGamesAndInningsPerGameProxy"
    saves_per_relief = ratio(saves, relief_appearances)
    holds_per_relief = ratio(holds, relief_appearances) if holds_available else 0.0
    games_finished_rate = ratio(games_finished, games) if games_finished_available else 0.0

    score_weights = config["roleScoreWeights"]
    starter_factors = {
        "gamesStartedRate": inferred_starter_rate,
        "inningsPerStart": min(1.0, innings_per_start / float(config["starterTargetInningsPerStart"])),
    }
    bullpen_factors = {
        "reliefRate": relief_rate,
        "inningsPerGame": min(1.0, innings_per_game / float(config["longReliefInningsPerGame"])),
    }
    setup_factors = {
        "holdsPerRelief": min(1.0, holds_per_relief / float(config["setupTargetHoldsPerRelief"])),
        "reliefRate": relief_rate,
        "gamesFinishedRate": games_finished_rate,
    }
    closer_factors = {
        "savesPerRelief": min(1.0, saves_per_relief / float(config["closerTargetSavesPerRelief"])),
        "gamesFinishedRate": games_finished_rate,
        "reliefRate": relief_rate,
    }
    factors_by_role = {
        "Starter": starter_factors,
        "Bullpen": bullpen_factors,
        "Setup": setup_factors,
        "Closer": closer_factors,
    }
    role_scores = {
        role: 100.0 * sum(factors[name] * float(weight) for name, weight in score_weights[role].items())
        for role, factors in factors_by_role.items()
    }
    official_role = str(player.get("seasonPitcherRole") or stats.get("pitcherRole") or "").strip()
    supported_roles = {"Starter", "Swingman", "LongRelief", "MiddleRelief", "Setup", "Closer"}
    missing_usage_sources = [
        source_name
        for source_name, is_available in (
            ("GamesStarted", games_started_available),
            ("GamesFinished", games_finished_available),
            ("Holds", holds_available),
        )
        if not is_available
    ]
    if games < float(config["lowConfidenceGames"]) or missing_usage_sources:
        role_confidence = "Low"
        confidence_reason = (
            "역할 판정 표본이 작거나 GS/GF/HLD 원천이 없어 proxy를 사용"
        )
    elif games >= float(config["highConfidenceGames"]):
        role_confidence = "High"
        confidence_reason = (
            "직접 기용 기록이 모두 제공되고 High confidence 최소 등판을 충족"
        )
    else:
        role_confidence = "Medium"
        confidence_reason = (
            "직접 기용 기록은 제공되지만 High confidence 최소 등판에는 미달"
        )
    if official_role in supported_roles:
        selected = official_role
        reason = "Normalized season record의 공식 역할 필드를 우선 사용"
    elif games_started_available and (
        games_started >= config["minimumStarterGames"]
        and game_started_rate >= config["minimumStarterGameRate"]
    ):
        selected = "Starter"
        reason = "GS 최소 경기와 GS/G 임계값을 모두 충족"
    elif not games_started_available and (
        innings_per_game >= float(config["legacyStarterMinimumInningsPerGame"])
        or (
            complete_games >= float(config["legacyStarterCompleteGameMinimum"])
            and innings_per_game >= float(config["legacyStarterCompleteGameInningsPerGame"])
        )
    ):
        selected = "Starter"
        reason = "GS 미제공 시즌이라 CG와 IP/G의 시대 fallback 기준을 충족"
    elif saves >= max(config["minimumCloserSaves"], holds):
        selected = "Closer"
        reason = "SV가 마무리 최소 표본을 충족하고 HLD 이상"
    elif holds >= config["minimumSetupHolds"]:
        selected = "Setup"
        reason = "HLD가 셋업 최소 표본을 충족"
    elif games > 0 and innings_per_game >= config["longReliefInningsPerGame"]:
        selected = "LongRelief"
        reason = "선발 기준 미달이며 경기당 투구 이닝이 LongRelief 임계값 이상"
    else:
        selected = "MiddleRelief"
        reason = "선발/마무리/셋업/LongRelief 신호가 부족해 일반 Bullpen으로 분류"

    warnings: list[dict[str, Any]] = []
    if role_confidence == "Low":
        warnings.append(
            {
                "code": "PITCHER_ROLE_LOW_CONFIDENCE",
                "message": confidence_reason,
                "games": games,
                "missingUsageSources": missing_usage_sources,
            }
        )
    return selected, {
        "classifierVersion": POSITION_ROLE_CLASSIFIER_VERSION,
        "pitcherRoleConfidence": role_confidence,
        "pitcherRoleConfidenceReason": confidence_reason,
        "roleMismatchPenaltyMultiplier": float(
            config["roleMismatchPenaltyMultipliers"][role_confidence]
        ),
        "pitcherRoleEvidence": {
            "games": games,
            "gamesStarted": games_started,
            "gamesStartedAvailable": games_started_available,
            "completeGames": complete_games,
            "reliefAppearances": relief_appearances,
            "gamesFinished": games_finished,
            "gamesFinishedAvailable": games_finished_available,
            "saves": saves,
            "holds": holds,
            "holdsAvailable": holds_available,
            "innings": round(innings, 3),
            "gamesStartedRate": round(game_started_rate, 6),
            "inferredStarterRate": round(inferred_starter_rate, 6),
            "reliefRate": round(relief_rate, 6),
            "inningsPerGame": round(innings_per_game, 6),
            "starterEvidenceMode": starter_evidence_mode,
        },
        "pitcherRoleScores": [
            {"role": role, "score": round(role_scores[role], 6)}
            for role in ("Starter", "Bullpen", "Setup", "Closer")
        ],
        "selectedNaturalPitcherRole": selected,
        "reason": reason,
        "warnings": warnings,
    }


def source_team_workload(record: dict[str, Any]) -> float:
    hitter = record.get("hitterStats") or {}
    pitcher = record.get("pitcherStats") or {}
    return max(
        safe_number(hitter.get("plateAppearances")),
        safe_number(pitcher.get("inningsOuts")),
        safe_number(hitter.get("games")),
        safe_number(pitcher.get("games")),
    )


def source_primary_team_name(player: dict[str, Any]) -> str:
    team_records = player.get("teamFilterRecords") or []
    if team_records:
        primary = max(team_records, key=source_team_workload)
        name = str(primary.get("sourceTeamName") or "").strip()
        if name:
            return name
    aggregate = str(player.get("aggregateTeamName") or "").strip()
    return aggregate or "팀 정보 없음"


def source_workload(player: dict[str, Any], player_type: str) -> float:
    if player_type == "Pitcher":
        return safe_number((player.get("pitcherStats") or {}).get("inningsOuts"))
    return safe_number((player.get("hitterStats") or {}).get("plateAppearances"))


def source_cost_eligibility_sample(player: dict[str, Any], player_type: str) -> float:
    """출전량 진단에 쓰는 상대한 타자 수 또는 타석 수를 만든다."""
    if player_type == "Pitcher":
        return pitcher_batters_faced(player.get("pitcherStats") or {})
    return max(0.0, safe_number((player.get("hitterStats") or {}).get("plateAppearances")))


def source_original_record(
    player: dict[str, Any],
    player_season_id: str,
    team_season_key: str,
    year: int,
    player_type: str,
    position: str,
) -> dict[str, Any]:
    """한 Normalized PlayerSeason의 저장 통계를 평균·혼합 없이 그대로 옮긴다."""
    defenses = player.get("defenseRecords") or []
    defensive_chances = sum(
        safe_number(record.get("putouts"))
        + safe_number(record.get("assists"))
        + safe_number(record.get("errors"))
        for record in defenses
    )
    fielding_errors = sum(safe_number(record.get("errors")) for record in defenses)
    record: dict[str, Any] = {
        "playerSeasonId": player_season_id,
        "teamSeasonKey": team_season_key,
        "seasonYear": year,
        "position": position,
        "defensiveChances": round(defensive_chances),
        "fieldingErrors": round(fielding_errors),
        "isOriginalSourceRecord": True,
    }
    if player_type == "Pitcher":
        stats = player.get("pitcherStats") or {}
        record.update(
            {
                "games": round(safe_number(stats.get("games"))),
                "gamesStarted": round(safe_number(stats.get("gamesStarted"))),
                "pitchingOuts": round(safe_number(stats.get("inningsOuts"))),
                "wins": round(safe_number(stats.get("wins"))),
                "losses": round(safe_number(stats.get("losses"))),
                "saves": round(safe_number(stats.get("saves"))),
                "holds": round(safe_number(stats.get("holds"))),
                "hitsAllowed": round(safe_number(stats.get("hitsAllowed"))),
                "homeRunsAllowed": round(safe_number(stats.get("homeRunsAllowed"))),
                "pitchingWalks": round(safe_number(stats.get("walks"))),
                "earnedRuns": round(safe_number(stats.get("earnedRuns"))),
                "pitchingStrikeouts": round(safe_number(stats.get("strikeouts"))),
                "hasStoredEarnedRunAverage": stats.get("sourceERA") is not None,
                "storedEarnedRunAverage": safe_number(stats.get("sourceERA")),
                "hasStoredWhip": stats.get("sourceWHIP") is not None,
                "storedWhip": safe_number(stats.get("sourceWHIP")),
            }
        )
        return record

    stats = player.get("hitterStats") or {}
    running = player.get("runningStats") or {}
    record.update(
        {
            "games": round(safe_number(stats.get("games"))),
            "plateAppearances": round(safe_number(stats.get("plateAppearances"))),
            "atBats": round(safe_number(stats.get("atBats"))),
            "hits": round(safe_number(stats.get("hits"))),
            "doubles": round(safe_number(stats.get("doubles"))),
            "triples": round(safe_number(stats.get("triples"))),
            "homeRuns": round(safe_number(stats.get("homeRuns"))),
            "runsBattedIn": round(safe_number(stats.get("runsBattedIn"))),
            "runs": round(safe_number(stats.get("runs"))),
            "walks": round(safe_number(stats.get("walks"))),
            "strikeouts": round(safe_number(stats.get("strikeouts"))),
            "stolenBases": round(safe_number(running.get("stolenBases"))),
            "caughtStealing": round(safe_number(running.get("caughtStealing"))),
            "hasStoredBattingAverage": stats.get("sourceAVG") is not None,
            "storedBattingAverage": safe_number(stats.get("sourceAVG")),
            "hasStoredOnBasePercentage": stats.get("sourceOBP") is not None,
            "storedOnBasePercentage": safe_number(stats.get("sourceOBP")),
            "hasStoredSluggingPercentage": stats.get("sourceSLG") is not None,
            "storedSluggingPercentage": safe_number(stats.get("sourceSLG")),
            "hasStoredOnBasePlusSlugging": stats.get("sourceOPS") is not None,
            "storedOnBasePlusSlugging": safe_number(stats.get("sourceOPS")),
        }
    )
    return record


def weighted_rating(row: dict[str, Any], weights: dict[str, float]) -> float:
    ratings = row["baseAttributes"]
    return sum(ratings[ABILITY_INDEX[name]] * weight for name, weight in weights.items())


def eligible_source_positions(player: dict[str, Any]) -> set[str]:
    """주수비 또는 반복 기용 근거가 있는 포지션만 수비 후보로 인정한다."""
    result: set[str] = set()
    _, position_trace = derive_source_position(player, "DH")
    primary = position_trace["primaryDefensivePosition"]
    for record in player.get("defenseRecords") or []:
        source_name = str(record.get("position") or "")
        if (
            source_name not in SOURCE_POSITION_MAP
            or (safe_number(record.get("inningsOuts")) <= 0 and safe_number(record.get("games")) <= 0)
            or (
                safe_number(record.get("inningsOuts")) < ROSTER_SELECTION_CONFIG["secondaryPositionMinimumOuts"]
                and safe_number(record.get("games")) < ROSTER_SELECTION_CONFIG["secondaryPositionMinimumGames"]
                and SOURCE_POSITION_MAP[source_name] != primary
            )
        ):
            continue
        if source_name == "외야수":
            result.update(("LF", "CF", "RF"))
            continue
        position = SOURCE_POSITION_MAP[source_name]
        if position in DEFENSIVE_HITTER_POSITIONS:
            result.add(position)
    return result


def position_starter_score(row: dict[str, Any], position: str, is_eligible: bool) -> float:
    if position == "DH":
        return weighted_rating(row, DH_ATTRIBUTE_WEIGHTS)
    score = weighted_rating(row, POSITION_STARTER_ATTRIBUTE_WEIGHTS[position])
    if row["position"] == position:
        score += ROSTER_SELECTION_CONFIG["naturalPositionBonus"]
    elif is_eligible:
        score += ROSTER_SELECTION_CONFIG["eligiblePositionBonus"]
    else:
        score -= ROSTER_SELECTION_CONFIG["offPositionPenalty"]
    return score


def assignment_stable_key(
    assignment: tuple[dict[str, Any] | None, ...],
) -> tuple[str, ...]:
    return tuple(row["playerSeasonId"] if row is not None else "~" for row in assignment)


def select_defensive_starters(
    hitters: list[dict[str, Any]],
    source_by_season_id: dict[str, dict[str, Any]],
    include_designated_hitter: bool = False,
) -> tuple[list[dict[str, Any] | None], list[dict[str, Any]], list[dict[str, Any]]]:
    """수비 적격 자리와 선택적 DH를 함께 최대 가중 매칭해 주전 기회비용을 반영한다."""
    positions = HITTER_POSITIONS if include_designated_hitter else DEFENSIVE_HITTER_POSITIONS
    ordered = sorted(hitters, key=lambda row: row["playerSeasonId"])
    eligible_by_id = {
        row["playerSeasonId"]: eligible_source_positions(source_by_season_id[row["playerSeasonId"]])
        for row in ordered
    }
    if include_designated_hitter:
        for eligible in eligible_by_id.values():
            eligible.add("DH")
    empty_assignment: tuple[dict[str, Any] | None, ...] = (None,) * len(positions)
    states: dict[int, tuple[float, tuple[dict[str, Any] | None, ...]]] = {
        0: (0.0, empty_assignment)
    }
    for row in ordered:
        previous_states = list(states.items())
        eligible = eligible_by_id[row["playerSeasonId"]]
        for mask, (total_score, assignment) in previous_states:
            for slot_index, position in enumerate(positions):
                bit = 1 << slot_index
                if mask & bit or position not in eligible:
                    continue
                materialized = list(assignment)
                materialized[slot_index] = row
                candidate_assignment = tuple(materialized)
                candidate_score = total_score + position_starter_score(row, position, True)
                existing = states.get(mask | bit)
                if (
                    existing is None
                    or candidate_score > existing[0] + 1e-9
                    or (
                        abs(candidate_score - existing[0]) <= 1e-9
                        and assignment_stable_key(candidate_assignment)
                        < assignment_stable_key(existing[1])
                    )
                ):
                    states[mask | bit] = (candidate_score, candidate_assignment)

    best_mask = 0
    best_score, best_assignment = states[0]
    defense_mask = (1 << len(DEFENSIVE_HITTER_POSITIONS)) - 1
    for mask, (score, assignment) in states.items():
        coverage = ((mask & defense_mask).bit_count(), mask.bit_count())
        best_coverage = ((best_mask & defense_mask).bit_count(), best_mask.bit_count())
        if (
            coverage > best_coverage
            or (coverage == best_coverage and score > best_score + 1e-9)
            or (
                coverage == best_coverage
                and abs(score - best_score) <= 1e-9
                and assignment_stable_key(assignment) < assignment_stable_key(best_assignment)
            )
        ):
            best_mask = mask
            best_score = score
            best_assignment = assignment

    selected_ids = {
        row["playerSeasonId"] for row in best_assignment if row is not None
    }
    remaining = [row for row in ordered if row["playerSeasonId"] not in selected_ids]
    warnings: list[dict[str, Any]] = []
    assignments = list(best_assignment)
    for slot_index, position in enumerate(positions):
        natural_count = sum(row["position"] == position for row in ordered)
        if natural_count == 0 and position != "DH":
            warnings.append(
                {
                    "code": "ROSTER_MISSING_NATURAL_POSITION",
                    "position": position,
                    "message": f"Natural {position} 후보가 없습니다.",
                }
            )
        if assignments[slot_index] is not None or not remaining:
            continue
        fallback = min(
            remaining,
            key=lambda row: (
                -position_starter_score(row, position, False),
                row["playerSeasonId"],
            ),
        )
        assignments[slot_index] = fallback
        remaining.remove(fallback)
        warnings.append(
            {
                "code": "ROSTER_POSITION_FALLBACK",
                "position": position,
                "playerSeasonId": fallback["playerSeasonId"],
                "naturalPosition": fallback["position"],
                "message": f"Eligible {position} 후보 부족으로 OffPosition을 배정했습니다.",
            }
        )

    trace: list[dict[str, Any]] = []
    for slot_index, position in enumerate(positions):
        selected = assignments[slot_index]
        candidates = []
        for row in ordered:
            eligible = position in eligible_by_id[row["playerSeasonId"]]
            if not eligible:
                continue
            candidates.append(
                {
                    "playerSeasonId": row["playerSeasonId"],
                    "naturalPosition": row["position"],
                    "isEligible": True,
                    "score": round(position_starter_score(row, position, True), 6),
                }
            )
        candidates.sort(key=lambda candidate: (-candidate["score"], candidate["playerSeasonId"]))
        selected_is_eligible = (
            selected is not None
            and position in eligible_by_id[selected["playerSeasonId"]]
        )
        trace.append(
            {
                "slot": position,
                "candidates": candidates,
                "selectedPlayerSeasonId": selected["playerSeasonId"] if selected else "",
                "selectionScore": round(
                    position_starter_score(selected, position, selected_is_eligible), 6
                ) if selected else 0.0,
                "isFallback": selected is not None and not selected_is_eligible,
                "reason": (
                    "수비와 DH를 함께 고려한 적격 포지션 최대 가중 매칭"
                    if selected_is_eligible
                    else "Eligible 후보 부족 OffPosition fallback"
                    if selected is not None
                    else "배정 가능한 타자 없음"
                ),
            }
        )
    return assignments, trace, warnings


def pitcher_assignment_score(row: dict[str, Any], assigned_group: str) -> float:
    """등판 패턴은 자격 판정에만 쓰고, 적격 후보의 우열은 보직별 능력치로 정한다."""
    return weighted_rating(row, PITCHER_ASSIGNMENT_ATTRIBUTE_WEIGHTS[assigned_group])


def select_pitcher_group(
    remaining: list[dict[str, Any]],
    count: int,
    assigned_group: str,
    natural_roles: set[str],
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    candidates = sorted(
        remaining,
        key=lambda row: (-pitcher_assignment_score(row, assigned_group), row["playerSeasonId"]),
    )
    # 선발 5명을 확보한 뒤 남은 선발도 단기 구원 능력으로 경쟁한다.
    natural = [row for row in candidates if assigned_group == "Bullpen" or row["pitcherRole"] in natural_roles]
    selected = natural[:count]
    selected_ids = {row["playerSeasonId"] for row in selected}
    remaining[:] = [row for row in remaining if row["playerSeasonId"] not in selected_ids]
    return selected, {
        "assignedRole": assigned_group,
        "reason": "적격 후보의 보직별 능력치 순위; 등판 비율 가산 없음",
        "candidates": [
            {
                "playerSeasonId": row["playerSeasonId"],
                "naturalPitcherRole": row["pitcherRole"],
                "isEligible": assigned_group == "Bullpen" or row["pitcherRole"] in natural_roles,
                "score": round(pitcher_assignment_score(row, assigned_group), 6),
            }
            for row in candidates
        ],
        "selectedPlayerSeasonIds": [row["playerSeasonId"] for row in selected],
        "fallbackCount": 0,
    }


def fill_pitcher_group_fallback(
    selected: list[dict[str, Any]],
    trace: dict[str, Any],
    remaining: list[dict[str, Any]],
    count: int,
    assigned_group: str,
    warnings: list[dict[str, Any]],
) -> None:
    needed = min(count - len(selected), len(remaining))
    if needed <= 0:
        return
    candidates = sorted(
        remaining,
        key=lambda row: (-pitcher_assignment_score(row, assigned_group), row["playerSeasonId"]),
    )
    fallback = candidates[:needed]
    for row in fallback:
        warnings.append(
            {
                "code": "PITCHER_ROLE_FALLBACK",
                "assignedRole": assigned_group,
                "playerSeasonId": row["playerSeasonId"],
                "naturalPitcherRole": row["pitcherRole"],
                "message": f"Natural {assigned_group} 후보 부족으로 다른 역할 투수를 배정했습니다.",
            }
        )
    selected.extend(fallback)
    fallback_ids = {row["playerSeasonId"] for row in fallback}
    remaining[:] = [row for row in remaining if row["playerSeasonId"] not in fallback_ids]
    trace["selectedPlayerSeasonIds"] = [row["playerSeasonId"] for row in selected]
    trace["fallbackCount"] = len(fallback)


def select_hitter_bench(
    hitters: list[dict[str, Any]],
    source_by_season_id: dict[str, dict[str, Any]],
    count: int,
) -> tuple[list[dict[str, Any]], list[dict[str, Any]], list[dict[str, Any]]]:
    """백업 포수를 확보한 뒤 미확보 수비 백업과 교체 능력치를 함께 평가한다."""
    remaining = sorted(hitters, key=lambda row: row["playerSeasonId"])
    eligible = {
        row["playerSeasonId"]: eligible_source_positions(source_by_season_id[row["playerSeasonId"]])
        for row in remaining
    }
    selected, traces, warnings = [], [], []
    covered: set[str] = set()
    while remaining and len(selected) < count:
        catchers = [row for row in remaining if "C" in eligible[row["playerSeasonId"]]]
        needs_catcher = not selected
        candidates = catchers if needs_catcher and catchers else remaining
        if needs_catcher and not catchers:
            warnings.append({
                "code": "ROSTER_BACKUP_CATCHER_MISSING",
                "message": "주전 외에 수비 근거를 충족하는 백업 포수가 없습니다.",
            })
        def score(row: dict[str, Any]) -> float:
            uncovered = eligible[row["playerSeasonId"]] - covered
            return weighted_rating(row, BENCH_ATTRIBUTE_WEIGHTS) + (
                len(uncovered) * float(ROSTER_SELECTION_CONFIG["benchCoverageBonus"])
            )
        ranked = sorted(candidates, key=lambda row: (-score(row), row["playerSeasonId"]))
        choice = ranked[0]
        newly_covered = eligible[choice["playerSeasonId"]] - covered
        traces.append({
            "playerSeasonId": choice["playerSeasonId"],
            "selectionScore": round(score(choice), 6),
            "abilityScore": round(weighted_rating(choice, BENCH_ATTRIBUTE_WEIGHTS), 6),
            "newBackupPositions": sorted(newly_covered),
            "reason": "백업 포수 확보" if needs_catcher and catchers else "교체 능력치와 미확보 수비 백업 평가",
            "candidates": [{"playerSeasonId": row["playerSeasonId"], "score": round(score(row), 6)} for row in ranked],
        })
        selected.append(choice)
        remaining.remove(choice)
        covered.update(newly_covered)
    return selected, traces, warnings


def hitter_overall_ability(row: dict[str, Any]) -> float:
    """로스터 재검토에 쓰는 타자 BaseAttributes 6종의 단순 평균이다."""
    ratings = [float(value) for value in row["baseAttributes"][:6]]
    return sum(ratings) / len(ratings)


def reconsider_hitter_starters(
    starting_assignment: list[dict[str, Any] | None],
    bench: list[dict[str, Any]],
    source_by_season_id: dict[str, dict[str, Any]],
) -> list[dict[str, Any]]:
    """Cost와 종합 Ability가 함께 튀는 동일 포지션 벤치만 제한적으로 재검토한다."""
    config = ROSTER_SELECTION_CONFIG["starterReconsideration"]
    decisions: list[dict[str, Any]] = []
    positions = HITTER_POSITIONS
    for slot_index, position in enumerate(positions):
        starter = starting_assignment[slot_index]
        if starter is None:
            continue
        starter_ability = hitter_overall_ability(starter)
        starter_eligible = eligible_source_positions(source_by_season_id[starter["playerSeasonId"]])
        bench_coverage_before = set().union(*(
            eligible_source_positions(source_by_season_id[row["playerSeasonId"]])
            for row in bench
        )) if bench else set()
        candidates = []
        for candidate in bench:
            candidate_eligible = eligible_source_positions(
                source_by_season_id[candidate["playerSeasonId"]]
            )
            if (
                candidate["position"] != starter["position"]
                or position not in candidate_eligible
                or int(candidate.get("cost", 1)) - int(starter.get("cost", 1))
                < int(config["minimumCostAdvantage"])
            ):
                continue
            ability_advantage = hitter_overall_ability(candidate) - starter_ability
            if ability_advantage < float(config["minimumAbilityAdvantage"]):
                continue
            starter_slot_score = position_starter_score(starter, position, position in starter_eligible)
            candidate_slot_score = position_starter_score(candidate, position, True)
            if candidate_slot_score < starter_slot_score - float(config["maximumSlotScoreLoss"]):
                continue
            if position != "DH":
                starter_defense = max(1.0, float(starter["baseAttributes"][ABILITY_INDEX["Defense"]]))
                starter_arm = max(1.0, float(starter["baseAttributes"][ABILITY_INDEX["Arm"]]))
                if (
                    float(candidate["baseAttributes"][ABILITY_INDEX["Defense"]])
                    < starter_defense * float(config["minimumDefenseRatio"])
                    or float(candidate["baseAttributes"][ABILITY_INDEX["Arm"]])
                    < starter_arm * float(config["minimumArmRatio"])
                ):
                    continue
                if (
                    position == "CF"
                    and float(candidate["baseAttributes"][ABILITY_INDEX["Speed"]])
                    < max(1.0, float(starter["baseAttributes"][ABILITY_INDEX["Speed"]]))
                    * float(config["minimumCenterFieldSpeedRatio"])
                ):
                    continue
            bench_after = [row for row in bench if row is not candidate] + [starter]
            bench_coverage_after = set().union(*(
                eligible_source_positions(source_by_season_id[row["playerSeasonId"]])
                for row in bench_after
            )) if bench_after else set()
            if not bench_coverage_before.issubset(bench_coverage_after):
                continue
            candidates.append(
                (
                    ability_advantage,
                    int(candidate.get("cost", 1)) - int(starter.get("cost", 1)),
                    candidate,
                    candidate_slot_score,
                )
            )
        if not candidates:
            continue
        candidates.sort(key=lambda row: (-row[0], -row[1], row[2]["playerSeasonId"]))
        ability_advantage, cost_advantage, candidate, candidate_slot_score = candidates[0]
        bench.remove(candidate)
        bench.append(starter)
        bench.sort(key=lambda row: row["playerSeasonId"])
        starting_assignment[slot_index] = candidate
        decisions.append(
            {
                "slot": position,
                "starterBefore": starter["playerSeasonId"],
                "starterAfter": candidate["playerSeasonId"],
                "costAdvantage": cost_advantage,
                "abilityAdvantage": round(ability_advantage, 8),
                "selectionScoreAfter": round(candidate_slot_score, 8),
                "reason": "동일 Natural Position에서 Cost와 종합 Ability가 모두 명확히 우위",
            }
        )
    return decisions


def assign_source_team_roles(
    team_rows: list[dict[str, Any]],
    source_by_season_id: dict[str, dict[str, Any]],
) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    """Natural Position/Role을 보존하며 Core25의 Assigned Role만 결정한다."""
    hitters = [row for row in team_rows if row["playerType"] == "Hitter"]
    pitchers = [row for row in team_rows if row["playerType"] == "Pitcher"]
    for index, row in enumerate(sorted(hitters, key=lambda candidate: candidate["playerSeasonId"])):
        row["rosterRole"] = f"ReserveHitter:{index + 1}"
    for index, row in enumerate(sorted(pitchers, key=lambda candidate: candidate["playerSeasonId"])):
        row["rosterRole"] = f"ReservePitcher:{index + 1}"

    starting_assignment, joint_trace, warnings = select_defensive_starters(
        hitters,
        source_by_season_id,
        include_designated_hitter=True,
    )
    defensive_starters = starting_assignment[:-1]
    designated_hitter = starting_assignment[-1]
    starting_trace = joint_trace[:-1]
    if len(team_rows) < 25:
        warnings.append(
            {
                "code": "ROSTER_TOTAL_POOL_SHORTAGE",
                "message": f"원본 시즌 선수풀이 25명보다 작습니다: {len(team_rows)}명.",
            }
        )
    if len(hitters) < 14:
        warnings.append(
            {
                "code": "ROSTER_HITTER_POOL_SHORTAGE",
                "message": f"원본 시즌 야수풀이 14명보다 작습니다: {len(hitters)}명.",
            }
        )
    if len(pitchers) < 11:
        warnings.append(
            {
                "code": "ROSTER_PITCHER_POOL_SHORTAGE",
                "message": f"원본 시즌 투수풀이 11명보다 작습니다: {len(pitchers)}명.",
            }
        )
    selected_hitter_ids = {
        row["playerSeasonId"] for row in starting_assignment if row is not None
    }
    remaining_hitters = [row for row in hitters if row["playerSeasonId"] not in selected_hitter_ids]
    remaining_hitters.sort(
        key=lambda row: (-weighted_rating(row, DH_ATTRIBUTE_WEIGHTS), row["playerSeasonId"])
    )
    designated_hitter_candidates = sorted(
        hitters, key=lambda row: (-weighted_rating(row, DH_ATTRIBUTE_WEIGHTS), row["playerSeasonId"])
    )
    remaining_hitters.sort(
        key=lambda row: (-weighted_rating(row, BENCH_ATTRIBUTE_WEIGHTS), row["playerSeasonId"])
    )
    bench_candidates = list(remaining_hitters)
    bench, bench_trace, bench_warnings = select_hitter_bench(remaining_hitters, source_by_season_id, 5)
    warnings.extend(bench_warnings)
    reconsideration_trace = reconsider_hitter_starters(
        starting_assignment,
        bench,
        source_by_season_id,
    )
    defensive_starters = starting_assignment[:-1]
    designated_hitter = starting_assignment[-1]
    reconsidered_by_slot = {row["slot"]: row for row in reconsideration_trace}
    for slot_trace in joint_trace:
        decision = reconsidered_by_slot.get(slot_trace["slot"])
        if decision is None:
            continue
        selected = next(
            row for row in starting_assignment
            if row is not None and row["playerSeasonId"] == decision["starterAfter"]
        )
        slot_trace["selectedPlayerSeasonId"] = selected["playerSeasonId"]
        slot_trace["selectionScore"] = decision["selectionScoreAfter"]
        slot_trace["reason"] = decision["reason"]

    for position, row in zip(DEFENSIVE_HITTER_POSITIONS, defensive_starters):
        if row is not None:
            row["rosterRole"] = f"StartingHitter:{position}"
    if designated_hitter is not None:
        designated_hitter["rosterRole"] = "StartingHitter:DH"
    for index, row in enumerate(bench):
        row["rosterRole"] = f"BenchHitter:{index + 1}"

    remaining_pitchers = sorted(pitchers, key=lambda row: row["playerSeasonId"])
    pitching_trace: list[dict[str, Any]] = []
    starters, starter_trace = select_pitcher_group(remaining_pitchers, 5, "Starter", {"Starter"})
    closer, trace = select_pitcher_group(remaining_pitchers, 1, "Closer", {"Closer"})
    pitching_trace.append(trace)
    setup, trace = select_pitcher_group(remaining_pitchers, 1, "Setup", {"Setup"})
    pitching_trace.append(trace)
    pitching_trace.append(starter_trace)
    # 희소한 전문 보직을 먼저 확보하고, 선발 결손을 채운 뒤 잔여 투수 전체로 불펜을 경쟁시킨다.
    fill_pitcher_group_fallback(starters, pitching_trace[2], remaining_pitchers, 5, "Starter", warnings)
    fill_pitcher_group_fallback(closer, pitching_trace[0], remaining_pitchers, 1, "Closer", warnings)
    fill_pitcher_group_fallback(setup, pitching_trace[1], remaining_pitchers, 1, "Setup", warnings)
    bullpen, trace = select_pitcher_group(
        remaining_pitchers,
        4,
        "Bullpen",
        {"Swingman", "LongRelief", "MiddleRelief"},
    )
    pitching_trace.append(trace)

    fill_pitcher_group_fallback(bullpen, pitching_trace[3], remaining_pitchers, 4, "Bullpen", warnings)

    for index, row in enumerate(starters):
        row["rosterRole"] = f"StartingPitcher:{index + 1}"
    for index, row in enumerate(bullpen):
        row["rosterRole"] = f"Bullpen{index + 1}"
    if setup:
        setup[0]["rosterRole"] = "Setup"
    if closer:
        closer[0]["rosterRole"] = "Closer"

    starting_hitters = [row for row in defensive_starters if row is not None]
    if designated_hitter is not None:
        starting_hitters.append(designated_hitter)
    core = starting_hitters + bench + starters + bullpen + setup + closer
    if len(core) < min(25, len(team_rows)):
        selected_ids = {row["playerSeasonId"] for row in core}
        fillers = sorted(
            (row for row in team_rows if row["playerSeasonId"] not in selected_ids),
            key=lambda row: (
                -source_workload(source_by_season_id[row["playerSeasonId"]], row["playerType"]),
                row["playerSeasonId"],
            ),
        )
        core.extend(fillers[: min(25, len(team_rows)) - len(core)])
    trace = {
        "rosterBuilderVersion": ROSTER_BUILDER_VERSION,
        "startingSlots": starting_trace,
        "designatedHitter": {
            "candidates": [
                {
                    "playerSeasonId": row["playerSeasonId"],
                    "score": round(weighted_rating(row, DH_ATTRIBUTE_WEIGHTS), 6),
                }
                for row in designated_hitter_candidates
            ],
            "selectedPlayerSeasonId": designated_hitter["playerSeasonId"] if designated_hitter else "",
            "selectionScore": round(weighted_rating(designated_hitter, DH_ATTRIBUTE_WEIGHTS), 6)
            if designated_hitter else 0.0,
            "reason": "수비 8자리와 DH를 함께 최대 가중 매칭; 다른 포지션 기회비용 반영",
        },
        "bench": bench_trace,
        "starterReconsideration": reconsideration_trace,
        "finalBenchPlayerSeasonIds": [row["playerSeasonId"] for row in bench],
        "benchCandidates": [
            {
                "playerSeasonId": row["playerSeasonId"],
                "score": round(weighted_rating(row, BENCH_ATTRIBUTE_WEIGHTS), 6),
            }
            for row in bench_candidates
        ],
        "pitchingStaff": pitching_trace,
        "validationWarnings": warnings,
    }
    return core[:25], trace


def source_award_position(award: dict[str, Any], player_position: str) -> str:
    mapping = {
        "투수": "P",
        "포수": "C",
        "1루수": "1B",
        "2루수": "2B",
        "3루수": "3B",
        "유격수": "SS",
        "외야수": "OF",
        "지명타자": "DH",
    }
    value = str(award.get("awardPosition") or award.get("sourcePosition") or "").strip()
    return mapping.get(value, player_position)


def source_season_games(data: dict[str, Any]) -> tuple[float, str]:
    """당시 구단 순위표의 경기 수로 출전 기회를 정하고 대체 근거를 명시한다."""
    games = [safe_number((team.get("rankStats") or {}).get("games")) for team in data.get("teams") or []]
    known = [value for value in games if value > 0.0]
    if known:
        return max(known), "TeamSeasonGames"
    player_games = [
        max(safe_number((player.get("hitterStats") or {}).get("games")),
            safe_number((player.get("pitcherStats") or {}).get("games")))
        for player in data["players"]
    ]
    if any(value > 0.0 for value in player_games):
        return max(player_games), "ObservedPlayerGamesFallback"
    return float(DERIVATION_BALANCE["costEligibility"]["referenceSeasonGames"]), "ConfiguredReferenceFallback"


def build_editor_original_content(
    input_dir: Path,
    years: list[int],
) -> dict[str, Any]:
    """Normalized cache를 선수·시즌 1:1 Editor 검수 Archive로 Bake한다."""
    references = [
        load_reference(input_dir / f"{year}.json", year)
        for year in sorted(years)
    ]
    reference_manifest_fields = build_reference_manifest_fields(references)
    persons: dict[str, dict[str, Any]] = {}
    year_contents: list[dict[str, Any]] = []

    for data in references:
        year = int(data["year"])
        source_players = data["players"]
        season_games, season_games_origin = source_season_games(data)
        hitters = [player for player in source_players if source_player_type(player) == "Hitter"]
        pitchers = [player for player in source_players if source_player_type(player) == "Pitcher"]
        pitcher_role_availability = derive_pitcher_role_availability(source_players)
        hitter_vector_by_id, hitter_components_by_id, hitter_group_by_id = (
            build_adjusted_feature_pool(hitters, year, "Hitter", season_games=season_games)
            if hitters else ({}, {}, {})
        )
        pitcher_vector_by_id, pitcher_components_by_id, pitcher_group_by_id = (
            build_adjusted_feature_pool(
                pitchers,
                year,
                "Pitcher",
                pitcher_role_availability,
                season_games=season_games,
            )
            if pitchers else ({}, {}, {})
        )

        seasons: list[dict[str, Any]] = []
        records: list[dict[str, Any]] = []
        source_by_season_id: dict[str, dict[str, Any]] = {}
        team_rows: dict[str, list[dict[str, Any]]] = {}
        team_key_by_name: dict[str, str] = {}
        season_id_by_source_id: dict[str, str] = {}

        for player in source_players:
            source_id = str(player.get("sourcePlayerId") or "").strip()
            player_name = str(player.get("playerName") or "").strip()
            if not source_id or not player_name:
                raise ValueError(f"Editor 원본 선수의 ID 또는 이름이 없습니다: {year}")
            person_id = "PERSON_" + stable_digest("editor-source-person-v1", source_id)
            season_id = "SEASON_" + stable_digest("editor-source-season-v1", source_id, year)
            player_type = source_player_type(player)
            if player_type == "Pitcher":
                position = "P"
                natural_pitcher_role, position_role_trace = derive_source_pitcher_role(
                    player,
                    pitcher_role_availability,
                )
                position_role_trace["selectedNaturalPosition"] = position
                position_role_trace["positionCandidates"] = []
            else:
                position, position_role_trace = derive_source_position(player, "DH")
                natural_pitcher_role = ""
                position_role_trace["pitcherRoleConfidence"] = "High"
                position_role_trace["selectedNaturalPitcherRole"] = ""
                position_role_trace["pitcherRoleEvidence"] = {}
                position_role_trace["pitcherRoleScores"] = []
                position_role_trace["warnings"] = []
            position_role_trace["playerSeasonId"] = season_id
            position_role_trace["seasonYear"] = year
            team_name = source_primary_team_name(player)
            team_key = team_key_by_name.setdefault(
                team_name,
                "SOURCE_TEAM_" + stable_digest("editor-source-team-v1", team_name, year),
            )
            vector = (
                hitter_vector_by_id[source_id]
                if player_type == "Hitter"
                else pitcher_vector_by_id[source_id]
            )
            components = (
                hitter_components_by_id[source_id]
                if player_type == "Hitter"
                else pitcher_components_by_id[source_id]
            )
            group_key = (
                hitter_group_by_id[source_id]
                if player_type == "Hitter"
                else pitcher_group_by_id[source_id]
            )
            ratings, ability_trace = to_ratings_with_trace(
                player_type,
                vector,
                components,
                season_id,
                year,
                group_key,
            )
            hitter_stats = player.get("hitterStats") or {}
            pitcher_stats = player.get("pitcherStats") or {}
            defensive_innings_outs = sum(
                max(0.0, safe_number(record.get("inningsOuts")))
                for record in player.get("defenseRecords") or []
            )
            cost_value_inputs = {
                "plateAppearances": max(0.0, safe_number(hitter_stats.get("plateAppearances"), safe_number(hitter_stats.get("atBats")))),
                "inningsOuts": max(0.0, safe_number(pitcher_stats.get("inningsOuts"))),
                "games": max(0.0, safe_number(pitcher_stats.get("games"))),
                "gamesStarted": max(0.0, safe_number(pitcher_stats.get("gamesStarted"))),
                "defensiveInningsOuts": defensive_innings_outs,
            }
            pitcher_evidence = position_role_trace.get("pitcherRoleEvidence") or {}
            if player_type == "Pitcher":
                cost_value_inputs["gamesStartedAvailable"] = bool(
                    pitcher_evidence.get("gamesStartedAvailable", pitcher_stats.get("gamesStarted") is not None)
                )
                if "inferredStarterRate" in pitcher_evidence:
                    cost_value_inputs["inferredStarterRate"] = pitcher_evidence["inferredStarterRate"]
                    cost_value_inputs["starterEvidenceMode"] = pitcher_evidence.get("starterEvidenceMode", "")
            season = {
                "playerSeasonId": season_id,
                "playerPersonId": person_id,
                "originYear": year,
                "originFranchiseId": team_name,
                "originTeamSeasonKey": team_key,
                "position": position,
                "pitcherRole": natural_pitcher_role,
                "pitcherRoleConfidence": position_role_trace["pitcherRoleConfidence"],
                "dataProvenance": "SourceBacked",
                "positionRoleDerivationTrace": position_role_trace,
                "playerType": player_type,
                "registrationType": "Unknown",
                "baseAttributes": ratings,
                "abilityDerivationTrace": ability_trace,
                "derivationWarnings": build_ability_validation_warnings(ability_trace),
                "costEligibilitySample": source_cost_eligibility_sample(player, player_type),
                "_costValueInputs": cost_value_inputs,
                "sourceSeasonGames": season_games,
                "sourceSeasonGamesOrigin": season_games_origin,
                "cost": 0,
                "trainingCeiling": [],
                "rosterRole": "",
                "referenceSimilarityDistance": -1.0,
                "sourceReferenceNames": [player_name],
                "isOriginalSourceSeason": True,
            }
            seasons.append(season)
            source_by_season_id[season_id] = player
            season_id_by_source_id[source_id] = season_id
            team_rows.setdefault(team_name, []).append(season)
            records.append(source_original_record(player, season_id, team_key, year, player_type, position))

            workload = source_workload(player, player_type)
            person = persons.get(person_id)
            if person is None:
                persons[person_id] = {
                    "playerPersonId": person_id,
                    "originalName": player_name,
                    "birthYear": 0,
                    "bats": "Unknown",
                    "throws": "Unknown",
                    "primaryPosition": position,
                    "registrationType": "Unknown",
                    "careerStartYear": year,
                    "careerEndYear": year,
                    "personPotentialTrait": [],
                    "_primaryWorkload": workload,
                }
            else:
                person["careerStartYear"] = min(person["careerStartYear"], year)
                person["careerEndYear"] = max(person["careerEndYear"], year)
                if workload > person["_primaryWorkload"]:
                    person["_primaryWorkload"] = workload
                    person["primaryPosition"] = position

        assign_origin_year_costs(seasons)

        teams: list[dict[str, Any]] = []
        for team_name in sorted(team_rows):
            rows = team_rows[team_name]
            core, roster_selection_trace = assign_source_team_roles(rows, source_by_season_id)
            team_key = team_key_by_name[team_name]
            roster_selection_trace["teamSeasonKey"] = team_key
            teams.append(
                {
                    "teamSeasonKey": team_key,
                    "franchiseId": team_name,
                    "originYear": year,
                    "allNormalCardIds": [f"{row['playerSeasonId']}:Normal" for row in rows],
                    "core25CardIds": [f"{row['playerSeasonId']}:Normal" for row in core],
                    "rosterSelectionTrace": roster_selection_trace,
                    "validationWarnings": roster_selection_trace["validationWarnings"],
                    "referenceStrength": round(
                        mean(
                            mean(row["baseAttributes"][:6] if row["playerType"] == "Hitter" else row["baseAttributes"][6:])
                            for row in core
                        ),
                        4,
                    ),
                }
            )

        awards: list[dict[str, Any]] = []
        for award in data.get("awards") or []:
            source_id = str(award.get("resolvedSourcePlayerId") or "").strip()
            season_id = season_id_by_source_id.get(source_id)
            if season_id is None:
                continue
            season = next(row for row in seasons if row["playerSeasonId"] == season_id)
            awards.append(
                {
                    "seasonYear": year,
                    "awardType": str(award.get("awardType") or ""),
                    "playerSeasonId": season_id,
                    "position": source_award_position(award, season["position"]),
                    "source": str(award.get("sourceKey") or award.get("origin") or ""),
                }
            )

        year_contents.append(
            {
                "year": year,
                "playerSeasons": seasons,
                "normalCards": [
                    {
                        "cardId": f"{season['playerSeasonId']}:Normal",
                        "playerSeasonId": season["playerSeasonId"],
                        "edition": "Normal",
                        "editionStatModifiers": [0] * len(ABILITY_NAMES),
                    }
                    for season in seasons
                ],
                "teamSeasons": teams,
                "originalSeasonRecords": records,
                "originalAwardRecords": awards,
            }
        )

    player_persons = []
    for person in persons.values():
        materialized = dict(person)
        materialized.pop("_primaryWorkload", None)
        player_persons.append(materialized)
    content: dict[str, Any] = {
        "schemaVersion": CONTENT_SCHEMA_VERSION,
        "playerPersons": sorted(player_persons, key=lambda person: person["playerPersonId"]),
        "years": year_contents,
        "manifest": {
            **reference_manifest_fields,
            "generatorVersion": "editor-original-bake-v1",
            "balanceVersion": "source-record-derived-rating-v1",
            "generationSeed": 0,
            "generationSeedAffectsCanonicalBake": False,
            "namePolicyVersion": "original-source-name-v1",
            "nameDataPolicy": EDITOR_ORIGINAL_NAME_POLICY,
            "sourceIdentityPolicyVersion": "editor-source-identity-v1",
            "sourceAllocationPolicyVersion": "official-source-team-audit-v1",
            "sourceFranchiseIdentityPolicyVersion": "editor-source-franchise-id-v1",
            "sourceTeamSeasonIdentityPolicyVersion": "editor-source-team-season-id-v1",
            "replacementGeneratorVersion": "quota-fallback-percentile-v2",
            "replacementPopulationPolicyVersion": "origin-year-position-role-source-only-v1",
            "sourceBackedPlayerPersonCount": len(player_persons),
            "sourceBackedPlayerSeasonCount": sum(
                len(year_content["playerSeasons"])
                for year_content in year_contents
            ),
            "replacementGeneratedPlayerPersonCount": 0,
            "replacementGeneratedPlayerSeasonCount": 0,
            "contentHash": "",
        },
    }
    validate_editor_original_content(content)
    refresh_content_hash(content)
    return content


def load_reference(path: Path, expected_year: int) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8"))
    issues = validate_saved_document(data)
    if issues:
        raise ValueError(f"Normalized Reference 계약 위반: {path}: {'; '.join(issues[:5])}")
    if data.get("year") != expected_year:
        raise ValueError(
            "SEASON_RECORD_CROSS_YEAR_REFERENCE: "
            f"expectedYear={expected_year}, documentYear={data.get('year')}, path={path}"
        )
    if not isinstance(data.get("players"), list) or not data["players"]:
        raise ValueError(f"선수 Reference가 없습니다: {path}")
    if not data.get("isSeasonComplete", False):
        raise ValueError(f"완결 시즌 Reference만 Bake할 수 있습니다: {path}")
    source_ids = [str(player.get("sourcePlayerId") or "").strip() for player in data["players"]]
    if any(not source_id for source_id in source_ids):
        raise ValueError(f"sourcePlayerId가 없는 PlayerSeason이 있습니다: {path}")
    if len(source_ids) != len(set(source_ids)):
        raise ValueError(f"동일 연도 sourcePlayerId가 중복되었습니다: {path}")
    return data


def build_reference_manifest_fields(references: list[dict[str, Any]]) -> dict[str, Any]:
    """Raw provenance와 Normalized 결과를 Bake 파생 버전과 독립적으로 고정한다."""
    ordered = sorted(references, key=lambda item: int(item["year"]))
    raw_provenance = [
        {
            "year": int(reference["year"]),
            "sourceSnapshotHash": reference["sourceMetadata"]["sourceSnapshotHash"],
            "overrideHash": reference["sourceMetadata"]["overrideHash"],
        }
        for reference in ordered
    ]
    normalized_inputs = json.loads(json.dumps(ordered, ensure_ascii=False))
    for reference in normalized_inputs:
        reference.get("sourceMetadata", {}).pop("importedAtUtc", None)

    return {
        "referenceDataVersion": REFERENCE_DATA_VERSION,
        "rawDataVersion": hashlib.sha256(canonical_json_bytes(raw_provenance)).hexdigest(),
        "normalizedSchemaVersion": NORMALIZED_SCHEMA_VERSION,
        "normalizedImporterVersion": NORMALIZED_IMPORTER_VERSION,
        "normalizedContentHash": hashlib.sha256(canonical_json_bytes(normalized_inputs)).hexdigest(),
        "abilityFormulaVersion": ABILITY_FORMULA_VERSION,
        "positionRoleClassifierVersion": POSITION_ROLE_CLASSIFIER_VERSION,
        "rosterBuilderVersion": ROSTER_BUILDER_VERSION,
        "costFormulaVersion": COST_FORMULA_VERSION,
        "derivationBalanceVersion": DERIVATION_BALANCE_VERSION,
    }


def validate_derivation_manifest(manifest: dict[str, Any]) -> None:
    expected = {
        "referenceDataVersion": REFERENCE_DATA_VERSION,
        "normalizedSchemaVersion": NORMALIZED_SCHEMA_VERSION,
        "normalizedImporterVersion": NORMALIZED_IMPORTER_VERSION,
        "abilityFormulaVersion": ABILITY_FORMULA_VERSION,
        "positionRoleClassifierVersion": POSITION_ROLE_CLASSIFIER_VERSION,
        "rosterBuilderVersion": ROSTER_BUILDER_VERSION,
        "costFormulaVersion": COST_FORMULA_VERSION,
        "derivationBalanceVersion": DERIVATION_BALANCE_VERSION,
    }
    mismatches = [
        f"{field}=expected:{expected_value},actual:{manifest.get(field)}"
        for field, expected_value in expected.items()
        if manifest.get(field) != expected_value
    ]
    for field in ("rawDataVersion", "normalizedContentHash"):
        value = manifest.get(field)
        if not isinstance(value, str) or len(value) != 64:
            mismatches.append(f"{field}=invalid-sha256")
            continue
        try:
            int(value, 16)
        except ValueError:
            mismatches.append(f"{field}=invalid-sha256")
    if mismatches:
        raise ValueError("DERIVED_CACHE_VERSION_MISMATCH: " + "; ".join(mismatches))


def validate_bake(content: dict[str, Any]) -> None:
    """Runtime-safe SourceBacked/Replacement Archive의 독립 구조 계약을 검증한다."""
    manifest = content.get("manifest", {})
    if manifest.get("nameDataPolicy") != RUNTIME_NAME_POLICY:
        raise ValueError("Runtime Archive 이름 데이터 정책이 아닙니다.")
    validate_derivation_manifest(manifest)
    for field in (
        "sourceIdentityPolicyVersion",
        "sourceFranchiseIdentityPolicyVersion",
        "sourceTeamSeasonIdentityPolicyVersion",
        "sourceAllocationPolicyVersion",
        "replacementGeneratorVersion",
        "replacementPopulationPolicyVersion",
    ):
        if not str(manifest.get(field) or "").strip():
            raise ValueError(f"Runtime provenance manifest version이 없습니다: {field}")

    persons = content["playerPersons"]
    person_ids = [person["playerPersonId"] for person in persons]
    if len(person_ids) != len(set(person_ids)):
        raise ValueError("PlayerPersonId가 중복되었습니다.")
    if any("fictionalName" in person or "displayName" in person for person in persons):
        raise ValueError("Canonical PlayerPerson에 World DisplayName을 고정할 수 없습니다.")
    if any("originalName" in person for person in persons):
        raise ValueError("Runtime PlayerPerson에 실제 이름이 남아 있습니다.")
    identity_pool = content.get("worldIdentityNamePool")
    if not isinstance(identity_pool, dict) or not str(identity_pool.get("version") or ""):
        raise ValueError("Runtime World Identity 이름 후보 풀이 없습니다.")
    domestic_names = list(identity_pool.get("domesticPlayerNames") or [])
    foreign_names = list(identity_pool.get("foreignPlayerNames") or [])
    franchise_names = list(identity_pool.get("franchiseNames") or [])
    expected_domestic_count = sum(person.get("registrationType") != "Foreign" for person in persons)
    expected_foreign_count = len(persons) - expected_domestic_count
    if len(domestic_names) < expected_domestic_count or len(foreign_names) < expected_foreign_count:
        raise ValueError("Runtime World Player Identity 후보가 부족합니다.")
    if len(domestic_names + foreign_names) != len(set(domestic_names + foreign_names)):
        raise ValueError("Runtime World Player Identity 후보가 중복됩니다.")
    if len(franchise_names) != len(set(franchise_names)):
        raise ValueError("Runtime World Franchise Identity 후보가 중복됩니다.")

    all_season_ids: set[str] = set()
    source_person_ids: set[str] = set()
    replacement_person_ids: set[str] = set()
    source_count = 0
    replacement_count = 0
    for year_content in content["years"]:
        year = int(year_content["year"])
        seasons = year_content["playerSeasons"]
        season_by_id = {season["playerSeasonId"]: season for season in seasons}
        if len(season_by_id) != len(seasons) or all_season_ids.intersection(season_by_id):
            raise ValueError("PlayerSeasonId가 중복되었습니다.")
        all_season_ids.update(season_by_id)
        if not year_content["teamSeasons"]:
            raise ValueError("정규 Canonical TeamSeason이 없습니다.")
        team_keys = [team["teamSeasonKey"] for team in year_content["teamSeasons"]]
        if len(team_keys) != len(set(team_keys)):
            raise ValueError("Canonical TeamSeasonKey가 중복됩니다.")
        for season in seasons:
            provenance = season.get("dataProvenance")
            if provenance == "SourceBacked":
                source_count += 1
                source_person_ids.add(season["playerPersonId"])
            elif provenance == "ReplacementGenerated":
                replacement_count += 1
                replacement_person_ids.add(season["playerPersonId"])
            else:
                raise ValueError("PlayerSeason DataProvenance가 유효하지 않습니다.")
            if int(season["originYear"]) != year:
                raise ValueError("SEASON_RECORD_CROSS_YEAR_REFERENCE")
            if not 1 <= int(season["cost"]) <= 10:
                raise ValueError("Cost는 1~10이어야 합니다.")
            if len(season["baseAttributes"]) != len(ABILITY_NAMES) or len(season["trainingCeiling"]) != len(ABILITY_NAMES):
                raise ValueError("BaseAttributes/TrainingCeiling은 12개여야 합니다.")
            if any(ceiling < base for base, ceiling in zip(season["baseAttributes"], season["trainingCeiling"])):
                raise ValueError("TrainingCeiling이 BaseAttributes보다 낮습니다.")
            if "sourceReferenceNames" in season or "sourcePlayerId" in season:
                raise ValueError("Runtime PlayerSeason에 Source 식별 정보가 남아 있습니다.")

        allocated_cards: list[str] = []
        for team in year_content["teamSeasons"]:
            all_cards = team["allNormalCardIds"]
            core_cards = team["core25CardIds"]
            allocated_cards.extend(all_cards)
            if len(core_cards) != 25 or len(set(core_cards)) != 25:
                raise ValueError("Core25는 중복 없는 정확한 25장이어야 합니다.")
            if not set(core_cards).issubset(all_cards):
                raise ValueError("Core25는 해당 TeamSeason의 전체 Normal Pool에 포함되어야 합니다.")
            core = [season_by_id[card_id.removesuffix(":Normal")] for card_id in core_cards]
            if len({season["playerPersonId"] for season in core}) != 25:
                raise ValueError("Core25에 같은 PlayerPerson이 중복되었습니다.")
            if sum(season["registrationType"] == "Foreign" for season in core) > 3:
                raise ValueError("Core25의 Foreign 등록 선수는 최대 3명입니다.")
            if sum(season["playerType"] == "Hitter" for season in core) != 14:
                raise ValueError("Core25 야수는 14명이어야 합니다.")
            if sum(season["playerType"] == "Pitcher" for season in core) != 11:
                raise ValueError("Core25 투수는 11명이어야 합니다.")
            roles = [season["rosterRole"] for season in core]
            if sum(role.startswith("StartingHitter:") for role in roles) != 9:
                raise ValueError("주전 야수는 9명이어야 합니다.")
            if sum(role.startswith("BenchHitter:") for role in roles) != 5:
                raise ValueError("벤치 야수는 5명이어야 합니다.")
            if sum(role.startswith("StartingPitcher:") for role in roles) != 5:
                raise ValueError("선발 투수는 5명이어야 합니다.")
            if sum(role.startswith("Bullpen") for role in roles) != 4:
                raise ValueError("일반 Bullpen은 4명이어야 합니다.")
            if roles.count("Setup") != 1 or roles.count("Closer") != 1:
                raise ValueError("Setup/Closer는 각 1명이어야 합니다.")
        unique_franchise_count = len(
            {
                team["franchiseId"]
                for year in content["years"]
                for team in year["teamSeasons"]
            }
        )
        if len(franchise_names) < unique_franchise_count:
            raise ValueError("Runtime World Franchise Identity 후보가 부족합니다.")
        expected_cards = {f"{season_id}:Normal" for season_id in season_by_id}
        if len(allocated_cards) != len(set(allocated_cards)) or set(allocated_cards) != expected_cards:
            raise ValueError("모든 PlayerSeason은 정확히 한 Team Pool에 배치되어야 합니다.")

        record_ids = [record["playerSeasonId"] for record in year_content["originalSeasonRecords"]]
        if len(record_ids) != len(set(record_ids)) or set(record_ids) != set(season_by_id):
            raise ValueError("PlayerSeason과 Baked record는 1:1이어야 합니다.")
        if any(int(record["seasonYear"]) != year for record in year_content["originalSeasonRecords"]):
            raise ValueError("SEASON_RECORD_CROSS_YEAR_REFERENCE")
        if any(award["playerSeasonId"] not in season_by_id for award in year_content["originalAwardRecords"]):
            raise ValueError("Original Award가 존재하지 않는 PlayerSeason을 참조합니다.")

    if source_count != int(manifest.get("sourceBackedPlayerSeasonCount", -1)):
        raise ValueError("Manifest SourceBacked PlayerSeason 수가 실제와 다릅니다.")
    if len(source_person_ids) != int(manifest.get("sourceBackedPlayerPersonCount", -1)):
        raise ValueError("Manifest SourceBacked PlayerPerson 수가 실제와 다릅니다.")
    if replacement_count != int(manifest.get("replacementGeneratedPlayerSeasonCount", -1)):
        raise ValueError("Manifest Replacement PlayerSeason 수가 실제와 다릅니다.")
    if len(replacement_person_ids) != int(manifest.get("replacementGeneratedPlayerPersonCount", -1)):
        raise ValueError("Manifest Replacement PlayerPerson 수가 실제와 다릅니다.")
    if source_person_ids.intersection(replacement_person_ids):
        raise ValueError("SourceBacked와 ReplacementGenerated가 PlayerPerson을 공유합니다.")
    if source_person_ids.union(replacement_person_ids) != set(person_ids):
        raise ValueError("Runtime PlayerPerson이 provenance PlayerSeason에 연결되지 않았습니다.")


def validate_editor_original_content(content: dict[str, Any]) -> None:
    """Editor 원본 Archive가 실명 선수·시즌을 합성 없이 1:1로 보존하는지 검증한다."""
    policy = str(content.get("manifest", {}).get("nameDataPolicy") or "")
    if policy != EDITOR_ORIGINAL_NAME_POLICY:
        raise ValueError(f"Editor 원본 이름 정책이 아닙니다: {policy}")

    manifest = content["manifest"]
    for field in (
        "sourceIdentityPolicyVersion",
        "sourceAllocationPolicyVersion",
        "replacementGeneratorVersion",
        "replacementPopulationPolicyVersion",
    ):
        if not str(manifest.get(field) or "").strip():
            raise ValueError(f"Editor Source provenance manifest version이 없습니다: {field}")

    persons = content["playerPersons"]
    person_by_id = {person["playerPersonId"]: person for person in persons}
    if len(person_by_id) != len(persons):
        raise ValueError("Editor 원본 PlayerPersonId가 중복되었습니다.")
    if any(not str(person.get("originalName") or "").strip() for person in persons):
        raise ValueError("Editor 원본 PlayerPerson 이름이 비어 있습니다.")
    if any(str(person.get("fictionalName") or "").strip() for person in persons):
        raise ValueError("Editor 원본 PlayerPerson에 Runtime 가명이 섞였습니다.")
    if any(person.get("personPotentialTrait") for person in persons):
        raise ValueError("원본에 없는 PersonPotentialTrait를 Editor 원본 Archive에 만들 수 없습니다.")

    all_season_ids: set[str] = set()
    for year_content in content["years"]:
        year = int(year_content["year"])
        seasons = year_content["playerSeasons"]
        if any(season.get("dataProvenance") != "SourceBacked" for season in seasons):
            raise ValueError("Editor Source Audit에는 SourceBacked PlayerSeason만 있어야 합니다.")
        season_by_id = {season["playerSeasonId"]: season for season in seasons}
        if len(season_by_id) != len(seasons):
            raise ValueError("Editor 원본 PlayerSeasonId가 중복되었습니다.")
        overlap = all_season_ids.intersection(season_by_id)
        if overlap:
            raise ValueError("Editor 원본 PlayerSeasonId가 연도 사이에서 중복되었습니다.")
        all_season_ids.update(season_by_id)

        records = year_content["originalSeasonRecords"]
        record_ids = [record["playerSeasonId"] for record in records]
        if len(record_ids) != len(set(record_ids)) or set(record_ids) != set(season_by_id):
            raise ValueError("Editor 원본 시즌은 정확히 하나의 원본 기록과 1:1이어야 합니다.")
        record_by_id = {record["playerSeasonId"]: record for record in records}
        if any(not record.get("isOriginalSourceRecord", False) for record in records):
            raise ValueError("Editor 원본 기록 표식이 없습니다.")

        cards = year_content["normalCards"]
        if len(cards) != len(seasons):
            raise ValueError("Editor 원본 PlayerSeason과 Card 수가 다릅니다.")
        card_ids = {card["cardId"] for card in cards}
        if len(card_ids) != len(cards):
            raise ValueError("Editor 원본 CardId가 중복되었습니다.")
        if {card["playerSeasonId"] for card in cards} != set(season_by_id):
            raise ValueError("Editor 원본 Card와 PlayerSeason 연결이 1:1이 아닙니다.")

        team_by_key = {team["teamSeasonKey"]: team for team in year_content["teamSeasons"]}
        for season in seasons:
            if int(season["originYear"]) != year:
                raise ValueError(
                    "SEASON_RECORD_CROSS_YEAR_REFERENCE: "
                    f"PlayerSeason={season['playerSeasonId']}, originYear={season['originYear']}, year={year}"
                )
            person = person_by_id.get(season["playerPersonId"])
            if person is None:
                raise ValueError("Editor 원본 PlayerSeason의 PlayerPerson 연결이 끊겼습니다.")
            if season["originTeamSeasonKey"] not in team_by_key:
                raise ValueError("Editor 원본 PlayerSeason의 TeamSeason 연결이 끊겼습니다.")
            names = season.get("sourceReferenceNames") or []
            if names != [person["originalName"]]:
                raise ValueError("Editor 원본 PlayerSeason 이름이 PlayerPerson과 1:1로 일치하지 않습니다.")
            if season.get("trainingCeiling"):
                raise ValueError("원본에 없는 TrainingCeiling을 Editor 원본 Archive에 만들 수 없습니다.")
            if not 1 <= int(season["cost"]) <= 10:
                raise ValueError("Editor 원본 파생 Cost는 1~10이어야 합니다.")
            record = record_by_id[season["playerSeasonId"]]
            if (
                int(record["seasonYear"]) != year
                or record["teamSeasonKey"] != season["originTeamSeasonKey"]
                or record["position"] != season["position"]
            ):
                raise ValueError(
                    "SEASON_RECORD_CROSS_YEAR_REFERENCE: "
                    f"PlayerSeason={season['playerSeasonId']} record year/team/position mismatch"
                )
            ability_trace = season.get("abilityDerivationTrace") or []
            expected_trace_count = 6
            if len(ability_trace) != expected_trace_count:
                raise ValueError("Editor 원본 PlayerSeason의 Ability 파생 Trace가 불완전합니다.")
            for attribute_trace in ability_trace:
                if attribute_trace.get("playerSeasonId") != season["playerSeasonId"]:
                    raise ValueError("Ability 파생 Trace의 PlayerSeason 연결이 일치하지 않습니다.")
                if attribute_trace.get("roleTier") not in {"Qualified", "Limited"}:
                    raise ValueError("Ability 파생 Trace의 RoleTier 진단 metadata가 없습니다.")
                if str(attribute_trace.get("groupKey") or "").endswith((":Qualified", ":Limited")):
                    raise ValueError("Qualified/Limited는 Ability Z-score GroupKey에 포함될 수 없습니다.")
                for component in attribute_trace.get("components") or []:
                    for field in ("groupMean", "groupStdDev", "rawZ", "reliability", "adjustedZ", "weight", "contribution"):
                        if not math.isfinite(safe_number(component.get(field), float("nan"))):
                            raise ValueError("Ability 파생 Trace에 NaN/Infinity가 있습니다.")
            cost_trace = season.get("costDerivationTrace") or {}
            type_population = sum(
                1 for row in seasons if row["playerType"] == season["playerType"]
            )
            if (
                int(cost_trace.get("originYear", -1)) != int(season["originYear"])
                or int(cost_trace.get("populationCount", 0)) != type_population
                or int(cost_trace.get("cost", 0)) != int(season["cost"])
            ):
                raise ValueError("Cost 파생 Trace의 OriginYear 모집단 또는 Cost가 일치하지 않습니다.")
            eligibility_trace = cost_trace.get("costEligibility") or {}
            if eligibility_trace.get("tier") not in {"Full", "Regular", "Limited", "Tiny"}:
                raise ValueError("Cost 자격 Tier 판정 근거가 없습니다.")
            elite_trace = cost_trace.get("eliteEligibility") or {}
            expected_cost = resolve_value_cost(
                float(cost_trace["continuousValue"]),
                (
                    (float(row["upperExclusive"]), int(row["cost"]))
                    for row in DERIVATION_BALANCE["costValueModel"]["valueTierThresholds"]
                ),
                int(elite_trace.get("maximumCost", 0)),
            )
            if (int(season["cost"]) != expected_cost
                    or cost_trace.get("costMethod") != "SeasonValueOrdinalWithEliteGate"
                    or eligibility_trace.get("affectsCost") is not True):
                raise ValueError("COST_VALUE_MISMATCH: 시즌 가치와 Cost가 일치하지 않습니다.")
            metric_influence_audit = cost_trace.get("metricInfluenceAudit") or {}
            if not metric_influence_audit or metric_influence_audit.get("hasViolation"):
                raise ValueError("ABILITY_METRIC_INFLUENCE_CAP_EXCEEDED: Cost metric 영향도 검증 실패")

        for team in team_by_key.values():
            if int(team["originYear"]) != year:
                raise ValueError(
                    "SEASON_RECORD_CROSS_YEAR_REFERENCE: "
                    f"TeamSeason={team['teamSeasonKey']}, originYear={team['originYear']}, year={year}"
                )
            all_cards = team["allNormalCardIds"]
            core_cards = team["core25CardIds"]
            if len(all_cards) != len(set(all_cards)) or len(core_cards) != len(set(core_cards)):
                raise ValueError("Editor 원본 TeamSeason Card에 중복이 있습니다.")
            if len(core_cards) > 25 or not set(core_cards).issubset(all_cards):
                raise ValueError("Editor 원본 Core25가 Team Player Pool 범위를 벗어났습니다.")
            if any(card_id not in card_ids for card_id in all_cards):
                raise ValueError("Editor 원본 TeamSeason이 존재하지 않는 Card를 참조합니다.")
            pool_seasons = [season_by_id[card_id.removesuffix(":Normal")] for card_id in all_cards]
            pool_hitters = sum(season["playerType"] == "Hitter" for season in pool_seasons)
            pool_pitchers = sum(season["playerType"] == "Pitcher" for season in pool_seasons)
            can_satisfy_core = len(all_cards) >= 25 and pool_hitters >= 14 and pool_pitchers >= 11
            warning_codes = {
                warning.get("code")
                for warning in team.get("validationWarnings") or []
            }
            if can_satisfy_core:
                core_seasons = [season_by_id[card_id.removesuffix(":Normal")] for card_id in core_cards]
                roles = [season["rosterRole"] for season in core_seasons]
                if len(core_cards) != 25:
                    raise ValueError("Editor 원본 Core25는 충족 가능한 선수풀에서 정확히 25명이어야 합니다.")
                if sum(season["playerType"] == "Hitter" for season in core_seasons) != 14:
                    raise ValueError("Editor 원본 Core25 야수는 14명이어야 합니다.")
                if sum(season["playerType"] == "Pitcher" for season in core_seasons) != 11:
                    raise ValueError("Editor 원본 Core25 투수는 11명이어야 합니다.")
                if sum(role.startswith("StartingHitter:") for role in roles) != 9:
                    raise ValueError("Editor 원본 Core25 주전 야수는 9명이어야 합니다.")
                if sum(role.startswith("BenchHitter:") for role in roles) != 5:
                    raise ValueError("Editor 원본 Core25 벤치 야수는 5명이어야 합니다.")
                if sum(role.startswith("StartingPitcher:") for role in roles) != 5:
                    raise ValueError("Editor 원본 Core25 선발 투수는 5명이어야 합니다.")
                if sum(role.startswith("Bullpen") for role in roles) != 4:
                    raise ValueError("Editor 원본 Core25 Bullpen은 4명이어야 합니다.")
                if roles.count("Setup") != 1 or roles.count("Closer") != 1:
                    raise ValueError("Editor 원본 Core25 Setup/Closer는 각 1명이어야 합니다.")
            else:
                required_shortage_codes = set()
                if len(all_cards) < 25:
                    required_shortage_codes.add("ROSTER_TOTAL_POOL_SHORTAGE")
                if pool_hitters < 14:
                    required_shortage_codes.add("ROSTER_HITTER_POOL_SHORTAGE")
                if pool_pitchers < 11:
                    required_shortage_codes.add("ROSTER_PITCHER_POOL_SHORTAGE")
                if not required_shortage_codes.issubset(warning_codes):
                    raise ValueError("Editor 원본 선수풀 부족이 명시적 Validation Warning으로 남지 않았습니다.")
        if any(award["playerSeasonId"] not in season_by_id for award in year_content["originalAwardRecords"]):
            raise ValueError("Editor 원본 Award가 존재하지 않는 PlayerSeason을 참조합니다.")
        if any(int(award["seasonYear"]) != year for award in year_content["originalAwardRecords"]):
            raise ValueError("SEASON_RECORD_CROSS_YEAR_REFERENCE: Award SeasonYear가 연도 묶음과 다릅니다.")

    if int(manifest.get("sourceBackedPlayerPersonCount", -1)) != len(persons):
        raise ValueError("Editor Source manifest의 SourceBacked PlayerPerson 수가 실제와 다릅니다.")
    if int(manifest.get("sourceBackedPlayerSeasonCount", -1)) != len(all_season_ids):
        raise ValueError("Editor Source manifest의 SourceBacked PlayerSeason 수가 실제와 다릅니다.")
    if int(manifest.get("replacementGeneratedPlayerPersonCount", -1)) != 0:
        raise ValueError("Editor Source Audit manifest에 Replacement PlayerPerson이 포함되었습니다.")
    if int(manifest.get("replacementGeneratedPlayerSeasonCount", -1)) != 0:
        raise ValueError("Editor Source Audit manifest에 Replacement PlayerSeason이 포함되었습니다.")


def validate_archive_content(content: dict[str, Any]) -> None:
    manifest = content.get("manifest", {})
    validate_derivation_manifest(manifest)
    policy = str(manifest.get("nameDataPolicy") or "")
    if policy == EDITOR_ORIGINAL_NAME_POLICY:
        validate_editor_original_content(content)
        return
    validate_bake(content)


def bake_with_report(
    input_dir: Path,
    years: list[int],
    generation_seed: int,
) -> tuple[dict[str, Any], dict[str, Any]]:
    """Source Player/Season 1:1 정본에서 최종 Runtime과 검증 보고서를 만든다."""
    from source_backed_final_bake import build_runtime_content

    references = [
        load_reference(input_dir / f"{year}.json", year)
        for year in sorted(years)
    ]
    editor_source_content = build_editor_original_content(input_dir, years)
    return build_runtime_content(
        editor_source_content,
        references,
        generation_seed,
        derivation=__import__(__name__),
    )


def bake(input_dir: Path, years: list[int], generation_seed: int) -> dict[str, Any]:
    """SourceBacked + 최소 Replacement Runtime 콘텐츠를 Bake한다."""
    content, _ = bake_with_report(input_dir, years, generation_seed)
    return content


def refresh_content_hash(content: dict[str, Any]) -> None:
    content["manifest"]["contentHash"] = ""
    canonical = json.dumps(content, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    content["manifest"]["contentHash"] = hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def create_runtime_safe_content(editor_content: dict[str, Any]) -> dict[str, Any]:
    """Editor 검수용 원본 이름을 제거하고 Player Build용 콘텐츠 경계를 만든다."""
    runtime_content = json.loads(json.dumps(editor_content, ensure_ascii=False))
    for person in runtime_content["playerPersons"]:
        person.pop("originalName", None)
    for year_content in runtime_content["years"]:
        for season in year_content["playerSeasons"]:
            season.pop("sourceReferenceNames", None)
            season.pop("abilityDerivationTrace", None)
            season.pop("costDerivationTrace", None)
            season.pop("derivationWarnings", None)
            season.pop("positionRoleDerivationTrace", None)
            season.pop("replacementGenerationTrace", None)
            season.pop("generationReason", None)
        for team in year_content["teamSeasons"]:
            team.pop("rosterSelectionTrace", None)
            team.pop("validationWarnings", None)
    runtime_content["manifest"]["nameDataPolicy"] = RUNTIME_NAME_POLICY
    refresh_content_hash(runtime_content)
    validate_bake(runtime_content)
    verify_content_hash(runtime_content)
    return runtime_content


def canonical_json_bytes(value: object) -> bytes:
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def verify_content_hash(content: dict[str, Any]) -> None:
    expected_hash = str(content["manifest"]["contentHash"])
    hash_source = json.loads(json.dumps(content, ensure_ascii=False))
    hash_source["manifest"]["contentHash"] = ""
    canonical = json.dumps(hash_source, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    actual_hash = hashlib.sha256(canonical.encode("utf-8")).hexdigest()
    if actual_hash != expected_hash:
        raise ValueError(f"ContentHash가 일치하지 않습니다: expected={expected_hash}, actual={actual_hash}")


def write_bytes_atomically(path: Path, payload: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary_path = path.with_name(path.name + ".tmp")
    temporary_path.write_bytes(payload)
    temporary_path.replace(path)


def write_editor_asset_archive(content: dict[str, Any], output_dir: Path) -> dict[str, Any]:
    """대형 Bake를 Editor 전용 TextAsset 묶음으로 분할해 기록한다."""
    validate_archive_content(content)
    verify_content_hash(content)

    player_persons_path = "player_persons.json"
    player_persons_document: dict[str, Any] = {
        "items": content["playerPersons"],
    }
    if "worldIdentityNamePool" in content:
        player_persons_document["worldIdentityNamePool"] = content[
            "worldIdentityNamePool"
        ]
    player_persons_payload = canonical_json_bytes(player_persons_document)
    write_bytes_atomically(output_dir / player_persons_path, player_persons_payload)

    year_entries: list[dict[str, Any]] = []
    archive_hash_entries: list[tuple[str, str]] = [
        (player_persons_path, hashlib.sha256(player_persons_payload).hexdigest())
    ]
    for year_content in sorted(content["years"], key=lambda item: item["year"]):
        relative_path = f"Years/{year_content['year']}.json"
        payload = canonical_json_bytes(year_content)
        payload_hash = hashlib.sha256(payload).hexdigest()
        write_bytes_atomically(output_dir / relative_path, payload)
        archive_hash_entries.append((relative_path, payload_hash))
        awards = year_content["originalAwardRecords"]
        source_seasons = [
            season
            for season in year_content["playerSeasons"]
            if season.get("dataProvenance", "SourceBacked") == "SourceBacked"
        ]
        replacement_seasons = [
            season
            for season in year_content["playerSeasons"]
            if season.get("dataProvenance") == "ReplacementGenerated"
        ]
        year_entries.append(
            {
                "year": year_content["year"],
                "path": relative_path,
                "sha256": payload_hash,
                "byteLength": len(payload),
                "playerSeasonCount": len(year_content["playerSeasons"]),
                "teamSeasonCount": len(year_content["teamSeasons"]),
                "normalCardCount": len(year_content["normalCards"]),
                "originalRecordCount": len(year_content["originalSeasonRecords"]),
                "allStarCount": sum(award["awardType"] == "AllStar" for award in awards),
                "goldenGloveCount": sum(award["awardType"] == "GoldenGlove" for award in awards),
                "sourceHitterCount": sum(season["playerType"] == "Hitter" for season in source_seasons),
                "sourcePitcherCount": sum(season["playerType"] == "Pitcher" for season in source_seasons),
                "replacementHitterCount": sum(season["playerType"] == "Hitter" for season in replacement_seasons),
                "replacementPitcherCount": sum(season["playerType"] == "Pitcher" for season in replacement_seasons),
                "replacementRatio": round(
                    len(replacement_seasons)
                    / (len(year_content["teamSeasons"]) * 25.0),
                    8,
                ),
            }
        )

    archive_hash_source = json.dumps(archive_hash_entries, ensure_ascii=False, separators=(",", ":"))
    manifest = {
        "assetFormatVersion": EDITOR_ASSET_FORMAT_VERSION,
        "contentSchemaVersion": content["schemaVersion"],
        "sourceManifest": content["manifest"],
        "assetArchiveHash": hashlib.sha256(archive_hash_source.encode("utf-8")).hexdigest(),
        "playerPersons": {
            "path": player_persons_path,
            "sha256": hashlib.sha256(player_persons_payload).hexdigest(),
            "byteLength": len(player_persons_payload),
            "count": len(content["playerPersons"]),
        },
        "years": year_entries,
        "summary": {
            "yearCount": len(year_entries),
            "playerPersonCount": len(content["playerPersons"]),
            "playerSeasonCount": sum(entry["playerSeasonCount"] for entry in year_entries),
            "teamSeasonCount": sum(entry["teamSeasonCount"] for entry in year_entries),
            "normalCardCount": sum(entry["normalCardCount"] for entry in year_entries),
            "originalRecordCount": sum(entry["originalRecordCount"] for entry in year_entries),
            "originalAwardCount": sum(
                len(year_content["originalAwardRecords"])
                for year_content in content["years"]
            ),
            "sourceBackedPlayerSeasonCount": sum(
                entry["sourceHitterCount"] + entry["sourcePitcherCount"]
                for entry in year_entries
            ),
            "replacementGeneratedPlayerSeasonCount": sum(
                entry["replacementHitterCount"] + entry["replacementPitcherCount"]
                for entry in year_entries
            ),
            "sourceBackedPlayerPersonCount": int(
                content["manifest"].get("sourceBackedPlayerPersonCount", len(content["playerPersons"]))
            ),
            "replacementGeneratedPlayerPersonCount": int(
                content["manifest"].get("replacementGeneratedPlayerPersonCount", 0)
            ),
        },
    }
    write_bytes_atomically(output_dir / "manifest.json", canonical_json_bytes(manifest))
    return manifest


def build_archive_validation_snapshot(manifest: dict[str, Any]) -> dict[str, Any]:
    """문서와 검증 보고서가 공유할 결정론적 Archive 스냅샷을 만든다."""
    payload_byte_length = int(manifest["playerPersons"]["byteLength"]) + sum(
        int(entry["byteLength"])
        for entry in manifest["years"]
    )
    return {
        "contentSchemaVersion": int(manifest["contentSchemaVersion"]),
        "contentHash": manifest["sourceManifest"]["contentHash"],
        "assetArchiveHash": manifest["assetArchiveHash"],
        "archivePayloadByteLength": payload_byte_length,
        "manifestByteLength": len(canonical_json_bytes(manifest)),
        "summary": manifest["summary"],
    }


def load_and_validate_editor_asset_archive(output_dir: Path) -> dict[str, Any]:
    """분할 Asset의 파일 Hash와 공통 Bake 규칙을 다시 검증한다."""
    manifest = json.loads((output_dir / "manifest.json").read_text(encoding="utf-8"))
    if manifest["assetFormatVersion"] != EDITOR_ASSET_FORMAT_VERSION:
        raise ValueError("지원하지 않는 Editor Asset Format입니다.")

    person_entry = manifest["playerPersons"]
    person_payload = (output_dir / person_entry["path"]).read_bytes()
    if len(person_payload) != person_entry["byteLength"]:
        raise ValueError("PlayerPerson Asset의 byteLength가 Manifest와 다릅니다.")
    if hashlib.sha256(person_payload).hexdigest() != person_entry["sha256"]:
        raise ValueError("PlayerPerson Asset의 SHA-256이 Manifest와 다릅니다.")

    player_persons_document = json.loads(person_payload.decode("utf-8"))
    if isinstance(player_persons_document, list):
        # Asset Format v1 구 archive는 배열 자체를 저장했다.
        player_persons = player_persons_document
        world_identity_name_pool = None
    elif isinstance(player_persons_document, dict):
        player_persons = player_persons_document.get("items")
        world_identity_name_pool = player_persons_document.get(
            "worldIdentityNamePool"
        )
        if not isinstance(player_persons, list):
            raise ValueError("PlayerPerson wrapper의 items가 배열이 아닙니다.")
    else:
        raise ValueError("PlayerPerson Asset JSON 구조가 잘못되었습니다.")
    years: list[dict[str, Any]] = []
    archive_hash_entries: list[tuple[str, str]] = [(person_entry["path"], person_entry["sha256"])]
    for year_entry in manifest["years"]:
        payload = (output_dir / year_entry["path"]).read_bytes()
        if len(payload) != year_entry["byteLength"]:
            raise ValueError(f"{year_entry['year']} Asset의 byteLength가 Manifest와 다릅니다.")
        payload_hash = hashlib.sha256(payload).hexdigest()
        if payload_hash != year_entry["sha256"]:
            raise ValueError(f"{year_entry['year']} Asset의 SHA-256이 Manifest와 다릅니다.")
        archive_hash_entries.append((year_entry["path"], payload_hash))
        years.append(json.loads(payload.decode("utf-8")))

    archive_hash_source = json.dumps(archive_hash_entries, ensure_ascii=False, separators=(",", ":"))
    actual_archive_hash = hashlib.sha256(archive_hash_source.encode("utf-8")).hexdigest()
    if actual_archive_hash != manifest["assetArchiveHash"]:
        raise ValueError("Editor Asset Archive Hash가 Manifest와 다릅니다.")

    content = {
        "schemaVersion": manifest["contentSchemaVersion"],
        "playerPersons": player_persons,
        "years": years,
        "manifest": manifest["sourceManifest"],
    }
    if world_identity_name_pool is not None:
        content["worldIdentityNamePool"] = world_identity_name_pool
    validate_archive_content(content)
    verify_content_hash(content)
    return content


def parse_years(value: str) -> list[int]:
    years: list[int] = []
    for token in value.split(","):
        token = token.strip()
        if "-" in token:
            start, end = (int(part) for part in token.split("-", 1))
            years.extend(range(start, end + 1))
        elif token:
            years.append(int(token))
    if not years:
        raise argparse.ArgumentTypeError("하나 이상의 연도가 필요합니다.")
    return sorted(set(years))


def main() -> int:
    parser = argparse.ArgumentParser(
        description="KBO Source 1:1 정본과 최소 Replacement를 Runtime-safe 콘텐츠로 Bake합니다."
    )
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--years", type=parse_years, required=True)
    parser.add_argument("--seed", type=int, default=20260901)
    output_group = parser.add_mutually_exclusive_group(required=True)
    output_group.add_argument(
        "--output",
        type=Path,
        help="원본 이름을 제거하고 World Identity 이름 후보군을 포함한 Runtime-safe 단일 JSON 경로입니다.",
    )
    output_group.add_argument(
        "--editor-assets-dir",
        type=Path,
        help="실제 선수·시즌을 1:1로 보존하는 Editor Source Archive 경로입니다. Runtime 결과는 Runtime/에 생성됩니다.",
    )
    parser.add_argument(
        "--verify-editor-assets",
        action="store_true",
        help="분할 Editor Asset을 다시 읽어 파일 Hash와 Bake 규칙을 검증합니다.",
    )
    args = parser.parse_args()
    runtime_content, validation_report = bake_with_report(args.input_dir, args.years, args.seed)
    if args.output is not None:
        content = create_runtime_safe_content(runtime_content)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        write_bytes_atomically(
            args.output,
            (json.dumps(content, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8"),
        )
        report_path = args.output.with_name(args.output.stem + ".validation_report.json")
        write_bytes_atomically(report_path, canonical_json_bytes(validation_report))
    else:
        content = build_editor_original_content(args.input_dir, args.years)
        source_archive_manifest = write_editor_asset_archive(content, args.editor_assets_dir)
        runtime_content = create_runtime_safe_content(runtime_content)
        runtime_assets_dir = args.editor_assets_dir / "Runtime"
        runtime_archive_manifest = write_editor_asset_archive(runtime_content, runtime_assets_dir)
        validation_report["sourceArchive"] = build_archive_validation_snapshot(source_archive_manifest)
        validation_report["runtimeArchive"] = build_archive_validation_snapshot(runtime_archive_manifest)
        write_bytes_atomically(
            runtime_assets_dir / "validation_report.json",
            canonical_json_bytes(validation_report),
        )
        if args.verify_editor_assets:
            reloaded = load_and_validate_editor_asset_archive(args.editor_assets_dir)
            if reloaded != content:
                raise ValueError("분할 Editor Asset을 다시 조립한 내용이 Bake 결과와 다릅니다.")
            reloaded_runtime = load_and_validate_editor_asset_archive(runtime_assets_dir)
            if reloaded_runtime != runtime_content:
                raise ValueError("분할 Runtime Asset을 다시 조립한 내용이 정제 결과와 다릅니다.")
    print(f"Baked {sum(len(year['playerSeasons']) for year in content['years'])} PlayerSeasons")
    print(f"NameDataPolicy={content['manifest']['nameDataPolicy']}")
    print(f"ContentHash={content['manifest']['contentHash']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
