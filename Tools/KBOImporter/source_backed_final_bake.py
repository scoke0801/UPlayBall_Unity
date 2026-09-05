"""SourceBacked 1:1 콘텐츠와 명시적 Replacement를 최종 Runtime Archive로 조립한다."""

from __future__ import annotations

from collections import Counter
import copy
import random
from typing import Any, Mapping, Sequence

import replacement_generation as replacement
import source_backed_runtime_bake as source_plan


GENERATOR_VERSION = "source-backed-runtime-bake-v2"
BALANCE_VERSION = "historical-source-backed-v2"
REPLACEMENT_POPULATION_POLICY_VERSION = "quota-fallback-aggregate-percentile-v2"
SOURCE_BACKED = "SourceBacked"
REPLACEMENT_GENERATED = "ReplacementGenerated"
ROSTER_SHORTAGE = "RosterShortage"

DEFENSIVE_POSITIONS = ("C", "1B", "2B", "3B", "SS", "LF", "CF", "RF")
PITCHER_ROLE_QUOTAS = (
    ("Closer", 1),
    ("Setup", 1),
    ("Starter", 5),
    ("MiddleRelief", 4),
)
SOURCE_POSITION_NAMES = {
    "C": "포수",
    "1B": "1루수",
    "2B": "2루수",
    "3B": "3루수",
    "SS": "유격수",
    "LF": "좌익수",
    "CF": "중견수",
    "RF": "우익수",
}


