from __future__ import annotations

import argparse
import hashlib
import json
import math
import random
import statistics
from pathlib import Path
from typing import Any, Iterable


GENERATOR_VERSION = "synthetic-bake-v1"
BALANCE_VERSION = "historical-normal-v1"
EDITOR_ASSET_FORMAT_VERSION = 1
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
NAME_SYLLABLES = (
    "강",
    "건",
    "결",
    "겸",
    "규",
    "근",
    "기",
    "길",
    "나",
    "담",
    "도",
    "라",
    "림",
    "명",
    "별",
    "산",
    "솔",
    "윤",
    "재",
    "찬",
    "태",
    "하",
    "해",
    "현",
)


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
    percentile = (rank + 0.5) / count
    if percentile < 0.05:
        return 1
    if percentile < 0.15:
        return 2
    if percentile < 0.30:
        return 3
    if percentile < 0.45:
        return 4
    if percentile < 0.60:
        return 5
    if percentile < 0.72:
        return 6
    if percentile < 0.82:
        return 7
    if percentile < 0.90:
        return 8
    if percentile < 0.97:
        return 9
    return 10


def headroom_range(cost: int) -> tuple[int, int]:
    if cost <= 3:
        return 4, 8
    if cost <= 6:
        return 2, 5
    if cost <= 8:
        return 1, 3
    return 0, 2


def clamp_rating(value: float) -> int:
    return max(25, min(95, int(round(value))))


def mean(values: Iterable[float]) -> float:
    materialized = tuple(values)
    return sum(materialized) / len(materialized) if materialized else 0.0


