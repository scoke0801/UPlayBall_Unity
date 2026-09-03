from __future__ import annotations

from . import IMPORTER_VERSION, SCHEMA_VERSION
from .errors import NormalizedDataError


def validate_normalized_season(document: dict[str, object]) -> None:
    errors: list[str] = document["validationSummary"]["errors"]
    warnings: list[str] = document["validationSummary"]["warnings"]

    ranked_team_names = {
        str(team.get("sourceTeamName") or "")
        for team in document["teams"]
        if team.get("rankStats") is not None
    }
    _validate_aggregate_team_coverage(document, ranked_team_names, errors)

    for player in document["players"]:
        label = f"{player.get('sourcePlayerId') or 'Unresolved'}:{player.get('playerName')}"
        aggregate_team_name = str(player.get("aggregateTeamName") or "")
        if (
            ranked_team_names
            and aggregate_team_name
            and aggregate_team_name not in ranked_team_names
        ):
            errors.append(
                f"{label} 시즌 Aggregate 팀이 해당 시즌 순위표에 없습니다: "
                f"{aggregate_team_name}"
            )
        _validate_aggregate_corroboration(player, label, errors)
        _validate_hitter(player.get("hitterStats"), label, errors, warnings)
        _validate_pitcher(player.get("pitcherStats"), label, errors, warnings)
        _validate_running(player.get("runningStats"), label, errors)
        for defense in player.get("defenseRecords") or []:
            _validate_defense(defense, label, errors)
        for team_filter in player.get("teamFilterRecords") or []:
            filter_label = f"{label}/TeamFilter:{team_filter.get('sourceTeamId')}"
            _validate_hitter(team_filter.get("hitterStats"), filter_label, errors, warnings)
            _validate_pitcher(team_filter.get("pitcherStats"), filter_label, errors, warnings)
            _validate_running(team_filter.get("runningStats"), filter_label, errors)
            for defense in team_filter.get("defenseRecords") or []:
                _validate_defense(defense, filter_label, errors)
        for stint in player.get("teamStints") or []:
            stint_label = f"{label}/{stint.get('sourceTeamId')}"
            _validate_hitter(stint.get("hitterStats"), stint_label, errors, warnings)
            _validate_pitcher(stint.get("pitcherStats"), stint_label, errors, warnings)
            _validate_running(stint.get("runningStats"), stint_label, errors)
            for defense in stint.get("defenseRecords") or []:
                _validate_defense(defense, stint_label, errors)

    for team in document["teams"]:
        label = f"Team:{team.get('sourceTeamId')}:{team.get('sourceTeamName')}"
        _validate_hitter(team.get("hitterStats"), label, errors, warnings)
        _validate_pitcher(team.get("pitcherStats"), label, errors, warnings)
        _validate_running(team.get("runningStats"), label, errors)
        if team.get("defenseStats"):
            _validate_defense(team["defenseStats"], label, errors)
        rank = team.get("rankStats")
        if rank and _all_present(rank, "games", "wins", "losses", "ties"):
            if rank["wins"] + rank["losses"] + rank["ties"] != rank["games"]:
                errors.append(f"{label} W + L + D != G")

    document["validationSummary"]["errorCount"] = len(errors)
    document["validationSummary"]["warningCount"] = len(warnings)
    if errors:
        preview = "; ".join(errors[:5])
        raise NormalizedDataError(
            f"{document['year']} 정규화 검증에 실패했습니다({len(errors)}건): {preview}"
        )


