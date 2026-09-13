"""조사된 실제 투타·생년월일을 Runtime PlayerPersonId 기준으로 조회한다.

`research_player_handedness.py`가 KBO 공식 프로필에서 모은 결과를 읽어, 베이크가
sourcePlayerId 대신 Runtime PlayerPersonId로 찾을 수 있게 색인한다. 조사에 없는 인물
(ReplacementGenerated 등 실존하지 않는 선수)은 추첨으로 남기되, 추첨 분포는 임의값이
아니라 **조사된 실제 선수 전체의 경험 분포**를 쓴다. 좌우 투타는 `BattedBallResolver`와
`ManagerMatchupAi`의 좌우 매치업 판단에 들어가므로, 가상 선수만 양타 1/3처럼 비현실적인
분포를 가지면 리그 전체의 기용 판단이 왜곡된다.
"""

from __future__ import annotations

import json
from datetime import date
from functools import lru_cache
from pathlib import Path
from typing import Any, Mapping, Sequence

import source_backed_runtime_bake as source_plan

RESEARCH_FILENAME = "player_handedness_research.json"
EXPECTED_RESEARCH_VERSION = "player-handedness-research-v1"

THROWS_VALUES = ("Right", "Left")
BATS_VALUES = ("Right", "Left", "Switch")


class PersonIdentityResearch:
    """Runtime PlayerPersonId → 실제 투타·생년월일 색인."""

    def __init__(self, records_by_person_id: Mapping[str, Mapping[str, Any]], research_version: str) -> None:
        self._records = dict(records_by_person_id)
        self.research_version = research_version
        self._throws_weights = self._empirical_weights("throws", THROWS_VALUES)
        self._bats_weights = self._empirical_weights("bats", BATS_VALUES)

    def _empirical_weights(self, field: str, values: Sequence[str]) -> list[tuple[str, int]]:
        counts = {value: 0 for value in values}
        for record in self._records.values():
            value = record.get(field)
            if value in counts:
                counts[value] += 1
        if not any(counts.values()):
            # 조사 결과가 비면 균등 추첨으로 되돌아간다.
            return [(value, 1) for value in values]
        return [(value, counts[value]) for value in values]

    def __len__(self) -> int:
        return len(self._records)

    def get(self, runtime_person_id: str) -> Mapping[str, Any] | None:
        return self._records.get(runtime_person_id)

    def _weighted_choice(self, rng: Any, weights: Sequence[tuple[str, int]]) -> str:
        total = sum(weight for _, weight in weights)
        threshold = rng.randrange(total)
        cumulative = 0
        for value, weight in weights:
            cumulative += weight
            if threshold < cumulative:
                return value
        return weights[-1][0]

    def resolve_throws(self, runtime_person_id: str, rng: Any) -> tuple[str, bool]:
        """(투구손, 실제 조사 근거 여부)를 돌려준다."""
        record = self._records.get(runtime_person_id)
        if record is not None and record.get("throws") in THROWS_VALUES:
            return str(record["throws"]), True
        return self._weighted_choice(rng, self._throws_weights), False

    def resolve_bats(self, runtime_person_id: str, rng: Any) -> tuple[str, bool]:
        """(타격손, 실제 조사 근거 여부)를 돌려준다."""
        record = self._records.get(runtime_person_id)
        if record is not None and record.get("bats") in BATS_VALUES:
            return str(record["bats"]), True
        return self._weighted_choice(rng, self._bats_weights), False

    def resolve_birth_year(self, runtime_person_id: str, fallback_birth_year: int) -> tuple[int, bool]:
        """(출생연도, 실제 조사 근거 여부)를 돌려준다."""
        record = self._records.get(runtime_person_id)
        birth_date = record.get("birthDate") if record is not None else None
        if birth_date:
            try:
                return date.fromisoformat(str(birth_date)).year, True
            except ValueError:
                pass
        return fallback_birth_year, False


def _default_research_path() -> Path:
    return Path(__file__).resolve().parent / RESEARCH_FILENAME


def load_research(path: Path | None = None) -> PersonIdentityResearch:
    """조사 결과를 읽어 Runtime PlayerPersonId로 색인한다. 파일이 없으면 빈 색인."""
    research_path = path or _default_research_path()
    if not research_path.is_file():
        return PersonIdentityResearch({}, "missing")

    content = json.loads(research_path.read_text(encoding="utf-8"))
    version = str(content.get("version", ""))
    if version != EXPECTED_RESEARCH_VERSION:
        raise ValueError(
            f"투타 조사 파일 버전이 예상과 다릅니다: {version} (예상 {EXPECTED_RESEARCH_VERSION})"
        )

    records: dict[str, dict[str, Any]] = {}
    for row in content.get("players", []):
        source_player_id = str(row["sourcePlayerId"])
        runtime_person_id = source_plan.runtime_player_person_id(source_player_id)
        if runtime_person_id in records:
            raise ValueError(f"Runtime PlayerPersonId가 중복된 조사 항목입니다: {source_player_id}")
        records[runtime_person_id] = {
            "sourcePlayerId": source_player_id,
            "displayName": row.get("displayName"),
            "bats": row.get("bats"),
            "throws": row.get("throws"),
            "birthDate": row.get("birthDate"),
        }
    return PersonIdentityResearch(records, version)


@lru_cache(maxsize=1)
def cached_research() -> PersonIdentityResearch:
    return load_research()
