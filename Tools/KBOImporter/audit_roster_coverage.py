"""전체 역사 구단의 로스터와 연구 일반 카드의 수록 범위를 대조한다."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path

from source_backed_runtime_bake import (
    editor_source_person_id, editor_source_season_id, runtime_franchise_id, runtime_player_season_id,
)

ROOT = Path(__file__).resolve().parents[2]


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def normalize_team(team):
    return {"히어": "히어로즈", "우리": "히어로즈", "넥센": "히어로즈", "키움": "히어로즈",
        "기아": "KIA", "kt": "KT"}.get(team, team)


def read_research_cards(root):
    """월별·특수 판본을 제외하고 원본 일반 카드의 근거를 보존한다."""
    cards = []
    for table, kind in (("ta", "Hitter"), ("too", "Pitcher")):
        path = root / f"{table}.csv"
        with path.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                if row["카드종류"] != "일반":
                    continue
                cards.append(dict(year=2000 + int(row["년도"]), team=normalize_team(row["팀"]),
                    name=row["이름"], kind=kind, cost=int(row["코스트"]),
                    reference=f"Database:{table}:{row['ID']}", sourceFile=str(path.relative_to(ROOT)),
                    sourceValues=row))
    for folder in ("1985-Samsung", "1989-1999", "2010-2016"):
        path = root / folder / "archive-cards.csv"
        with path.open(encoding="utf-8-sig", newline="") as stream:
            for row in csv.DictReader(stream):
                year = int(row["SeasonYear"])
                if row["CardType"] != "일반" or row["CardTypeCss"] != "playerCard1":
                    continue
                if "SourceYearLabel" in row and row["SourceYearLabel"] != f"{year % 100:02d}'":
                    continue
                cards.append(dict(year=year, team=normalize_team(row["Team"]), name=row["Name"],
                    kind="Pitcher" if row["Position"] in ("선발", "중계", "셋업", "마무리") else "Hitter",
                    cost=int(row["Cost"]), position=row["Position"], reference=f"ArchivedWebCard:{row['CardId']}",
                    sourceFile=str(path.relative_to(ROOT)), sourceUrl=row["SourceUrl"],
                    sourceSha256=row["SourceSha256"], rowSha256=row["RowSha256"], sourceValues=row))
    return sorted(cards, key=lambda r: (r["year"], r["team"], r["name"], r["reference"]))


def audit(normalized_dir, runtime_dir, research_dir):
    """개명·동명이인 후보와 실제 시즌 결손을 구분하며 44시즌을 검사한다."""
    documents = {int(p.stem): read_json(p) for p in sorted(normalized_dir.glob("[0-9][0-9][0-9][0-9].json"))}
    persons = defaultdict(lambda: dict(names=set(), years=set(), teams=set()))
    for year, document in documents.items():
        for player in document["players"]:
            person = persons[str(player["sourcePlayerId"])]
            person["names"].add(player["playerName"])
            person["years"].add(year)
            person["teams"].update(normalize_team(s["sourceTeamName"]) for s in player.get("teamStints", []))
    ids_by_name = defaultdict(set)
    for source_id, person in persons.items():
        for name in person["names"]:
            ids_by_name[name].add(source_id)

    # 기존의 다중 기록 검증으로 확정된 개명 연결만 재사용한다.
    source_by_editor_person = {editor_source_person_id(value): value for value in persons}
    alias_evidence = {}
    with (research_dir / "Calibration/comparison.csv").open(encoding="utf-8-sig", newline="") as stream:
        for row in csv.DictReader(stream):
            if row["joinMethod"] != "TeamAndRecordCounts":
                continue
            source_id = source_by_editor_person.get(row["person"])
            if source_id:
                ids_by_name[row["referenceName"]].add(source_id)
                alias_evidence[row["referenceName"], source_id] = row["origin"]
    runtime_players_by_year = {}
    editor_season_sources = {}
    reference_season_sources = {}
    overrides = read_json(Path(__file__).with_name("annual_reference_overrides.json"))
    for year, document in documents.items():
        for player in document["players"]:
            source_id = str(player["sourcePlayerId"])
            editor_season_sources[editor_source_season_id(source_id, year)] = source_id
    for card in overrides["cards"]:
        source_id = editor_season_sources.get(card["playerSeasonId"])
        if not source_id:
            continue
        for reference in set(card["sources"].values()):
            if reference.startswith("ArchivedWebCard:"):
                reference = "ArchivedWebCard:" + reference.rsplit(":", 1)[1]
            reference_season_sources[reference] = source_id
    team_rows = []
    issues = []
    source_lookup = {}
    source_mapping_issues = []
    runtime_sync_issues = []
    for year, document in documents.items():
        runtime = read_json(runtime_dir / "Years" / f"{year}.json")
        runtime_players = {s["playerSeasonId"]: s for s in runtime["playerSeasons"]}
        runtime_players_by_year[year] = runtime_players
        runtime_cards = {c["cardId"]: c for c in runtime["normalCards"]}
        runtime_teams = {t["franchiseId"]: t for t in runtime["teamSeasons"]}
        for player in document["players"]:
            source_lookup[year, str(player["sourcePlayerId"])] = player
            season_id = runtime_player_season_id(str(player["sourcePlayerId"]), year)
            if season_id not in runtime_players:
                source_mapping_issues.append(dict(year=year, sourcePlayerId=player["sourcePlayerId"], issue="MissingSourceSeasonInRuntime"))
        for team in document["teams"]:
            team_name = normalize_team(team["sourceTeamName"])
            franchise = runtime_franchise_id(team["sourceFranchiseId"])
            baked_team = runtime_teams.get(franchise)
            if not baked_team:
                issues.append(dict(year=year, team=team_name, issue="MissingRuntimeTeam"))
                continue
            players = [s for s in runtime_players.values() if s["originFranchiseId"] == franchise]
            core = baked_team["core25CardIds"]
            all_cards = baked_team["allNormalCardIds"]
            broken = [c for c in set(all_cards + core) if c not in runtime_cards
                or runtime_cards[c]["playerSeasonId"] not in runtime_players
                or runtime_players[runtime_cards[c]["playerSeasonId"]]["originFranchiseId"] != franchise
                or (c in core and c not in all_cards)]
            expected_cards = {f"{s['playerSeasonId']}:Normal" for s in players}
            if len(core) != 25 or len(set(core)) != 25 or broken or len(set(all_cards)) != len(all_cards) or set(all_cards) != expected_cards:
                issues.append(dict(year=year, team=team_name, issue="InvalidRoster", coreCount=len(core), brokenCards=broken))
            team_rows.append(dict(year=year, team=team_name, totalPlayers=len(players), core25Count=len(core),
                normalCardCount=len(all_cards), sourcePlayers=sum(s.get("dataProvenance") == "SourceBacked" for s in players),
                replacementPlayers=sum(s.get("dataProvenance") == "ReplacementGenerated" for s in players)))

    editor_runtime = ROOT / "Assets/Editor Default Resources/HistoricalSimulation/1982-2025/Runtime"
    for runtime_path in sorted(runtime_dir.rglob("*.json")):
        relative = runtime_path.relative_to(runtime_dir)
        editor_path = editor_runtime / relative
        if not editor_path.is_file() or hashlib.sha256(runtime_path.read_bytes()).digest() != hashlib.sha256(editor_path.read_bytes()).digest():
            runtime_sync_issues.append(str(relative))
    reference_cards = read_research_cards(research_dir)
    supplement_path = Path(__file__).with_name("research_roster_supplement.json")
    research_by_reference = {}
    if supplement_path.is_file():
        research_by_reference = {card["reference"]: card for card in read_json(supplement_path)["cards"]}
    results = []
    for card in reference_cards:
        year, team, name = card["year"], card["team"], card["name"]
        candidates = ids_by_name[name]
        exact = [source_id for source_id in candidates if (year, source_id) in source_lookup
            and any(normalize_team(s["sourceTeamName"]) == team for s in source_lookup[year, source_id].get("teamStints", []))]
        typed = [value for value in exact if runtime_players_by_year[year].get(runtime_player_season_id(value, year), {}).get("playerType") == card["kind"]]
        if typed:
            exact = typed
        verified_id = reference_season_sources.get(card["reference"])
        if verified_id in exact:
            exact = [verified_id]
        if len(exact) == 1:
            source_id = exact[0]
            runtime_id = runtime_player_season_id(source_id, year)
            found = runtime_players_by_year[year].get(runtime_id)
            status = "Matched" if found else "MissingFromBake"
            if found and found["playerType"] != card["kind"]:
                status = "DifferentPlayerType"
            expected_team = next((t for t in documents[year]["teams"] if normalize_team(t["sourceTeamName"]) == team), None)
            if found and expected_team and found["originFranchiseId"] != runtime_franchise_id(expected_team["sourceFranchiseId"]):
                status = "DifferentOriginTeam"
        elif len(exact) > 1:
            source_id, status = None, "AmbiguousIdentity"
        else:
            source_id = None
            status = "MissingSourceSeason" if candidates else "UnresolvedIdentity"
            team_candidates = [value for value in candidates if team in persons[value]["teams"]]
            if team_candidates and all((year, value) not in source_lookup for value in team_candidates):
                status = "MissingSourceSeason"
            elif any((year, value) in source_lookup for value in candidates):
                status = "DifferentTeamSeason"
        supplement_card = research_by_reference.get(card["reference"])
        if supplement_card:
            supplement_id = runtime_player_season_id(supplement_card["sourcePersonKey"], year)
            season = runtime_players_by_year[year].get(supplement_id)
            if (season and season.get("sourceDataKind") == "ResearchCardSupplement"
                    and season["originFranchiseId"] == runtime_franchise_id(supplement_card["sourceFranchiseKey"])
                    and season["playerType"] == card["kind"]):
                status, source_id = "MatchedResearch", supplement_card["sourcePersonKey"]
        result = dict(card, status=status, sourcePlayerId=source_id,
            aliasEvidence=alias_evidence.get((name, source_id)),
            identityCandidates=[dict(sourcePlayerId=value, names=sorted(persons[value]["names"]),
                years=sorted(persons[value]["years"]), teams=sorted(persons[value]["teams"])) for value in sorted(candidates)])
        if status in ("Matched", "MatchedResearch"):
            result.pop("sourceValues")
            result.pop("identityCandidates")
        results.append(result)
    by_team = defaultdict(list)
    for card in results:
        by_team[card["year"], card["team"]].append(card)
    for row in team_rows:
        references = by_team[row["year"], row["team"]]
        row["researchNormalCards"] = len(references)
        row["researchStatusCounts"] = dict(sorted(Counter(c["status"] for c in references).items()))
    return dict(summary=dict(teamSeasons=len(team_rows), runtimePlayers=sum(r["totalPlayers"] for r in team_rows),
        under25Teams=sum(r["totalPlayers"] < 25 for r in team_rows), invalidRosterCount=len(issues),
        sourceUnder25Teams=sum(r["sourcePlayers"] < 25 for r in team_rows),
        replacementPlayers=sum(r["replacementPlayers"] for r in team_rows),
        sourceMappingIssueCount=len(source_mapping_issues), runtimeSyncIssueCount=len(runtime_sync_issues),
        teamsWithUnmatchedResearch=sum(any(key not in ("Matched", "MatchedResearch") for key in r["researchStatusCounts"]) for r in team_rows),
        researchNormalCards=len(results), researchStatusCounts=dict(sorted(Counter(r["status"] for r in results).items()))),
        teams=team_rows, rosterIssues=issues, sourceMappingIssues=source_mapping_issues,
        runtimeSyncIssues=runtime_sync_issues, researchCards=results)


def write_report(result, path):
    """정원 검사와 연구 카드 미연결을 다른 표로 보고한다."""
    summary = result["summary"]
    lg = next(t for t in result["teams"] if t["year"] == 1994 and t["team"] == "LG")
    missing_lg = [r for r in result["researchCards"] if r["year"] == 1994 and r["team"] == "LG" and r["status"] not in ("Matched", "MatchedResearch")]
    added_count = summary["researchStatusCounts"].get("MatchedResearch", 0)
    lg_matched = sum(value for key, value in lg["researchStatusCounts"].items() if key in ("Matched", "MatchedResearch"))
    lines = ["# 역사 구단 로스터와 research 대조", "",
        "## 확인 결과", "",
        f"- 1982~2025년 {summary['teamSeasons']}개 구단·{summary['runtimePlayers']:,}개 선수 시즌 검사.",
        f"- Runtime 전체 선수풀이 25명 미만인 구단: {summary['under25Teams']}개. Core25 중복·결손·참조 오류: {summary['invalidRosterCount']}개.",
        f"- KBO Source → Runtime 선수 시즌 누락: {summary['sourceMappingIssueCount']}건. Editor Runtime와 게임 Runtime JSON 불일치: {summary['runtimeSyncIssueCount']}개.",
        f"- KBO Source 선수만으로 25명 미만인 구단: {summary['sourceUnder25Teams']}개. 전체 생성 보충 선수: {summary['replacementPlayers']}명.",
        f"- research 일반 카드 {summary['researchNormalCards']:,}장 중 KBO {summary['researchStatusCounts']['Matched']:,}장·연구 보충 {added_count:,}장 연결. 미연결 항목이 있는 구단·연도는 {summary['teamsWithUnmatchedResearch']}개.",
        f"- 1994 LG: SourceBacked 전체 {lg['sourcePlayers']}명 / Runtime {lg['totalPlayers']}명 / Core25 {lg['core25Count']}명 / research {lg['researchNormalCards']}명. research {lg_matched}명 연결, {len(missing_lg)}명 미연결.",
        "", "`25인 로스터가 부족하다`는 현상은 현재 디스크의 Canonical bake에서 재현되지 않았다. 화면·저장 데이터에서 관찰한 현상은 별도 확인이 필요하다.",
        "", "## 분류 기준과 제한", "",
        "| 분류 | 카드 수 | 의미 |", "|---|---:|---|"]
    descriptions = dict(Matched="해당 연도·소속·선수 타입의 KBO Source와 Runtime ID 연결",
        MatchedResearch="연구 보충 정본과 Runtime ID 연결; KBO 원기록은 미확보",
        MissingSourceSeason="이름에 해당하는 Source 인물 후보는 있지만 그 구단의 해당 시즌 원본이 없음",
        UnresolvedIdentity="현재 Source에 이름 또는 검증된 개명으로 연결되는 인물 후보가 없음",
        DifferentTeamSeason="같은 이름의 해당 시즌 원본은 다른 구단에 존재; 이적·동명이인 검토 필요",
        DifferentPlayerType="같은 시즌이 있지만 타자/투수 카드 타입이 다름",
        DifferentOriginTeam="Source 소속 이력에는 있으나 canonical 대표 구단이 다름",
        AmbiguousIdentity="동일 연도·소속·타입으로도 여러 인물 후보가 남음",
        MissingFromBake="Source 시즌이 있으나 Runtime에서 누락")
    for status, count in summary["researchStatusCounts"].items():
        lines.append(f"| {status} | {count:,} | {descriptions[status]} |")
    lines += ["", "미연결 카드 수를 곧바로 추가 선수 수로 해석하면 안 된다. 개명·동명이인·이적·투타 전환이 섞여 있다.",
        "과거 이름 연결은 기존 Calibration의 다중 기록 일치 근거를 재사용했다. KIA/기아, KT/kt, 히어로즈 계보의 표기는 해당 연도 안에서 정규화했다.",
        "월별·올스타·EX 등 특수 판본은 제외했다. 연구 자료는 부분 복원본이므로 연구 카드가 없는 구단을 누락 없는 구단으로 판정하지 않는다.",
        "이번 검사는 명단·ID·카드 참조 감사이며 능력치 일치율 검사와 구분한다. 보충 결과와 경기 검증은 supplement-result.json을 따른다.",
        "", "## 1994 LG 추가 검토 대상", "",
        "유지현은 기존 다중 기록 매칭을 통해 KBO 류지현(94106)과 연결되므로 추가 대상에서 제외했다.",
        "", "| 선수 | 타입 | research Cost | Source 인물 후보 |", "|---|---|---:|---|"]
    for card in missing_lg:
        candidates = "; ".join(f"{r['sourcePlayerId']} ({'/'.join(r['teams'])})" for r in card["identityCandidates"]) or "없음"
        lines.append(f"| {card['name']} | {card['kind']} | {card['cost']} | {candidates} |")
    lines += ["", "미연결 목록은 아직 추가하지 않은 검토 대상이며 MatchedResearch는 Runtime에 반영한 선수다.",
        "KBO의 실제 기록이 없는 상태를 실제 0경기 기록으로 만들거나, 같은 이름의 다른 인물에게 시즌을 붙이면 안 된다.",
        "audit.json의 미연결 항목에 원본 카드 필드, 카드 ID, 출처 파일, 웹 원본 URL·해시와 다년도 인물 후보를 보존했다.",
        "", "## Source 선수만으로 25명 미만인 구단", "", "| 연도 | 구단 | Source | 생성 보충 | Runtime 전체 |", "|---|---|---:|---:|---:|"]
    for team in result["teams"]:
        if team["sourcePlayers"] < 25:
            lines.append(f"| {team['year']} | {team['team']} | {team['sourcePlayers']} | {team['replacementPlayers']} | {team['totalPlayers']} |")
    lines += ["", "전체 인원이 25명을 넘어도 타자14·투수11 정원 때문에 보충 선수가 있을 수 있다.",
        "", "## 전체 구단 대조", "", "| 연도 | 구단 | Source | 생성 보충 | Runtime | Core25 | 연구 일반 | 연결 | 미연결 |", "|---|---|---:|---:|---:|---:|---:|---:|---:|"]
    for team in result["teams"]:
        matched = team["researchStatusCounts"].get("Matched", 0) + team["researchStatusCounts"].get("MatchedResearch", 0)
        lines.append(f"| {team['year']} | {team['team']} | {team['sourcePlayers']} | {team['replacementPlayers']} | {team['totalPlayers']} | {team['core25Count']} | {team['researchNormalCards']} | {matched} | {team['researchNormalCards'] - matched} |")
    lines += ["", "## 재현", "", "프로젝트 루트에서 실행:", "", "```powershell",
        "uv run --project Tools/KBOImporter Tools/KBOImporter/audit_roster_coverage.py --output docs/reports/historical-roster-coverage/audit.json", "```", ""]
    path.write_text("\n".join(lines), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = audit(ROOT / "Tools/KBOImporter/.cache/KBOImport/Normalized",
        ROOT / "Assets/10.Datas/HistoricalSimulation/1982-2025", ROOT / "Research/PyaMaeCardDb")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    write_report(result, args.output.with_name("README.md"))
    print(json.dumps(result["summary"], ensure_ascii=False))
    print(json.dumps([r for r in result["teams"] if r["year"] == 1994 and r["team"] == "LG"], ensure_ascii=False))


if __name__ == "__main__":
    main()