def validate_saved_document(document: dict[str, object]) -> list[str]:
    issues: list[str] = []
    for key in (
        "schemaVersion",
        "importerVersion",
        "year",
        "isSeasonComplete",
        "players",
        "teams",
        "awardAvailabilityStatus",
    ):
        if key not in document:
            issues.append(f"필수 필드가 없습니다: {key}")
    if not isinstance(document.get("players", []), list):
        issues.append("players는 배열이어야 합니다.")
    if not isinstance(document.get("teams", []), list):
        issues.append("teams는 배열이어야 합니다.")
    if document.get("schemaVersion") != SCHEMA_VERSION:
        issues.append(
            f"지원하지 않는 schemaVersion입니다: {document.get('schemaVersion')} (expected={SCHEMA_VERSION})"
        )
    if document.get("importerVersion") != IMPORTER_VERSION:
        issues.append(
            "지원하지 않는 importerVersion입니다: "
            f"{document.get('importerVersion')} (expected={IMPORTER_VERSION})"
        )
    if not isinstance(document.get("year"), int):
        issues.append("year는 정수여야 합니다.")
    source_metadata = document.get("sourceMetadata")
    if not isinstance(source_metadata, dict):
        issues.append("sourceMetadata는 Object여야 합니다.")
    else:
        if source_metadata.get("schemaVersion") != document.get("schemaVersion"):
            issues.append("sourceMetadata.schemaVersion이 문서 schemaVersion과 다릅니다.")
        if source_metadata.get("importerVersion") != document.get("importerVersion"):
            issues.append("sourceMetadata.importerVersion이 문서 importerVersion과 다릅니다.")
        _validate_sha256_field(source_metadata, "sourceSnapshotHash", issues)
        _validate_sha256_field(source_metadata, "overrideHash", issues)

    document_year = document.get("year")
    if isinstance(document_year, int):
        for player in document.get("players", []):
            if not isinstance(player, dict):
                issues.append("players 항목은 Object여야 합니다.")
                continue
            if player.get("year") != document_year:
                issues.append(
                    "SEASON_RECORD_CROSS_YEAR_REFERENCE: "
                    f"Player {player.get('sourcePlayerId')} year={player.get('year')} "
                    f"documentYear={document_year}"
                )
            _validate_saved_aggregate_scopes(player, issues)
        for team in document.get("teams", []):
            if not isinstance(team, dict):
                issues.append("teams 항목은 Object여야 합니다.")
                continue
            if team.get("seasonYear") != document_year:
                issues.append(
                    "SEASON_RECORD_CROSS_YEAR_REFERENCE: "
                    f"Team {team.get('sourceTeamId')} seasonYear={team.get('seasonYear')} "
                    f"documentYear={document_year}"
                )
    award_statuses = document.get("awardAvailabilityStatus")
    if not isinstance(award_statuses, dict):
        issues.append("awardAvailabilityStatus는 Object여야 합니다.")
    else:
        expected_awards = {
            "RegularSeasonMvp",
            "AllStarGameMvp",
            "KoreanSeriesMvp",
            "GoldenGlove",
            "AllStarSelection",
        }
        if set(award_statuses) != expected_awards:
            issues.append("awardAvailabilityStatus의 수상 유형이 계약과 다릅니다.")
        allowed_statuses = {
            "Available",
            "AvailableEmpty",
            "Unavailable",
            "Partial",
            "NotSelected",
        }
        invalid = sorted(
            str(value) for value in award_statuses.values() if value not in allowed_statuses
        )
        if invalid:
            issues.append(
                f"awardAvailabilityStatus에 지원하지 않는 상태가 있습니다: {invalid}"
            )
    return issues


def _validate_sha256_field(
    container: dict[str, object],
    field_name: str,
    issues: list[str],
) -> None:
    value = container.get(field_name)
    if not isinstance(value, str) or len(value) != 64:
        issues.append(f"sourceMetadata.{field_name}는 SHA-256이어야 합니다.")
        return
    try:
        int(value, 16)
    except ValueError:
        issues.append(f"sourceMetadata.{field_name}는 SHA-256이어야 합니다.")


def _validate_saved_aggregate_scopes(
    player: dict[str, object],
    issues: list[str],
) -> None:
    allowed = {
        "KboLeagueAggregate",
        "KboTeamFilterSeasonTotal",
        "SingleTeamStint",
    }
    label = str(player.get("sourcePlayerId") or "Unresolved")
    for field_name in ("hitterStats", "pitcherStats", "runningStats"):
        stats = player.get(field_name)
        if not isinstance(stats, dict) or not stats:
            continue
        origin = stats.get("aggregateOrigin")
        if origin not in allowed:
            issues.append(
                f"{label} {field_name}.aggregateOrigin이 시즌 Aggregate 계약과 다릅니다: {origin}"
            )
    defense_records = player.get("defenseRecords")
    if not isinstance(defense_records, list):
        return
    for record in defense_records:
        if not isinstance(record, dict) or not record:
            continue
        origin = record.get("aggregateOrigin")
        if origin not in allowed:
            issues.append(
                f"{label} defenseRecords.aggregateOrigin이 시즌 Aggregate 계약과 다릅니다: {origin}"
            )