def build_runtime_content(
    editor_source_content: Mapping[str, Any],
    normalized_references: Sequence[Mapping[str, Any]],
    generation_seed: int,
    derivation: Any,
) -> tuple[dict[str, Any], dict[str, Any]]:
    """Source 우선 배치 후 Core25 부족분만 Replacement로 채운다."""

    plan = source_plan.build_source_backed_runtime_plan(
        editor_source_content,
        normalized_references,
    )
    del generation_seed
    source_plan.validate_source_backed_runtime_plan(plan)
    runtime = copy.deepcopy(plan.runtime_content)
    years_by_value = {int(row["year"]): row for row in runtime["years"]}
    source_seasons = [
        season
        for year in runtime["years"]
        for season in year["playerSeasons"]
    ]
    source_cost_snapshot = {
        season["playerSeasonId"]: int(season["cost"])
        for season in source_seasons
    }
    normalized_by_runtime_season = _index_normalized_sources(normalized_references)
    _attach_source_role_traces(source_seasons, normalized_references, normalized_by_runtime_season, derivation)

    shortage_slots = _build_shortage_slots(runtime, plan.replacement_requests)
    settings = replacement.ReplacementGenerationSettings.from_balance(derivation.DERIVATION_BALANCE)
    generated = replacement.generate_replacements(
        source_seasons,
        shortage_slots,
        GENERATOR_VERSION,
        settings,
    )
    replacements_by_year: dict[int, list[dict[str, Any]]] = {}
    for season in generated.replacements:
        materialized = copy.deepcopy(season)
        materialized["rosterRole"] = str(materialized.pop("assignedRosterRole", ""))
        materialized.pop("naturalPitcherRole", None)
        materialized["registrationType"] = "Domestic"
        materialized["pitcherRoleConfidence"] = "High"
        materialized["positionRoleDerivationTrace"] = _replacement_role_trace(materialized)
        replacements_by_year.setdefault(int(materialized["originYear"]), []).append(materialized)

    source_records_by_year = {
        int(year["year"]): list(year["seasonRecords"])
        for year in runtime["years"]
    }
    awards_by_year = {
        int(year["year"]): list(year["awardRecords"])
        for year in runtime["years"]
    }
    final_years: list[dict[str, Any]] = []
    year_reports: list[dict[str, Any]] = []
    for year in sorted(years_by_value):
        plan_year = years_by_value[year]
        replacements_for_year = sorted(
            replacements_by_year.get(year, []),
            key=lambda row: row["playerSeasonId"],
        )
        seasons = sorted(
            list(plan_year["playerSeasons"]) + replacements_for_year,
            key=lambda row: row["playerSeasonId"],
        )
        season_by_id = {row["playerSeasonId"]: row for row in seasons}
        proxy_by_season_id = {
            season_id: normalized_by_runtime_season[season_id]
            for season_id in (
                row["playerSeasonId"]
                for row in plan_year["playerSeasons"]
            )
        }
        proxy_by_season_id.update(
            {
                row["playerSeasonId"]: _replacement_source_proxy(row)
                for row in replacements_for_year
            }
        )

        team_plans = {
            row["teamSeasonKey"]: row
            for row in plan_year["teamAllocationPlans"]
        }
        replacements_by_team: dict[str, list[dict[str, Any]]] = {}
        for row in replacements_for_year:
            replacements_by_team.setdefault(row["originTeamSeasonKey"], []).append(row)
        teams: list[dict[str, Any]] = []
        for team_key in sorted(team_plans):
            team_plan = team_plans[team_key]
            team_rows = [
                season_by_id[season_id]
                for season_id in team_plan["sourceBackedPlayerSeasonIds"]
            ]
            team_rows.extend(replacements_by_team.get(team_key, []))
            team_rows.sort(key=lambda row: row["playerSeasonId"])
            core, roster_trace = derivation.assign_source_team_roles(team_rows, proxy_by_season_id)
            roster_trace["teamSeasonKey"] = team_key
            teams.append(
                {
                    "teamSeasonKey": team_key,
                    "franchiseId": team_plan["franchiseId"],
                    "canonicalSourceTeamSeasonId": team_plan[
                        "canonicalSourceTeamSeasonId"
                    ],
                    "originYear": year,
                    "allNormalCardIds": [
                        f"{row['playerSeasonId']}:Normal" for row in team_rows
                    ],
                    "core25CardIds": [
                        f"{row['playerSeasonId']}:Normal" for row in core
                    ],
                    "rosterSelectionTrace": roster_trace,
                    "validationWarnings": roster_trace["validationWarnings"],
                    "referenceStrength": round(
                        derivation.mean(
                            derivation.mean(
                                row["baseAttributes"][:6]
                                if row["playerType"] == "Hitter"
                                else row["baseAttributes"][6:]
                            )
                            for row in core
                        ),
                        4,
                    ),
                }
            )

        for season in seasons:
            season["registrationType"] = "Domestic"
            _assign_training_ceiling(season, derivation)
        records = sorted(
            source_records_by_year[year]
            + [_replacement_record(row) for row in replacements_for_year],
            key=lambda row: row["playerSeasonId"],
        )
        final_years.append(
            {
                "year": year,
                "playerSeasons": seasons,
                "normalCards": [
                    {
                        "cardId": f"{season['playerSeasonId']}:Normal",
                        "playerSeasonId": season["playerSeasonId"],
                        "edition": "Normal",
                        "editionStatModifiers": [0] * len(derivation.ABILITY_NAMES),
                    }
                    for season in seasons
                ],
                "teamSeasons": teams,
                "originalSeasonRecords": records,
                "originalAwardRecords": sorted(
                    awards_by_year[year],
                    key=lambda row: (
                        row["awardType"],
                        row["playerSeasonId"],
                        row.get("position", ""),
                    ),
                ),
            }
        )
        year_reports.append(
            _build_year_report(
                year,
                seasons,
                generated.source_cost_thresholds[year],
                plan_year["allocationReport"],
            )
        )

    runtime_persons = _materialize_persons(
        runtime["playerPersons"],
        replacements_by_year,
        derivation,
    )
    world_identity_name_pool = source_plan.build_world_identity_name_pool(
        domestic_player_count=sum(
            person["registrationType"] != "Foreign" for person in runtime_persons
        ),
        foreign_player_count=sum(
            person["registrationType"] == "Foreign" for person in runtime_persons
        ),
        franchise_count=len(
            {
                team["franchiseId"]
                for year_content in final_years
                for team in year_content["teamSeasons"]
            }
        ),
        forbidden_player_names=(
            str(player.get("playerName") or "").strip()
            for reference in normalized_references
            for player in reference["players"]
        ),
        forbidden_franchise_names=(
            str(team.get("sourceTeamName") or "").strip()
            for reference in normalized_references
            for team in reference.get("teams", [])
        ),
    )
    manifest = {
        **_source_manifest(editor_source_content["manifest"]),
        "generatorVersion": GENERATOR_VERSION,
        "balanceVersion": BALANCE_VERSION,
        "generationSeed": 0,
        "generationSeedAffectsCanonicalBake": False,
        "namePolicyVersion": source_plan.WORLD_IDENTITY_NAME_POOL_VERSION,
        "nameDataPolicy": derivation.RUNTIME_NAME_POLICY,
        "sourceIdentityPolicyVersion": source_plan.IDENTITY_POLICY_VERSION,
        "sourceFranchiseIdentityPolicyVersion": source_plan.FRANCHISE_ID_POLICY_VERSION,
        "sourceTeamSeasonIdentityPolicyVersion": source_plan.TEAM_SEASON_ID_POLICY_VERSION,
        "sourceAllocationPolicyVersion": source_plan.ALLOCATION_POLICY_VERSION,
        "replacementGeneratorVersion": str(
            derivation.DERIVATION_BALANCE["replacementGeneration"]["version"]
        ),
        "replacementPopulationPolicyVersion": REPLACEMENT_POPULATION_POLICY_VERSION,
        "sourceBackedPlayerPersonCount": len(runtime["playerPersons"]),
        "sourceBackedPlayerSeasonCount": len(source_seasons),
        "replacementGeneratedPlayerPersonCount": len(generated.replacements),
        "replacementGeneratedPlayerSeasonCount": len(generated.replacements),
        "contentHash": "",
    }
    content = {
        "schemaVersion": derivation.CONTENT_SCHEMA_VERSION,
        "playerPersons": runtime_persons,
        "worldIdentityNamePool": world_identity_name_pool,
        "years": final_years,
        "manifest": manifest,
    }
    _validate_source_costs_unchanged(content, source_cost_snapshot)
    validate_runtime_content(content, editor_source_content, plan, derivation)
    derivation.refresh_content_hash(content)
    report = {
        "contractVersion": "source-team-season-one-to-one-report-v2",
        "generationSeed": 0,
        "generationSeedAffectsCanonicalBake": False,
        "sourceBackedPlayerPersonCount": len(runtime["playerPersons"]),
        "sourceBackedPlayerSeasonCount": len(source_seasons),
        "replacementGeneratedPlayerPersonCount": len(generated.replacements),
        "replacementGeneratedPlayerSeasonCount": len(generated.replacements),
        "worldIdentityNameSample": {
            "players": (
                world_identity_name_pool["domesticPlayerNames"]
                + world_identity_name_pool["foreignPlayerNames"]
            )[:300],
            "franchises": world_identity_name_pool["franchiseNames"][:300],
        },
        "years": year_reports,
        "sourceCostThresholds": generated.source_cost_thresholds,
        "replacementGenerationTraces": generated.generation_traces,
    }
    return content, report


