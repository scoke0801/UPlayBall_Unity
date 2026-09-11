"""같은 연도·시드의 실제 시즌 결과를 KBO 정규시즌 성적과 비교한다."""
import argparse
import hashlib
import json
import statistics
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'KBOImporter'))
from source_backed_runtime_bake import runtime_team_season_key


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def summarize(rows, key):
    teams = [next(t for t in row['teams'] if t['TeamSeasonKey'] == key) for row in rows]
    wins, losses = sum(t['Wins'] for t in teams), sum(t['Losses'] for t in teams)
    return dict(wins=wins, losses=losses, ties=sum(t['Ties'] for t in teams),
                winRate=wins / (wins + losses),
                averageRank=statistics.mean(next(s['Rank'] for s in row['standings']
                                                if s['TeamSeasonKey'] == key) for row in rows))


def league_stats(data):
    teams = [t for row in data['rows'] for t in row['teams']]
    players = [p for row in data['rows'] for p in row['statistics']
               if not p['IsFirstHalf'] and not p['IsPostseason'] and not p['IsAllStarGame']]
    games = sum(t['Games'] for t in teams) // 2
    walks, strikeouts = sum(p['Walks'] for p in players), sum(p['Strikeouts'] for p in players)
    return dict(regularGames=games, AVG=sum(t['Hits'] for t in teams) / sum(t['AtBats'] for t in teams),
                ERA=sum(t['EarnedRuns'] for t in teams) * 27 / sum(t['PitchingOuts'] for t in teams),
                runsPerGame=sum(t['RunsScored'] for t in teams) / games,
                homeRunsPerGame=sum(p['HomeRuns'] for p in players) / games,
                walksPerGame=walks / games, strikeoutsPerGame=strikeouts / games,
                walksToStrikeouts=walks / strikeouts,
                errorsPerGame=sum(p['FieldingErrors'] for p in players) / games)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--before', type=Path, required=True)
    parser.add_argument('--after', type=Path, required=True)
    parser.add_argument('--normalized', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--maximum-repeats', type=int,
        help='연도별 앞쪽 시드를 같은 수만 남겨 반복 수가 다른 실행을 짝지어 비교한다.')
    args = parser.parse_args()
    before, after = read(args.before), read(args.after)
    if args.maximum_repeats is not None:
        if args.maximum_repeats < 2:
            raise ValueError('짝지은 비교는 최소 2회 반복이 필요합니다.')
        for data in (before, after):
            selected = []
            for year in sorted({row['year'] for row in data['rows']}):
                rows = sorted((row for row in data['rows'] if row['year'] == year), key=lambda row: row['seed'])
                if len(rows) < args.maximum_repeats:
                    raise ValueError('요청한 반복 수보다 적은 연도가 있습니다.')
                selected.extend(rows[:args.maximum_repeats])
            data['rows'] = selected
    keys = lambda data: {(r['year'], r['seed']) for r in data['rows']}
    if keys(before) != keys(after) or len(keys(before)) != len(before['rows']) or len(keys(after)) != len(after['rows']):
        raise ValueError('전후의 연도·시드가 일치하고 중복이 없어야 합니다.')
    years = sorted({r['year'] for r in after['rows']})
    teams = []
    for year in years:
        rows = {phase: sorted((r for r in data['rows'] if r['year'] == year), key=lambda r: r['seed'])
                for phase, data in [('before', before), ('after', after)]}
        source = read(args.normalized / f'{year}.json')
        for team in source['teams']:
            key = runtime_team_season_key(team.get('sourceFranchiseId') or 'source-team-id:' + team['sourceTeamId'], year)
            actual = team['rankStats']
            diffs = []
            for first, last in zip(rows['before'], rows['after']):
                rates = [next(t['WinningPercentage'] for t in r['teams'] if t['TeamSeasonKey'] == key)
                         for r in (first, last)]
                diffs.append(rates[1] - rates[0])
            teams.append(dict(year=year, name=team['sourceTeamName'], key=key,
                actualWinRate=actual['wins'] / (actual['wins'] + actual['losses']),
                before=summarize(rows['before'], key), after=summarize(rows['after'], key),
                pairedMeanDifference=statistics.mean(diffs),
                paired95HalfWidth=1.96 * statistics.stdev(diffs) / len(diffs) ** .5 if len(diffs) > 1 else None))
    for team in teams:
        cohort = [t for t in teams if t['year'] == team['year']]
        team['actualRank'] = 1 + sum(t['actualWinRate'] > team['actualWinRate'] for t in cohort)
        for phase in ('before', 'after'):
            team[phase]['rank'] = 1 + sum(t[phase]['winRate'] > team[phase]['winRate'] for t in cohort)
    agreement = {}
    for phase in ('before', 'after'):
        actual, predicted = [], []
        for year in years:
            cohort = [t for t in teams if t['year'] == year]
            a_mean = statistics.mean(t['actualWinRate'] for t in cohort)
            p_mean = statistics.mean(t[phase]['winRate'] for t in cohort)
            actual.extend(t['actualWinRate'] - a_mean for t in cohort)
            predicted.extend(t[phase]['winRate'] - p_mean for t in cohort)
        agreement[phase] = dict(teamCount=len(teams),
            winRateMae=statistics.mean(abs(t['actualWinRate'] - t[phase]['winRate']) for t in teams),
            withinYearCorrelation=statistics.correlation(actual, predicted),
            actualFirstPlaceAlsoPredictedFirst=sum(t['actualRank'] == 1 and t[phase]['rank'] == 1 for t in teams))
    metadata = {phase: dict(path=str(path), sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                           **{k: v for k, v in data.items() if k != 'rows'})
                for phase, data, path in [('before', before, args.before), ('after', after, args.after)]}
    result = dict(metadata=metadata, comparedSeasons=len(before['rows']),
                  maximumRepeats=args.maximum_repeats, agreement=agreement,
                  leagues={'before': league_stats(before), 'after': league_stats(after)}, teams=teams)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({k: result[k] for k in ('agreement', 'leagues')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