def _validate_hitter(
    stats: dict[str, object] | None,
    label: str,
    errors: list[str],
    warnings: list[str],
) -> None:
    if not stats:
        return
    _validate_non_negative(
        stats,
        (
            "games",
            "plateAppearances",
            "atBats",
            "hits",
            "doubles",
            "triples",
            "homeRuns",
            "walks",
            "strikeouts",
        ),
        label,
        errors,
    )
    if _all_present(stats, "plateAppearances", "atBats") and stats["plateAppearances"] < stats["atBats"]:
        errors.append(f"{label} PA < AB")
    if _all_present(stats, "hits", "atBats") and stats["hits"] > stats["atBats"]:
        errors.append(f"{label} H > AB")
    if _all_present(stats, "doubles", "triples", "homeRuns", "hits"):
        if stats["doubles"] + stats["triples"] + stats["homeRuns"] > stats["hits"]:
            errors.append(f"{label} 2B + 3B + HR > H")

    if _all_present(stats, "sourceAVG", "hits", "atBats") and stats["atBats"] > 0:
        calculated = stats["hits"] / stats["atBats"]
        _warn_rate_difference(label, "AVG", stats["sourceAVG"], calculated, 0.002, warnings)
    if _all_present(stats, "sourceSLG", "hits", "doubles", "triples", "homeRuns", "atBats") and stats["atBats"] > 0:
        singles = stats["hits"] - stats["doubles"] - stats["triples"] - stats["homeRuns"]
        calculated = (
            singles + 2 * stats["doubles"] + 3 * stats["triples"] + 4 * stats["homeRuns"]
        ) / stats["atBats"]
        _warn_rate_difference(label, "SLG", stats["sourceSLG"], calculated, 0.002, warnings)
    if _all_present(stats, "sourceOBP", "hits", "walks", "hitByPitch", "atBats", "sacrificeFlies"):
        denominator = stats["atBats"] + stats["walks"] + stats["hitByPitch"] + stats["sacrificeFlies"]
        if denominator > 0:
            calculated = (stats["hits"] + stats["walks"] + stats["hitByPitch"]) / denominator
            _warn_rate_difference(label, "OBP", stats["sourceOBP"], calculated, 0.002, warnings)


def _validate_pitcher(
    stats: dict[str, object] | None,
    label: str,
    errors: list[str],
    warnings: list[str],
) -> None:
    if not stats:
        return
    _validate_non_negative(
        stats,
        ("inningsOuts", "earnedRuns", "runsAllowed", "strikeouts", "walks"),
        label,
        errors,
    )
    if _all_present(stats, "earnedRuns", "runsAllowed") and stats["earnedRuns"] > stats["runsAllowed"]:
        errors.append(f"{label} ER > R")
    if _all_present(stats, "sourceERA", "earnedRuns", "inningsOuts") and stats["inningsOuts"] > 0:
        calculated = stats["earnedRuns"] * 27 / stats["inningsOuts"]
        _warn_rate_difference(label, "ERA", stats["sourceERA"], calculated, 0.02, warnings)
    if _all_present(stats, "sourceWHIP", "hitsAllowed", "walks", "inningsOuts") and stats["inningsOuts"] > 0:
        calculated = (stats["hitsAllowed"] + stats["walks"]) * 3 / stats["inningsOuts"]
        _warn_rate_difference(label, "WHIP", stats["sourceWHIP"], calculated, 0.02, warnings)


def _validate_running(
    stats: dict[str, object] | None,
    label: str,
    errors: list[str],
) -> None:
    if not stats:
        return
    _validate_non_negative(
        stats,
        ("stolenBaseAttempts", "stolenBases", "caughtStealing"),
        label,
        errors,
    )
    if _all_present(stats, "stolenBaseAttempts", "stolenBases", "caughtStealing"):
        if stats["stolenBases"] + stats["caughtStealing"] > stats["stolenBaseAttempts"]:
            errors.append(f"{label} SB + CS > SBA")


