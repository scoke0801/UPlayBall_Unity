"""역사 강팀의 반복 시즌 승률·순위를 실제 기록과 비교하는 실패 폐쇄형 통과 판정이다."""
import argparse
import json
import math
import statistics
from collections import defaultdict
from pathlib import Path


def evaluate(simulation, reference, tolerance=0.05, minimum_repeats=32):
    """실제 승률은 검증에만 사용하며 경기 결과나 능력치를 변경하지 않는다."""
    targets = reference["teams"]
    if not targets:
        raise ValueError("대상 목록이 비어 있습니다.")
    if not math.isfinite(tolerance) or tolerance < 0 or minimum_repeats < 2:
        raise ValueError("허용 오차와 최소 반복 수가 유효하지 않습니다.")
    by_year = defaultdict(list)
    for row in simulation["rows"]:
        by_year[row["year"]].append(row)
    result = []
    for target in targets:
        year = target["year"]
        if year not in by_year:
            result.append(dict(year=year, team=target["team"], passed=False, reason="연도 실행 누락"))
            continue
        rows = by_year[year]
        if len({r["seed"] for r in rows}) != len(rows):
            raise ValueError("같은 시드를 중복 실행한 결과는 독립 반복으로 셀 수 없습니다.")
        samples = defaultdict(list)
        for row in rows:
            for team in row["teams"]:
                decisions = team["Wins"] + team["Losses"]
                if decisions <= 0:
                    raise ValueError("승패가 없는 팀은 승률을 검증할 수 없습니다.")
                samples[team["TeamSeasonKey"]].append(team["Wins"] / decisions)
        if any(len(s) != len(rows) for s in samples.values()):
            raise ValueError("반복 시드 사이에 팀 구성이 다릅니다.")
        key = target["teamSeasonKey"]
        if key not in samples:
            raise ValueError(f"대상 팀 누락: {key}")
        ordered = sorted(samples, key=lambda k: (-statistics.mean(samples[k]), k))
        observed = statistics.mean(samples[key])
        actual = target["actualWins"] / (target["actualWins"] + target["actualLosses"])
        peers = [t for t in targets if t["year"] == year]
        best_actual = max(t["actualWins"] / (t["actualWins"] + t["actualLosses"]) for t in peers)
        must_lead = target.get("actualRegularLeader", False) or abs(actual - best_actual) < 1e-12
        rank = 1 + sum(statistics.mean(samples[k]) > observed + 1e-12 for k in ordered)
        half = 2.04 * statistics.stdev(samples[key]) / math.sqrt(len(rows)) if len(rows) > 1 else None
        rate_pass = abs(observed - actual) <= tolerance
        # 동년 강팀 모두에게 동시에 1위를 요구하지 않는다. 실제 승률이 높은 대상에 뒤처지는지 확인한다.
        relative_pass = all(observed <= statistics.mean(samples[t["teamSeasonKey"]])
            for t in peers if t["actualWins"] / (t["actualWins"] + t["actualLosses"]) > actual + 1e-12)
        # 실제 선두는 사용자가 정한 1~3위 범위로 판정한다. 단일 Seed의 우승 강제 대신 반복 평균을 본다.
        rank_pass = rank <= 3 if must_lead else relative_pass
        enough = len(rows) >= minimum_repeats
        result.append(dict(year=year, team=target["team"], teamSeasonKey=key, repeats=len(rows),
            actualWinRate=actual, simulatedWinRate=observed, difference=observed-actual,
            approximate95HalfWidth=half, rank=rank, requiresTopThree=must_lead,
            ratePassed=rate_pass, rankPassed=rank_pass, samplePassed=enough,
            passed=rate_pass and rank_pass and enough))
    return dict(passed=all(r["passed"] for r in result), tolerance=tolerance,
        minimumRepeats=minimum_repeats, contentHash=simulation["contentHash"],
        games=simulation["games"], targetCount=len(result), passedCount=sum(r["passed"] for r in result), teams=result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("simulation", type=Path)
    parser.add_argument("reference", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--years", help="단계 검증에서 선택할 연도 목록")
    parser.add_argument("--tolerance", type=float, default=.05)
    args = parser.parse_args()
    read = lambda path: json.loads(path.read_text(encoding="utf-8-sig"))
    reference = read(args.reference)
    if args.years:
        years = set(map(int, args.years.split(',')))
        reference = dict(teams=[t for t in reference["teams"] if t["year"] in years])
        if not reference["teams"]:
            raise ValueError("대상 목록이 비어 있습니다.")
    result = evaluate(read(args.simulation), reference, args.tolerance)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result, ensure_ascii=False))
    return 0 if result["passed"] else 2


if __name__ == "__main__":
    raise SystemExit(main())
