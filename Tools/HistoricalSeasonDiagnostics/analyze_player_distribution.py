"""역사 시즌의 연도별 투타 평균과 규정 타석·이닝 개인 분포를 원기록과 비교한다."""
import argparse
import json
from pathlib import Path
from collections import defaultdict
from statistics import mean


def distribution(hitters, pitchers):
    averages = [h / ab for pa, ab, h, games in hitters if ab and pa >= games * 3.1]
    eras = [27 * er / outs for outs, er, games in pitchers if outs >= games * 3]
    return dict(qualifiedHitters=len(averages), hitters400=sum(a >= .4 for a in averages),
                highestAverage=max(averages, default=0), qualifiedPitchers=len(eras),
                pitchersBelow3=sum(e < 3 for e in eras), lowestEra=min(eras, default=0))


def analyze(path, normalized):
    # 선수 상세를 접는 simulation_report.read 대신 원시 정규시즌 행을 읽는다.
    with path.open(encoding="utf-8-sig") as stream:
        report = json.load(stream)
    years = defaultdict(list)
    for row in report['rows']:
        years[row['year']].append(row)
    result = []
    for year, runs in sorted(years.items()):
        source = json.loads((normalized / f'{year}.json').read_text(encoding='utf-8-sig'))
        source_games = {t['sourceTeamName']: t['rankStats']['games'] for t in source['teams']}
        hitters, pitchers = [], []
        for p in source['players']:
            games = source_games.get(p['aggregateTeamName'], max(source_games.values()))
            h, pit = p.get('hitterStats'), p.get('pitcherStats')
            if h:
                hitters.append((h['plateAppearances'], h['atBats'], h['hits'], games))
            if pit:
                pitchers.append((pit['inningsOuts'], pit['earnedRuns'], games))
        actual = distribution(hitters, pitchers)
        # 과거 연도는 팀별 세부 기록이 없으므로 개인의 리그 합산 원기록을 사용한다.
        actual['average'] = sum(h for pa, ab, h, games in hitters) / sum(ab for pa, ab, h, games in hitters)
        actual['era'] = 27 * sum(er for outs, er, games in pitchers) / sum(outs for outs, er, games in pitchers)
        samples = []
        for run in runs:
            players = [p for p in run['statistics'] if not any(p[k] for k in ('IsFirstHalf', 'IsPostseason', 'IsAllStarGame'))]
            games = {t['TeamSeasonKey']: t['Games'] for t in run['teams']}
            sample = distribution(
                [(p['PlateAppearances'], p['AtBats'], p['Hits'], games[p['TeamSeasonKey']]) for p in players],
                [(p['PitchingOuts'], p['EarnedRuns'], games[p['TeamSeasonKey']]) for p in players])
            teams = run['teams']
            sample['average'] = sum(t['Hits'] for t in teams) / sum(t['AtBats'] for t in teams)
            sample['era'] = 27 * sum(t['EarnedRuns'] for t in teams) / sum(t['PitchingOuts'] for t in teams)
            samples.append(sample)
        result.append(dict(year=year, actual=actual,
                           simulated={key: mean(s[key] for s in samples) for key in samples[0]}))
    return dict(input=str(path), contentHash=report['contentHash'], balanceHash=report['balanceHash'],
                engineVersion=report['engineVersion'], games=report['games'], repeats=report['repeatCount'], years=result)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('simulation', type=Path)
    parser.add_argument('--normalized', type=Path, default=Path('Tools/KBOImporter/.cache/KBOImport/Normalized'))
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = analyze(args.simulation, args.normalized)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
