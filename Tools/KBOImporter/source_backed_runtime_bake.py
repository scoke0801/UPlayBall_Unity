"""Source Player/PlayerSeason을 1:1로 보존하는 Runtime bake 계획을 만든다.

이 모듈은 기존 다중 Reference 합성 경로와 독립적이다. SourceBacked 콘텐츠의
identity/season 보존과 10개 Franchise 배치만 담당하며, Replacement 능력치 생성과
Core25 최종 선발은 주입 지점 뒤의 별도 단계가 담당한다.
"""

from __future__ import annotations

import copy
import hashlib
import json
from dataclasses import dataclass
from typing import Any, Callable, Iterable, Mapping, Sequence


IDENTITY_POLICY_VERSION = "source-backed-identity-v1"
NAME_POLICY_VERSION = "source-backed-fictional-name-v1"
ALLOCATION_POLICY_VERSION = "source-backed-franchise-allocation-v1"
REPLACEMENT_REQUEST_VERSION = "source-backed-replacement-request-v1"

EDITOR_SOURCE_PERSON_ID_VERSION = "editor-source-person-v1"
EDITOR_SOURCE_SEASON_ID_VERSION = "editor-source-season-v1"

FRANCHISE_IDS: tuple[str, ...] = (
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

CORE_HITTER_COUNT = 14
CORE_PITCHER_COUNT = 11

_SENSITIVE_EXACT_KEYS = frozenset(
    {
        "originalname",
        "referencename",
        "referencenames",
        "referencesimilaritydistance",
        "isoriginalsourceseason",
        "abilityderivationtrace",
        "costderivationtrace",
        "positionrolederivationtrace",
        "rosterselectiontrace",
        "derivationwarnings",
        "validationwarnings",
    }
)

_SURNAMES = tuple("김이박최정강조윤장임한오서신권황안송류홍전고문양손배백허유남심노하곽성차주우구민진지엄채원천방공현함변염여추도소석선설마길연위표명기반왕금옥육인맹제탁국")
_GIVEN_FIRST = tuple("민서지현준우성도하윤시재태수영진호건주혁찬승원정규경동환희")
_GIVEN_SECOND = tuple("준우호진혁민석현수빈영원성훈환재윤찬건규하도경태욱승")


def stable_digest(*parts: object, length: int = 20) -> str:
    """입력 순서가 같은 경우 플랫폼과 무관한 소문자 digest를 반환한다."""

    payload = "\0".join(str(part) for part in parts).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()[:length]


def stable_integer(*parts: object) -> int:
    """결정론적 정수 tie-break 값을 반환한다."""

    payload = "\0".join(str(part) for part in parts).encode("utf-8")
    return int.from_bytes(hashlib.sha256(payload).digest()[:8], "big")


def editor_source_person_id(source_player_id: str) -> str:
    """Editor Source archive의 opaque PlayerPersonId를 재구성한다."""

    return "PERSON_" + stable_digest(EDITOR_SOURCE_PERSON_ID_VERSION, source_player_id)


def editor_source_season_id(source_player_id: str, origin_year: int) -> str:
    """Editor Source archive의 opaque PlayerSeasonId를 재구성한다."""

    return "SEASON_" + stable_digest(
        EDITOR_SOURCE_SEASON_ID_VERSION,
        source_player_id,
        origin_year,
    )


def runtime_player_person_id(source_player_id: str) -> str:
    """실제 Source ID를 노출하지 않는 Runtime PlayerPersonId를 만든다."""

    return "PERSON_" + stable_digest(IDENTITY_POLICY_VERSION, source_player_id)


def runtime_player_season_id(source_player_id: str, origin_year: int) -> str:
    """Source PlayerSeason 1건에 대응하는 Runtime PlayerSeasonId를 만든다."""

    return "SEASON_" + stable_digest(
        IDENTITY_POLICY_VERSION,
        source_player_id,
        origin_year,
    )


@dataclass(frozen=True)
class ReplacementRequest:
    """별도 Replacement 생성기에 전달하는 Core quota 부족 요청이다."""

    request_id: str
    origin_year: int
    franchise_id: str
    team_season_key: str
    player_type: str
    count: int
    required_core_count: int
    source_backed_candidate_count: int
    generation_population_policy: str = "OriginYearPositionRoleSourceBackedOnly"

    def to_dict(self) -> dict[str, Any]:
        """JSON 직렬화 가능한 요청 DTO를 반환한다."""

        return {
            "requestId": self.request_id,
            "originYear": self.origin_year,
            "franchiseId": self.franchise_id,
            "teamSeasonKey": self.team_season_key,
            "playerType": self.player_type,
            "count": self.count,
            "requiredCoreCount": self.required_core_count,
            "sourceBackedCandidateCount": self.source_backed_candidate_count,
            "generationPopulationPolicy": self.generation_population_policy,
        }


@dataclass(frozen=True)
class SourceBackedRuntimeBakePlan:
    """SourceBacked archive 조각과 Replacement 요청을 묶은 중간 계약이다."""

    runtime_content: dict[str, Any]
    replacement_requests: tuple[ReplacementRequest, ...]


@dataclass(frozen=True)
class _SourceSeasonIdentity:
    source_player_id: str
    source_player_name: str
    origin_year: int
    editor_person_id: str
    editor_season_id: str
    runtime_person_id: str
    runtime_season_id: str


def build_source_backed_runtime_plan(
    editor_source_content: Mapping[str, Any],
    normalized_references: Sequence[Mapping[str, Any]],
    *,
    allocation_seed: int = 0,
    replacement_request_sink: Callable[[ReplacementRequest], None] | None = None,
) -> SourceBackedRuntimeBakePlan:
    """Editor Source와 normalized record에서 1:1 Runtime 배치 계획을 만든다.

    ``replacement_request_sink``는 Replacement 생성기의 주입 경계다. 이 함수는
    요청만 발행하고 Replacement vector나 최종 Core25를 직접 생성하지 않는다.
    """

    identities = _build_identity_index(normalized_references)
    editor_persons = _index_editor_persons(editor_source_content)
    expected_editor_person_ids = {
        identity.editor_person_id for identity in identities.values()
    }
    if set(editor_persons) != expected_editor_person_ids:
        missing = sorted(expected_editor_person_ids - set(editor_persons))
        extra = sorted(set(editor_persons) - expected_editor_person_ids)
        raise ValueError(
            "Editor/normalized PlayerPerson 집합이 1:1이 아닙니다: "
            f"missing={missing[:3]}, extra={extra[:3]}"
        )
    editor_seasons_by_year = _validate_and_index_editor_seasons(
        editor_source_content,
        identities,
        editor_persons,
    )
    fictional_names = _build_fictional_name_map(identities.values())
    runtime_persons = _build_runtime_persons(
        identities.values(),
        editor_persons,
        fictional_names,
    )

    runtime_years: list[dict[str, Any]] = []
    all_requests: list[ReplacementRequest] = []
    total_source_seasons = 0

    editor_years = {
        int(year_data["year"]): year_data
        for year_data in editor_source_content.get("years", [])
    }
    for origin_year in sorted(editor_seasons_by_year):
        editor_year = editor_years[origin_year]
        runtime_year, requests = _build_runtime_year(
            origin_year,
            editor_year,
            editor_seasons_by_year[origin_year],
            identities,
            allocation_seed,
        )
        runtime_years.append(runtime_year)
        all_requests.extend(requests)
        total_source_seasons += len(runtime_year["playerSeasons"])

    all_requests.sort(
        key=lambda request: (
            request.origin_year,
            request.franchise_id,
            request.player_type,
            request.request_id,
        )
    )
    if replacement_request_sink is not None:
        for request in all_requests:
            replacement_request_sink(request)

    runtime_content = {
        "contentKind": "SourceBackedRuntimeBakePlan",
        "schemaVersion": editor_source_content.get("schemaVersion", ""),
        "playerPersons": runtime_persons,
        "years": runtime_years,
        "planManifest": {
            "identityPolicyVersion": IDENTITY_POLICY_VERSION,
            "namePolicyVersion": NAME_POLICY_VERSION,
            "allocationPolicyVersion": ALLOCATION_POLICY_VERSION,
            "replacementRequestVersion": REPLACEMENT_REQUEST_VERSION,
            "allocationSeed": allocation_seed,
            "franchiseCount": len(FRANCHISE_IDS),
            "sourceBackedPlayerPersonCount": len(runtime_persons),
            "sourceBackedPlayerSeasonCount": total_source_seasons,
            "replacementRequestCount": len(all_requests),
            "isFinalCore25Archive": False,
        },
    }
    _assert_no_actual_identity_leak(runtime_content, identities.values())
    return SourceBackedRuntimeBakePlan(runtime_content, tuple(all_requests))


def validate_source_backed_runtime_plan(plan: SourceBackedRuntimeBakePlan) -> None:
    """1:1 보존, 10팀 배치, quota 부족 요청 계약을 독립 검증한다."""

    content = plan.runtime_content
    person_ids = [person["playerPersonId"] for person in content["playerPersons"]]
    if len(person_ids) != len(set(person_ids)):
        raise ValueError("Runtime PlayerPersonId가 중복되었습니다.")

    requests_by_id = {
        request.request_id: request for request in plan.replacement_requests
    }
    if len(requests_by_id) != len(plan.replacement_requests):
        raise ValueError("ReplacementRequestId가 중복되었습니다.")

    all_season_ids: set[str] = set()
    for year_data in content["years"]:
        year = int(year_data["year"])
        season_ids = [season["playerSeasonId"] for season in year_data["playerSeasons"]]
        if len(season_ids) != len(set(season_ids)):
            raise ValueError(f"{year} Runtime PlayerSeasonId가 중복되었습니다.")
        if all_season_ids.intersection(season_ids):
            raise ValueError(f"{year} PlayerSeason이 다른 연도에도 중복되었습니다.")
        all_season_ids.update(season_ids)

        teams = year_data["teamAllocationPlans"]
        if len(teams) != len(FRANCHISE_IDS):
            raise ValueError(f"{year} Franchise 수가 {len(FRANCHISE_IDS)}가 아닙니다.")
        allocated_ids: list[str] = []
        hitter_count = 0
        pitcher_count = 0
        requested_hitter_count = 0
        requested_pitcher_count = 0
        for team in teams:
            team_season_ids = team["sourceBackedPlayerSeasonIds"]
            allocated_ids.extend(team_season_ids)
            hitter_count += len(team["sourceBackedHitterSeasonIds"])
            pitcher_count += len(team["sourceBackedPitcherSeasonIds"])
            for request_id in team["replacementRequestIds"]:
                request = requests_by_id[request_id]
                if request.player_type == "Hitter":
                    requested_hitter_count += request.count
                elif request.player_type == "Pitcher":
                    requested_pitcher_count += request.count
                else:
                    raise ValueError(f"알 수 없는 PlayerType: {request.player_type}")

        if sorted(allocated_ids) != sorted(season_ids):
            raise ValueError(f"{year} Source PlayerSeason 배치가 1:1이 아닙니다.")
        expected_hitters = max(0, len(FRANCHISE_IDS) * CORE_HITTER_COUNT - hitter_count)
        expected_pitchers = max(0, len(FRANCHISE_IDS) * CORE_PITCHER_COUNT - pitcher_count)
        if requested_hitter_count != expected_hitters:
            raise ValueError(f"{year} Hitter Replacement 부족 수가 일치하지 않습니다.")
        if requested_pitcher_count != expected_pitchers:
            raise ValueError(f"{year} Pitcher Replacement 부족 수가 일치하지 않습니다.")


def _build_identity_index(
    normalized_references: Sequence[Mapping[str, Any]],
) -> dict[str, _SourceSeasonIdentity]:
    identities: dict[str, _SourceSeasonIdentity] = {}
    for year_document in normalized_references:
        origin_year = int(year_document["year"])
        for player in year_document.get("players", []):
            source_player_id = str(player.get("sourcePlayerId", "")).strip()
            if not source_player_id:
                raise ValueError(f"{origin_year} normalized player에 sourcePlayerId가 없습니다.")
            source_player_name = str(player.get("playerName", "")).strip()
            if not source_player_name:
                raise ValueError(f"{origin_year}/{source_player_id} playerName이 없습니다.")
            editor_season = editor_source_season_id(source_player_id, origin_year)
            if editor_season in identities:
                raise ValueError(
                    f"normalized Source PlayerSeason이 중복되었습니다: {source_player_id}/{origin_year}"
                )
            identities[editor_season] = _SourceSeasonIdentity(
                source_player_id=source_player_id,
                source_player_name=source_player_name,
                origin_year=origin_year,
                editor_person_id=editor_source_person_id(source_player_id),
                editor_season_id=editor_season,
                runtime_person_id=runtime_player_person_id(source_player_id),
                runtime_season_id=runtime_player_season_id(source_player_id, origin_year),
            )
    if not identities:
        raise ValueError("normalized Source PlayerSeason이 없습니다.")
    return identities


def _index_editor_persons(
    editor_source_content: Mapping[str, Any],
) -> dict[str, Mapping[str, Any]]:
    persons: dict[str, Mapping[str, Any]] = {}
    for person in editor_source_content.get("playerPersons", []):
        person_id = str(person["playerPersonId"])
        if person_id in persons:
            raise ValueError(f"Editor PlayerPersonId가 중복되었습니다: {person_id}")
        persons[person_id] = person
    return persons


def _validate_and_index_editor_seasons(
    editor_source_content: Mapping[str, Any],
    identities: Mapping[str, _SourceSeasonIdentity],
    editor_persons: Mapping[str, Mapping[str, Any]],
) -> dict[int, list[Mapping[str, Any]]]:
    by_year: dict[int, list[Mapping[str, Any]]] = {}
    consumed: set[str] = set()
    seen_years: set[int] = set()
    for year_data in editor_source_content.get("years", []):
        origin_year = int(year_data["year"])
        if origin_year in seen_years:
            raise ValueError(f"Editor archive year가 중복되었습니다: {origin_year}")
        seen_years.add(origin_year)
        seasons: list[Mapping[str, Any]] = []
        for season in year_data.get("playerSeasons", []):
            editor_season = str(season["playerSeasonId"])
            identity = identities.get(editor_season)
            if identity is None:
                raise ValueError(
                    f"normalized record와 연결되지 않는 Editor PlayerSeason입니다: {editor_season}"
                )
            if identity.origin_year != origin_year or int(season["originYear"]) != origin_year:
                raise ValueError(f"SeasonYear가 일치하지 않습니다: {editor_season}")
            if str(season["playerPersonId"]) != identity.editor_person_id:
                raise ValueError(f"Source PlayerPerson 연결이 일치하지 않습니다: {editor_season}")
            if identity.editor_person_id not in editor_persons:
                raise ValueError(f"Editor PlayerPerson이 없습니다: {identity.editor_person_id}")
            if "isOriginalSourceSeason" in season and not bool(
                season["isOriginalSourceSeason"]
            ):
                raise ValueError(f"합성 PlayerSeason은 SourceBacked 경로에 들어올 수 없습니다: {editor_season}")
            reference_names = season.get("sourceReferenceNames")
            if reference_names is not None and list(reference_names) != [
                identity.source_player_name
            ]:
                raise ValueError(
                    "SourceBacked PlayerSeason은 하나의 normalized Source와만 연결되어야 합니다: "
                    f"{editor_season}"
                )
            if editor_season in consumed:
                raise ValueError(f"Editor PlayerSeason이 중복되었습니다: {editor_season}")
            consumed.add(editor_season)
            seasons.append(season)
        by_year[origin_year] = seasons

    missing = sorted(set(identities) - consumed)
    if missing:
        raise ValueError(f"Editor archive에 없는 normalized PlayerSeason이 있습니다: {missing[:3]}")
    return by_year


def _build_fictional_name_map(
    identities: Iterable[_SourceSeasonIdentity],
) -> dict[str, str]:
    identities_by_person: dict[str, _SourceSeasonIdentity] = {}
    forbidden_names: set[str] = set()
    for identity in identities:
        identities_by_person.setdefault(identity.runtime_person_id, identity)
        forbidden_names.add(identity.source_player_name)

    candidate_count = len(_SURNAMES) * len(_GIVEN_FIRST) * len(_GIVEN_SECOND)
    if candidate_count < len(identities_by_person) + len(forbidden_names):
        raise ValueError("가명 후보 공간이 Source Person 수보다 작습니다.")

    used_names: set[str] = set()
    result: dict[str, str] = {}
    for runtime_person_id in sorted(identities_by_person):
        start = stable_integer(NAME_POLICY_VERSION, runtime_person_id) % candidate_count
        for probe in range(candidate_count):
            index = (start + probe) % candidate_count
            surname_index, remainder = divmod(
                index,
                len(_GIVEN_FIRST) * len(_GIVEN_SECOND),
            )
            first_index, second_index = divmod(remainder, len(_GIVEN_SECOND))
            candidate = (
                _SURNAMES[surname_index]
                + _GIVEN_FIRST[first_index]
                + _GIVEN_SECOND[second_index]
            )
            if candidate in forbidden_names or candidate in used_names:
                continue
            result[runtime_person_id] = candidate
            used_names.add(candidate)
            break
        else:
            raise ValueError("중복되지 않는 가명을 배정하지 못했습니다.")
    return result


def _build_runtime_persons(
    identities: Iterable[_SourceSeasonIdentity],
    editor_persons: Mapping[str, Mapping[str, Any]],
    fictional_names: Mapping[str, str],
) -> list[dict[str, Any]]:
    identity_by_runtime_person: dict[str, _SourceSeasonIdentity] = {}
    for identity in identities:
        identity_by_runtime_person.setdefault(identity.runtime_person_id, identity)

    runtime_persons: list[dict[str, Any]] = []
    for runtime_person_id in sorted(identity_by_runtime_person):
        identity = identity_by_runtime_person[runtime_person_id]
        editor_person = editor_persons[identity.editor_person_id]
        runtime_person = _sanitize_runtime_value(copy.deepcopy(dict(editor_person)))
        runtime_person["playerPersonId"] = runtime_person_id
        runtime_person["fictionalName"] = fictional_names[runtime_person_id]
        runtime_persons.append(runtime_person)
    return runtime_persons


def _build_runtime_year(
    origin_year: int,
    editor_year: Mapping[str, Any],
    editor_seasons: Sequence[Mapping[str, Any]],
    identities: Mapping[str, _SourceSeasonIdentity],
    allocation_seed: int,
) -> tuple[dict[str, Any], list[ReplacementRequest]]:
    rows: list[tuple[dict[str, Any], _SourceSeasonIdentity]] = []
    for editor_season in editor_seasons:
        editor_season_id_value = str(editor_season["playerSeasonId"])
        identity = identities[editor_season_id_value]
        runtime_season = _sanitize_runtime_value(copy.deepcopy(dict(editor_season)))
        runtime_season["playerSeasonId"] = identity.runtime_season_id
        runtime_season["playerPersonId"] = identity.runtime_person_id
        runtime_season["originYear"] = origin_year
        runtime_season["rosterRole"] = ""
        runtime_season["dataProvenance"] = "SourceBacked"
        rows.append((runtime_season, identity))

    team_rows: dict[str, list[tuple[dict[str, Any], _SourceSeasonIdentity]]] = {
        franchise_id: [] for franchise_id in FRANCHISE_IDS
    }
    for player_type in ("Hitter", "Pitcher"):
        typed_rows = [row for row in rows if row[0].get("playerType") == player_type]
        typed_rows.sort(
            key=lambda row: (
                stable_digest(
                    ALLOCATION_POLICY_VERSION,
                    allocation_seed,
                    origin_year,
                    player_type,
                    row[1].runtime_season_id,
                    length=32,
                ),
                row[1].runtime_season_id,
            )
        )
        offset = stable_integer(
            ALLOCATION_POLICY_VERSION,
            allocation_seed,
            origin_year,
            player_type,
        ) % len(FRANCHISE_IDS)
        for index, row in enumerate(typed_rows):
            franchise_id = FRANCHISE_IDS[(offset + index) % len(FRANCHISE_IDS)]
            team_rows[franchise_id].append(row)

    unsupported = [
        row[1].runtime_season_id
        for row in rows
        if row[0].get("playerType") not in {"Hitter", "Pitcher"}
    ]
    if unsupported:
        raise ValueError(f"알 수 없는 playerType이 있습니다: {unsupported[:3]}")

    assignment_by_editor_season: dict[str, tuple[str, str]] = {}
    team_plans: list[dict[str, Any]] = []
    requests: list[ReplacementRequest] = []
    for franchise_id in FRANCHISE_IDS:
        team_season_key = f"{franchise_id}_{origin_year}"
        allocated = sorted(
            team_rows[franchise_id],
            key=lambda row: row[1].runtime_season_id,
        )
        hitter_ids = [
            row[1].runtime_season_id
            for row in allocated
            if row[0]["playerType"] == "Hitter"
        ]
        pitcher_ids = [
            row[1].runtime_season_id
            for row in allocated
            if row[0]["playerType"] == "Pitcher"
        ]
        request_ids: list[str] = []
        for player_type, candidate_ids, required_count in (
            ("Hitter", hitter_ids, CORE_HITTER_COUNT),
            ("Pitcher", pitcher_ids, CORE_PITCHER_COUNT),
        ):
            missing_count = max(0, required_count - len(candidate_ids))
            if missing_count == 0:
                continue
            request = ReplacementRequest(
                request_id="REPLACEMENT_REQUEST_"
                + stable_digest(
                    REPLACEMENT_REQUEST_VERSION,
                    allocation_seed,
                    origin_year,
                    franchise_id,
                    player_type,
                ),
                origin_year=origin_year,
                franchise_id=franchise_id,
                team_season_key=team_season_key,
                player_type=player_type,
                count=missing_count,
                required_core_count=required_count,
                source_backed_candidate_count=len(candidate_ids),
            )
            requests.append(request)
            request_ids.append(request.request_id)

        all_ids = hitter_ids + pitcher_ids
        team_plans.append(
            {
                "teamSeasonKey": team_season_key,
                "franchiseId": franchise_id,
                "originYear": origin_year,
                "sourceBackedPlayerSeasonIds": all_ids,
                "sourceBackedNormalCardIds": [
                    _normal_card_id(season_id) for season_id in all_ids
                ],
                "sourceBackedHitterSeasonIds": hitter_ids,
                "sourceBackedPitcherSeasonIds": pitcher_ids,
                "replacementRequestIds": sorted(request_ids),
                "coreSelectionStatus": "PendingReplacementAndRosterSelection",
            }
        )
        for _, identity in allocated:
            assignment_by_editor_season[identity.editor_season_id] = (
                franchise_id,
                team_season_key,
            )

    runtime_seasons: list[dict[str, Any]] = []
    runtime_cards: list[dict[str, Any]] = []
    for runtime_season, identity in rows:
        franchise_id, team_season_key = assignment_by_editor_season[
            identity.editor_season_id
        ]
        runtime_season["originFranchiseId"] = franchise_id
        runtime_season["originTeamSeasonKey"] = team_season_key
        runtime_seasons.append(runtime_season)
        runtime_cards.append(
            {
                "cardId": _normal_card_id(identity.runtime_season_id),
                "cardType": "Normal",
                "playerSeasonId": identity.runtime_season_id,
                "editionId": "",
                "editionDelta": {},
            }
        )
    runtime_seasons.sort(key=lambda season: season["playerSeasonId"])
    runtime_cards.sort(key=lambda card: card["cardId"])

    record_by_editor_season = {
        str(record["playerSeasonId"]): record
        for record in editor_year.get("originalSeasonRecords", [])
    }
    runtime_records: list[dict[str, Any]] = []
    for _, identity in rows:
        record = record_by_editor_season.get(identity.editor_season_id)
        if record is None:
            continue
        runtime_record = _sanitize_runtime_value(copy.deepcopy(dict(record)))
        runtime_record["playerSeasonId"] = identity.runtime_season_id
        runtime_record["teamSeasonKey"] = assignment_by_editor_season[
            identity.editor_season_id
        ][1]
        runtime_records.append(runtime_record)
    runtime_records.sort(key=lambda record: record["playerSeasonId"])

    runtime_awards: list[dict[str, Any]] = []
    for award in editor_year.get("originalAwardRecords", []):
        editor_season_id_value = str(award.get("playerSeasonId", ""))
        identity = identities.get(editor_season_id_value)
        if identity is None:
            continue
        runtime_award = _sanitize_runtime_value(copy.deepcopy(dict(award)))
        runtime_award["playerSeasonId"] = identity.runtime_season_id
        runtime_awards.append(runtime_award)
    runtime_awards.sort(
        key=lambda award: (
            award.get("playerSeasonId", ""),
            award.get("awardType", ""),
        )
    )

    hitter_count = sum(
        1 for season in runtime_seasons if season["playerType"] == "Hitter"
    )
    pitcher_count = len(runtime_seasons) - hitter_count
    return (
        {
            "year": origin_year,
            "playerSeasons": runtime_seasons,
            "normalCards": runtime_cards,
            "teamAllocationPlans": team_plans,
            "seasonRecords": runtime_records,
            "awardRecords": runtime_awards,
            "allocationReport": {
                "sourceBackedHitterCount": hitter_count,
                "sourceBackedPitcherCount": pitcher_count,
                "requiredReplacementHitterCount": max(
                    0,
                    len(FRANCHISE_IDS) * CORE_HITTER_COUNT - hitter_count,
                ),
                "requiredReplacementPitcherCount": max(
                    0,
                    len(FRANCHISE_IDS) * CORE_PITCHER_COUNT - pitcher_count,
                ),
                "sourceBackedSeasonCount": len(runtime_seasons),
            },
        },
        requests,
    )


def _normal_card_id(runtime_season_id: str) -> str:
    return "CARD_" + stable_digest(IDENTITY_POLICY_VERSION, runtime_season_id, "Normal")


def _sanitize_runtime_value(value: Any) -> Any:
    if isinstance(value, Mapping):
        sanitized: dict[str, Any] = {}
        for key, child in value.items():
            normalized_key = str(key).casefold()
            if normalized_key.startswith("source"):
                continue
            if normalized_key in _SENSITIVE_EXACT_KEYS:
                continue
            sanitized[str(key)] = _sanitize_runtime_value(child)
        return sanitized
    if isinstance(value, list):
        return [_sanitize_runtime_value(child) for child in value]
    if isinstance(value, tuple):
        return [_sanitize_runtime_value(child) for child in value]
    return value


def _assert_no_actual_identity_leak(
    runtime_content: Mapping[str, Any],
    identities: Iterable[_SourceSeasonIdentity],
) -> None:
    actual_names = {identity.source_player_name for identity in identities}
    actual_ids = {identity.source_player_id for identity in identities}

    def inspect(value: Any, path: str) -> None:
        if isinstance(value, Mapping):
            for key, child in value.items():
                normalized_key = str(key).casefold()
                if normalized_key.startswith("sourceplayer") or normalized_key in {
                    "originalname",
                    "referencename",
                    "referencenames",
                }:
                    raise ValueError(f"Runtime provenance field가 남았습니다: {path}.{key}")
                inspect(child, f"{path}.{key}")
            return
        if isinstance(value, (list, tuple)):
            for index, child in enumerate(value):
                inspect(child, f"{path}[{index}]")
            return
        if isinstance(value, str) and (value in actual_names or value in actual_ids):
            raise ValueError(f"Runtime에 실제 선수 identity가 노출되었습니다: {path}")

    inspect(runtime_content, "runtime")


def canonical_json_bytes(plan: SourceBackedRuntimeBakePlan) -> bytes:
    """동일 입력/seed 결정론 검증용 canonical JSON bytes를 반환한다."""

    document = {
        "runtimeContent": plan.runtime_content,
        "replacementRequests": [
            request.to_dict() for request in plan.replacement_requests
        ],
    }
    return json.dumps(
        document,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8")
