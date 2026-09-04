from __future__ import annotations

from bisect import bisect_left, bisect_right
from dataclasses import dataclass, field
import hashlib
import json
import math
import random
from typing import Any, Iterable, Mapping, Sequence


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
HITTER_ABILITY_OFFSET = 0
PITCHER_ABILITY_OFFSET = 6
PLAYER_TYPE_HITTER = "Hitter"
PLAYER_TYPE_PITCHER = "Pitcher"
SOURCE_BACKED = "SourceBacked"
REPLACEMENT_GENERATED = "ReplacementGenerated"
ROSTER_SHORTAGE = "RosterShortage"

DEFAULT_COST_PERCENTILE_THRESHOLDS = (
    (0.05, 1),
    (0.15, 2),
    (0.30, 3),
    (0.45, 4),
    (0.60, 5),
    (0.72, 6),
    (0.82, 7),
    (0.90, 8),
    (0.97, 9),
    (1.01, 10),
)


@dataclass(frozen=True)
class ReplacementGenerationSettings:
    """Replacement 분포와 Cost 판정을 Bake 입력 데이터로 고정한다."""

    percentile_lower_inclusive: float = 0.05
    percentile_upper_inclusive: float = 0.30
    covariance_scale: float = 0.65
    covariance_regularization: float = 1e-6
    minimum_group_population: int = 6
    maximum_sampling_attempts: int = 256
    rating_minimum: int = 25
    rating_maximum: int = 95
    cost_percentile_thresholds: tuple[tuple[float, int], ...] = DEFAULT_COST_PERCENTILE_THRESHOLDS
    composite_profiles: Mapping[str, tuple[float, ...]] = field(
        default_factory=lambda: {
            "Hitter:Default": (1.0,) * 6,
            "Pitcher:Default": (1.0,) * 6,
        }
    )

    def __post_init__(self) -> None:
        if not 0.0 <= self.percentile_lower_inclusive < self.percentile_upper_inclusive <= 1.0:
            raise ValueError("Replacement percentile band가 유효하지 않습니다.")
        if self.covariance_scale <= 0.0 or self.covariance_regularization < 0.0:
            raise ValueError("Replacement covariance 설정이 유효하지 않습니다.")
        if self.minimum_group_population < 2 or self.maximum_sampling_attempts < 1:
            raise ValueError("Replacement 표본/재시도 설정이 유효하지 않습니다.")
        if self.rating_minimum >= self.rating_maximum:
            raise ValueError("Replacement 능력치 범위가 유효하지 않습니다.")

        previous = 0.0
        for upper_exclusive, cost in self.cost_percentile_thresholds:
            if upper_exclusive <= previous or not 1 <= cost <= 10:
                raise ValueError("Cost percentile threshold가 유효하지 않습니다.")
            previous = upper_exclusive
        if previous <= 1.0:
            raise ValueError("Cost percentile threshold가 전체 모집단을 덮지 않습니다.")

        for profile_name, weights in self.composite_profiles.items():
            if len(weights) != 6 or sum(weights) <= 0.0 or any(weight < 0.0 for weight in weights):
                raise ValueError(f"Role composite profile이 유효하지 않습니다: {profile_name}")

    @classmethod
    def from_balance(cls, balance: Mapping[str, Any]) -> ReplacementGenerationSettings:
        """derivation_balance 형태를 독립 모듈 설정으로 변환한다."""

        replacement = balance.get("replacementGeneration") or {}
        rating = balance.get("rating") or {}
        levels = balance.get("roleCompositeWeightLevels") or {}
        profiles: dict[str, tuple[float, ...]] = {}
        for player_type, role_profiles in (balance.get("roleCompositeProfiles") or {}).items():
            for role, profile in role_profiles.items():
                profiles[f"{player_type}:{role}"] = tuple(float(levels[level]) for level in profile)

        thresholds = tuple(
            (float(row["upperExclusive"]), int(row["cost"]))
            for row in balance.get("costPercentileThresholds", ())
        )
        return cls(
            percentile_lower_inclusive=float(replacement.get("percentileLowerInclusive", 0.05)),
            percentile_upper_inclusive=float(replacement.get("percentileUpperInclusive", 0.30)),
            covariance_scale=float(replacement.get("covarianceScale", 0.65)),
            covariance_regularization=float(replacement.get("covarianceRegularization", 1e-6)),
            minimum_group_population=int(replacement.get("minimumGroupPopulation", 6)),
            maximum_sampling_attempts=int(replacement.get("maximumSamplingAttempts", 256)),
            rating_minimum=int(rating.get("minimum", 25)),
            rating_maximum=int(rating.get("maximum", 95)),
            cost_percentile_thresholds=thresholds or DEFAULT_COST_PERCENTILE_THRESHOLDS,
            composite_profiles=profiles or {
                "Hitter:Default": (1.0,) * 6,
                "Pitcher:Default": (1.0,) * 6,
            },
        )


