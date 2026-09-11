"""연도별 선두의 실제 승률과 시뮬레이션 승률을 검증 전용 보고서로 만든다."""
import argparse
import hashlib
import json
import statistics
from pathlib import Path

from verify_strength import evaluate


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('simulation', type=Path)
    parser.add_argument('reference', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--supplement', type=Path, action='append', default=[])
    args = parser.parse_args()
    read = lambda path: json.loads(path.read_text(encoding='utf-8-sig'))
    reference = read(args.reference)
    source = read(args.simulation)
    selected = dict(source)
    inputs = [args.simulation]
    for supplement_path in args.supplement:
        supplement = read(supplement_path)
        for key in ('contentHash', 'balanceHash', 'engineVersion', 'rotationPolicy'):
            if source[key] != supplement[key]:
                raise ValueError(f'서로 다른 입력의 결과를 합칠 수 없습니다: {key}')
        years = {row['year'] for row in supplement['rows']}
        for year in years:
            original = {row['seed']: row for row in selected['rows'] if row['year'] == year}
            additional = {row['seed']: row for row in supplement['rows'] if row['year'] == year}
            if not original.keys() <= additional.keys():
                raise ValueError('보충 실행은 기존 시드를 모두 포함해야 합니다.')
            if any(row['checksum'] != additional[seed]['checksum'] for seed, row in original.items()):
                raise ValueError('보충 실행의 공통 시드 경기 결과가 다릅니다.')
        selected['rows'] = [row for row in selected['rows'] if row['year'] not in years] + supplement['rows']
        inputs.append(supplement_path)
    # 검사기의 games를 이 보고서에서는 중복 제거한 정규시즌 경기 수로 명시한다.
    selected['games'] = sum(team['Games'] for row in selected['rows'] for team in row['teams']) // 2
    result = evaluate(selected, reference)
    teams = result['teams']
    if any('difference' not in team for team in teams):
        raise ValueError('누락된 연도가 있어 완전한 선두 보고서를 만들 수 없습니다.')
    result['gamesDefinition'] = '선택한 연도·시드의 정규시즌 경기, 중복 제외'
    result['inputs'] = [{'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest()}
                        for path in inputs + [args.reference]]
    result['meanAbsoluteDifference'] = statistics.mean(abs(team['difference']) for team in teams)
    result['ratePassedCount'] = sum(team['ratePassed'] for team in teams)
    result['rankPassedCount'] = sum(team['rankPassed'] for team in teams)
    lines = ['# 연도별 정규시즌 선두 재현 진단', '',
             '실제 승패·순위는 검증 자료에만 사용한다. 경기 입력이나 팀 보너스로 주입하지 않는다.', '',
             f"엔진 {source['engineVersion']}, {len({t['year'] for t in teams})}개 연도, 선두 {len(teams)}개 사례.",
             f"선택한 정규시즌 {selected['games']:,}경기. 평균 절대 승률 오차 {result['meanAbsoluteDifference']:.2%}p.",
             f"승률 ±5%p 이내 {result['ratePassedCount']}/{len(teams)}, 평균 승률 상위 3위 {result['rankPassedCount']}/{len(teams)}.",
             f"최소 32시드까지 충족한 통과 사례 {result['passedCount']}/{len(teams)}. 전체 통과: {result['passed']}.", '',
             '승률은 무승부를 제외한 W/(W+L). 순위는 반복 평균 승률의 순위다.',
             '공동 선두 및 당시 공표 승률 선두와 W/(W+L) 선두가 다른 경우를 함께 표시한다.',
             '8시드는 예비 진단이며 최종 합격으로 간주하지 않는다. 보충 실행은 같은 시드의 checksum 일치를 확인했다.', '',
             '| 연도 | 팀 | 실제 승률 | 시뮬레이션 | 차이(%p) | 순위 | 시드 수 |',
             '|---|---|---:|---:|---:|---:|---:|']
    for team in sorted(teams, key=lambda item: (item['year'], item['team'])):
        lines.append(f"| {team['year']} | {team['team']} | {team['actualWinRate']:.3f} | "
                     f"{team['simulatedWinRate']:.3f} | {team['difference'] * 100:+.2f} | {team['rank']} | {team['repeats']} |")
    lines += ['', '## 재현 입력', '', f"- ContentHash: `{source['contentHash']}`",
              f"- BalanceHash: `{source['balanceHash']}`", '- 고정 5선발, 팀당 정규시즌 144경기.',
              '- 입력 경로·SHA-256 및 개별 판정은 같은 이름의 JSON 파일에 보존한다.', '']
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text('\n'.join(lines), encoding='utf-8')
    args.output.with_suffix('.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({key: result[key] for key in ('passed', 'games', 'meanAbsoluteDifference',
                     'ratePassedCount', 'rankPassedCount', 'passedCount')}, ensure_ascii=False))


if __name__ == '__main__':
    main()