def position_from_source(player: dict[str, Any], fallback: str) -> str:
    mapping = {
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
    records = player.get("defenseRecords") or []
    if not records:
        return fallback
    best = max(records, key=lambda record: safe_number(record.get("inningsOuts")))
    return mapping.get(str(best.get("position") or ""), fallback)


def hitter_features(player: dict[str, Any]) -> tuple[float, ...]:
    stats = player.get("hitterStats") or {}
    running = player.get("runningStats") or {}
    defenses = player.get("defenseRecords") or []
    chances = sum(
        safe_number(record.get("putouts"))
        + safe_number(record.get("assists"))
        + safe_number(record.get("errors"))
        for record in defenses
    )
    errors = sum(safe_number(record.get("errors")) for record in defenses)
    plate_appearances = safe_number(stats.get("plateAppearances"))
    return (
        safe_number(stats.get("sourceAVG")),
        safe_number(stats.get("sourceOBP")),
        safe_number(stats.get("sourceSLG")),
        ratio(stats.get("homeRuns"), plate_appearances),
        ratio(stats.get("walks"), plate_appearances),
        -ratio(stats.get("strikeouts"), plate_appearances),
        ratio(running.get("stolenBases"), running.get("stolenBaseAttempts")),
        1.0 - errors / chances if chances > 0 else 0.96,
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
    values = [50] * len(ABILITY_NAMES)
    if player_type == "Hitter":
        contact_z = mean((vector[0], vector[1], vector[5]))
        power_z = mean((vector[2], vector[3]))
        values[0] = clamp_rating(50 + 14 * contact_z)
        values[1] = clamp_rating(50 + 14 * power_z)
        values[2] = clamp_rating(50 + 14 * vector[6])
        values[3] = clamp_rating(50 + 10 * vector[7])
        values[4] = clamp_rating(50 + 14 * vector[7])
        values[5] = clamp_rating(50 + 14 * mean((vector[4], vector[5])))
    else:
        values[6] = clamp_rating(50 + 14 * vector[4])
        values[7] = clamp_rating(50 + 12 * vector[2])
        values[8] = clamp_rating(50 + 14 * mean((vector[0], vector[2])))
        values[9] = clamp_rating(50 + 12 * vector[0])
        values[10] = clamp_rating(50 + 14 * vector[3])
        values[11] = clamp_rating(50 + 12 * mean((vector[0], vector[1])))
    return values


def fictional_name(index: int, forbidden_names: set[str]) -> str:
    first = NAME_SYLLABLES[index % len(NAME_SYLLABLES)]
    second = NAME_SYLLABLES[(index // len(NAME_SYLLABLES) + 7) % len(NAME_SYLLABLES)]
    third = NAME_SYLLABLES[(index * 7 + 11) % len(NAME_SYLLABLES)]
    candidate = first + second + third
    if candidate in forbidden_names:
        candidate += NAME_SYLLABLES[(index * 11 + 3) % len(NAME_SYLLABLES)]
    return candidate


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


def load_reference(path: Path) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data.get("players"), list) or not data["players"]:
        raise ValueError(f"선수 Reference가 없습니다: {path}")
    if not data.get("isSeasonComplete", False):
        raise ValueError(f"완결 시즌 Reference만 Bake할 수 있습니다: {path}")
    return data


def bake_year(data: dict[str, Any], generation_seed: int) -> dict[str, Any]:
    year = int(data["year"])
    source_players = data["players"]
    hitters = [player for player in source_players if player.get("hitterStats")]
    pitchers = [player for player in source_players if player.get("pitcherStats")]
    if not hitters or not pitchers:
        raise ValueError(f"타자/투수 Reference가 모두 필요합니다: {year}")
    hitter_vectors, _, _ = normalized_pool(hitters, hitter_features)
    pitcher_vectors, _, _ = normalized_pool(pitchers, pitcher_features)
    forbidden_names = {str(player.get("playerName") or "") for player in source_players}
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
            rng = random.Random(stable_seed(GENERATOR_VERSION, generation_seed, year, franchise_id, roster_role))
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
            person_id = "PERSON_" + stable_digest(GENERATOR_VERSION, generation_seed, year, global_index)
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
            persons.append(
                {
                    "playerPersonId": person_id,
                    "fictionalName": fictional_name(year * len(FRANCHISE_IDS) * len(TEAM_POOL_ROLES) + global_index, forbidden_names),
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

    ranked = sorted(range(len(seasons)), key=lambda index: (seasons[index]["overall"], seasons[index]["playerSeasonId"]))
    rank_by_index = {season_index: rank for rank, season_index in enumerate(ranked)}
    for index, season in enumerate(seasons):
        cost = percentile_cost(rank_by_index[index], len(seasons))
        season["cost"] = cost
        low, high = headroom_range(cost)
        rng = random.Random(stable_seed("ceiling", generation_seed, season["playerSeasonId"]))
        season["trainingCeiling"] = [min(99, rating + rng.randint(low, high)) for rating in season["baseAttributes"]]
        del season["overall"]

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
    person_ids = [person["playerPersonId"] for person in content["playerPersons"]]
    if len(person_ids) != len(set(person_ids)):
        raise ValueError("PlayerPersonId가 중복되었습니다.")
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


def link_careers(
    year_contents: list[dict[str, Any]],
    generation_seed: int,
    forbidden_names: set[str],
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
                GENERATOR_VERSION,
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
                person["fictionalName"] = fictional_name(
                    stable_seed("name", person_id) % 100_000,
                    forbidden_names,
                )
                person["careerStartYear"] = year
                person["careerEndYear"] = year
                persons[person_id] = person
            else:
                person["careerEndYear"] = year

        ranked = sorted(
            range(len(seasons)),
            key=lambda index: (
                mean(
                    seasons[index]["baseAttributes"][:6]
                    if seasons[index]["playerType"] == "Hitter"
                    else seasons[index]["baseAttributes"][6:]
                ),
                seasons[index]["playerSeasonId"],
            ),
        )
        rank_by_index = {season_index: rank for rank, season_index in enumerate(ranked)}
        season_by_id = {season["playerSeasonId"]: season for season in seasons}
        for index, season in enumerate(seasons):
            cost = percentile_cost(rank_by_index[index], len(seasons))
            season["cost"] = cost
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
    return sorted(persons.values(), key=lambda person: person["playerPersonId"])


def bake(input_dir: Path, years: list[int], generation_seed: int) -> dict[str, Any]:
    references = [load_reference(input_dir / f"{year}.json") for year in sorted(years)]
    year_contents = [bake_year(data, generation_seed) for data in references]
    forbidden_names = {
        str(player.get("playerName") or "")
        for data in references
        for player in data["players"]
    }
    persons = link_careers(year_contents, generation_seed, forbidden_names)
    content: dict[str, Any] = {
        "schemaVersion": 1,
        "playerPersons": persons,
        "years": year_contents,
        "manifest": {
            "referenceDataVersion": "kbo-normalized-v1",
            "generatorVersion": GENERATOR_VERSION,
            "balanceVersion": BALANCE_VERSION,
            "generationSeed": generation_seed,
            "contentHash": "",
        },
    }
    validate_bake(content)
    canonical = json.dumps(content, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    content["manifest"]["contentHash"] = hashlib.sha256(canonical.encode("utf-8")).hexdigest()
    return content


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
    validate_bake(content)
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
    validate_bake(content)
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
    parser = argparse.ArgumentParser(description="KBO 익명 Reference를 Runtime-safe 가상 역사 콘텐츠로 Bake합니다.")
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--years", type=parse_years, required=True)
    parser.add_argument("--seed", type=int, default=20260901)
    output_group = parser.add_mutually_exclusive_group(required=True)
    output_group.add_argument("--output", type=Path)
    output_group.add_argument("--editor-assets-dir", type=Path)
    parser.add_argument(
        "--verify-editor-assets",
        action="store_true",
        help="분할 Editor Asset을 다시 읽어 파일 Hash와 Bake 규칙을 검증합니다.",
    )
    args = parser.parse_args()
    content = bake(args.input_dir, args.years, args.seed)
    if args.output is not None:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        write_bytes_atomically(
            args.output,
            (json.dumps(content, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8"),
        )
    else:
        write_editor_asset_archive(content, args.editor_assets_dir)
        if args.verify_editor_assets:
            reloaded = load_and_validate_editor_asset_archive(args.editor_assets_dir)
            if reloaded != content:
                raise ValueError("분할 Editor Asset을 다시 조립한 내용이 Bake 결과와 다릅니다.")
    print(f"Baked {sum(len(year['playerSeasons']) for year in content['years'])} PlayerSeasons")
    print(f"ContentHash={content['manifest']['contentHash']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
