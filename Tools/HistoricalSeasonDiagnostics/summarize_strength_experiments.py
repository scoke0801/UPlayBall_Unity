"""같은 통과 판정으로 단계별 실험을 요약하되 서로 다른 콘텐츠 해시를 숨기지 않는다."""
import json
import sys
from pathlib import Path
from verify_strength import evaluate


def main(folder, output):
    root = Path(__file__).resolve().parents[2]
    reference = json.loads((root/'docs/reports/historical-top30-current/result.json').read_text(encoding='utf-8-sig'))
    reference['teams'] = [t for t in reference['teams'] if t['year'] in (1985, 1992, 2010)]
    results = []
    for path in sorted(folder.glob('*-32.json')):
        data = json.loads(path.read_text(encoding='utf-8-sig'))
        result = evaluate(data, reference)
        result.update(experiment=path.stem, determinismChecks=data['determinismChecks'],
            center=data['center'], slope=data['slope'], inputOffset=data.get('inputOffset', 0),
            pitcherSlope=data.get('pitcherSlope', data['slope']), pitcherInputOffset=data.get('pitcherInputOffset', data.get('inputOffset', 0)))
        result['leagueRates'] = []
        for year in (1985, 1992, 2010):
            teams = [t for r in data['rows'] if r['year'] == year for t in r['teams']]
            result['leagueRates'].append(dict(year=year,
                battingAverage=sum(t['Hits'] for t in teams)/sum(t['AtBats'] for t in teams),
                earnedRunAverage=27*sum(t['EarnedRuns'] for t in teams)/sum(t['PitchingOuts'] for t in teams)))
        results.append(result)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(dict(acceptancePassed=False, fullTop30Verified=False,
        productionPublished=False, experiments=results), ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(f'실험 {len(results)}개, 총 {sum(r["games"] for r in results)}경기 요약: {output}')


if __name__ == '__main__':
    main(Path(sys.argv[1]), Path(sys.argv[2]))
