from __future__ import annotations

import argparse
from collections import Counter
import hashlib
import json
import math
import random
import statistics
from pathlib import Path
from typing import Any, Iterable

from kbo_importer import IMPORTER_VERSION as NORMALIZED_IMPORTER_VERSION
from kbo_importer import SCHEMA_VERSION as NORMALIZED_SCHEMA_VERSION
from kbo_importer.validation import validate_saved_document


GENERATOR_VERSION = "synthetic-bake-v2"
STABLE_GENERATION_VERSION = "synthetic-bake-v1"
BALANCE_VERSION = "historical-normal-v1"
REFERENCE_DATA_VERSION = f"kbo-normalized-v{NORMALIZED_SCHEMA_VERSION}"
CONTENT_SCHEMA_VERSION = 3
NAME_POLICY_VERSION = "korean-source-component-v2"
EDITOR_NAME_POLICY = "editor-original-reference-v1"
EDITOR_ORIGINAL_NAME_POLICY = "editor-original-source-v2"
RUNTIME_NAME_POLICY = "runtime-fictional-only-v2"
EDITOR_ASSET_FORMAT_VERSION = 1
DERIVATION_BALANCE_PATH = Path(__file__).with_name("derivation_balance.json")
DERIVATION_BALANCE = json.loads(DERIVATION_BALANCE_PATH.read_text(encoding="utf-8"))
ABILITY_FORMULA_VERSION = str(DERIVATION_BALANCE["abilityFormulaVersion"])
COST_FORMULA_VERSION = str(DERIVATION_BALANCE["costFormulaVersion"])
POSITION_ROLE_CLASSIFIER_VERSION = str(DERIVATION_BALANCE["positionRoleClassifierVersion"])
ROSTER_BUILDER_VERSION = str(DERIVATION_BALANCE["rosterBuilderVersion"])
DERIVATION_BALANCE_VERSION = str(DERIVATION_BALANCE["version"])
FRANCHISE_IDS = (
    "SEOUL_COMETS",
    "BUSAN_TIDES",
    "INCHEON_HARBORS",
    "DAEGU_FORGE",
    "DAEJEON_PIONEERS",
    "GWANGJU_PHOENIX",
    "SUWON_GUARDIANS",
    "CHANGWON_MARINERS",
    "JEONJU_STARS",
    "GANGNEUNG_WAVES",
)
HITTER_POSITIONS = ("C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH")
DEFENSIVE_HITTER_POSITIONS = HITTER_POSITIONS[:-1]
HITTER_ROLES = tuple(f"StartingHitter:{position}" for position in HITTER_POSITIONS) + tuple(
    f"BenchHitter:{index}" for index in range(1, 6)
)
PITCHER_ROLES = tuple(f"StartingPitcher:{index}" for index in range(1, 6)) + (
    "Bullpen1",
    "Bullpen2",
    "Bullpen3",
    "Bullpen4",
    "Setup",
    "Closer",
)
RESERVE_ROLES = tuple(f"ReserveHitter:{index}" for index in range(1, 4)) + tuple(
    f"ReservePitcher:{index}" for index in range(1, 3)
)
TEAM_POOL_ROLES = HITTER_ROLES + PITCHER_ROLES + RESERVE_ROLES
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
    "HomeRunRate",
    "WalkRate",
    "NegativeStrikeoutRate",
    "StolenBaseAttemptRate",
    "StolenBaseSuccessRate",
    "FieldingPercentage",
)
PITCHER_METRIC_NAMES = (
    "NegativeEarnedRunAverage",
    "NegativeWhip",
    "StrikeoutsPerNine",
    "NegativeWalksPerNine",
    "InningsPerGame",
    "SaveRate",
    "HoldRate",
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
COMMON_KOREAN_SURNAMES = tuple(
    "김이박최정강조윤장임한오서신권황안송전홍유고문양배백허남심노하곽"
)
FALLBACK_GIVEN_NAMES = (
    "도윤", "준서", "시우", "민재", "우진", "현우", "성민", "태호",
    "민준", "재현", "승우", "지훈", "동현", "예준", "민성", "준혁",
    "지환", "재원", "성훈", "민기", "건우", "은찬", "태윤", "시현",
    "준영", "승현", "도현", "건호", "재민", "윤성", "준호", "민규",
    "동욱", "정우", "진우", "성우", "상현", "정현", "영준", "재훈",
    "현준", "성호", "정훈", "승민", "승환", "진호", "대현", "영진",
    "정호", "경호", "경환", "동훈", "민석", "상민", "성진", "재영",
    "재호", "준우", "준형", "민철", "상우", "성철", "태영", "현수",
)


def validate_derivation_balance(config: dict[str, Any]) -> None:
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

    levels = config["roleCompositeWeightLevels"]
    for player_type, profiles in config["roleCompositeProfiles"].items():
        for profile_name, profile in profiles.items():
            if len(profile) != 6 or any(level not in levels for level in profile):
                raise ValueError(f"Cost 역할 가중치 설정이 유효하지 않습니다: {player_type}/{profile_name}")


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
    if cost <= 3:
        return 4, 8
    if cost <= 6:
        return 2, 5
    if cost <= 8:
        return 1, 3
    return 0, 2


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
    if not has_average and at_bats > 0.0:
        has_average, average = True, hits / at_bats
    has_on_base, on_base = optional_number(stats.get("sourceOBP"))
    has_slugging, slugging = optional_number(stats.get("sourceSLG"))

    pa_constant = float(reliability_config["plateAppearances"])
    result = [
        metric_evidence("BattingAverage", average, hits, at_bats, plate_appearances, pa_constant, has_average),
        metric_evidence("OnBasePercentage", on_base, on_base * plate_appearances, plate_appearances, plate_appearances, pa_constant, has_on_base),
        metric_evidence("SluggingPercentage", slugging, slugging * at_bats, at_bats, plate_appearances, pa_constant, has_slugging),
        metric_evidence("HomeRunRate", ratio(stats.get("homeRuns"), plate_appearances), safe_number(stats.get("homeRuns")), plate_appearances, plate_appearances, pa_constant, plate_appearances > 0.0),
        metric_evidence("WalkRate", ratio(stats.get("walks"), plate_appearances), safe_number(stats.get("walks")), plate_appearances, plate_appearances, pa_constant, plate_appearances > 0.0),
        metric_evidence("NegativeStrikeoutRate", -ratio(stats.get("strikeouts"), plate_appearances), -safe_number(stats.get("strikeouts")), plate_appearances, plate_appearances, pa_constant, plate_appearances > 0.0),
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

    chances = sum(
        safe_number(record.get("putouts"))
        + safe_number(record.get("assists"))
        + safe_number(record.get("errors"))
        for record in defenses
    )
    errors = sum(safe_number(record.get("errors")) for record in defenses)
    result.append(
        metric_evidence(
            "FieldingPercentage",
            1.0 - errors / chances if chances > 0.0 else 0.0,
            chances - errors,
            chances,
            chances,
            float(reliability_config["defensiveChances"]),
            chances > 0.0,
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
    if not has_era and innings > 0.0:
        has_era = True
        earned_run_average = ratio(stats.get("earnedRuns"), innings) * 9.0
    has_whip, whip = optional_number(stats.get("sourceWHIP"))
    return [
        metric_evidence("NegativeEarnedRunAverage", -earned_run_average, -safe_number(stats.get("earnedRuns")), innings, batters_faced, tbf_constant, has_era),
        metric_evidence("NegativeWhip", -whip, -(safe_number(stats.get("hitsAllowed")) + safe_number(stats.get("walks"))), innings, batters_faced, tbf_constant, has_whip),
        metric_evidence("StrikeoutsPerNine", ratio(stats.get("strikeouts"), innings) * 9.0, safe_number(stats.get("strikeouts")), innings, batters_faced, tbf_constant, innings > 0.0),
        metric_evidence("NegativeWalksPerNine", -ratio(stats.get("walks"), innings) * 9.0, -safe_number(stats.get("walks")), innings, batters_faced, tbf_constant, innings > 0.0),
        metric_evidence("InningsPerGame", innings / games if games > 0.0 else 0.0, innings, games, batters_faced, tbf_constant, games > 0.0),
        metric_evidence("SaveRate", ratio(stats.get("saves"), games), safe_number(stats.get("saves")), games, batters_faced, tbf_constant, games > 0.0),
        metric_evidence("HoldRate", ratio(stats.get("holds"), games), safe_number(stats.get("holds")), games, batters_faced, tbf_constant, holds_available and games > 0.0),
    ]


def derivation_group_key(
    player: dict[str, Any],
    year: int,
    player_type: str,
    pitcher_role_availability: dict[str, bool] | None = None,
) -> str:
    role_tier = DERIVATION_BALANCE["roleTier"]
    if player_type == "Hitter":
        sample_size = safe_number((player.get("hitterStats") or {}).get("plateAppearances"))
        tier = "Qualified" if sample_size >= float(role_tier["qualifiedPlateAppearances"]) else "Limited"
        group = source_position(player, player_type)
    else:
        sample_size = pitcher_batters_faced(player.get("pitcherStats") or {})
        tier = "Qualified" if sample_size >= float(role_tier["qualifiedBattersFaced"]) else "Limited"
        group, _ = derive_source_pitcher_role(player, pitcher_role_availability)
    return f"{year}:{group}:{tier}"


def build_adjusted_feature_pool(
    players: list[dict[str, Any]],
    year: int,
    player_type: str,
    pitcher_role_availability: dict[str, bool] | None = None,
) -> tuple[dict[str, tuple[float, ...]], dict[str, dict[str, dict[str, Any]]], dict[str, str]]:
    """시대·포지션/역할 집단 Z-score에 지표별 표본 신뢰도를 적용한다."""
    metric_names = HITTER_METRIC_NAMES if player_type == "Hitter" else PITCHER_METRIC_NAMES
    evidence_by_id: dict[str, list[dict[str, Any]]] = {}
    group_by_id: dict[str, str] = {}
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

    group_statistics: dict[tuple[str, str], tuple[float, float]] = {}
    for group_key in sorted(set(group_by_id.values())):
        member_ids = sorted(source_id for source_id, value in group_by_id.items() if value == group_key)
        for metric_name in metric_names:
            values = [
                float(component["rawValue"])
                for source_id in member_ids
                for component in evidence_by_id[source_id]
                if component["metric"] == metric_name and component["isAvailable"]
            ]
            center = mean(values)
            deviation = statistics.pstdev(values) if len(values) > 1 else 1.0
            group_statistics[(group_key, metric_name)] = (
                center,
                deviation if deviation > 1e-9 else 1.0,
            )

    vectors: dict[str, tuple[float, ...]] = {}
    traces: dict[str, dict[str, dict[str, Any]]] = {}
    for source_id in sorted(evidence_by_id):
        group_key = group_by_id[source_id]
        adjusted_values: list[float] = []
        component_traces: dict[str, dict[str, Any]] = {}
        by_metric = {component["metric"]: component for component in evidence_by_id[source_id]}
        for metric_name in metric_names:
            evidence = by_metric[metric_name]
            center, deviation = group_statistics[(group_key, metric_name)]
            raw_z = (
                (float(evidence["rawValue"]) - center) / deviation
                if evidence["isAvailable"]
                else 0.0
            )
            sample_reliability = reliability(
                float(evidence["sampleSize"]),
                float(evidence["reliabilityConstant"]),
            ) if evidence["isAvailable"] else 0.0
            adjusted_z = raw_z * sample_reliability
            adjusted_values.append(adjusted_z)
            component_traces[metric_name] = {
                **evidence,
                "groupMean": round(center, 8),
                "groupStdDev": round(deviation, 8),
                "rawZ": round(raw_z, 8),
                "reliability": round(sample_reliability, 8),
                "adjustedZ": round(adjusted_z, 8),
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
    selected = candidates[0]["position"] if candidates else fallback
    reason = (
        "해당 SeasonYear의 수비 이닝, 선발 경기, 출전 경기 순으로 선택"
        if candidates
        else f"해당 SeasonYear의 수비 기록이 없어 {fallback} fallback"
    )
    return selected, {
        "classifierVersion": POSITION_ROLE_CLASSIFIER_VERSION,
        "positionCandidates": candidates,
        "selectedNaturalPosition": selected,
        "reason": reason,
    }


def hitter_features(player: dict[str, Any]) -> tuple[float, ...]:
    return tuple(
        safe_number(component["rawValue"])
        for component in hitter_metric_evidence(player)
    )


def pitcher_features(player: dict[str, Any]) -> tuple[float, ...]:
    stats = player.get("pitcherStats") or {}
    outs = safe_number(stats.get("inningsOuts"))
    games = max(1.0, safe_number(stats.get("games"), 1.0))
    innings = outs / 3.0
    return (
        -safe_number(stats.get("sourceERA"), 9.0),
        -safe_number(stats.get("sourceWHIP"), 3.0),
        ratio(stats.get("strikeouts"), innings) * 9.0,
        -ratio(stats.get("walks"), innings) * 9.0,
        innings / games,
        ratio(stats.get("saves"), games),
        ratio(stats.get("holds"), games),
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


def mixed_vector(
    normalized: list[tuple[float, ...]],
    rng: random.Random,
    count: int,
) -> tuple[tuple[float, ...], tuple[int, ...]]:
    indices = tuple(rng.randrange(len(normalized)) for _ in range(count))
    width = len(normalized[0])
    vector = tuple(mean(normalized[index][field] for index in indices) for field in range(width))
    return vector, indices


def nearest_distance(vector: tuple[float, ...], references: list[tuple[float, ...]]) -> float:
    return min(
        math.sqrt(sum((left - right) ** 2 for left, right in zip(vector, reference)))
        for reference in references
    )


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
    for attribute, profile in profiles.items():
        attribute_components = []
        combined_z = 0.0
        for metric_name, weight_value in profile["metrics"].items():
            weight = float(weight_value)
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
            component["contribution"] = round(contribution, 8)
            attribute_components.append(component)
        rating_before_clamp = rating_center + float(profile["scale"]) * combined_z
        rating_after_clamp = clamp_rating(rating_before_clamp)
        values[ABILITY_NAMES.index(attribute)] = rating_after_clamp
        traces.append(
            {
                "playerSeasonId": player_season_id,
                "seasonYear": season_year,
                "attribute": attribute,
                "groupKey": group_key,
                "components": attribute_components,
                "combinedZ": round(combined_z, 8),
                "ratingBeforeClamp": round(rating_before_clamp, 8),
                "ratingAfterClamp": rating_after_clamp,
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
        absolute_total = sum(abs(float(component["contribution"])) for component in components)
        if absolute_total <= 1e-9:
            continue
        for component in components:
            contribution = abs(float(component["contribution"]))
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
                        "contribution": component["contribution"],
                        "contributionShare": round(contribution_share, 8),
                    }
                )
    return warnings


def role_composite_weights(season: dict[str, Any]) -> tuple[str, list[float]]:
    player_type = str(season["playerType"])
    profiles = DERIVATION_BALANCE["roleCompositeProfiles"][player_type]
    if player_type == "Hitter":
        profile_name = str(season.get("position") or "Default")
    else:
        profile_name = str(season.get("pitcherRole") or "Default")
    if profile_name not in profiles:
        profile_name = "Default"
    levels = DERIVATION_BALANCE["roleCompositeWeightLevels"]
    return profile_name, [float(levels[level]) for level in profiles[profile_name]]


def role_adjusted_composite(season: dict[str, Any]) -> tuple[float, dict[str, Any]]:
    """기존 PlayerValueEvaluator의 역할별 핵심/보조/일반 능력치 계약으로 가치를 계산한다."""
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
    return composite, {
        "baseAttributes": list(season["baseAttributes"]),
        "role": role,
        "roleProfile": profile_name,
        "roleWeights": [
            {"ability": ability_name, "weight": weight}
            for ability_name, weight in zip(ability_names, weights)
        ],
        "abilityContribution": contributions,
        "composite": round(composite, 8),
        "originYear": int(season["originYear"]),
        "populationCount": 0,
        "rank": 0,
        "percentile": 0.0,
        "cost": 0,
    }


def assign_origin_year_costs(seasons: list[dict[str, Any]]) -> None:
    """Team/Core25가 아니라 OriginYear 전체 PlayerSeason 모집단에서 Cost를 확정한다."""
    by_year: dict[int, list[tuple[dict[str, Any], float, dict[str, Any]]]] = {}
    for season in seasons:
        composite, trace = role_adjusted_composite(season)
        by_year.setdefault(int(season["originYear"]), []).append((season, composite, trace))

    for year in sorted(by_year):
        population = by_year[year]
        ranked = sorted(
            population,
            key=lambda entry: (entry[1], str(entry[0]["playerSeasonId"])),
        )
        count = len(ranked)
        for zero_based_rank, (season, _, trace) in enumerate(ranked):
            cost = percentile_cost(zero_based_rank, count)
            season["cost"] = cost
            trace["populationCount"] = count
            trace["rank"] = zero_based_rank + 1
            trace["percentile"] = round((zero_based_rank + 0.5) / count, 8)
            trace["cost"] = cost
            season["costDerivationTrace"] = trace


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
    if games < config["lowConfidenceGames"]:
        warnings.append(
            {
                "code": "PITCHER_ROLE_LOW_CONFIDENCE",
                "message": "투수 역할을 판정할 시즌 등판 표본이 작습니다.",
                "games": games,
            }
        )
    return selected, {
        "classifierVersion": POSITION_ROLE_CLASSIFIER_VERSION,
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
    result: set[str] = set()
    for record in player.get("defenseRecords") or []:
        source_name = str(record.get("position") or "")
        if (
            source_name not in SOURCE_POSITION_MAP
            or (
                safe_number(record.get("inningsOuts")) <= 0
                and safe_number(record.get("games")) <= 0
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
    score = weighted_rating(row, POSITION_STARTER_ATTRIBUTE_WEIGHTS[position])
    if row["position"] == position:
        score += ROSTER_SELECTION_CONFIG["naturalPositionBonus"]
    elif is_eligible:
        score += ROSTER_SELECTION_CONFIG["eligiblePositionBonus"]
    return score


def assignment_stable_key(
    assignment: tuple[dict[str, Any] | None, ...],
) -> tuple[str, ...]:
    return tuple(row["playerSeasonId"] if row is not None else "~" for row in assignment)


def select_defensive_starters(
    hitters: list[dict[str, Any]],
    source_by_season_id: dict[str, dict[str, Any]],
) -> tuple[list[dict[str, Any] | None], list[dict[str, Any]], list[dict[str, Any]]]:
    """Eligible Position 전체를 보며 결정론적 최대 가중 매칭으로 수비 8자리를 채운다."""
    ordered = sorted(hitters, key=lambda row: row["playerSeasonId"])
    eligible_by_id = {
        row["playerSeasonId"]: eligible_source_positions(source_by_season_id[row["playerSeasonId"]])
        for row in ordered
    }
    empty_assignment: tuple[dict[str, Any] | None, ...] = (None,) * len(DEFENSIVE_HITTER_POSITIONS)
    states: dict[int, tuple[float, tuple[dict[str, Any] | None, ...]]] = {
        0: (0.0, empty_assignment)
    }
    for row in ordered:
        previous_states = list(states.items())
        eligible = eligible_by_id[row["playerSeasonId"]]
        for mask, (total_score, assignment) in previous_states:
            for slot_index, position in enumerate(DEFENSIVE_HITTER_POSITIONS):
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
    for mask, (score, assignment) in states.items():
        if (
            mask.bit_count() > best_mask.bit_count()
            or (mask.bit_count() == best_mask.bit_count() and score > best_score + 1e-9)
            or (
                mask.bit_count() == best_mask.bit_count()
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
    for slot_index, position in enumerate(DEFENSIVE_HITTER_POSITIONS):
        natural_count = sum(row["position"] == position for row in ordered)
        if natural_count == 0:
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
    for slot_index, position in enumerate(DEFENSIVE_HITTER_POSITIONS):
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
                    "Natural Position 최고 점수"
                    if selected is not None and selected["position"] == position
                    else "Eligible Position 최대 가중 매칭"
                    if selected_is_eligible
                    else "Eligible 후보 부족 OffPosition fallback"
                    if selected is not None
                    else "배정 가능한 타자 없음"
                ),
            }
        )
    return assignments, trace, warnings


def pitcher_assignment_score(row: dict[str, Any], assigned_group: str) -> float:
    ability_score = weighted_rating(row, PITCHER_ASSIGNMENT_ATTRIBUTE_WEIGHTS[assigned_group])
    role_scores = row["positionRoleDerivationTrace"]["pitcherRoleScores"]
    role_score = next(
        (safe_number(item.get("score")) for item in role_scores if item.get("role") == assigned_group),
        0.0,
    )
    return (
        ability_score * ROSTER_SELECTION_CONFIG["pitcherAbilityWeight"]
        + role_score * ROSTER_SELECTION_CONFIG["pitcherRoleEvidenceWeight"]
    )


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
    natural = [row for row in candidates if row["pitcherRole"] in natural_roles]
    selected = natural[:count]
    selected_ids = {row["playerSeasonId"] for row in selected}
    remaining[:] = [row for row in remaining if row["playerSeasonId"] not in selected_ids]
    return selected, {
        "assignedRole": assigned_group,
        "candidates": [
            {
                "playerSeasonId": row["playerSeasonId"],
                "naturalPitcherRole": row["pitcherRole"],
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

    defensive_starters, starting_trace, warnings = select_defensive_starters(
        hitters,
        source_by_season_id,
    )
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
        row["playerSeasonId"] for row in defensive_starters if row is not None
    }
    remaining_hitters = [row for row in hitters if row["playerSeasonId"] not in selected_hitter_ids]
    remaining_hitters.sort(
        key=lambda row: (-weighted_rating(row, DH_ATTRIBUTE_WEIGHTS), row["playerSeasonId"])
    )
    designated_hitter_candidates = list(remaining_hitters)
    designated_hitter = remaining_hitters.pop(0) if remaining_hitters else None
    remaining_hitters.sort(
        key=lambda row: (-weighted_rating(row, BENCH_ATTRIBUTE_WEIGHTS), row["playerSeasonId"])
    )
    bench_candidates = list(remaining_hitters)
    bench = remaining_hitters[:5]

    for position, row in zip(DEFENSIVE_HITTER_POSITIONS, defensive_starters):
        if row is not None:
            row["rosterRole"] = f"StartingHitter:{position}"
    if designated_hitter is not None:
        designated_hitter["rosterRole"] = "StartingHitter:DH"
    for index, row in enumerate(bench):
        row["rosterRole"] = f"BenchHitter:{index + 1}"

    remaining_pitchers = sorted(pitchers, key=lambda row: row["playerSeasonId"])
    pitching_trace: list[dict[str, Any]] = []
    closer, trace = select_pitcher_group(remaining_pitchers, 1, "Closer", {"Closer"})
    pitching_trace.append(trace)
    setup, trace = select_pitcher_group(remaining_pitchers, 1, "Setup", {"Setup"})
    pitching_trace.append(trace)
    starters, trace = select_pitcher_group(remaining_pitchers, 5, "Starter", {"Starter"})
    pitching_trace.append(trace)
    bullpen, trace = select_pitcher_group(
        remaining_pitchers,
        4,
        "Bullpen",
        {"Swingman", "LongRelief", "MiddleRelief"},
    )
    pitching_trace.append(trace)

    fill_pitcher_group_fallback(closer, pitching_trace[0], remaining_pitchers, 1, "Closer", warnings)
    fill_pitcher_group_fallback(setup, pitching_trace[1], remaining_pitchers, 1, "Setup", warnings)
    fill_pitcher_group_fallback(starters, pitching_trace[2], remaining_pitchers, 5, "Starter", warnings)
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
            "reason": "수비 8자리 확정 후 남은 타자 중 HittingScore 최고",
        },
        "bench": [
            {
                "playerSeasonId": row["playerSeasonId"],
                "selectionScore": round(weighted_rating(row, BENCH_ATTRIBUTE_WEIGHTS), 6),
            }
            for row in bench
        ],
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
        hitters = [player for player in source_players if source_player_type(player) == "Hitter"]
        pitchers = [player for player in source_players if source_player_type(player) == "Pitcher"]
        pitcher_role_availability = derive_pitcher_role_availability(source_players)
        hitter_vector_by_id, hitter_components_by_id, hitter_group_by_id = (
            build_adjusted_feature_pool(hitters, year, "Hitter")
            if hitters else ({}, {}, {})
        )
        pitcher_vector_by_id, pitcher_components_by_id, pitcher_group_by_id = (
            build_adjusted_feature_pool(
                pitchers,
                year,
                "Pitcher",
                pitcher_role_availability,
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
            season = {
                "playerSeasonId": season_id,
                "playerPersonId": person_id,
                "originYear": year,
                "originFranchiseId": team_name,
                "originTeamSeasonKey": team_key,
                "position": position,
                "pitcherRole": natural_pitcher_role,
                "positionRoleDerivationTrace": position_role_trace,
                "playerType": player_type,
                "registrationType": "Unknown",
                "baseAttributes": ratings,
                "abilityDerivationTrace": ability_trace,
                "derivationWarnings": build_ability_validation_warnings(ability_trace),
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
            "namePolicyVersion": "original-source-name-v1",
            "nameDataPolicy": EDITOR_ORIGINAL_NAME_POLICY,
            "contentHash": "",
        },
    }
    validate_editor_original_content(content)
    refresh_content_hash(content)
    return content


def is_standard_korean_name(value: str) -> bool:
    return (
        len(value) == 3
        and value[0] in COMMON_KOREAN_SURNAMES
        and all("가" <= character <= "힣" for character in value)
    )


def is_natural_fictional_name(value: str) -> bool:
    return is_standard_korean_name(value) and len(set(value)) == len(value)


def build_fictional_name_map(
    person_ids: Iterable[str],
    source_names: Iterable[str],
) -> dict[str, str]:
    """실제 이름의 검증된 이름 조각을 재조합해 중복 없는 Runtime 가명을 만든다."""
    forbidden_names = {str(name).strip() for name in source_names if str(name).strip()}
    eligible_source_names = {
        name for name in forbidden_names if is_standard_korean_name(name)
    }
    given_name_counts = Counter(name[1:] for name in eligible_source_names)
    given_names = set(FALLBACK_GIVEN_NAMES)
    given_names.update(
        given_name
        for given_name, count in given_name_counts.items()
        if count >= 4
    )
    candidates = sorted(
        surname + given_name
        for surname in COMMON_KOREAN_SURNAMES
        for given_name in given_names
        if surname + given_name not in forbidden_names
        and is_natural_fictional_name(surname + given_name)
    )
    ordered_person_ids = sorted(set(person_ids))
    if len(candidates) < len(ordered_person_ids):
        raise ValueError(
            "품질 기준을 만족하는 Runtime 가명이 부족합니다: "
            f"필요 {len(ordered_person_ids)}, 사용 가능 {len(candidates)}"
        )

    result: dict[str, str] = {}
    used_names: set[str] = set()
    for person_id in ordered_person_ids:
        start = stable_seed(NAME_POLICY_VERSION, person_id) % len(candidates)
        for offset in range(len(candidates)):
            candidate = candidates[(start + offset) % len(candidates)]
            if candidate in used_names:
                continue
            result[person_id] = candidate
            used_names.add(candidate)
            break
        else:
            raise ValueError(f"중복 없는 Runtime 가명을 배정할 수 없습니다: {person_id}")
    return result


def original_record(
    player_season_id: str,
    team_season_key: str,
    year: int,
    player_type: str,
    position: str,
    references: list[dict[str, Any]],
    indices: tuple[int, ...],
) -> dict[str, Any]:
    selected = [references[index] for index in indices]
    if player_type == "Hitter":
        stats = [player.get("hitterStats") or {} for player in selected]
        return {
            "playerSeasonId": player_season_id,
            "teamSeasonKey": team_season_key,
            "seasonYear": year,
            "position": position,
            "plateAppearances": round(mean(safe_number(row.get("plateAppearances")) for row in stats)),
            "hits": round(mean(safe_number(row.get("hits")) for row in stats)),
            "homeRuns": round(mean(safe_number(row.get("homeRuns")) for row in stats)),
            "walks": round(mean(safe_number(row.get("walks")) for row in stats)),
            "strikeouts": round(mean(safe_number(row.get("strikeouts")) for row in stats)),
            "defensiveChances": round(
                mean(
                    sum(
                        safe_number(record.get("putouts"))
                        + safe_number(record.get("assists"))
                        + safe_number(record.get("errors"))
                        for record in (player.get("defenseRecords") or [])
                    )
                    for player in selected
                )
            ),
            "fieldingErrors": round(
                mean(
                    sum(safe_number(record.get("errors")) for record in (player.get("defenseRecords") or []))
                    for player in selected
                )
            ),
        }
    stats = [player.get("pitcherStats") or {} for player in selected]
    return {
        "playerSeasonId": player_season_id,
        "teamSeasonKey": team_season_key,
        "seasonYear": year,
        "position": "P",
        "pitchingOuts": round(mean(safe_number(row.get("inningsOuts")) for row in stats)),
        "earnedRuns": round(mean(safe_number(row.get("earnedRuns")) for row in stats)),
        "pitchingStrikeouts": round(mean(safe_number(row.get("strikeouts")) for row in stats)),
    }


def record_score(record: dict[str, Any]) -> float:
    if safe_number(record.get("pitchingOuts")) > 0:
        return (
            safe_number(record.get("pitchingStrikeouts")) * 1.5
            + safe_number(record.get("pitchingOuts")) * 0.2
            - safe_number(record.get("earnedRuns")) * 2.0
        )
    return (
        safe_number(record.get("hits"))
        + safe_number(record.get("homeRuns")) * 4.0
        + safe_number(record.get("walks")) * 0.5
        - safe_number(record.get("fieldingErrors")) * 0.5
    )


def build_original_awards(year_content: dict[str, Any]) -> list[dict[str, Any]]:
    year = year_content["year"]
    records = year_content["originalSeasonRecords"]
    ordered = sorted(records, key=lambda record: (-record_score(record), record["playerSeasonId"]))

    def best(position: str, excluded: set[str]) -> dict[str, Any]:
        return next(
            record
            for record in ordered
            if record["position"] == position and record["playerSeasonId"] not in excluded
        )

    awards: list[dict[str, Any]] = []
    golden_glove_ids: set[str] = set()
    for position in ("P", "C", "1B", "2B", "3B", "SS"):
        winner = best(position, golden_glove_ids)
        golden_glove_ids.add(winner["playerSeasonId"])
        awards.append(
            {
                "seasonYear": year,
                "awardType": "GoldenGlove",
                "playerSeasonId": winner["playerSeasonId"],
                "position": position,
            }
        )
    outfielders = [
        record
        for record in ordered
        if record["position"] in {"LF", "CF", "RF"}
        and record["playerSeasonId"] not in golden_glove_ids
    ][:3]
    for winner in outfielders:
        golden_glove_ids.add(winner["playerSeasonId"])
        awards.append(
            {
                "seasonYear": year,
                "awardType": "GoldenGlove",
                "playerSeasonId": winner["playerSeasonId"],
                "position": "OF",
            }
        )
    designated_hitter = best("DH", golden_glove_ids)
    awards.append(
        {
            "seasonYear": year,
            "awardType": "GoldenGlove",
            "playerSeasonId": designated_hitter["playerSeasonId"],
            "position": "DH",
        }
    )

    all_star_ids: list[str] = []
    for position in HITTER_POSITIONS:
        candidate = best(position, set(all_star_ids))
        all_star_ids.append(candidate["playerSeasonId"])
    # 공통 ActiveRoster의 SP5+Bullpen/Setup/Closer6 쿼터와 같은 11명 투수 구성을 사용한다.
    pitcher_candidates = [record for record in ordered if record["position"] == "P"][:11]
    all_star_ids.extend(record["playerSeasonId"] for record in pitcher_candidates)
    all_star_ids.extend(
        record["playerSeasonId"]
        for record in ordered
        if record["position"] != "P" and record["playerSeasonId"] not in all_star_ids
    )
    all_star_ids = all_star_ids[:25]
    record_by_id = {record["playerSeasonId"]: record for record in records}
    for player_season_id in all_star_ids:
        awards.append(
            {
                "seasonYear": year,
                "awardType": "AllStar",
                "playerSeasonId": player_season_id,
                "position": record_by_id[player_season_id]["position"],
            }
        )

    awards.extend(
        (
            {
                "seasonYear": year,
                "awardType": "RegularSeasonMvp",
                "playerSeasonId": ordered[0]["playerSeasonId"],
                "position": ordered[0]["position"],
            },
            {
                "seasonYear": year,
                "awardType": "AllStarGameMvp",
                "playerSeasonId": all_star_ids[0],
                "position": record_by_id[all_star_ids[0]]["position"],
            },
            {
                "seasonYear": year,
                "awardType": "PostseasonMvp",
                "playerSeasonId": ordered[1]["playerSeasonId"],
                "position": ordered[1]["position"],
            },
        )
    )
    return awards


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


def bake_year(data: dict[str, Any], generation_seed: int) -> dict[str, Any]:
    year = int(data["year"])
    source_players = data["players"]
    hitters = [player for player in source_players if player.get("hitterStats")]
    pitchers = [player for player in source_players if player.get("pitcherStats")]
    if not hitters or not pitchers:
        raise ValueError(f"타자/투수 Reference가 모두 필요합니다: {year}")
    hitter_vectors_by_id, _, _ = build_adjusted_feature_pool(hitters, year, "Hitter")
    pitcher_vectors_by_id, _, _ = build_adjusted_feature_pool(pitchers, year, "Pitcher")
    hitter_vectors = [hitter_vectors_by_id[str(player["sourcePlayerId"])] for player in hitters]
    pitcher_vectors = [pitcher_vectors_by_id[str(player["sourcePlayerId"])] for player in pitchers]
    seasons: list[dict[str, Any]] = []
    persons: list[dict[str, Any]] = []
    original_records: list[dict[str, Any]] = []
    teams: list[dict[str, Any]] = []

    for team_index, franchise_id in enumerate(FRANCHISE_IDS):
        team_key = f"{franchise_id}_{year}"
        core_card_ids: list[str] = []
        all_card_ids: list[str] = []
        for slot_index, roster_role in enumerate(TEAM_POOL_ROLES):
            player_type = "Hitter" if "Hitter" in roster_role else "Pitcher"
            references = hitters if player_type == "Hitter" else pitchers
            normalized = hitter_vectors if player_type == "Hitter" else pitcher_vectors
            rng = random.Random(stable_seed(STABLE_GENERATION_VERSION, generation_seed, year, franchise_id, roster_role))
            vector: tuple[float, ...] | None = None
            selected_indices: tuple[int, ...] = ()
            distance = 0.0
            for _ in range(32):
                vector, selected_indices = mixed_vector(normalized, rng, rng.randint(3, 7))
                distance = nearest_distance(vector, normalized)
                if distance >= 0.12:
                    break
            assert vector is not None
            global_index = team_index * len(TEAM_POOL_ROLES) + slot_index
            person_id = "PERSON_" + stable_digest(STABLE_GENERATION_VERSION, generation_seed, year, global_index)
            season_id = "SEASON_" + stable_digest(person_id, year, team_key)
            card_id = f"{season_id}:Normal"
            position = (
                HITTER_POSITIONS[slot_index]
                if slot_index < len(HITTER_POSITIONS)
                else position_from_source(references[selected_indices[0]], "CF")
            ) if player_type == "Hitter" else "P"
            if player_type == "Pitcher":
                if roster_role.startswith("StartingPitcher"):
                    pitcher_role = "Starter"
                elif roster_role == "Setup":
                    pitcher_role = "Setup"
                elif roster_role == "Closer":
                    pitcher_role = "Closer"
                else:
                    pitcher_role = "MiddleRelief"
            else:
                pitcher_role = "MiddleRelief"
            ratings = to_ratings(player_type, vector)
            overall = mean(ratings[:6] if player_type == "Hitter" else ratings[6:])
            source_reference_names = list(
                dict.fromkeys(
                    str(references[index].get("playerName") or "").strip()
                    for index in selected_indices
                    if str(references[index].get("playerName") or "").strip()
                )
            )
            if not source_reference_names:
                raise ValueError(f"원본 선수 이름이 없는 Reference 조합입니다: {year} {roster_role}")
            persons.append(
                {
                    "playerPersonId": person_id,
                    "originalName": source_reference_names[0],
                    "fictionalName": "",
                    "birthYear": year - rng.randint(18, 34),
                    "bats": "Left" if rng.random() < 0.28 else "Right",
                    "throws": "Left" if rng.random() < 0.18 else "Right",
                    "primaryPosition": position,
                    "registrationType": "Foreign" if global_index % 97 == 0 else "Domestic",
                    "careerStartYear": year,
                    "careerEndYear": year,
                    "personPotentialTrait": [rng.randint(70, 100) for _ in ABILITY_NAMES],
                }
            )
            seasons.append(
                {
                    "playerSeasonId": season_id,
                    "playerPersonId": person_id,
                    "originYear": year,
                    "originFranchiseId": franchise_id,
                    "originTeamSeasonKey": team_key,
                    "position": position,
                    "pitcherRole": pitcher_role,
                    "playerType": player_type,
                    "registrationType": persons[-1]["registrationType"],
                    "baseAttributes": ratings,
                    "cost": 0,
                    "trainingCeiling": [],
                    "rosterRole": roster_role,
                    "referenceSimilarityDistance": round(distance, 6),
                    "sourceReferenceNames": source_reference_names,
                    "overall": overall,
                }
            )
            original_records.append(
                original_record(
                    season_id,
                    team_key,
                    year,
                    player_type,
                    position,
                    references,
                    selected_indices,
                )
            )
            if not roster_role.startswith("Reserve"):
                core_card_ids.append(card_id)
            all_card_ids.append(card_id)
        teams.append(
            {
                "teamSeasonKey": team_key,
                "franchiseId": franchise_id,
                "originYear": year,
                "allNormalCardIds": all_card_ids,
                "core25CardIds": core_card_ids,
                "referenceStrength": 0.0,
            }
        )

    for season in seasons:
        del season["overall"]
    assign_origin_year_costs(seasons)
    for season in seasons:
        cost = season["cost"]
        low, high = headroom_range(cost)
        rng = random.Random(stable_seed("ceiling", generation_seed, season["playerSeasonId"]))
        season["trainingCeiling"] = [min(99, rating + rng.randint(low, high)) for rating in season["baseAttributes"]]

    season_by_id = {season["playerSeasonId"]: season for season in seasons}
    for team in teams:
        team["referenceStrength"] = round(
            mean(
                mean(season_by_id[card_id.removesuffix(":Normal")]["baseAttributes"])
                for card_id in team["core25CardIds"]
            ),
            4,
        )
    result = {
        "year": year,
        "playerPersons": persons,
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
        "originalSeasonRecords": original_records,
    }
    result["originalAwardRecords"] = build_original_awards(result)
    return result


def validate_bake(content: dict[str, Any]) -> None:
    name_data_policy = str(content.get("manifest", {}).get("nameDataPolicy") or "")
    persons = content["playerPersons"]
    person_ids = [person["playerPersonId"] for person in content["playerPersons"]]
    if len(person_ids) != len(set(person_ids)):
        raise ValueError("PlayerPersonId가 중복되었습니다.")
    fictional_names = [str(person.get("fictionalName") or "") for person in persons]
    if any(not is_natural_fictional_name(name) for name in fictional_names):
        raise ValueError("Runtime 가명은 음절 반복이 없는 검증된 3음절 한국 이름이어야 합니다.")
    if len(fictional_names) != len(set(fictional_names)):
        raise ValueError("Runtime 가상 선수 이름이 중복되었습니다.")
    if name_data_policy == EDITOR_NAME_POLICY:
        if any(not str(person.get("originalName") or "").strip() for person in persons):
            raise ValueError("Editor Archive의 PlayerPerson에 대표 원본 이름이 없습니다.")
        if any(person["fictionalName"] == person["originalName"] for person in persons):
            raise ValueError("Runtime 가명이 실제 원본 이름과 같습니다.")
    elif name_data_policy == RUNTIME_NAME_POLICY:
        if any("originalName" in person for person in persons):
            raise ValueError("Runtime 콘텐츠에 Editor 전용 원본 이름이 남아 있습니다.")
    else:
        raise ValueError(f"지원하지 않는 이름 데이터 정책입니다: {name_data_policy}")

    ids: set[str] = set()
    for year_content in content["years"]:
        seasons = year_content["playerSeasons"]
        season_by_id = {season["playerSeasonId"]: season for season in seasons}
        if len(year_content["teamSeasons"]) != 10:
            raise ValueError("정규 Franchise Team은 연도마다 정확히 10개여야 합니다.")
        for season in seasons:
            if season["playerSeasonId"] in ids:
                raise ValueError("PlayerSeasonId가 중복되었습니다.")
            ids.add(season["playerSeasonId"])
            if not 1 <= season["cost"] <= 10:
                raise ValueError("Cost는 1~10이어야 합니다.")
            if any(ceiling < base for base, ceiling in zip(season["baseAttributes"], season["trainingCeiling"])):
                raise ValueError("TrainingCeiling이 BaseAttributes보다 낮습니다.")
            if season["referenceSimilarityDistance"] < 0.12:
                raise ValueError("실존 Reference와 지나치게 가까운 Synthetic PlayerSeason이 있습니다.")
            if name_data_policy == EDITOR_NAME_POLICY:
                if not season.get("sourceReferenceNames"):
                    raise ValueError("Editor Archive의 PlayerSeason에 원본 Reference 이름이 없습니다.")
            elif "sourceReferenceNames" in season:
                raise ValueError("Runtime 콘텐츠에 Editor 전용 Reference 이름이 남아 있습니다.")
        for team in year_content["teamSeasons"]:
            if len(team["core25CardIds"]) != 25 or len(set(team["core25CardIds"])) != 25:
                raise ValueError("Core25는 중복 없는 정확한 25장이어야 합니다.")
            if not set(team["core25CardIds"]).issubset(team["allNormalCardIds"]):
                raise ValueError("Core25는 해당 TeamSeason의 전체 Normal Pool에 포함되어야 합니다.")
            if not 28 <= len(team["allNormalCardIds"]) <= 40:
                raise ValueError("TeamSeason 전체 Normal Pool은 권장 범위 28~40명을 지켜야 합니다.")
            team_seasons = [season_by_id[card_id.removesuffix(":Normal")] for card_id in team["core25CardIds"]]
            if len({season["playerPersonId"] for season in team_seasons}) != 25:
                raise ValueError("Core25에 같은 PlayerPerson이 중복되었습니다.")
            if sum(season["registrationType"] == "Foreign" for season in team_seasons) > 3:
                raise ValueError("Core25의 Foreign 등록 선수는 최대 3명입니다.")
            roles = [season_by_id[card_id.removesuffix(":Normal")]["rosterRole"] for card_id in team["core25CardIds"]]
            if sum(role.startswith("StartingHitter") for role in roles) != 9:
                raise ValueError("주전 야수는 9명이어야 합니다.")
            if sum(role.startswith("BenchHitter") for role in roles) != 5:
                raise ValueError("벤치 야수는 5명이어야 합니다.")
            if sum(role.startswith("StartingPitcher") for role in roles) != 5:
                raise ValueError("선발 투수는 5명이어야 합니다.")
            if sum(role.startswith("Bullpen") for role in roles) != 4:
                raise ValueError("일반 불펜은 4명이어야 합니다.")
            if roles.count("Setup") != 1 or roles.count("Closer") != 1:
                raise ValueError("Setup/Closer는 각 1명이어야 합니다.")
        awards = year_content["originalAwardRecords"]
        if any(award["playerSeasonId"] not in season_by_id for award in awards):
            raise ValueError("Original Award가 존재하지 않는 PlayerSeason을 참조합니다.")
        all_stars = [award for award in awards if award["awardType"] == "AllStar"]
        if len(all_stars) != 25 or sum(award["position"] == "P" for award in all_stars) != 11:
            raise ValueError("Original All-Star는 공통 Position quota를 만족하는 25명이어야 합니다.")
        golden_gloves = [award for award in awards if award["awardType"] == "GoldenGlove"]
        golden_glove_positions = [award["position"] for award in golden_gloves]
        if (
            len(golden_gloves) != 10
            or golden_glove_positions.count("OF") != 3
            or any(
                golden_glove_positions.count(position) != 1
                for position in ("P", "C", "1B", "2B", "3B", "SS", "DH")
            )
        ):
            raise ValueError("Golden Glove는 P/C/내야/DH와 OF 3명을 합쳐 10명이어야 합니다.")


def validate_editor_original_content(content: dict[str, Any]) -> None:
    """Editor 원본 Archive가 실명 선수·시즌을 합성 없이 1:1로 보존하는지 검증한다."""
    policy = str(content.get("manifest", {}).get("nameDataPolicy") or "")
    if policy != EDITOR_ORIGINAL_NAME_POLICY:
        raise ValueError(f"Editor 원본 이름 정책이 아닙니다: {policy}")

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
                for component in attribute_trace.get("components") or []:
                    for field in ("groupMean", "groupStdDev", "rawZ", "reliability", "adjustedZ", "weight", "contribution"):
                        if not math.isfinite(safe_number(component.get(field), float("nan"))):
                            raise ValueError("Ability 파생 Trace에 NaN/Infinity가 있습니다.")
            cost_trace = season.get("costDerivationTrace") or {}
            if (
                int(cost_trace.get("originYear", -1)) != int(season["originYear"])
                or int(cost_trace.get("populationCount", 0)) != len(seasons)
                or int(cost_trace.get("cost", 0)) != int(season["cost"])
            ):
                raise ValueError("Cost 파생 Trace의 OriginYear 모집단 또는 Cost가 일치하지 않습니다.")

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


def validate_archive_content(content: dict[str, Any]) -> None:
    manifest = content.get("manifest", {})
    validate_derivation_manifest(manifest)
    policy = str(manifest.get("nameDataPolicy") or "")
    if policy == EDITOR_ORIGINAL_NAME_POLICY:
        validate_editor_original_content(content)
        return
    validate_bake(content)


def link_careers(
    year_contents: list[dict[str, Any]],
    generation_seed: int,
    source_names: Iterable[str],
) -> list[dict[str, Any]]:
    first_year = min(year["year"] for year in year_contents)
    persons: dict[str, dict[str, Any]] = {}
    previous_ratings: dict[str, list[int]] = {}
    for year_content in sorted(year_contents, key=lambda item: item["year"]):
        year = year_content["year"]
        source_persons = year_content.pop("playerPersons")
        seasons = year_content["playerSeasons"]
        for slot_index, (source_person, season) in enumerate(zip(source_persons, seasons)):
            career_length = 5 + stable_seed("career-length", generation_seed, slot_index) % 8
            career_episode = (year - first_year) // career_length
            person_id = "PERSON_" + stable_digest(
                STABLE_GENERATION_VERSION,
                generation_seed,
                slot_index,
                career_episode,
            )
            season["playerPersonId"] = person_id
            previous = previous_ratings.get(person_id)
            if previous is not None:
                season["baseAttributes"] = [
                    max(25, min(95, round(current * 0.75 + prior * 0.25)))
                    for current, prior in zip(season["baseAttributes"], previous)
                ]
            previous_ratings[person_id] = list(season["baseAttributes"])

            person = persons.get(person_id)
            if person is None:
                person = dict(source_person)
                person["playerPersonId"] = person_id
                person["careerStartYear"] = year
                person["careerEndYear"] = year
                persons[person_id] = person
            else:
                person["careerEndYear"] = year

        season_by_id = {season["playerSeasonId"]: season for season in seasons}
        assign_origin_year_costs(seasons)
        for season in seasons:
            cost = season["cost"]
            low, high = headroom_range(cost)
            rng = random.Random(stable_seed("ceiling", generation_seed, season["playerSeasonId"]))
            season["trainingCeiling"] = [
                min(99, rating + rng.randint(low, high))
                for rating in season["baseAttributes"]
            ]
        for team in year_content["teamSeasons"]:
            team["referenceStrength"] = round(
                mean(
                    mean(season_by_id[card_id.removesuffix(":Normal")]["baseAttributes"])
                    for card_id in team["core25CardIds"]
                ),
                4,
            )
    fictional_names = build_fictional_name_map(persons.keys(), source_names)
    for person_id, person in persons.items():
        person["fictionalName"] = fictional_names[person_id]
    return sorted(persons.values(), key=lambda person: person["playerPersonId"])


def bake(input_dir: Path, years: list[int], generation_seed: int) -> dict[str, Any]:
    references = [
        load_reference(input_dir / f"{year}.json", year)
        for year in sorted(years)
    ]
    reference_manifest_fields = build_reference_manifest_fields(references)
    year_contents = [bake_year(data, generation_seed) for data in references]
    source_names = {
        str(player.get("playerName") or "")
        for data in references
        for player in data["players"]
    }
    persons = link_careers(year_contents, generation_seed, source_names)
    content: dict[str, Any] = {
        "schemaVersion": CONTENT_SCHEMA_VERSION,
        "playerPersons": persons,
        "years": year_contents,
        "manifest": {
            **reference_manifest_fields,
            "generatorVersion": GENERATOR_VERSION,
            "balanceVersion": BALANCE_VERSION,
            "generationSeed": generation_seed,
            "namePolicyVersion": NAME_POLICY_VERSION,
            "nameDataPolicy": EDITOR_NAME_POLICY,
            "contentHash": "",
        },
    }
    validate_bake(content)
    refresh_content_hash(content)
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
    player_persons_payload = canonical_json_bytes(content["playerPersons"])
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
        },
    }
    write_bytes_atomically(output_dir / "manifest.json", canonical_json_bytes(manifest))
    return manifest


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

    player_persons = json.loads(person_payload.decode("utf-8"))
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
        description="KBO Reference를 1:1 Editor 원본 Archive와 분리된 Runtime-safe 합성 콘텐츠로 Bake합니다."
    )
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--years", type=parse_years, required=True)
    parser.add_argument("--seed", type=int, default=20260901)
    output_group = parser.add_mutually_exclusive_group(required=True)
    output_group.add_argument(
        "--output",
        type=Path,
        help="원본 이름을 제거하고 자연스러운 가명만 남긴 Runtime-safe 단일 JSON 경로입니다.",
    )
    output_group.add_argument(
        "--editor-assets-dir",
        type=Path,
        help="실제 선수·시즌을 1:1로 보존하는 Editor 원본 Archive 경로입니다. Runtime 합성본은 Runtime/에 생성됩니다.",
    )
    parser.add_argument(
        "--verify-editor-assets",
        action="store_true",
        help="분할 Editor Asset을 다시 읽어 파일 Hash와 Bake 규칙을 검증합니다.",
    )
    args = parser.parse_args()
    synthetic_audit_content = bake(args.input_dir, args.years, args.seed)
    if args.output is not None:
        content = create_runtime_safe_content(synthetic_audit_content)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        write_bytes_atomically(
            args.output,
            (json.dumps(content, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8"),
        )
    else:
        content = build_editor_original_content(args.input_dir, args.years)
        write_editor_asset_archive(content, args.editor_assets_dir)
        runtime_content = create_runtime_safe_content(synthetic_audit_content)
        runtime_assets_dir = args.editor_assets_dir / "Runtime"
        write_editor_asset_archive(runtime_content, runtime_assets_dir)
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