@dataclass(frozen=True)
class ShortageSlotSpec:
    """Core25 부족 한 자리의 결정론적 생성 요청이다."""

    origin_year: int
    origin_franchise_id: str
    origin_team_season_key: str
    player_type: str
    position: str = ""
    natural_pitcher_role: str = ""
    assigned_roster_role: str = ""
    slot_key: str = ""
    count: int = 1


@dataclass
class ReplacementGenerationResult:
    replacements: list[dict[str, Any]]
    generation_traces: list[dict[str, Any]]
    source_cost_thresholds: dict[int, dict[str, Any]]

    def to_dict(self) -> dict[str, Any]:
        return {
            "replacements": self.replacements,
            "generationTraces": self.generation_traces,
            "sourceCostThresholds": self.source_cost_thresholds,
        }


def generate_replacements(
    source_backed_seasons: Sequence[Mapping[str, Any]],
    shortage_slots: Sequence[ShortageSlotSpec | Mapping[str, Any]],
    seed: int | str,
    settings: ReplacementGenerationSettings,
) -> ReplacementGenerationResult:
    """Source aggregate만으로 Core25 부족 수만큼 Replacement를 Bake한다."""

    sources = [dict(row) for row in source_backed_seasons]
    _validate_source_population(sources)
    expanded_slots = _expand_and_sort_slots(shortage_slots)
    source_thresholds, sorted_source_composites = _build_source_cost_thresholds(sources, settings)
    sources_by_year_type = _group_sources_by_year_type(sources)
    source_vectors = {tuple(int(value) for value in row["baseAttributes"]) for row in sources}
    occupied_vectors = set(source_vectors)

    replacements: list[dict[str, Any]] = []
    traces: list[dict[str, Any]] = []
    for slot, occurrence in expanded_slots:
        role = _role_for_slot(slot)
        profile_weights, profile_name = _resolve_profile_weights(slot.player_type, role, settings)
        group_rows, group_key, fallback_reason = _resolve_distribution_group(
            slot,
            sources_by_year_type,
            settings.minimum_group_population,
        )
        offset = _ability_offset(slot.player_type)
        relevant_vectors = [
            tuple(float(value) for value in row["baseAttributes"][offset : offset + 6])
            for row in group_rows
        ]
        mean_vector = _mean_vector(relevant_vectors)
        covariance = _covariance_matrix(relevant_vectors)
        comparison_composites = sorted(
            _weighted_composite(vector, profile_weights) for vector in relevant_vectors
        )

        slot_identity = _slot_identity(slot, occurrence)
        random_seed = _stable_seed("ReplacementGenerated", seed, slot_identity)
        rng = random.Random(random_seed)
        relevant_ratings, sample_trace = _sample_low_tail_ratings(
            rng,
            mean_vector,
            covariance,
            comparison_composites,
            profile_weights,
            offset,
            occupied_vectors,
            settings,
        )
        base_attributes = [50] * len(ABILITY_NAMES)
        base_attributes[offset : offset + 6] = relevant_ratings
        occupied_vectors.add(tuple(base_attributes))

        composite = _weighted_composite(relevant_ratings, profile_weights)
        year_composites = sorted_source_composites.get(slot.origin_year)
        if not year_composites:
            raise ValueError(f"OriginYear Source Cost 모집단이 없습니다: {slot.origin_year}")
        insertion_lower_rank, insertion_upper_rank, insertion_percentile = _insertion_percentile(
            year_composites,
            composite,
        )
        cost = _cost_from_percentile(insertion_percentile, settings.cost_percentile_thresholds)
        cost_threshold = source_thresholds[slot.origin_year]

        person_digest = _stable_digest("replacement-person", seed, slot_identity, length=24)
        season_digest = _stable_digest("replacement-season", seed, slot_identity, length=24)
        person_id = f"REPL-PERSON-{person_digest}"
        season_id = f"REPL-SEASON-{season_digest}"
        cost_trace = {
            "dataProvenance": REPLACEMENT_GENERATED,
            "generationReason": ROSTER_SHORTAGE,
            "costPopulationSource": "OriginYearSourceBacked",
            "sourcePopulationSize": len(year_composites),
            "replacementExcludedFromThresholdCalculation": True,
            "composite": round(composite, 8),
            "insertionLowerRank": insertion_lower_rank,
            "insertionUpperRank": insertion_upper_rank,
            "percentile": round(insertion_percentile, 8),
            "thresholds": cost_threshold["thresholds"],
            "cost": cost,
        }
        generation_trace = {
            "playerSeasonId": season_id,
            "dataProvenance": REPLACEMENT_GENERATED,
            "generationReason": ROSTER_SHORTAGE,
            "slotKey": slot_identity,
            "originYear": slot.origin_year,
            "playerType": slot.player_type,
            "requestedRole": role,
            "aggregateGroupKey": group_key,
            "aggregatePopulationCount": len(group_rows),
            "aggregateFallbackReason": fallback_reason,
            "aggregateMean": [round(value, 8) for value in mean_vector],
            "aggregateCovariance": [
                [round(value, 8) for value in row] for row in covariance
            ],
            "compositeProfile": profile_name,
            "compositeWeights": [round(value, 8) for value in profile_weights],
            "replacementPercentileBand": {
                "lowerInclusive": settings.percentile_lower_inclusive,
                "upperInclusive": settings.percentile_upper_inclusive,
            },
            "groupInsertionPercentile": sample_trace["groupInsertionPercentile"],
            "samplingAttempts": sample_trace["samplingAttempts"],
            "acceptedWithinBand": sample_trace["acceptedWithinBand"],
            "collisionAdjustmentCount": sample_trace["collisionAdjustmentCount"],
            "randomSeedDigest": _stable_digest(random_seed, length=16),
            "costDerivation": cost_trace,
        }
        replacement = {
            "playerPersonId": person_id,
            "playerSeasonId": season_id,
            "dataProvenance": REPLACEMENT_GENERATED,
            "generationReason": ROSTER_SHORTAGE,
            "originYear": slot.origin_year,
            "originFranchiseId": slot.origin_franchise_id,
            "originTeamSeasonKey": slot.origin_team_season_key,
            "playerType": slot.player_type,
            "position": slot.position if slot.player_type == PLAYER_TYPE_HITTER else "P",
            "pitcherRole": slot.natural_pitcher_role if slot.player_type == PLAYER_TYPE_PITCHER else "",
            "naturalPitcherRole": slot.natural_pitcher_role if slot.player_type == PLAYER_TYPE_PITCHER else "",
            "assignedRosterRole": slot.assigned_roster_role,
            "baseAttributes": base_attributes,
            "cost": cost,
            "costDerivationTrace": cost_trace,
            "replacementGenerationTrace": generation_trace,
        }
        replacements.append(replacement)
        traces.append(generation_trace)

    replacements.sort(key=lambda row: row["playerSeasonId"])
    traces.sort(key=lambda row: row["playerSeasonId"])
    return ReplacementGenerationResult(replacements, traces, source_thresholds)


