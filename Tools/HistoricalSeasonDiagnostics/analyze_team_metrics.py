"""역사 시즌의 실제 팀 지표와 시뮬레이션 팀 지표를 연도 내 서열 중심으로 비교한다."""
import argparse
import hashlib
import json
import math
import statistics
from collections import Counter, defaultdict
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def season_id(source_player_id, year):
    value = f"source-backed-identity-v1\0{source_player_id}\0{year}".encode()
    return "SEASON_" + hashlib.sha256(value).hexdigest()[:20]


def correlation(left, right):
    if len(left) < 3 or statistics.pstdev(left) == 0 or statistics.pstdev(right) == 0:
        return None
    return statistics.correlation(left, right)


def centered_correlation(rows, actual_name, simulated_name):
    by_year = defaultdict(list)
    for row in rows:
        if row[actual_name] is None:
            continue
        by_year[row["year"]].append(row)
    actual, simulated = [], []
    for group in by_year.values():
        actual_mean = statistics.mean(r[actual_name] for r in group)
        simulated_mean = statistics.mean(r[simulated_name] for r in group)
        actual.extend(r[actual_name] - actual_mean for r in group)
        simulated.extend(r[simulated_name] - simulated_mean for r in group)
    return correlation(actual, simulated)


def map_teams(runtime_root, normalized_root, year):
    source = read(normalized_root/f"{year}.json")
    archive = read(runtime_root/"Years"/f"{year}.json")
    source_players = {season_id(p["sourcePlayerId"], year): p for p in source["players"]}
    seasons = {p["playerSeasonId"]: p for p in archive["playerSeasons"]}
    team_source_names = defaultdict(Counter)
    for sid, player in source_players.items():
        if sid in seasons:
            team_source_names[seasons[sid]["originTeamSeasonKey"]][player["aggregateTeamName"]] += 1
    source_teams = {t["sourceTeamName"]: t for t in source["teams"]}
    source_players_by_team = defaultdict(list)
    for player in source["players"]:
        source_players_by_team[player["aggregateTeamName"]].append(player)
    result = {}
    for team in archive["teamSeasons"]:
        key = team["teamSeasonKey"]
        if not team_source_names[key]:
            raise ValueError(f"원본 팀 연결 실패: {key}")
        name = team_source_names[key].most_common(1)[0][0]
        if name not in source_teams:
            raise ValueError(f"원본 팀 기록 누락: {year} {name}")
        result[key] = (source_teams[name], source_players_by_team[name], team, seasons)
    return result


def actual_metrics(team, players):
    def summed(section, field):
        return sum((p.get(section) or {}).get(field, 0) or 0 for p in players)
    hitting, pitching, running = team["hitterStats"], team["pitcherStats"], team["runningStats"]
    games = team["rankStats"]["games"]
    at_bats = hitting["atBats"] if hitting else summed("hitterStats", "atBats")
    hits = hitting["hits"] if hitting else summed("hitterStats", "hits")
    home_runs = hitting["homeRuns"] if hitting else summed("hitterStats", "homeRuns")
    doubles = hitting["doubles"] if hitting else summed("hitterStats", "doubles")
    triples = hitting["triples"] if hitting else summed("hitterStats", "triples")
    runs = hitting["runs"] if hitting else summed("hitterStats", "runs")
    walks = hitting["walks"] if hitting else summed("hitterStats", "walks")
    strikeouts = hitting["strikeouts"] if hitting else summed("hitterStats", "strikeouts")
    has_running_evidence = running is not None or any(p.get("runningStats") is not None for p in players)
    stolen_bases = (running["stolenBases"] if running else summed("runningStats", "stolenBases")) \
        if has_running_evidence else None
    earned_runs = pitching["earnedRuns"] if pitching else summed("pitcherStats", "earnedRuns")
    innings_outs = pitching["inningsOuts"] if pitching else summed("pitcherStats", "inningsOuts")
    return dict(actualAverage=hits/at_bats,
        actualHomeRunsPerGame=home_runs/games,
        actualExtraBaseHitsPerGame=(doubles+triples+home_runs)/games,
        actualStolenBasesPerGame=stolen_bases/games if stolen_bases is not None else None,
        actualRunsPerGame=runs/games,
        actualWalksPerGame=walks/games,
        actualStrikeoutsPerGame=strikeouts/games,
        actualEra=27*earned_runs/innings_outs)


