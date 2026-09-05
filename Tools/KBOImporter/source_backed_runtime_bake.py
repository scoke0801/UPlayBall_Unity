"""Source PlayerSeason과 Source TeamSeason을 1:1로 보존하는 bake 계획을 만든다.

일반 Franchise는 normalized Source TeamSeason을 그대로 canonicalize한다. 선수는
자신의 Source TeamSeason 안에서만 Core25 후보가 되며, 부족 슬롯만 명시적인
Replacement 요청으로 남긴다. 실제 Source ID/이름은 runtime-safe stable ID로 바꾼다.
"""

from __future__ import annotations

import copy
import hashlib
import json
from collections import Counter
from dataclasses import dataclass
from typing import Any, Callable, Iterable, Mapping, Sequence


IDENTITY_POLICY_VERSION = "source-backed-identity-v1"
FRANCHISE_ID_POLICY_VERSION = "source-franchise-identity-v1"
TEAM_SEASON_ID_POLICY_VERSION = "source-team-season-identity-v1"
ALLOCATION_POLICY_VERSION = "source-team-season-one-to-one-v2"
REPLACEMENT_REQUEST_VERSION = "source-team-shortage-request-v2"
WORLD_IDENTITY_NAME_POOL_VERSION = "world-identity-name-pool-v1"

EDITOR_SOURCE_PERSON_ID_VERSION = "editor-source-person-v1"
EDITOR_SOURCE_SEASON_ID_VERSION = "editor-source-season-v1"

CORE_HITTER_COUNT = 14
CORE_PITCHER_COUNT = 11
LEAGUE_TEAM_TARGET_COUNT = 10

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

_FOREIGN_GIVEN_NAMES = (
    "마이클", "다니엘", "데이비드", "제임스", "로버트", "존", "윌리엄", "리처드",
    "토마스", "찰스", "조셉", "크리스", "매튜", "앤드류", "라이언", "브랜든",
    "나다니엘", "사무엘", "헨리", "벤자민", "루이스", "카를로스", "미겔", "안토니오",
)
_FOREIGN_FAMILY_NAMES = (
    "존슨", "스미스", "윌리엄스", "브라운", "존스", "밀러", "데이비스", "윌슨",
    "무어", "타일러", "앤더슨", "토마스", "잭슨", "화이트", "해리스", "마틴",
    "톰슨", "로빈슨", "클라크", "루이스", "리", "워커", "홀", "엘리스",
    "영", "킹", "라이트", "터너", "힐", "그린", "베이커", "넬슨",
)
_FRANCHISE_REGIONS = (
    "서울", "부산", "인천", "대구", "대전", "광주", "수원", "창원", "전주", "강릉",
    "고양", "울산", "제주", "포항", "청주", "천안", "원주", "김해", "성남", "안양",
)
_FRANCHISE_NICKNAMES = (
    "코멧츠", "타이드", "하버스", "포지", "파이오니어스", "피닉스", "가디언즈", "마리너스",
    "스타즈", "웨이브즈", "팔콘즈", "파워스", "세이버즈", "볼트즈", "레이더스", "타이탄즈",
    "렉스", "크라운즈", "베어스", "윈드스",
)
_BANNED_PLAYER_NAME_TOKENS = ("블레이즈", "썬더", "파워", "베이스볼", "스타")

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


def runtime_franchise_id(source_franchise_key: str) -> str:
    """Source Franchise를 노출하지 않는 연도 공통 Runtime FranchiseId를 만든다."""

    return "FRANCHISE_" + stable_digest(
        FRANCHISE_ID_POLICY_VERSION,
        source_franchise_key,
    )


def runtime_team_season_key(source_franchise_key: str, origin_year: int) -> str:
    """Source TeamSeason 한 건에 대응하는 Runtime TeamSeasonKey를 만든다.

    Runtime 검증 계약은 ``TeamSeasonKey == FranchiseId + "_" + OriginYear``다. 같은
    Franchise의 여러 시즌을 Key만 보고 묶을 수 있어야 하므로 독립 Digest를 쓰지 않고
    FranchiseId에서 직접 파생한다.
    """

    return f"{runtime_franchise_id(source_franchise_key)}_{origin_year}"