def build_source_cost_thresholds(
    source_backed_seasons: Sequence[Mapping[str, Any]],
    settings: ReplacementGenerationSettings,
) -> dict[int, dict[str, Any]]:
    """Replacement를 제외한 SourceBacked Cost composite 경계를 공개한다."""

    sources = [dict(row) for row in source_backed_seasons]
    _validate_source_population(sources)
    thresholds, _ = _build_source_cost_thresholds(sources, settings)
    return thresholds


def _validate_source_population(sources: Sequence[Mapping[str, Any]]) -> None:
    if not sources:
        raise ValueError("Replacement 생성에 필요한 SourceBacked 모집단이 비어 있습니다.")
    seen_seasons: set[str] = set()
    for row in sources:
        provenance = row.get("dataProvenance", SOURCE_BACKED)
        if provenance != SOURCE_BACKED:
            raise ValueError("Source Cost/aggregate 모집단에는 SourceBacked만 허용됩니다.")
        ratings = row.get("baseAttributes")
        if not isinstance(ratings, Sequence) or isinstance(ratings, (str, bytes)) or len(ratings) != 12:
            raise ValueError("SourceBacked BaseAttributes는 12개여야 합니다.")
        if any(not math.isfinite(float(value)) for value in ratings):
            raise ValueError("SourceBacked BaseAttributes에 NaN/Infinity가 있습니다.")
        if row.get("playerType") not in (PLAYER_TYPE_HITTER, PLAYER_TYPE_PITCHER):
            raise ValueError("SourceBacked playerType이 유효하지 않습니다.")
        if "originYear" not in row:
            raise ValueError("SourceBacked OriginYear가 없습니다.")
        season_id = str(row.get("playerSeasonId") or "")
        if season_id and season_id in seen_seasons:
            raise ValueError(f"Source PlayerSeason이 중복되었습니다: {season_id}")
        if season_id:
            seen_seasons.add(season_id)