def _validate_defense(
    stats: dict[str, object],
    label: str,
    errors: list[str],
) -> None:
    _validate_non_negative(
        stats,
        (
            "games",
            "gamesStarted",
            "inningsOuts",
            "errors",
            "pickoffs",
            "putouts",
            "assists",
            "doublePlays",
            "passedBalls",
            "stolenBasesAllowed",
            "caughtStealing",
        ),
        label,
        errors,
    )


def _validate_aggregate_corroboration(
    player: dict[str, object],
    label: str,
    errors: list[str],
) -> None:
    team_filter_records = player.get("teamFilterRecords") or []
    for target in ("hitterStats", "pitcherStats", "runningStats"):
        stats = player.get(target)
        if not stats or stats.get("aggregateOrigin") != "KboLeagueAggregate":
            continue
        if not any(record.get(target) is not None for record in team_filter_records):
            errors.append(
                f"{label} {target} 시즌 Aggregate가 Team Filter에서 확인되지 않습니다."
            )

    defense_records = player.get("defenseRecords") or []
    if any(
        record.get("aggregateOrigin") == "KboLeagueAggregate"
        for record in defense_records
    ) and not any(record.get("defenseRecords") for record in team_filter_records):
        errors.append(
            f"{label} defenseRecords 시즌 Aggregate가 Team Filter에서 확인되지 않습니다."
        )


def _validate_aggregate_team_coverage(
    document: dict[str, object],
    ranked_team_names: set[str],
    errors: list[str],
) -> None:
    """Team Filter가 Aggregate Cache로 저장된 한 구단짜리 표를 차단한다."""
    if not ranked_team_names:
        return
    statuses = document.get("dataAvailabilityStatus", {})
    checks = (
        ("hasHitterBasic1", "hitterStats", "plateAppearances"),
        ("hasHitterBasic2", "hitterStats", "walks"),
        ("hasHitterDetail", "hitterStats", "extraBaseHits"),
        ("hasPitcherBasic1", "pitcherStats", "inningsOuts"),
        ("hasPitcherBasic2", "pitcherStats", "completeGames"),
        ("hasPitcherDetail", "pitcherStats", "gamesStarted"),
        ("hasRunning", "runningStats", "stolenBaseAttempts"),
    )
    players = document.get("players", [])
    for status_key, target, sentinel_field in checks:
        if statuses.get(status_key) not in {"Available", "AvailableEmpty"}:
            continue
        observed = {
            str(player.get("aggregateTeamName") or "")
            for player in players
            if isinstance(player.get(target), dict)
            and player[target].get("aggregateOrigin") == "KboLeagueAggregate"
            and sentinel_field in player[target]
        }
        observed_ranked = sorted(ranked_team_names & observed)
        if len(observed_ranked) < 2:
            errors.append(
                f"{status_key} 시즌 Aggregate가 두 구단 미만입니다: {observed_ranked}"
            )

    if statuses.get("hasDefense") in {"Available", "AvailableEmpty"}:
        observed_defense = {
            str(player.get("aggregateTeamName") or "")
            for player in players
            if any(
                record.get("aggregateOrigin") == "KboLeagueAggregate"
                and "position" in record
                for record in (player.get("defenseRecords") or [])
            )
        }
        observed_ranked_defense = sorted(ranked_team_names & observed_defense)
        if len(observed_ranked_defense) < 2:
            errors.append(
                "hasDefense 시즌 Aggregate가 두 구단 미만입니다: "
                f"{observed_ranked_defense}"
            )


def _validate_non_negative(
    stats: dict[str, object],
    keys: tuple[str, ...],
    label: str,
    errors: list[str],
) -> None:
    for key in keys:
        value = stats.get(key)
        if value is not None and value < 0:
            errors.append(f"{label} {key} < 0")


def _all_present(stats: dict[str, object], *keys: str) -> bool:
    return all(stats.get(key) is not None for key in keys)


def _warn_rate_difference(
    label: str,
    metric: str,
    source: float,
    calculated: float,
    tolerance: float,
    warnings: list[str],
) -> None:
    if abs(source - calculated) > tolerance:
        warnings.append(
            f"{label} {metric} 표기값/재계산값 차이: {source:.4f}/{calculated:.4f}"
        )
