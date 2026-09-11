"""승률을 경기 입력에 넣지 않고 로스터·선수 원기록의 손실을 진단한다."""
import argparse
import json
from pathlib import Path

from analyze_team_metrics import map_teams, read, season_id


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("runtime", type=Path)
    parser.add_argument("normalized", type=Path)
    parser.add_argument("years")
    parser.add_argument("--team")
    parser.add_argument("--reference-output", type=Path)
    parser.add_argument("--leaders-only", action="store_true")
    args = parser.parse_args()
    references = []
    for year in map(int, args.years.split(",")):
        source = read(args.normalized / f"{year}.json")
        players = {season_id(p["sourcePlayerId"], year): p for p in source["players"]}
        for key, (original, _, team, seasons) in map_teams(args.runtime, args.normalized, year).items():
            if args.team and original["sourceTeamName"] != args.team:
                continue
            references.append(dict(year=year, team=original["sourceTeamName"], teamSeasonKey=key,
                actualWins=original["rankStats"]["wins"], actualLosses=original["rankStats"]["losses"],
                actualSourceRank=original["rankStats"].get("rank"),
                actualSourceWinPct=original["rankStats"].get("sourceWinPct")))
            if args.reference_output:
                continue
            rows = []
            for slot, card_id in enumerate(team["core25CardIds"]):
                sid = card_id.rsplit(":", 1)[0]
                card = seasons[sid]
                player = players.get(sid, {})
                rows.append(dict(slot=slot, name=player.get("playerName", sid),
                    position=card["position"], role=card.get("pitcherRole"),
                    ratings=card["baseAttributes"], provenance=card.get("dataProvenance"),
                    hitting=player.get("hitterStats"), pitching=player.get("pitcherStats")))
            print(json.dumps(dict(year=year, team=original["sourceTeamName"], roster=rows), ensure_ascii=False))
    if args.reference_output:
        # 원본 rank에는 한국시리즈 결과나 양대리그 순위가 섞인다. 정규시즌 선두 판정에 사용하지 않는다.
        highest_official = {}
        for team in references:
            rate = team["actualSourceWinPct"]
            if rate is not None:
                highest_official[team["year"]] = max(highest_official.get(team["year"], 0), rate)
        for team in references:
            team["actualRegularLeader"] = (team["actualSourceWinPct"] is not None and
                team["actualSourceWinPct"] == highest_official[team["year"]])
        if args.leaders_only:
            highest = {}
            for team in references:
                rate = team["actualWins"] / (team["actualWins"] + team["actualLosses"])
                highest[team["year"]] = max(highest.get(team["year"], 0), rate)
            # 무승부를 패배처럼 계산했던 시대의 공식 선두도 승패 기준 선두와 함께 검증한다.
            references = [team for team in references if team["actualRegularLeader"] or
                team["actualWins"] / (team["actualWins"] + team["actualLosses"]) == highest[team["year"]]]
        args.reference_output.parent.mkdir(parents=True, exist_ok=True)
        args.reference_output.write_text(json.dumps(dict(teams=references), ensure_ascii=False, indent=2), encoding="utf-8")
        print(f"원기록 연결 완료: {len(references)}개 팀시즌")


if __name__ == "__main__":
    main()