def _expand_and_sort_slots(
    shortage_slots: Sequence[ShortageSlotSpec | Mapping[str, Any]],
) -> list[tuple[ShortageSlotSpec, int]]:
    normalized: list[ShortageSlotSpec] = []
    for raw in shortage_slots:
        if isinstance(raw, ShortageSlotSpec):
            slot = raw
        else:
            slot = ShortageSlotSpec(
                origin_year=int(raw.get("originYear", raw.get("origin_year", 0))),
                origin_franchise_id=str(raw.get("originFranchiseId", raw.get("origin_franchise_id", ""))),
                origin_team_season_key=str(raw.get("originTeamSeasonKey", raw.get("origin_team_season_key", ""))),
                player_type=str(raw.get("playerType", raw.get("player_type", ""))),
                position=str(raw.get("position", "")),
                natural_pitcher_role=str(raw.get("naturalPitcherRole", raw.get("natural_pitcher_role", ""))),
                assigned_roster_role=str(raw.get("assignedRosterRole", raw.get("assigned_roster_role", ""))),
                slot_key=str(raw.get("slotKey", raw.get("slot_key", ""))),
                count=int(raw.get("count", 1)),
            )
        _validate_slot(slot)
        normalized.append(slot)

    expanded: list[ShortageSlotSpec] = []
    for slot in normalized:
        expanded.extend([slot] * slot.count)
    expanded.sort(key=_slot_sort_key)

    occurrences: dict[tuple[Any, ...], int] = {}
    result: list[tuple[ShortageSlotSpec, int]] = []
    for slot in expanded:
        key = _slot_sort_key(slot)
        occurrence = occurrences.get(key, 0)
        occurrences[key] = occurrence + 1
        result.append((slot, occurrence))
    return result


def _validate_slot(slot: ShortageSlotSpec) -> None:
    if slot.origin_year <= 0 or not slot.origin_franchise_id or not slot.origin_team_season_key:
        raise ValueError("Replacement shortage slot의 연도/구단 정보가 유효하지 않습니다.")
    if slot.player_type == PLAYER_TYPE_HITTER:
        if not slot.position:
            raise ValueError("Hitter replacement slot에는 position이 필요합니다.")
    elif slot.player_type == PLAYER_TYPE_PITCHER:
        if not slot.natural_pitcher_role:
            raise ValueError("Pitcher replacement slot에는 naturalPitcherRole이 필요합니다.")
    else:
        raise ValueError("Replacement shortage slot의 playerType이 유효하지 않습니다.")
    if slot.count < 1:
        raise ValueError("Replacement shortage slot count는 1 이상이어야 합니다.")


