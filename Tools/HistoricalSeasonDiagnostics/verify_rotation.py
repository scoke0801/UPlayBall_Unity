"""고정 5선발의 순번별 팀 승패를 집계하고 정규시즌 기록과 대조한다."""
import argparse
import json
from simulation_report import read
from pathlib import Path


def summarize(simulation):
    """순번별 승률은 관측 결과이며 특정 선발이 패배의 원인이라는 판정은 아니다."""
    if simulation.get('rotationPolicy') != 'FixedFive' or not simulation.get('rows'):
        raise ValueError('고정 5선발 실행 결과가 필요합니다.')
    totals, seeds = {}, set()
    fields = ('regularStarts', 'regularTeamWins', 'regularTeamLosses', 'regularTeamDraws')
    for row in simulation['rows']:
        identity = row['year'], row['seed']
        if identity in seeds:
            raise ValueError('중복 실행 시드')
        seeds.add(identity)
        teams = {t['TeamSeasonKey']: t for t in row['teams']}
        rotations = row['rotations']
        if len(teams) != len(row['teams']) or len(rotations) != len(teams) or {
                r['teamSeasonKey'] for r in rotations} != set(teams):
            raise ValueError('로테이션 팀 구성 불일치')
        for rotation in rotations:
            team = teams[rotation['teamSeasonKey']]
            for field in fields:
                values = rotation.get(field)
                if not isinstance(values, list) or len(values) != 5 or any(
                        type(v) is not int or v < 0 for v in values):
                    raise ValueError('순번별 비음수 정수 기록 다섯 개가 필요합니다.')
            expected = [team['Games'] // 5 + (i < team['Games'] % 5) for i in range(5)]
            if rotation['regularStarts'] != expected:
                raise ValueError('고정 5선발 배분 위반')
            for field, statistic in zip(fields, ('Games', 'Wins', 'Losses', 'Ties')):
                if sum(rotation[field]) != team[statistic]:
                    raise ValueError('정규시즌 팀 기록과 순번 합계 불일치')
            for i in range(5):
                if rotation['regularStarts'][i] != sum(rotation[f][i] for f in fields[1:]):
                    raise ValueError('순번별 경기 수와 승패무 합계 불일치')
            key = row['year'], rotation['teamSeasonKey']
            total = totals.setdefault(key, dict(year=key[0], teamSeasonKey=key[1], repeats=0,
                                               **{f: [0] * 5 for f in fields}))
            total['repeats'] += 1
            for field in fields:
                for i in range(5):
                    total[field][i] += rotation[field][i]
    for total in totals.values():
        total['teamWinRatesByStarterSlot'] = [w / (w + l) if w + l else None for w, l in zip(
            total['regularTeamWins'], total['regularTeamLosses'])]
    return dict(passed=True, contentHash=simulation['contentHash'],
                teams=[totals[key] for key in sorted(totals)])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('simulation', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    report = summarize(read(args.simulation))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(passed=True, teams=len(report['teams']))))