def canonical_source_team_season_id(
    source_franchise_key: str,
    source_team_id: str,
    origin_year: int,
) -> str:
    """Offline 검증에서 Source TeamSeason 1:1을 추적할 opaque ID를 만든다."""

    return "SOURCE_TEAM_SEASON_" + stable_digest(
        TEAM_SEASON_ID_POLICY_VERSION,
        source_franchise_key,
        source_team_id,
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
    source_team_id: str
    source_team_name: str
    canonical_source_team_season_id: str
    runtime_franchise_id: str
    runtime_team_season_key: str
    team_assignment_basis: str


@dataclass(frozen=True)
class _SourceTeamSeasonIdentity:
    source_team_id: str
    source_team_name: str
    source_franchise_key: str
    source_franchise_basis: str
    origin_year: int
    canonical_source_team_season_id: str
    runtime_franchise_id: str
    runtime_team_season_key: str


def build_source_backed_runtime_plan(
    editor_source_content: Mapping[str, Any],
    normalized_references: Sequence[Mapping[str, Any]],
    *,
    allocation_seed: int = 0,
    replacement_request_sink: Callable[[ReplacementRequest], None] | None = None,
) -> SourceBackedRuntimeBakePlan:
    """Editor Source와 normalized record에서 1:1 Runtime bake 계획을 만든다.

    ``replacement_request_sink``는 Replacement 생성기의 주입 경계다. 이 함수는
    요청만 발행하고 Replacement vector나 최종 Core25를 직접 생성하지 않는다.

    ``allocation_seed``는 구 API 호환용으로만 받는다. Source TeamSeason 배치와
    stable ID는 seed와 무관하며 normalized Source mapping으로만 결정된다.
    """

    del allocation_seed
    team_seasons_by_year = _build_team_season_index(normalized_references)
    identities = _build_identity_index(normalized_references, team_seasons_by_year)
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
    runtime_persons = _build_runtime_persons(
        identities.values(),
        editor_persons,
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
            team_seasons_by_year[origin_year],
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
            "franchiseIdentityPolicyVersion": FRANCHISE_ID_POLICY_VERSION,
            "teamSeasonIdentityPolicyVersion": TEAM_SEASON_ID_POLICY_VERSION,
            "allocationPolicyVersion": ALLOCATION_POLICY_VERSION,
            "replacementRequestVersion": REPLACEMENT_REQUEST_VERSION,
            "allocationSeedAffectsCanonicalMapping": False,
            "canonicalFranchiseCount": len(
                {
                    team.runtime_franchise_id
                    for teams in team_seasons_by_year.values()
                    for team in teams
                }
            ),
            "canonicalTeamSeasonCount": sum(
                len(teams) for teams in team_seasons_by_year.values()
            ),
            "sourceBackedPlayerPersonCount": len(runtime_persons),
            "sourceBackedPlayerSeasonCount": total_source_seasons,
            "replacementRequestCount": len(all_requests),
            "isFinalCore25Archive": False,
        },
    }
    _assert_no_actual_identity_leak(runtime_content, identities.values())
    return SourceBackedRuntimeBakePlan(runtime_content, tuple(all_requests))