def _slot_sort_key(slot: ShortageSlotSpec) -> tuple[Any, ...]:
    return (
        slot.origin_year,
        slot.origin_franchise_id,
        slot.origin_team_season_key,
        slot.player_type,
        slot.position,
        slot.natural_pitcher_role,
        slot.assigned_roster_role,
        slot.slot_key,
    )


def _slot_identity(slot: ShortageSlotSpec, occurrence: int) -> str:
    explicit = slot.slot_key or "auto"
    return ":".join(
        (
            str(slot.origin_year),
            slot.origin_franchise_id,
            slot.origin_team_season_key,
            slot.player_type,
            slot.position or slot.natural_pitcher_role,
            slot.assigned_roster_role,
            explicit,
            str(occurrence),
        )
    )


def _group_sources_by_year_type(
    sources: Sequence[Mapping[str, Any]],
) -> dict[tuple[int, str], list[Mapping[str, Any]]]:
    result: dict[tuple[int, str], list[Mapping[str, Any]]] = {}
    for row in sources:
        key = (int(row["originYear"]), str(row["playerType"]))
        result.setdefault(key, []).append(row)
    for rows in result.values():
        rows.sort(key=lambda row: str(row.get("playerSeasonId") or ""))
    return result


def _resolve_distribution_group(
    slot: ShortageSlotSpec,
    sources_by_year_type: Mapping[tuple[int, str], list[Mapping[str, Any]]],
    minimum_group_population: int,
) -> tuple[list[Mapping[str, Any]], str, str]:
    year_type_rows = sources_by_year_type.get((slot.origin_year, slot.player_type), [])
    if not year_type_rows:
        raise ValueError(
            f"Replacement aggregate Source 모집단이 없습니다: {slot.origin_year}/{slot.player_type}"
        )
    role = _role_for_slot(slot)
    role_rows = [row for row in year_type_rows if _role_for_source(row) == role]
    requested_key = f"{slot.origin_year}:{slot.player_type}:{role}"
    if len(role_rows) >= minimum_group_population:
        return role_rows, requested_key, ""
    fallback_key = f"{slot.origin_year}:{slot.player_type}:AllRoles"
    reason = f"ROLE_GROUP_TOO_SMALL:{len(role_rows)}<{minimum_group_population}:{requested_key}"
    return list(year_type_rows), fallback_key, reason


def _role_for_slot(slot: ShortageSlotSpec) -> str:
    return slot.position if slot.player_type == PLAYER_TYPE_HITTER else slot.natural_pitcher_role


def _role_for_source(row: Mapping[str, Any]) -> str:
    if row["playerType"] == PLAYER_TYPE_HITTER:
        return str(row.get("position") or "Default")
    return str(row.get("naturalPitcherRole") or row.get("pitcherRole") or "Default")


def _ability_offset(player_type: str) -> int:
    return HITTER_ABILITY_OFFSET if player_type == PLAYER_TYPE_HITTER else PITCHER_ABILITY_OFFSET


def _resolve_profile_weights(
    player_type: str,
    role: str,
    settings: ReplacementGenerationSettings,
) -> tuple[tuple[float, ...], str]:
    role_profile = f"{player_type}:{role}"
    default_profile = f"{player_type}:Default"
    if role_profile in settings.composite_profiles:
        return tuple(float(value) for value in settings.composite_profiles[role_profile]), role_profile
    if default_profile in settings.composite_profiles:
        return tuple(float(value) for value in settings.composite_profiles[default_profile]), default_profile
    raise ValueError(f"Replacement composite profile이 없습니다: {role_profile}")


def _build_source_cost_thresholds(
    sources: Sequence[Mapping[str, Any]],
    settings: ReplacementGenerationSettings,
) -> tuple[dict[int, dict[str, Any]], dict[int, list[float]]]:
    by_year: dict[int, list[float]] = {}
    for row in sources:
        year = int(row["originYear"])
        by_year.setdefault(year, []).append(_source_composite(row, settings))

    results: dict[int, dict[str, Any]] = {}
    sorted_by_year: dict[int, list[float]] = {}
    for year, values in sorted(by_year.items()):
        ordered = sorted(values)
        sorted_by_year[year] = ordered
        threshold_rows: list[dict[str, Any]] = []
        for upper_exclusive, cost in settings.cost_percentile_thresholds:
            capped_percentile = min(1.0, upper_exclusive)
            index = min(len(ordered) - 1, max(0, math.ceil(capped_percentile * len(ordered)) - 1))
            threshold_rows.append(
                {
                    "upperExclusive": upper_exclusive,
                    "cost": cost,
                    "sourceCompositeAtBoundary": round(ordered[index], 8),
                }
            )
        results[year] = {
            "costPopulationSource": "OriginYearSourceBacked",
            "sourcePopulationSize": len(ordered),
            "replacementExcludedFromThresholdCalculation": True,
            "minimumComposite": round(ordered[0], 8),
            "maximumComposite": round(ordered[-1], 8),
            "thresholds": threshold_rows,
        }
    return results, sorted_by_year


