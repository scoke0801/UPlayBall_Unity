"""출처가 확인된 시즌 포지션을 원기록 결측의 보조 근거로 연결한다."""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any

EVIDENCE_PATH = Path(__file__).with_name("season_position_evidence.json")
SEASON_ROSTER_PATH = Path(__file__).with_name("season_position_research_all.json")
POSITIONS = {"C", "1B", "2B", "3B", "SS", "LF", "CF", "RF", "DH"}


def load_position_evidence(path: Path = EVIDENCE_PATH) -> dict[tuple[int, str], dict[str, Any]]:
    """출처·시즌·선수 ID가 없는 보강 자료와 중복 선언을 거부한다."""
    document = json.loads(path.read_text(encoding="utf-8"))
    if path == EVIDENCE_PATH:
        supplemental = json.loads(SEASON_ROSTER_PATH.read_text(encoding="utf-8"))
        document["sources"].extend(supplemental["sources"])
        document["players"].extend(supplemental["players"])
        document["version"] += "+" + supplemental["version"]
    sources = {row["id"]: row for row in document["sources"]}
    if len(sources) != len(document["sources"]):
        raise ValueError("시즌 포지션 근거의 출처 ID가 중복됩니다.")
    result = {}
    for row in document["players"]:
        key = (int(row["seasonYear"]), str(row["sourcePlayerId"]))
        if key in result or not key[1] or not row["sourceTeamName"]:
            raise ValueError("시즌 포지션 근거의 식별자가 비어 있거나 중복됩니다.")
        if (not row["positions"] or not set(row["positions"]).issubset(POSITIONS)
                or row["primaryPosition"] not in row["positions"]):
            raise ValueError("시즌 포지션 근거의 위치가 유효하지 않습니다.")
        if not row["sourceIds"] or any(sid not in sources for sid in row["sourceIds"]):
            raise ValueError("시즌 포지션의 웹 출처가 없습니다.")
        evidence_sources = [sources[sid] for sid in row["sourceIds"]]
        if any(not s["url"].startswith("https://") or not s["retrievedAt"] for s in evidence_sources):
            raise ValueError("시즌 포지션 출처의 URL·조회일이 유효하지 않습니다.")
        result[key] = {**row, "sources": evidence_sources, "version": document["version"]}
    return result


def attach_position_evidence(reference: dict[str, Any], evidence: dict) -> None:
    """시즌과 원본 ID가 같은 선수에게만 근거를 붙이고 수비 기록은 수정하지 않는다."""
    year = int(reference["year"])
    for player in reference["players"]:
        row = evidence.get((year, str(player.get("sourcePlayerId") or "")))
        if row is None:
            continue
        if player.get("aggregateTeamName") != row["sourceTeamName"]:
            raise ValueError("포지션 보강 자료와 해당 시즌 원본 구단이 다릅니다.")
        player["_supplementalPositionEvidence"] = row