def _source_manifest(editor_manifest: Mapping[str, Any]) -> dict[str, Any]:
    fields = (
        "referenceDataVersion",
        "rawDataVersion",
        "normalizedSchemaVersion",
        "normalizedImporterVersion",
        "normalizedContentHash",
        "abilityFormulaVersion",
        "positionRoleClassifierVersion",
        "rosterBuilderVersion",
        "costFormulaVersion",
        "derivationBalanceVersion",
    )
    return {field: editor_manifest[field] for field in fields}


def _index_normalized_sources(
    references: Sequence[Mapping[str, Any]],
) -> dict[str, Mapping[str, Any]]:
    result: dict[str, Mapping[str, Any]] = {}
    for reference in references:
        year = int(reference["year"])
        for player in reference["players"]:
            source_id = str(player["sourcePlayerId"])
            season_id = source_plan.runtime_player_season_id(source_id, year)
            if season_id in result:
                raise ValueError(f"Source PlayerSeason이 중복되었습니다: {season_id}")
            result[season_id] = player
    return result


def _attach_source_role_traces(
    source_seasons: Sequence[dict[str, Any]],
    references: Sequence[Mapping[str, Any]],
    normalized_by_runtime_season: Mapping[str, Mapping[str, Any]],
    derivation: Any,
) -> None:
    availability_by_year = {
        int(reference["year"]): derivation.derive_pitcher_role_availability(reference["players"])
        for reference in references
    }
    for season in source_seasons:
        source = normalized_by_runtime_season[season["playerSeasonId"]]
        if season["playerType"] == "Pitcher":
            _, trace = derivation.derive_source_pitcher_role(
                source,
                availability_by_year[int(season["originYear"])],
            )
            trace["selectedNaturalPosition"] = "P"
            trace["positionCandidates"] = []
        else:
            _, trace = derivation.derive_source_position(source, "DH")
            trace.update(
                {
                    "pitcherRoleConfidence": "High",
                    "selectedNaturalPitcherRole": "",
                    "pitcherRoleEvidence": {},
                    "pitcherRoleScores": [],
                    "warnings": [],
                }
            )
        trace["playerSeasonId"] = season["playerSeasonId"]
        trace["seasonYear"] = int(season["originYear"])
        season["positionRoleDerivationTrace"] = trace