def _source_composite(
    row: Mapping[str, Any],
    settings: ReplacementGenerationSettings,
) -> float:
    trace = row.get("costDerivationTrace") or {}
    if "composite" in trace:
        return float(trace["composite"])
    if "Composite" in trace:
        return float(trace["Composite"])
    if "roleAdjustedComposite" in row:
        return float(row["roleAdjustedComposite"])

    player_type = str(row["playerType"])
    role = _role_for_source(row)
    weights, _ = _resolve_profile_weights(player_type, role, settings)
    offset = _ability_offset(player_type)
    return _weighted_composite(row["baseAttributes"][offset : offset + 6], weights)


def _sample_low_tail_ratings(
    rng: random.Random,
    mean_vector: Sequence[float],
    covariance: Sequence[Sequence[float]],
    comparison_composites: Sequence[float],
    profile_weights: Sequence[float],
    ability_offset: int,
    occupied_vectors: set[tuple[int, ...]],
    settings: ReplacementGenerationSettings,
) -> tuple[list[int], dict[str, Any]]:
    cholesky = _cholesky_with_regularization(covariance, settings.covariance_regularization)
    best: tuple[float, list[int], float, int] | None = None
    target_midpoint = (settings.percentile_lower_inclusive + settings.percentile_upper_inclusive) * 0.5

    for attempt in range(1, settings.maximum_sampling_attempts + 1):
        normal = _standard_normal_vector(rng, len(mean_vector))
        correlated = [
            sum(cholesky[row][column] * normal[column] for column in range(row + 1))
            for row in range(len(mean_vector))
        ]
        ratings = [
            max(
                settings.rating_minimum,
                min(
                    settings.rating_maximum,
                    int(round(mean + settings.covariance_scale * deviation)),
                ),
            )
            for mean, deviation in zip(mean_vector, correlated)
        ]
        ratings, collision_adjustments = _avoid_vector_collision(
            ratings,
            ability_offset,
            occupied_vectors,
            settings.rating_minimum,
            settings.rating_maximum,
        )
        composite = _weighted_composite(ratings, profile_weights)
        _, _, percentile = _insertion_percentile(comparison_composites, composite)
        distance = abs(percentile - target_midpoint)
        if best is None or distance < best[0]:
            best = (distance, ratings, percentile, collision_adjustments)
        if settings.percentile_lower_inclusive <= percentile <= settings.percentile_upper_inclusive:
            return ratings, {
                "groupInsertionPercentile": round(percentile, 8),
                "samplingAttempts": attempt,
                "acceptedWithinBand": True,
                "collisionAdjustmentCount": collision_adjustments,
            }

    if best is None:
        raise RuntimeError("Replacement 능력치 표본 생성에 실패했습니다.")
    _, ratings, percentile, collision_adjustments = best
    return ratings, {
        "groupInsertionPercentile": round(percentile, 8),
        "samplingAttempts": settings.maximum_sampling_attempts,
        "acceptedWithinBand": False,
        "collisionAdjustmentCount": collision_adjustments,
    }


