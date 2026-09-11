"""전체 시즌의 결측 포지션을 공개 시즌 선수단 목록과 대조한다."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import time
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

from bs4 import BeautifulSoup
import source_backed_runtime_bake as identity

ROOT = Path(__file__).resolve().parents[2]
POSITION_NAMES = {"포수": "C", "1루수": "1B", "2루수": "2B", "3루수": "3B",
                  "유격수": "SS", "좌익수": "LF", "중견수": "CF", "우익수": "RF", "지명타자": "DH"}
TEAM_TITLES = {"삼성": "삼성 라이온즈", "롯데": "롯데 자이언츠", "해태": "해태 타이거즈",
               "KIA": "KIA 타이거즈", "MBC": "MBC 청룡", "LG": "LG 트윈스", "OB": "OB 베어스",
               "두산": "두산 베어스", "삼미": "삼미 슈퍼스타즈", "청보": "청보 핀토스",
               "태평양": "태평양 돌핀스", "현대": "현대 유니콘스", "빙그레": "빙그레 이글스",
               "한화": "한화 이글스", "쌍방울": "쌍방울 레이더스", "SK": "SK 와이번스",
               "SSG": "SSG 랜더스", "우리": "우리 히어로즈", "히어로즈": "히어로즈",
               "넥센": "넥센 히어로즈", "키움": "키움 히어로즈", "NC": "NC 다이노스", "KT": "KT 위즈"}


def parse_roster(html: str, year: int) -> dict[str, list[str]]:
    """선수단 절의 포지션 목록만 읽어 수상·퓨처스·다른 시즌 정보를 배제한다."""
    soup = BeautifulSoup(html, "html.parser")
    result = {}
    for heading in soup.find_all(["h2", "h3"]):
        heading_title = heading.get_text("", strip=True).replace("[편집]", "").strip()
        if heading_title not in ("선수단", "원년 선수 구성", "야수진"):
            continue
        # 여러 시즌을 합친 문서로 리다이렉트되면 해당 연도 절만 선택한다.
        parent_heading = heading.find_previous("h2" if heading.name == "h3" else "h1")
        if parent_heading is None:
            continue
        parent_title = parent_heading.get_text("", strip=True).replace("[편집]", "").strip()
        if heading.name == "h3":
            page_title = heading.find_previous("h1")
            is_single_season_hitters = (heading_title == "야수진" and parent_title == "정규 시즌"
                and page_title is not None and page_title.get_text().startswith(f"{year}년 "))
            if parent_title != f"{year}년" and not is_single_season_hitters:
                continue
        elif not parent_title.startswith(f"{year}년 "):
            continue
        for element in heading.find_all_next():
            if element.name in ("h2", "h3"):
                break
            if element.name != "li":
                continue
            text = re.sub(r"\[\d+\]", "", element.get_text("", strip=True))
            label, separator, names = text.partition(":")
            if not separator or label.strip() not in POSITION_NAMES:
                continue
            position = POSITION_NAMES[label.strip()]
            for name in names.split(","):
                # 괄호로 별도 설명이 있는 항목은 자동으로 단정하지 않는다.
                name = name.strip()
                if name and not any(char in name for char in "()（）"):
                    values = result.setdefault(name, [])
                    if position not in values:
                        values.append(position)
    # 포지션별 소제목과 통계 표 형식도 이름 열만 읽는다. 통계 수치는 수집하지 않는다.
    for heading in soup.find_all("h3"):
        position = POSITION_NAMES.get(heading.get_text("", strip=True))
        parent = heading.find_previous("h2")
        title = heading.find_previous("h1")
        if (position is None or parent is None or parent.get_text() != "선수단"
                or title is None or not title.get_text().startswith(f"{year}년 ")):
            continue
        for element in heading.find_all_next(["h2", "h3", "table"]):
            if element.name != "table":
                break
            headers = [cell.get_text(strip=True) for cell in element.find_all("th")]
            if not headers or headers[0] != "이름":
                continue
            for row in element.find_all("tr"):
                cells = row.find_all("td", recursive=False)
                if cells:
                    name = re.sub(r"\[\d+\]", "", cells[0].get_text("", strip=True))
                    values = result.setdefault(name, [])
                    if position not in values:
                        values.append(position)
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--cache", type=Path, default=ROOT / "Tools/KBOImporter/.cache/SeasonPositions")
    parser.add_argument("--years", default="1982-2025")
    parser.add_argument("--runtime-root", type=Path,
                        default=ROOT / "Assets/10.Datas/HistoricalSimulation/1982-2025")
    args = parser.parse_args()
    start, end = map(int, args.years.split("-"))
    args.cache.mkdir(parents=True, exist_ok=True)
    existing = json.loads((Path(__file__).with_name("season_position_evidence.json")).read_text(encoding="utf-8"))
    existing_keys = {(row["seasonYear"], row["sourcePlayerId"]) for row in existing["players"]}
    # 재베이크 후 결측이 줄어도 이미 조사한 정본을 다음 실행에서 지우지 않는다.
    previous = json.loads(args.output.read_text(encoding="utf-8")) if args.output.exists() else {}
    researched_keys = {(row["seasonYear"], str(row["sourcePlayerId"]))
                       for group in ("players", "deferred") for row in previous.get(group, [])}
    report = {"version": "season-roster-research-v1", "retrievedAt": datetime.now(timezone.utc).date().isoformat(),
              "sources": [], "players": [], "deferred": [], "conflicts": []}
    for year in range(start, end + 1):
        source = json.loads((ROOT / f"Tools/KBOImporter/.cache/KBOImport/Normalized/{year}.json").read_text(encoding="utf-8"))
        baked = json.loads((args.runtime_root / f"Years/{year}.json").read_text(encoding="utf-8"))
        missing_ids = {row["playerSeasonId"] for row in baked["playerSeasons"] if row.get("isPositionEvidenceMissing")}
        missing = [row for row in source["players"]
                   if identity.runtime_player_season_id(str(row["sourcePlayerId"]), year) in missing_ids
                   or (year, str(row["sourcePlayerId"])) in researched_keys]
        for team in sorted({row["aggregateTeamName"] for row in missing}):
            candidates = [row for row in missing if row["aggregateTeamName"] == team]
            title = f"{year}년 {TEAM_TITLES.get(team, team)} 시즌"
            url = "https://ko.wikipedia.org/wiki/" + urllib.parse.quote(title.replace(" ", "_"))
            cache = args.cache / f"{year}-{team}.html"
            try:
                if not cache.exists():
                    request = urllib.request.Request(url, headers={"User-Agent": "UPlayBallPositionResearch/1.0"})
                    with urllib.request.urlopen(request, timeout=25) as response:
                        cache.write_bytes(response.read())
                    time.sleep(0.5)
                html = cache.read_text(encoding="utf-8")
                roster = parse_roster(html, year)
                if not roster:
                    raise ValueError("선수단 포지션 목록 없음")
            except Exception as error:
                report["deferred"].extend({"seasonYear": year, "sourcePlayerId": row["sourcePlayerId"],
                    "displayName": row["playerName"], "sourceTeamName": team, "reason": str(error), "url": url} for row in candidates)
                continue
            source_id = f"season-roster-{year}-{team}"
            retrieved_at = datetime.fromtimestamp(cache.stat().st_mtime, timezone.utc).date().isoformat()
            report["sources"].append({"id": source_id, "url": url, "retrievedAt": retrieved_at,
                "scope": "해당 시즌 선수단 목록의 포지션. 경기·이닝 미상. B급 보조 근거",
                "contentSha256": hashlib.sha256(cache.read_bytes()).hexdigest()})
            for row in candidates:
                positions = roster.get(row["playerName"], [])
                matches = [p for p in source["players"] if p["playerName"] == row["playerName"] and p["aggregateTeamName"] == team]
                item = {"seasonYear": year, "sourcePlayerId": str(row["sourcePlayerId"]),
                        "displayName": row["playerName"], "sourceTeamName": team}
                if len(matches) != 1 or len(positions) != 1 or (year, str(row["sourcePlayerId"])) in existing_keys:
                    report["deferred"].append({**item, "reason": "이름·시즌·소속의 유일 연결 또는 단일 포지션 근거 없음", "positions": positions, "url": url})
                    continue
                report["players"].append({**item, "positions": positions, "primaryPosition": positions[0], "sourceIds": [source_id]})
            # 기존 조사 결과는 덮어쓰지 않고 자료 간 충돌만 기록한다.
            for row in existing["players"]:
                if row["seasonYear"] == year and row["sourceTeamName"] == team:
                    positions = roster.get(row["displayName"], [])
                    if positions and row["primaryPosition"] not in positions:
                        report["conflicts"].append({"seasonYear": year, "displayName": row["displayName"],
                            "sourceTeamName": team, "retained": row["primaryPosition"], "candidate": positions, "url": url})
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"{year}: recovered={len(report['players'])}, deferred={len(report['deferred'])}", flush=True)


if __name__ == "__main__":
    main()