def _build_shortage_slots(
    runtime: Mapping[str, Any],
    requests: Sequence[source_plan.ReplacementRequest],
) -> list[replacement.ShortageSlotSpec]:
    seasons_by_year = {
        int(year["year"]): {
            season["playerSeasonId"]: season for season in year["playerSeasons"]
        }
        for year in runtime["years"]
    }
    plans_by_team = {
        plan["teamSeasonKey"]: plan
        for year in runtime["years"]
        for plan in year["teamAllocationPlans"]
    }
    slots: list[replacement.ShortageSlotSpec] = []
    for request in sorted(
        requests,
        key=lambda row: (row.origin_year, row.franchise_id, row.player_type),
    ):
        plan = plans_by_team[request.team_season_key]
        source_rows = [
            seasons_by_year[request.origin_year][season_id]
            for season_id in plan["sourceBackedPlayerSeasonIds"]
        ]
        if request.player_type == "Hitter":
            positions = _replacement_hitter_positions(source_rows, request.count)
            for index, position in enumerate(positions, 1):
                slots.append(
                    replacement.ShortageSlotSpec(
                        request.origin_year,
                        request.franchise_id,
                        request.team_season_key,
                        "Hitter",
                        position=position,
                        assigned_roster_role="Core25Shortage",
                        slot_key=f"{request.request_id}:H:{index}:{position}",
                    )
                )
            continue
        roles = _replacement_pitcher_roles(source_rows, request.count)
        for index, role in enumerate(roles, 1):
            slots.append(
                replacement.ShortageSlotSpec(
                    request.origin_year,
                    request.franchise_id,
                    request.team_season_key,
                    "Pitcher",
                    natural_pitcher_role=role,
                    assigned_roster_role="Core25Shortage",
                    slot_key=f"{request.request_id}:P:{index}:{role}",
                )
            )
    return slots


def _replacement_hitter_positions(
    source_rows: Sequence[Mapping[str, Any]],
    count: int,
) -> list[str]:
    counts = Counter(str(row.get("position") or "DH") for row in source_rows)
    result: list[str] = []
    for position in DEFENSIVE_POSITIONS:
        if counts[position] == 0 and len(result) < count:
            result.append(position)
            counts[position] += 1
    while len(result) < count:
        position = min(
            (*DEFENSIVE_POSITIONS, "DH"),
            key=lambda value: (counts[value], value),
        )
        result.append(position)
        counts[position] += 1
    return result


def _replacement_pitcher_roles(
    source_rows: Sequence[Mapping[str, Any]],
    count: int,
) -> list[str]:
    natural = Counter()
    for row in source_rows:
        role = str(row.get("pitcherRole") or "MiddleRelief")
        natural["MiddleRelief" if role in {"Bullpen", "Swingman", "LongRelief", "MiddleRelief"} else role] += 1
    result: list[str] = []
    for role, quota in PITCHER_ROLE_QUOTAS:
        missing = max(0, quota - natural[role])
        result.extend([role] * min(missing, count - len(result)))
        if len(result) == count:
            return result
    while len(result) < count:
        result.append("MiddleRelief")
    return result


def _replacement_role_trace(season: Mapping[str, Any]) -> dict[str, Any]:
    role = str(season.get("pitcherRole") or "")
    return {
        "classifierVersion": "replacement-explicit-role-v1",
        "playerSeasonId": season["playerSeasonId"],
        "seasonYear": int(season["originYear"]),
        "positionCandidates": [season["position"]] if season["playerType"] == "Hitter" else [],
        "selectedNaturalPosition": season["position"],
        "pitcherRoleConfidence": "High",
        "pitcherRoleEvidence": {"generationReason": ROSTER_SHORTAGE},
        "pitcherRoleScores": [
            {"role": candidate, "score": 100.0 if candidate == _assigned_group(role) else 0.0}
            for candidate in ("Starter", "Bullpen", "Setup", "Closer")
        ] if season["playerType"] == "Pitcher" else [],
        "selectedNaturalPitcherRole": role,
        "reason": "Core25 부족 슬롯에 명시적으로 생성된 Natural role",
        "warnings": [],
    }


def _assigned_group(role: str) -> str:
    return "Bullpen" if role in {"Swingman", "LongRelief", "MiddleRelief"} else role


def _replacement_source_proxy(season: Mapping[str, Any]) -> dict[str, Any]:
    position = str(season["position"])
    defense = []
    if position in SOURCE_POSITION_NAMES:
        defense.append(
            {
                "position": SOURCE_POSITION_NAMES[position],
                "games": 1,
                "gamesStarted": 1,
                "inningsOuts": 27,
                "putouts": 0,
                "assists": 0,
                "errors": 0,
            }
        )
    return {
        "defenseRecords": defense,
        "hitterStats": {"plateAppearances": 0},
        "pitcherStats": {"inningsOuts": 0},
    }