def validate_source_backed_runtime_plan(plan: SourceBackedRuntimeBakePlan) -> None:
    """Player/TeamSeason 1:1 보존과 팀 내 quota 부족 요청을 검증한다."""

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
        if not teams:
            raise ValueError(f"{year} Canonical TeamSeason이 없습니다.")
        team_keys = [team["teamSeasonKey"] for team in teams]
        canonical_source_team_ids = [
            team["canonicalSourceTeamSeasonId"] for team in teams
        ]
        if len(team_keys) != len(set(team_keys)):
            raise ValueError(f"{year} Runtime TeamSeasonKey가 중복되었습니다.")
        if len(canonical_source_team_ids) != len(set(canonical_source_team_ids)):
            raise ValueError(f"{year} Source TeamSeason 1:1 ID가 중복되었습니다.")
        allocated_ids: list[str] = []
        for team in teams:
            team_season_ids = team["sourceBackedPlayerSeasonIds"]
            allocated_ids.extend(team_season_ids)
            requested_hitter_count = 0
            requested_pitcher_count = 0
            for request_id in team["replacementRequestIds"]:
                request = requests_by_id[request_id]
                if (
                    request.team_season_key != team["teamSeasonKey"]
                    or request.franchise_id != team["franchiseId"]
                ):
                    raise ValueError(f"{year} Replacement 요청이 다른 TeamSeason을 참조합니다.")
                if request.player_type == "Hitter":
                    requested_hitter_count += request.count
                elif request.player_type == "Pitcher":
                    requested_pitcher_count += request.count
                else:
                    raise ValueError(f"알 수 없는 PlayerType: {request.player_type}")

            expected_hitters = max(
                0,
                CORE_HITTER_COUNT - len(team["sourceBackedHitterSeasonIds"]),
            )
            expected_pitchers = max(
                0,
                CORE_PITCHER_COUNT - len(team["sourceBackedPitcherSeasonIds"]),
            )
            if requested_hitter_count != expected_hitters:
                raise ValueError(f"{year} Hitter Replacement 부족 수가 팀 내 인원과 다릅니다.")
            if requested_pitcher_count != expected_pitchers:
                raise ValueError(f"{year} Pitcher Replacement 부족 수가 팀 내 인원과 다릅니다.")

        if sorted(allocated_ids) != sorted(season_ids):
            raise ValueError(f"{year} Source PlayerSeason 배치가 1:1이 아닙니다.")


def _build_team_season_index(
    normalized_references: Sequence[Mapping[str, Any]],
) -> dict[int, tuple[_SourceTeamSeasonIdentity, ...]]:
    by_year: dict[int, tuple[_SourceTeamSeasonIdentity, ...]] = {}
    for year_document in normalized_references:
        origin_year = int(year_document["year"])
        if origin_year in by_year:
            raise ValueError(f"normalized year가 중복되었습니다: {origin_year}")

        teams: list[_SourceTeamSeasonIdentity] = []
        seen_team_ids: set[str] = set()
        seen_team_names: set[str] = set()
        seen_franchises: set[str] = set()
        for source_team in year_document.get("teams", []):
            source_team_id = str(source_team.get("sourceTeamId") or "").strip()
            source_team_name = str(source_team.get("sourceTeamName") or "").strip()
            if not source_team_id or not source_team_name:
                raise ValueError(f"{origin_year} Source TeamSeason identity가 불완전합니다.")
            if source_team_id in seen_team_ids or source_team_name in seen_team_names:
                raise ValueError(f"{origin_year} Source TeamSeason이 중복되었습니다: {source_team_id}")

            source_franchise_id = str(
                source_team.get("sourceFranchiseId") or ""
            ).strip()
            if source_franchise_id:
                source_franchise_key = source_franchise_id
                source_franchise_basis = "SourceFranchiseId"
            else:
                # 역사적 승계 정보가 없는 자료에서 다른 팀을 임의로 합치지 않는다.
                source_franchise_key = f"source-team-id:{source_team_id}"
                source_franchise_basis = "SourceTeamIdFallback"
            if source_franchise_key in seen_franchises:
                raise ValueError(
                    f"{origin_year} 하나의 Source Franchise가 두 TeamSeason에 중복됩니다: "
                    f"{source_franchise_key}"
                )

            teams.append(
                _SourceTeamSeasonIdentity(
                    source_team_id=source_team_id,
                    source_team_name=source_team_name,
                    source_franchise_key=source_franchise_key,
                    source_franchise_basis=source_franchise_basis,
                    origin_year=origin_year,
                    canonical_source_team_season_id=canonical_source_team_season_id(
                        source_franchise_key,
                        source_team_id,
                        origin_year,
                    ),
                    runtime_franchise_id=runtime_franchise_id(source_franchise_key),
                    runtime_team_season_key=runtime_team_season_key(
                        source_franchise_key,
                        origin_year,
                    ),
                )
            )
            seen_team_ids.add(source_team_id)
            seen_team_names.add(source_team_name)
            seen_franchises.add(source_franchise_key)

        if not teams:
            raise ValueError(f"{origin_year} normalized Source TeamSeason이 없습니다.")
        by_year[origin_year] = tuple(
            sorted(teams, key=lambda team: team.runtime_team_season_key)
        )
    return by_year


