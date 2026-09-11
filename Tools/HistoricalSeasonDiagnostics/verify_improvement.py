"""개별 연도 진단과 구분하여 강팀 개선 후보의 전체 통계 채택 조건을 검사한다."""
import argparse
import json
import math
from pathlib import Path


def evaluate(comparison, leaders, allow_balance_change=False):
    """실제 성적은 검사에만 사용한다. 이 결과는 콘텐츠 보존·컴파일·규칙 검증을 대신하지 않는다."""
    if not leaders.get('teams') or not comparison.get('teams'):
        raise ValueError('빈 결과로 개선을 판정할 수 없습니다.')
    before = comparison['metadata']['before']
    after = comparison['metadata']['after']
    for field in ('engineVersion', 'regularSeasonGamesPerTeam', 'rotationPolicy'):
        if before[field] != after[field]:
            raise ValueError('동일 경기 조건의 전후 비교가 아닙니다: '+field)
    balance_changed = before['balanceHash'] != after['balanceHash']
    if balance_changed and not allow_balance_change:
        raise ValueError('밸런스 계수 변경을 포함한 비교임을 명시해야 합니다.')
    if leaders['contentHash'] != after['contentHash']:
        raise ValueError('선두 보고서와 전후 비교의 콘텐츠가 다릅니다.')
    if before['rotationPolicy'] != 'FixedFive':
        raise ValueError('고정 5선발 비교가 아닙니다.')
    target_keys = {(t['year'], t['teamSeasonKey']) for t in leaders['teams']}
    team_keys = {(t['year'], t['key']) for t in comparison['teams']}
    if not target_keys <= team_keys:
        raise ValueError('선두 보고서에 비교되지 않은 팀이 있습니다.')
    differences = [abs(t['difference']) for t in leaders['teams']]
    mean_error = sum(differences)/len(differences)
    rank_share = sum(t['rankPassed'] for t in leaders['teams'])/len(differences)
    sample_passed = all(t['samplePassed'] and t['repeats'] >= 32 for t in leaders['teams'])
    changes = {}
    for metric in ('AVG', 'ERA', 'runsPerGame', 'homeRunsPerGame', 'walksToStrikeouts'):
        old, new = comparison['leagues']['before'][metric], comparison['leagues']['after'][metric]
        if not math.isfinite(old) or not math.isfinite(new) or old <= 0:
            raise ValueError('리그 지표가 유효하지 않습니다: '+metric)
        changes[metric] = abs(new / old - 1)
    agreement = comparison['agreement']
    checks = dict(leaderMeanError=mean_error <= .04, leaderRankCoverage=rank_share >= .9,
                  sufficientSamples=sample_passed,
                  allTeamErrorImproved=agreement['after']['winRateMae'] < agreement['before']['winRateMae'],
                  leagueStatisticsPreserved=all(change <= .05 for change in changes.values()))
    return dict(statisticalSelectionPassed=all(checks.values()), checks=checks,
                leaderMeanAbsoluteError=mean_error, leaderRankShare=rank_share,
                leagueRelativeChanges=changes, individualYearTargetsPassed=leaders['passed'],
                individualYearPassedCount=leaders['passedCount'], targetCount=len(differences),
                balanceChanged=balance_changed,
                baselineBalanceHash=before['balanceHash'], candidateBalanceHash=after['balanceHash'],
                baselineHash=before['contentHash'], contentHash=after['contentHash'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('comparison', type=Path)
    parser.add_argument('leaders', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--allow-balance-change', action='store_true',
                        help='계수 변경을 포함한 전후 비교임을 명시한다. 리그 통계 보존 검사는 유지한다.')
    args = parser.parse_args()
    read = lambda path: json.loads(path.read_text(encoding='utf-8-sig'))
    result = evaluate(read(args.comparison), read(args.leaders), args.allow_balance_change)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
    raise SystemExit(0 if result['statisticalSelectionPassed'] else 2)