def _avoid_vector_collision(
    relevant_ratings: Sequence[int],
    ability_offset: int,
    occupied_vectors: set[tuple[int, ...]],
    rating_minimum: int,
    rating_maximum: int,
) -> tuple[list[int], int]:
    ratings = list(relevant_ratings)
    full = [50] * len(ABILITY_NAMES)
    full[ability_offset : ability_offset + 6] = ratings
    if tuple(full) not in occupied_vectors:
        return ratings, 0

    adjustments = 0
    for distance in range(1, rating_maximum - rating_minimum + 1):
        for index in range(6):
            for direction in (-1, 1):
                candidate_value = ratings[index] + direction * distance
                if not rating_minimum <= candidate_value <= rating_maximum:
                    continue
                candidate = list(ratings)
                candidate[index] = candidate_value
                candidate_full = [50] * len(ABILITY_NAMES)
                candidate_full[ability_offset : ability_offset + 6] = candidate
                adjustments += 1
                if tuple(candidate_full) not in occupied_vectors:
                    return candidate, adjustments
    raise ValueError("Source/Replacement와 구별되는 BaseAttributes를 생성할 수 없습니다.")


def _mean_vector(vectors: Sequence[Sequence[float]]) -> list[float]:
    if not vectors:
        raise ValueError("Aggregate mean 모집단이 비어 있습니다.")
    width = len(vectors[0])
    return [sum(vector[index] for vector in vectors) / len(vectors) for index in range(width)]


def _covariance_matrix(vectors: Sequence[Sequence[float]]) -> list[list[float]]:
    if len(vectors) < 2:
        raise ValueError("Aggregate covariance에는 SourceBacked 2명 이상이 필요합니다.")
    means = _mean_vector(vectors)
    divisor = len(vectors) - 1
    width = len(means)
    return [
        [
            sum(
                (vector[row] - means[row]) * (vector[column] - means[column])
                for vector in vectors
            )
            / divisor
            for column in range(width)
        ]
        for row in range(width)
    ]


def _cholesky_with_regularization(
    covariance: Sequence[Sequence[float]],
    regularization: float,
) -> list[list[float]]:
    width = len(covariance)
    jitter = max(regularization, 1e-12)
    for _ in range(8):
        lower = [[0.0] * width for _ in range(width)]
        valid = True
        for row in range(width):
            for column in range(row + 1):
                residual = float(covariance[row][column])
                if row == column:
                    residual += jitter
                residual -= sum(lower[row][k] * lower[column][k] for k in range(column))
                if row == column:
                    if residual <= 0.0:
                        valid = False
                        break
                    lower[row][column] = math.sqrt(residual)
                else:
                    pivot = lower[column][column]
                    if pivot <= 0.0:
                        valid = False
                        break
                    lower[row][column] = residual / pivot
            if not valid:
                break
        if valid:
            return lower
        jitter *= 10.0
    raise ValueError("SourceBacked covariance를 안정적으로 분해할 수 없습니다.")


def _standard_normal_vector(rng: random.Random, count: int) -> list[float]:
    values: list[float] = []
    while len(values) < count:
        first = max(rng.random(), 1e-15)
        second = rng.random()
        magnitude = math.sqrt(-2.0 * math.log(first))
        angle = 2.0 * math.pi * second
        values.append(magnitude * math.cos(angle))
        if len(values) < count:
            values.append(magnitude * math.sin(angle))
    return values


def _weighted_composite(values: Sequence[float], weights: Sequence[float]) -> float:
    total_weight = sum(weights)
    return sum(float(value) * weight for value, weight in zip(values, weights)) / total_weight


def _insertion_percentile(
    ordered_values: Sequence[float],
    value: float,
) -> tuple[int, int, float]:
    if not ordered_values:
        raise ValueError("삽입 백분위 Source 모집단이 비어 있습니다.")
    lower_rank = bisect_left(ordered_values, value)
    upper_rank = bisect_right(ordered_values, value)
    percentile = (lower_rank + upper_rank) / (2.0 * len(ordered_values))
    return lower_rank, upper_rank, percentile


def _cost_from_percentile(
    percentile: float,
    thresholds: Iterable[tuple[float, int]],
) -> int:
    for upper_exclusive, cost in thresholds:
        if percentile < upper_exclusive:
            return cost
    raise ValueError("Cost percentile threshold가 전체 모집단을 덮지 않습니다.")


def _stable_digest(*parts: object, length: int) -> str:
    payload = json.dumps(parts, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()[:length]


def _stable_seed(*parts: object) -> int:
    return int(_stable_digest(*parts, length=16), 16)