def _build_identity_index(
    normalized_references: Sequence[Mapping[str, Any]],
    team_seasons_by_year: Mapping[int, Sequence[_SourceTeamSeasonIdentity]],
) -> dict[str, _SourceSeasonIdentity]:
    identities: dict[str, _SourceSeasonIdentity] = {}
    for year_document in normalized_references:
        origin_year = int(year_document["year"])
        teams_by_id = {
            team.source_team_id: team for team in team_seasons_by_year[origin_year]
        }
        teams_by_name = {
            team.source_team_name: team for team in team_seasons_by_year[origin_year]
        }
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
            source_team, assignment_basis = _resolve_source_player_team(
                player,
                teams_by_id,
                teams_by_name,
                origin_year,
            )
            identities[editor_season] = _SourceSeasonIdentity(
                source_player_id=source_player_id,
                source_player_name=source_player_name,
                origin_year=origin_year,
                editor_person_id=editor_source_person_id(source_player_id),
                editor_season_id=editor_season,
                runtime_person_id=runtime_player_person_id(source_player_id),
                runtime_season_id=runtime_player_season_id(source_player_id, origin_year),
                source_team_id=source_team.source_team_id,
                source_team_name=source_team.source_team_name,
                canonical_source_team_season_id=(
                    source_team.canonical_source_team_season_id
                ),
                runtime_franchise_id=source_team.runtime_franchise_id,
                runtime_team_season_key=source_team.runtime_team_season_key,
                team_assignment_basis=assignment_basis,
            )
    if not identities:
        raise ValueError("normalized Source PlayerSeason이 없습니다.")
    return identities


def _resolve_source_player_team(
    player: Mapping[str, Any],
    teams_by_id: Mapping[str, _SourceTeamSeasonIdentity],
    teams_by_name: Mapping[str, _SourceTeamSeasonIdentity],
    origin_year: int,
) -> tuple[_SourceTeamSeasonIdentity, str]:
    source_player_id = str(player.get("sourcePlayerId") or "").strip()
    aggregate_team_id = str(player.get("aggregateTeamId") or "").strip()
    aggregate_team_name = str(player.get("aggregateTeamName") or "").strip()
    if aggregate_team_id:
        team = teams_by_id.get(aggregate_team_id)
        if team is None:
            raise ValueError(
                f"{origin_year}/{source_player_id} aggregateTeamId가 Source TeamSeason에 없습니다: "
                f"{aggregate_team_id}"
            )
        if aggregate_team_name and aggregate_team_name != team.source_team_name:
            raise ValueError(
                f"{origin_year}/{source_player_id} aggregate team identity가 불일치합니다."
            )
        return team, "AggregateTeamId"

    if aggregate_team_name:
        team = teams_by_name.get(aggregate_team_name)
        if team is not None:
            return team, "AggregateTeamNameFallback"

    for collection_name, basis in (
        ("teamStints", "SingleTeamStintFallback"),
        ("teamFilterRecords", "SingleTeamFilterFallback"),
    ):
        team_ids = {
            str(row.get("sourceTeamId") or "").strip()
            for row in player.get(collection_name, []) or []
            if str(row.get("sourceTeamId") or "").strip()
        }
        if len(team_ids) == 1:
            team_id = next(iter(team_ids))
            team = teams_by_id.get(team_id)
            if team is None:
                raise ValueError(
                    f"{origin_year}/{source_player_id} {collection_name} team이 "
                    f"Source TeamSeason에 없습니다: {team_id}"
                )
            return team, basis
        if len(team_ids) > 1:
            raise ValueError(
                f"{origin_year}/{source_player_id} Source TeamSeason을 하나로 "
                f"결정할 수 없습니다: {sorted(team_ids)}"
            )

    raise ValueError(
        f"{origin_year}/{source_player_id} Source TeamSeason identity가 없습니다."
    )


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