def _assign_training_ceiling(season: dict[str, Any], derivation: Any) -> None:
    low, high = derivation.headroom_range(int(season["cost"]))
    rng = random.Random(
        derivation.stable_seed(
            "training-ceiling-v3",
            derivation.DERIVATION_BALANCE_VERSION,
            season["playerSeasonId"],
        )
    )
    season["trainingCeiling"] = [
        min(99, int(rating) + rng.randint(low, high))
        for rating in season["baseAttributes"]
    ]


def _replacement_record(season: Mapping[str, Any]) -> dict[str, Any]:
    return {
        "playerSeasonId": season["playerSeasonId"],
        "teamSeasonKey": season["originTeamSeasonKey"],
        "seasonYear": int(season["originYear"]),
        "position": season["position"],
        "defensiveChances": 0,
        "fieldingErrors": 0,
        "isOriginalSourceRecord": False,
        "games": 0,
        "plateAppearances": 0,
        "atBats": 0,
        "hits": 0,
        "doubles": 0,
        "triples": 0,
        "homeRuns": 0,
        "runsBattedIn": 0,
        "runs": 0,
        "walks": 0,
        "strikeouts": 0,
        "stolenBases": 0,
        "caughtStealing": 0,
        "pitchingOuts": 0,
        "wins": 0,
        "losses": 0,
        "saves": 0,
        "holds": 0,
        "hitsAllowed": 0,
        "homeRunsAllowed": 0,
        "pitchingWalks": 0,
        "earnedRuns": 0,
        "pitchingStrikeouts": 0,
        "hasStoredBattingAverage": False,
        "storedBattingAverage": 0.0,
        "hasStoredOnBasePercentage": False,
        "storedOnBasePercentage": 0.0,
        "hasStoredSluggingPercentage": False,
        "storedSluggingPercentage": 0.0,
        "hasStoredOnBasePlusSlugging": False,
        "storedOnBasePlusSlugging": 0.0,
        "hasStoredEarnedRunAverage": False,
        "storedEarnedRunAverage": 0.0,
        "hasStoredWhip": False,
        "storedWhip": 0.0,
    }


def _materialize_persons(
    source_persons: Sequence[Mapping[str, Any]],
    replacements_by_year: Mapping[int, Sequence[Mapping[str, Any]]],
    derivation: Any,
) -> list[dict[str, Any]]:
    persons = [copy.deepcopy(dict(row)) for row in source_persons]
    for year, seasons in replacements_by_year.items():
        for season in seasons:
            persons.append(
                {
                    "playerPersonId": season["playerPersonId"],
                    "primaryPosition": season["position"],
                    "careerStartYear": year,
                    "careerEndYear": year,
                }
            )
    result: list[dict[str, Any]] = []
    for person in persons:
        person_id = str(person["playerPersonId"])
        career_start = int(person["careerStartYear"])
        identity_seed = derivation.stable_seed(
            "runtime-person-metadata-v2",
            derivation.DERIVATION_BALANCE_VERSION,
            person_id,
        )
        rng = random.Random(identity_seed)
        materialized = {
            "playerPersonId": person_id,
            "birthYear": career_start - rng.randint(18, 32),
            "bats": ("Right", "Left", "Switch")[rng.randrange(3)],
            "throws": ("Right", "Left")[rng.randrange(2)],
            "primaryPosition": person["primaryPosition"],
            "registrationType": "Domestic",
            "careerStartYear": career_start,
            "careerEndYear": int(person["careerEndYear"]),
            "personPotentialTrait": [rng.randint(35, 65) for _ in range(len(derivation.ABILITY_NAMES))],
        }
        result.append(materialized)
    result.sort(key=lambda row: row["playerPersonId"])
    if len(result) != len({row["playerPersonId"] for row in result}):
        raise ValueError("Runtime PlayerPersonId가 중복되었습니다.")
    return result


