"""KBO 공식 선수 프로필에서 투타(Bats/Throws)와 생년월일을 수집한다.

베이크의 `_materialize_persons`는 투타·생년을 PlayerPersonId 시드로 추첨해 왔다. 실제
김광현(sourcePlayerId 77829)이 우투양타로 나오는 등 실존 인물과 어긋나고, 좌우 매치업이
`BattedBallResolver`·`ManagerMatchupAi`의 판단에 실제로 들어가므로 표기 문제에 그치지 않는다.

수집 대상은 Normalized 캐시에 등장하는 모든 sourcePlayerId이며, 원문은 Raw Snapshot으로
보존해 재현 가능하게 둔다. 결과 JSON은 `season_position_research_all.json`과 같은 형식
(version/retrievedAt/sources/players/deferred)을 따른다.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import re
import time
from collections import OrderedDict
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Any

import requests

RESEARCH_VERSION = "player-handedness-research-v1"
PROFILE_URL = "https://www.koreabaseball.com/Record/Player/PitcherDetail/Basic.aspx"
USER_AGENT = "Mozilla/5.0 (compatible; UPlayBall-KBOImporter/1.0)"

# 프로필 헤더의 포지션 항목은 "투수(좌투좌타)" 형태로 투구손·타격손을 함께 담는다.
POSITION_PATTERN = re.compile(
    r'id="cphContents_cphContents_cphContents_playerProfile_lblPosition">([^<]*)<'
)
NAME_PATTERN = re.compile(
    r'id="cphContents_cphContents_cphContents_playerProfile_lblName">([^<]*)<'
)
BIRTHDAY_PATTERN = re.compile(
    r'id="cphContents_cphContents_cphContents_playerProfile_lblBirthday">([^<]*)<'
)
HANDEDNESS_PATTERN = re.compile(r"([좌우양])투([좌우양])타")
BIRTHDAY_VALUE_PATTERN = re.compile(r"(\d{4})년\s*(\d{1,2})월\s*(\d{1,2})일")

THROWS_BY_TOKEN = {"좌": "Left", "우": "Right"}
BATS_BY_TOKEN = {"좌": "Left", "우": "Right", "양": "Switch"}


def _repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _normalized_root() -> Path:
    return Path(__file__).resolve().parent / ".cache" / "KBOImport" / "Normalized"


def _raw_root() -> Path:
    return Path(__file__).resolve().parent / ".cache" / "KBOImport" / "Raw" / "_PlayerProfile"


def collect_targets(normalized_root: Path) -> "OrderedDict[str, dict[str, Any]]":
    """Normalized 캐시에 등장하는 모든 선수를 sourcePlayerId 기준으로 모은다."""
    targets: "OrderedDict[str, dict[str, Any]]" = OrderedDict()
    for path in sorted(normalized_root.glob("*.json")):
        content = json.loads(path.read_text(encoding="utf-8"))
        for player in content.get("players", []):
            source_player_id = str(player["sourcePlayerId"])
            entry = targets.setdefault(
                source_player_id,
                {"sourcePlayerId": source_player_id, "displayName": player["playerName"], "years": []},
            )
            entry["years"].append(int(content["year"]))
    for entry in targets.values():
        entry["years"] = sorted(set(entry["years"]))
    return OrderedDict(sorted(targets.items(), key=lambda item: item[0]))


def parse_profile(html: str) -> dict[str, Any]:
    """프로필 원문에서 이름·투타·생년월일을 뽑는다. 없으면 해당 항목만 None으로 둔다."""
    name_match = NAME_PATTERN.search(html)
    position_match = POSITION_PATTERN.search(html)
    birthday_match = BIRTHDAY_PATTERN.search(html)

    bats: str | None = None
    throws: str | None = None
    position_text = position_match.group(1).strip() if position_match else ""
    handedness_match = HANDEDNESS_PATTERN.search(position_text)
    if handedness_match:
        throws = THROWS_BY_TOKEN.get(handedness_match.group(1))
        bats = BATS_BY_TOKEN.get(handedness_match.group(2))

    birth_date: str | None = None
    if birthday_match:
        birthday_value = BIRTHDAY_VALUE_PATTERN.search(birthday_match.group(1))
        if birthday_value:
            year, month, day = (int(part) for part in birthday_value.groups())
            try:
                birth_date = date(year, month, day).isoformat()
            except ValueError:
                birth_date = None

    return {
        "profileName": name_match.group(1).strip() if name_match else None,
        "positionText": position_text or None,
        "bats": bats,
        "throws": throws,
        "birthDate": birth_date,
    }


def _fetch(session: requests.Session, source_player_id: str, timeout: float) -> str:
    response = session.get(
        PROFILE_URL,
        params={"playerId": source_player_id},
        timeout=timeout,
        headers={"User-Agent": USER_AGENT},
    )
    response.raise_for_status()
    response.encoding = response.encoding or "utf-8"
    return response.text


def _read_or_fetch(
    session: requests.Session,
    source_player_id: str,
    raw_root: Path,
    *,
    delay_min: float,
    delay_max: float,
    timeout: float,
    max_retry: int,
    rng: random.Random,
) -> tuple[str, bool]:
    """Raw Snapshot이 있으면 재사용하고, 없을 때만 네트워크로 수집한다."""
    path = raw_root / f"{source_player_id}.html"
    if path.exists():
        return path.read_text(encoding="utf-8"), False

    last_error: Exception | None = None
    for attempt in range(max_retry):
        try:
            html = _fetch(session, source_player_id, timeout)
            break
        except Exception as error:  # 네트워크 계열 실패는 재시도로 흡수한다.
            last_error = error
            time.sleep(min(2 ** attempt, 8))
    else:
        raise RuntimeError(f"프로필 수집 실패: {source_player_id}") from last_error

    raw_root.mkdir(parents=True, exist_ok=True)
    path.write_text(html, encoding="utf-8")
    path.with_suffix(".html.meta.json").write_text(
        json.dumps(
            {
                "fetchedAtUtc": datetime.now(timezone.utc).isoformat(),
                "method": "GET",
                "requestParameters": {"playerId": source_player_id},
                "sha256": hashlib.sha256(html.encode("utf-8")).hexdigest(),
                "sourceUrl": PROFILE_URL,
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    time.sleep(rng.uniform(delay_min, delay_max))
    return html, True


def build_research(
    targets: "OrderedDict[str, dict[str, Any]]",
    session: requests.Session,
    raw_root: Path,
    *,
    delay_min: float,
    delay_max: float,
    timeout: float,
    max_retry: int,
    retrieved_at: str,
    progress_every: int = 100,
) -> dict[str, Any]:
    rng = random.Random(0)
    players: list[dict[str, Any]] = []
    deferred: list[dict[str, Any]] = []
    fetched_count = 0

    for index, (source_player_id, target) in enumerate(targets.items(), start=1):
        try:
            html, fetched = _read_or_fetch(
                session,
                source_player_id,
                raw_root,
                delay_min=delay_min,
                delay_max=delay_max,
                timeout=timeout,
                max_retry=max_retry,
                rng=rng,
            )
        except RuntimeError as error:
            deferred.append(
                {
                    "sourcePlayerId": source_player_id,
                    "displayName": target["displayName"],
                    "reason": f"프로필 응답 없음: {error}",
                }
            )
            continue
        fetched_count += int(fetched)

        parsed = parse_profile(html)
        record = {
            "sourcePlayerId": source_player_id,
            "displayName": target["displayName"],
            "profileName": parsed["profileName"],
            "bats": parsed["bats"],
            "throws": parsed["throws"],
            "birthDate": parsed["birthDate"],
            "positionText": parsed["positionText"],
            "firstSeasonYear": target["years"][0],
            "lastSeasonYear": target["years"][-1],
            "sourceUrl": f"{PROFILE_URL}?playerId={source_player_id}",
            "contentSha256": hashlib.sha256(html.encode("utf-8")).hexdigest(),
        }

        if parsed["bats"] is None or parsed["throws"] is None:
            deferred.append(
                {
                    "sourcePlayerId": source_player_id,
                    "displayName": target["displayName"],
                    "reason": "프로필에 투타 표기 없음",
                    "positionText": parsed["positionText"],
                    "sourceUrl": record["sourceUrl"],
                }
            )
            continue

        # 이름이 어긋나면 playerId 매칭 자체가 틀린 것이므로 채택하지 않는다.
        if parsed["profileName"] and parsed["profileName"] != target["displayName"]:
            deferred.append(
                {
                    "sourcePlayerId": source_player_id,
                    "displayName": target["displayName"],
                    "reason": f"프로필 이름 불일치: {parsed['profileName']}",
                    "sourceUrl": record["sourceUrl"],
                }
            )
            continue

        players.append(record)
        if index % progress_every == 0:
            print(f"[{index}/{len(targets)}] 수집 {fetched_count}건, 채택 {len(players)}건", flush=True)

    players.sort(key=lambda row: row["sourcePlayerId"])
    deferred.sort(key=lambda row: row["sourcePlayerId"])
    return {
        "version": RESEARCH_VERSION,
        "retrievedAt": retrieved_at,
        "sourceUrlTemplate": f"{PROFILE_URL}?playerId={{sourcePlayerId}}",
        "scope": "KBO 공식 선수 프로필 헤더의 포지션 표기(투타)와 생년월일. A급 1차 근거",
        "players": players,
        "deferred": deferred,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="KBO 선수 투타·생년월일 조사")
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parent / "player_handedness_research.json")
    parser.add_argument("--normalized-root", type=Path, default=_normalized_root())
    parser.add_argument("--raw-root", type=Path, default=_raw_root())
    parser.add_argument("--delay-min", type=float, default=0.35)
    parser.add_argument("--delay-max", type=float, default=0.75)
    parser.add_argument("--timeout", type=float, default=30.0)
    parser.add_argument("--max-retry", type=int, default=3)
    parser.add_argument("--limit", type=int, default=0, help="0이면 전체")
    args = parser.parse_args()

    targets = collect_targets(args.normalized_root)
    if args.limit:
        targets = OrderedDict(list(targets.items())[: args.limit])
    print(f"대상 선수 {len(targets)}명", flush=True)

    with requests.Session() as session:
        research = build_research(
            targets,
            session,
            args.raw_root,
            delay_min=args.delay_min,
            delay_max=args.delay_max,
            timeout=args.timeout,
            max_retry=args.max_retry,
            retrieved_at=date.today().isoformat(),
        )

    args.output.write_text(
        json.dumps(research, ensure_ascii=False, indent=2, sort_keys=False) + "\n",
        encoding="utf-8",
    )
    print(
        f"채택 {len(research['players'])}명 / 보류 {len(research['deferred'])}명 → {args.output}",
        flush=True,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