def _build_runtime_persons(
    identities: Iterable[_SourceSeasonIdentity],
    editor_persons: Mapping[str, Mapping[str, Any]],
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
        runtime_person.pop("fictionalName", None)
        runtime_persons.append(runtime_person)
    return runtime_persons


def _build_runtime_year(
    origin_year: int,
    editor_year: Mapping[str, Any],
    editor_seasons: Sequence[Mapping[str, Any]],
    identities: Mapping[str, _SourceSeasonIdentity],
    source_team_seasons: Sequence[_SourceTeamSeasonIdentity],
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
        team.runtime_team_season_key: [] for team in source_team_seasons
    }
    for row in rows:
        team_rows[row[1].runtime_team_season_key].append(row)

    unsupported = [
        row[1].runtime_season_id
        for row in rows
        if row[0].get("playerType") not in {"Hitter", "Pitcher"}
    ]
    if unsupported:
        raise ValueError(f"알 수 없는 playerType이 있습니다: {unsupported[:3]}")

    team_plans: list[dict[str, Any]] = []
    requests: list[ReplacementRequest] = []
    for source_team in source_team_seasons:
        franchise_id = source_team.runtime_franchise_id
        team_season_key = source_team.runtime_team_season_key
        allocated = sorted(
            team_rows[team_season_key],
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
                    origin_year,
                    source_team.canonical_source_team_season_id,
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
                "canonicalSourceTeamSeasonId": (
                    source_team.canonical_source_team_season_id
                ),
                "originYear": origin_year,
                "sourceBackedPlayerSeasonIds": all_ids,
                "sourceBackedNormalCardIds": [
                    _normal_card_id(season_id) for season_id in all_ids
                ],
                "sourceBackedHitterSeasonIds": hitter_ids,
                "sourceBackedPitcherSeasonIds": pitcher_ids,
                "replacementRequestIds": sorted(request_ids),
                "coreSelectionStatus": "PendingReplacementAndRosterSelection",
                "sourceFranchiseIdentityBasis": source_team.source_franchise_basis,
            }
        )

    runtime_seasons: list[dict[str, Any]] = []
    runtime_cards: list[dict[str, Any]] = []
    for runtime_season, identity in rows:
        runtime_season["originFranchiseId"] = identity.runtime_franchise_id
        runtime_season["originTeamSeasonKey"] = identity.runtime_team_season_key
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
        runtime_record["teamSeasonKey"] = identity.runtime_team_season_key
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
    required_replacement_hitters = sum(
        max(0, CORE_HITTER_COUNT - len(team["sourceBackedHitterSeasonIds"]))
        for team in team_plans
    )
    required_replacement_pitchers = sum(
        max(0, CORE_PITCHER_COUNT - len(team["sourceBackedPitcherSeasonIds"]))
        for team in team_plans
    )
    team_count = len(source_team_seasons)
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
                "requiredReplacementHitterCount": required_replacement_hitters,
                "requiredReplacementPitcherCount": required_replacement_pitchers,
                "sourceBackedSeasonCount": len(runtime_seasons),
                "sourceTeamSeasonCount": team_count,
                "canonicalTeamSeasonCount": len(team_plans),
                "leagueTeamTargetCount": LEAGUE_TEAM_TARGET_COUNT,
                "teamCountDisposition": _team_count_disposition(team_count),
                "sourceFranchiseIdFallbackCount": sum(
                    team.source_franchise_basis == "SourceTeamIdFallback"
                    for team in source_team_seasons
                ),
                "playerTeamAssignmentBasisCounts": dict(
                    sorted(Counter(identity.team_assignment_basis for _, identity in rows).items())
                ),
            },
        },
        requests,
    )