def _build_year_report(
    year: int,
    seasons: Sequence[Mapping[str, Any]],
    source_thresholds: Mapping[str, Any],
    allocation_report: Mapping[str, Any],
) -> dict[str, Any]:
    source_rows = [row for row in seasons if row["dataProvenance"] == SOURCE_BACKED]
    replacement_rows = [row for row in seasons if row["dataProvenance"] == REPLACEMENT_GENERATED]
    team_count = int(allocation_report["canonicalTeamSeasonCount"])
    required = team_count * (
        source_plan.CORE_HITTER_COUNT + source_plan.CORE_PITCHER_COUNT
    )
    replacement_costs = [int(row["cost"]) for row in replacement_rows]
    replacement_ratings = [
        value
        for row in replacement_rows
        for value in (
            row["baseAttributes"][:6]
            if row["playerType"] == "Hitter"
            else row["baseAttributes"][6:]
        )
    ]
    return {
        "originYear": year,
        "sourceHitterCount": sum(row["playerType"] == "Hitter" for row in source_rows),
        "sourcePitcherCount": sum(row["playerType"] == "Pitcher" for row in source_rows),
        "requiredHitterCount": team_count * source_plan.CORE_HITTER_COUNT,
        "requiredPitcherCount": team_count * source_plan.CORE_PITCHER_COUNT,
        "replacementHitterCount": sum(row["playerType"] == "Hitter" for row in replacement_rows),
        "replacementPitcherCount": sum(row["playerType"] == "Pitcher" for row in replacement_rows),
        "replacementRatio": round(len(replacement_rows) / required, 8),
        "replacementAverageCost": round(sum(replacement_costs) / len(replacement_costs), 8) if replacement_costs else 0.0,
        "replacementAverageRelevantAbility": round(sum(replacement_ratings) / len(replacement_ratings), 8) if replacement_ratings else 0.0,
        "sourceCostThresholds": dict(source_thresholds),
        "sourceTeamSeasonCount": int(allocation_report["sourceTeamSeasonCount"]),
        "canonicalTeamSeasonCount": team_count,
        "leagueTeamTargetCount": int(allocation_report["leagueTeamTargetCount"]),
        "teamCountDisposition": allocation_report["teamCountDisposition"],
        "sourceFranchiseIdFallbackCount": int(
            allocation_report["sourceFranchiseIdFallbackCount"]
        ),
        "playerTeamAssignmentBasisCounts": dict(
            allocation_report["playerTeamAssignmentBasisCounts"]
        ),
    }


def _validate_source_costs_unchanged(
    content: Mapping[str, Any],
    expected: Mapping[str, int],
) -> None:
    actual = {
        row["playerSeasonId"]: int(row["cost"])
        for year in content["years"]
        for row in year["playerSeasons"]
        if row["dataProvenance"] == SOURCE_BACKED
    }
    if actual != dict(expected):
        raise ValueError("Replacement가 SourceBacked Cost를 변경했습니다.")


def _validate_world_identity_name_pool(
    pool: Any,
    persons: Sequence[Mapping[str, Any]],
    editor_source_content: Mapping[str, Any],
    expected_franchise_count: int,
) -> None:
    if not isinstance(pool, Mapping):
        raise ValueError("World Identity 이름 후보 풀이 없습니다.")
    if pool.get("version") != source_plan.WORLD_IDENTITY_NAME_POOL_VERSION:
        raise ValueError("World Identity 이름 후보 풀 버전이 다릅니다.")

    domestic_names = list(pool.get("domesticPlayerNames") or [])
    foreign_names = list(pool.get("foreignPlayerNames") or [])
    franchise_names = list(pool.get("franchiseNames") or [])
    expected_domestic_count = sum(
        person["registrationType"] != "Foreign" for person in persons
    )
    expected_foreign_count = len(persons) - expected_domestic_count
    if len(domestic_names) < expected_domestic_count:
        raise ValueError("Domestic World Player Identity 후보가 부족합니다.")
    if len(foreign_names) < expected_foreign_count:
        raise ValueError("Foreign World Player Identity 후보가 부족합니다.")
    if len(franchise_names) < expected_franchise_count:
        raise ValueError("World Franchise Identity 후보가 부족합니다.")

    all_player_names = domestic_names + foreign_names
    if len(all_player_names) != len(set(all_player_names)):
        raise ValueError("World Player Identity 후보가 중복됩니다.")
    if len(franchise_names) != len(set(franchise_names)):
        raise ValueError("World Franchise Identity 후보가 중복됩니다.")

    forbidden_player_names = {
        str(person.get("originalName") or "").strip()
        for person in editor_source_content.get("playerPersons", [])
        if str(person.get("originalName") or "").strip()
    }
    forbidden_franchise_names = {
        str(team.get("franchiseId") or "").strip()
        for year in editor_source_content.get("years", [])
        for team in year.get("teamSeasons", [])
        if str(team.get("franchiseId") or "").strip()
    }
    if forbidden_player_names.intersection(all_player_names):
        raise ValueError("World Player Identity 후보가 Source 선수명을 재사용합니다.")
    if forbidden_franchise_names.intersection(franchise_names):
        raise ValueError("World Franchise Identity 후보가 Source 구단명을 재사용합니다.")

    for label, names in (
        ("World Player", all_player_names),
        ("World Franchise", franchise_names),
    ):
        if any(
            not isinstance(name, str)
            or not name.strip()
            or len(name) > 40
            or any(character.isdigit() or ord(character) < 32 for character in name)
            for name in names
        ):
            raise ValueError(f"{label} Identity 후보 품질 계약을 위반했습니다.")