def simulated_metrics(items):
    total = lambda name: sum(item[name] for item in items)
    games, at_bats = total("Games"), total("AtBats")
    pitching_outs = total("StarterOuts") + total("ReliefOuts")
    return dict(simulatedAverage=total("Hits")/at_bats,
        simulatedHomeRunsPerGame=total("HomeRuns")/games,
        simulatedExtraBaseHitsPerGame=(total("Doubles")+total("Triples")+total("HomeRuns"))/games,
        simulatedStolenBasesPerGame=total("StolenBases")/games,
        simulatedRunsPerGame=total("Runs")/games,
        simulatedWalksPerGame=total("Walks")/games,
        simulatedHitByPitchesPerGame=total("HitByPitches")/games,
        simulatedStrikeoutsPerGame=total("Strikeouts")/games,
        simulatedSacrificeFliesPerGame=total("SacrificeFlies")/games,
        simulatedDoublePlaysPerGame=total("GroundedIntoDoublePlays")/games,
        simulatedCaughtStealingPerGame=total("CaughtStealing")/games,
        simulatedBaserunningOutsPerGame=total("BaserunningOuts")/games,
        simulatedEra=27*(total("StarterEarnedRuns")+total("ReliefEarnedRuns"))/pitching_outs,
        simulatedStarterEra=27*total("StarterEarnedRuns")/total("StarterOuts"),
        simulatedReliefEra=27*total("ReliefEarnedRuns")/total("ReliefOuts"))


def summarize(simulation_path, runtime_root, normalized_root):
    simulation = read(simulation_path)
    metric_rows = defaultdict(list)
    win_rate_rows = defaultdict(list)
    for run in simulation["rows"]:
        for metric in run["teamMetrics"]:
            metric_rows[(run["year"], metric["teamSeasonKey"])].append(metric)
        for team in run["teams"]:
            decisions = team["Wins"] + team["Losses"]
            win_rate_rows[(run["year"], team["TeamSeasonKey"])].append(team["Wins"] / decisions)
    mappings = {}
    rows = []
    for (year, key), metrics in sorted(metric_rows.items()):
        if year not in mappings:
            mappings[year] = map_teams(runtime_root, normalized_root, year)
        team, source_players, definition, seasons = mappings[year][key]
        row = dict(year=year, team=team["sourceTeamName"], teamSeasonKey=key)
        row["simulatedWinRate"] = statistics.mean(win_rate_rows[(year, key)])
        row.update(actual_metrics(team, source_players))
        row.update(simulated_metrics(metrics))
        hitters = [seasons[c.rsplit(":", 1)[0]]["baseAttributes"] for c in definition["core25CardIds"][:9]]
        pitchers = [seasons[c.rsplit(":", 1)[0]]["baseAttributes"] for c in definition["core25CardIds"][14:]]
        row.update(contact=statistics.mean(a[0] for a in hitters), power=statistics.mean(a[1] for a in hitters),
            speed=statistics.mean(a[2] for a in hitters), pitcherQuality=statistics.mean(statistics.mean(a[7:12]) for a in pitchers))
        rows.append(row)
    for year in {row["year"] for row in rows}:
        year_rows = [row for row in rows if row["year"] == year]
        for row in year_rows:
            row["simulatedRank"] = 1 + sum(
                peer["simulatedWinRate"] > row["simulatedWinRate"] + 1e-12 for peer in year_rows
            )
    pairs = (("average", "actualAverage", "simulatedAverage"),
        ("homeRuns", "actualHomeRunsPerGame", "simulatedHomeRunsPerGame"),
        ("extraBaseHits", "actualExtraBaseHitsPerGame", "simulatedExtraBaseHitsPerGame"),
        ("stolenBases", "actualStolenBasesPerGame", "simulatedStolenBasesPerGame"),
        ("runs", "actualRunsPerGame", "simulatedRunsPerGame"),
        ("walks", "actualWalksPerGame", "simulatedWalksPerGame"),
        ("strikeouts", "actualStrikeoutsPerGame", "simulatedStrikeoutsPerGame"),
        ("era", "actualEra", "simulatedEra"))
    metrics = {}
    for label, actual, simulated in pairs:
        valid = [r for r in rows if r[actual] is not None]
        metrics[label] = dict(correlation=centered_correlation(valid, actual, simulated),
            sampleCount=len(valid), meanActual=statistics.mean(r[actual] for r in valid),
            meanSimulated=statistics.mean(r[simulated] for r in valid),
            meanAbsoluteError=statistics.mean(abs(r[actual]-r[simulated]) for r in valid))
    return dict(experiment=simulation_path.stem, contentHash=simulation["contentHash"], games=simulation["games"],
        repeats=simulation["repeatCount"], center=simulation["center"], slope=simulation["slope"],
        pitcherSlope=simulation["pitcherSlope"], metrics=metrics, teams=rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", type=Path, required=True)
    parser.add_argument("--normalized", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("simulations", nargs="+", type=Path)
    args = parser.parse_args()
    result = [summarize(path, args.runtime, args.normalized) for path in args.simulations]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")
    print(json.dumps([{"experiment": r["experiment"], "metrics": r["metrics"]} for r in result], ensure_ascii=False))


if __name__ == "__main__":
    main()