def _team_count_disposition(team_count: int) -> str:
    if team_count < LEAGUE_TEAM_TARGET_COUNT:
        return "UnderTargetPreservedWithoutSyntheticTeams"
    if team_count > LEAGUE_TEAM_TARGET_COUNT:
        return "OverTargetPreservedWithoutDroppingTeams"
    return "ExactTargetPreserved"


def build_world_identity_name_pool(
    *,
    domestic_player_count: int,
    foreign_player_count: int,
    franchise_count: int,
    forbidden_player_names: Iterable[str],
    forbidden_franchise_names: Iterable[str],
) -> dict[str, Any]:
    """World 생성 시 shuffle할 mapping-비종속 이름 후보 풀을 만든다."""

    forbidden_players = {
        str(name).strip() for name in forbidden_player_names if str(name).strip()
    }
    forbidden_franchises = {
        str(name).strip() for name in forbidden_franchise_names if str(name).strip()
    }
    domestic_candidates = (
        surname + first + second
        for surname in _SURNAMES
        for first in _GIVEN_FIRST
        for second in _GIVEN_SECOND
        if len({surname, first, second}) == 3
    )
    foreign_candidates = (
        f"{given} {family}"
        for given in _FOREIGN_GIVEN_NAMES
        for family in _FOREIGN_FAMILY_NAMES
        if given != family
    )
    franchise_candidates = (
        f"{region} {nickname}"
        for region in _FRANCHISE_REGIONS
        for nickname in _FRANCHISE_NICKNAMES
    )
    return {
        "version": WORLD_IDENTITY_NAME_POOL_VERSION,
        "domesticPlayerNames": _take_unique_candidates(
            domestic_candidates,
            domestic_player_count,
            forbidden_players,
            "Domestic Player",
            _BANNED_PLAYER_NAME_TOKENS,
        ),
        "foreignPlayerNames": _take_unique_candidates(
            foreign_candidates,
            foreign_player_count,
            forbidden_players,
            "Foreign Player",
            _BANNED_PLAYER_NAME_TOKENS,
        ),
        "franchiseNames": _take_unique_candidates(
            franchise_candidates,
            franchise_count,
            forbidden_franchises,
            "Franchise",
            (),
        ),
    }


def _take_unique_candidates(
    candidates: Iterable[str],
    required_count: int,
    forbidden: set[str],
    label: str,
    forbidden_tokens: Sequence[str],
) -> list[str]:
    if required_count < 0:
        raise ValueError(f"{label} 후보 요청 수가 음수입니다.")
    if required_count == 0:
        return []
    result: list[str] = []
    seen: set[str] = set()
    for candidate in candidates:
        if candidate in forbidden or candidate in seen:
            continue
        if (
            not candidate
            or len(candidate) > 30
            or any(character.isdigit() or ord(character) < 32 for character in candidate)
            or any(token in candidate for token in forbidden_tokens)
        ):
            continue
        result.append(candidate)
        seen.add(candidate)
        if len(result) == required_count:
            return result
    if len(result) != required_count:
        raise ValueError(
            f"{label} identity 후보가 부족합니다: "
            f"required={required_count}, available={len(result)}"
        )
    return result


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
    identities = tuple(identities)
    actual_names = {
        name
        for identity in identities
        for name in (identity.source_player_name, identity.source_team_name)
    }
    actual_ids = {identity.source_player_id for identity in identities}

    def inspect(value: Any, path: str) -> None:
        if isinstance(value, Mapping):
            for key, child in value.items():
                normalized_key = str(key).casefold()
                if normalized_key.startswith("sourceplayer") or normalized_key in {
                    "sourceteamid",
                    "sourceteamname",
                    "sourcefranchiseid",
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