def validate_runtime_content(
    content: Mapping[str, Any],
    editor_source_content: Mapping[str, Any],
    plan: source_plan.SourceBackedRuntimeBakePlan,
    derivation: Any,
) -> None:
    """Player/Team 1:1 provenance, Cost 격리, Core25를 최종 JSON에서 검증한다."""

    source_person_count = len(editor_source_content["playerPersons"])
    source_season_count = sum(len(year["playerSeasons"]) for year in editor_source_content["years"])
    persons = content["playerPersons"]
    if len(persons) != len({row["playerPersonId"] for row in persons}):
        raise ValueError("Runtime PlayerPersonId가 중복되었습니다.")
    if any("fictionalName" in row or "displayName" in row for row in persons):
        raise ValueError("Canonical PlayerPerson에 World DisplayName을 고정할 수 없습니다.")
    _validate_world_identity_name_pool(
        content.get("worldIdentityNamePool"),
        persons,
        editor_source_content,
        len(
            {
                team["franchiseId"]
                for year_content in content["years"]
                for team in year_content["teamSeasons"]
            }
        ),
    )
    seasons_all = [row for year in content["years"] for row in year["playerSeasons"]]
    source_rows = [row for row in seasons_all if row.get("dataProvenance") == SOURCE_BACKED]
    replacement_rows = [row for row in seasons_all if row.get("dataProvenance") == REPLACEMENT_GENERATED]
    replacement_person_ids = {row["playerPersonId"] for row in replacement_rows}
    if len(source_rows) != source_season_count:
        raise ValueError("Source PlayerSeason 1:1 수가 유지되지 않았습니다.")
    source_person_ids = {row["playerPersonId"] for row in source_rows}
    if len(source_person_ids) != source_person_count:
        raise ValueError("Source Player 1:1 수가 유지되지 않았습니다.")
    if len(replacement_rows) != sum(request.count for request in plan.replacement_requests):
        raise ValueError("Replacement 수가 Core25 부족분과 다릅니다.")
    if len(replacement_person_ids) != len(replacement_rows):
        raise ValueError("Replacement PlayerPerson은 PlayerSeason과 1:1이어야 합니다.")
    if any(row.get("generationReason") != ROSTER_SHORTAGE for row in replacement_rows):
        raise ValueError("Replacement 생성 이유는 RosterShortage여야 합니다.")
    if any(row["playerPersonId"] in source_person_ids for row in replacement_rows):
        raise ValueError("Replacement가 Source PlayerPerson을 재사용했습니다.")
    if source_person_ids.union(replacement_person_ids) != {row["playerPersonId"] for row in persons}:
        raise ValueError("Runtime PlayerPerson이 provenance PlayerSeason에 연결되지 않았습니다.")
    if any("sourcePlayerId" in row or "sourceReferenceNames" in row for row in seasons_all):
        raise ValueError("Runtime PlayerSeason에 Source 식별 정보가 노출되었습니다.")

    seen_seasons: set[str] = set()
    plan_years = {
        int(row["year"]): row for row in plan.runtime_content["years"]
    }
    for year_content in content["years"]:
        year = int(year_content["year"])
        plan_year = plan_years[year]
        seasons = year_content["playerSeasons"]
        season_by_id = {row["playerSeasonId"]: row for row in seasons}
        if len(season_by_id) != len(seasons) or seen_seasons.intersection(season_by_id):
            raise ValueError("PlayerSeasonId가 중복되었습니다.")
        seen_seasons.update(season_by_id)
        expected_team_count = len(plan_year["teamAllocationPlans"])
        if len(year_content["teamSeasons"]) != expected_team_count:
            raise ValueError("Canonical TeamSeason 수가 Source TeamSeason 수와 다릅니다.")
        source_plan_by_team = {
            row["teamSeasonKey"]: row for row in plan_year["teamAllocationPlans"]
        }
        allocated_cards: list[str] = []
        for team in year_content["teamSeasons"]:
            team_plan = source_plan_by_team.get(team["teamSeasonKey"])
            if team_plan is None:
                raise ValueError("Source와 연결되지 않은 Canonical TeamSeason입니다.")
            if (
                team["franchiseId"] != team_plan["franchiseId"]
                or team["canonicalSourceTeamSeasonId"]
                != team_plan["canonicalSourceTeamSeasonId"]
            ):
                raise ValueError("Canonical TeamSeason 1:1 provenance가 변경되었습니다.")
            all_cards = team["allNormalCardIds"]
            core_cards = team["core25CardIds"]
            allocated_cards.extend(all_cards)
            if len(core_cards) != 25 or len(set(core_cards)) != 25:
                raise ValueError("Core25는 중복 없는 정확한 25명이어야 합니다.")
            if not set(core_cards).issubset(all_cards):
                raise ValueError("Core25가 Team Pool 밖의 선수를 참조합니다.")
            team_pool = [
                season_by_id[card_id.removesuffix(":Normal")]
                for card_id in all_cards
            ]
            if any(
                row["originTeamSeasonKey"] != team["teamSeasonKey"]
                or row["originFranchiseId"] != team["franchiseId"]
                for row in team_pool
            ):
                raise ValueError("Team Pool에 다른 Source TeamSeason 선수가 섞였습니다.")
            core = [season_by_id[card_id.removesuffix(":Normal")] for card_id in core_cards]
            roles = [row["rosterRole"] for row in core]
            if sum(row["playerType"] == "Hitter" for row in core) != 14:
                raise ValueError("Core25 야수는 정확히 14명이어야 합니다.")
            if sum(row["playerType"] == "Pitcher" for row in core) != 11:
                raise ValueError("Core25 투수는 정확히 11명이어야 합니다.")
            if sum(role.startswith("StartingHitter:") for role in roles) != 9:
                raise ValueError("주전 야수는 정확히 9명이어야 합니다.")
            if sum(role.startswith("BenchHitter:") for role in roles) != 5:
                raise ValueError("벤치 야수는 정확히 5명이어야 합니다.")
            if sum(role.startswith("StartingPitcher:") for role in roles) != 5:
                raise ValueError("선발 투수는 정확히 5명이어야 합니다.")
            if sum(role.startswith("Bullpen") for role in roles) != 4:
                raise ValueError("Bullpen은 정확히 4명이어야 합니다.")
            if roles.count("Setup") != 1 or roles.count("Closer") != 1:
                raise ValueError("Setup/Closer는 정확히 한 명이어야 합니다.")
            if len({row["playerPersonId"] for row in core}) != 25:
                raise ValueError("Core25 PlayerPerson이 중복되었습니다.")
            for row in core:
                if row["originTeamSeasonKey"] != team["teamSeasonKey"]:
                    raise ValueError("Core25에 다른 Source TeamSeason 선수가 섞였습니다.")
            source_team_season_ids = set(
                team_plan["sourceBackedPlayerSeasonIds"]
            )
            actual_source_ids = {
                row["playerSeasonId"]
                for row in core
                if row["dataProvenance"] == SOURCE_BACKED
            }
            if not actual_source_ids.issubset(source_team_season_ids):
                raise ValueError("Core25 SourceBacked 선수가 다른 Source 구단에서 왔습니다.")
            if sum(row["registrationType"] == "Foreign" for row in core) > 3:
                raise ValueError("Core25 Foreign 제한을 위반했습니다.")
        expected_cards = {f"{season_id}:Normal" for season_id in season_by_id}
        if len(allocated_cards) != len(set(allocated_cards)) or set(allocated_cards) != expected_cards:
            raise ValueError("한 연도의 모든 PlayerSeason은 정확히 한 Team Pool에 배치되어야 합니다.")
        record_ids = [row["playerSeasonId"] for row in year_content["originalSeasonRecords"]]
        if len(record_ids) != len(set(record_ids)) or set(record_ids) != set(season_by_id):
            raise ValueError("PlayerSeason과 Baked record는 1:1이어야 합니다.")
        if any(int(row["originYear"]) != year for row in seasons):
            raise ValueError("SEASON_RECORD_CROSS_YEAR_REFERENCE")
        if any(int(row["seasonYear"]) != year for row in year_content["originalSeasonRecords"]):
            raise ValueError("SEASON_RECORD_CROSS_YEAR_REFERENCE")

    derivation.validate_derivation_manifest(dict(content["manifest"]))
