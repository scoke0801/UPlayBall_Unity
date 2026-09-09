"""일반 카드 구속만으로 선수 분리 검증을 거친 경량 추정 모델을 저작한다."""
import argparse
import hashlib
import json
import math
from pathlib import Path

import synthetic_bake as bake
from calibrate_annual_reference import split_person
from record_calibration import read_feature
from velocity_estimation import estimate, validate_model

ROOT = Path(__file__).resolve().parents[2]
DEFINITIONS = [('StrikeoutsPerNine', 'identity'), ('NegativeWalksPerNine', 'identity'),
               ('NegativeEarnedRunAverage', 'identity'), ('InningsPerGame', 'identity'),
               ('SaveRate', 'identity'), ('HoldRate', 'identity'),
               ('SeasonInnings', 'log1p'), ('PitchingGames', 'log1p')]


def solve(matrix, target):
    """작은 정규방정식을 부분 피벗 소거로 풀어 외부 학습 패키지 없이 재현한다."""
    rows = [list(row) + [value] for row, value in zip(matrix, target)]
    size = len(rows)
    for index in range(size):
        pivot = max(range(index, size), key=lambda r: abs(rows[r][index]))
        rows[index], rows[pivot] = rows[pivot], rows[index]
        denominator = rows[index][index]
        if abs(denominator) < 1e-12:
            raise ValueError('구속 회귀의 정규방정식이 특이합니다.')
        rows[index] = [v / denominator for v in rows[index]]
        for other in range(size):
            if other == index:
                continue
            factor = rows[other][index]
            rows[other] = [a - factor * b for a, b in zip(rows[other], rows[index])]
    return [r[-1] for r in rows]


def fit(rows, ridge):
    """선수 식별자는 분리 검증에만 쓰며 회귀 입력은 투구 기록으로 제한한다."""
    features = []
    for metric, transform in DEFINITIONS:
        f = dict(source=metric + '.rawValue', transform=transform)
        values = sorted(v for r in rows if (v := read_feature(f, r['evidence'], 0)) is not None
                        and r['evidence'][metric].get('sampleSize', 0) > 0)
        if not values:
            continue
        low, high = values[int((len(values)-1)*0.01)], values[int((len(values)-1)*0.99)]
        bounded = [max(low, min(high, v)) for v in values]
        mean = sum(bounded)/len(bounded)
        scale = math.sqrt(sum((v-mean)**2 for v in bounded)/len(bounded))
        if scale < 1e-8:
            continue
        f.update(mean=mean, scale=scale, minimum=low, maximum=high, coefficient=0.0)
        features.append(f)
    xs = []
    for r in rows:
        x = [1.0]
        for f in features:
            metric = r['evidence'][f['source'].split('.')[0]]
            value = read_feature(f, r['evidence'], 0) if metric.get('sampleSize', 0) > 0 else None
            bounded = f['mean'] if value is None else max(f['minimum'], min(f['maximum'], value))
            x.append((bounded-f['mean'])/f['scale'])
        xs.append(x)
    n = len(features)+1
    matrix = [[sum(x[i]*x[j] for x in xs) + (ridge if i == j and i else 0)
               for j in range(n)] for i in range(n)]
    target = [sum(x[i]*r['expected'] for x, r in zip(xs, rows)) for i in range(n)]
    coefficients = solve(matrix, target)
    for f, coefficient in zip(features, coefficients[1:]):
        f['coefficient'] = coefficient
    model = dict(version='normal-card-velocity-estimation-v1', target='NormalCardVelocityRating',
                 measuredVelocity=False, intercept=coefficients[0], features=features,
                 minimumRating=min(r['expected'] for r in rows), maximumRating=max(r['expected'] for r in rows),
                 ridge=ridge, trainingCount=len(rows))
    validate_model(model)
    return model


def scores(rows, model=None):
    """기존 55와 같은 보류 선수 집합에서 오차를 비교한다."""
    errors = [(round(estimate(r['evidence'], model)[0]) if model else 55)-r['expected'] for r in rows]
    return dict(count=len(rows), mae=sum(map(abs, errors))/len(rows),
                bias=sum(errors)/len(rows), within5=sum(abs(e)<=5 for e in errors)/len(rows))


def main():
    """검증 기준을 통과한 모델과 근거만 지정 경로에 쓴다."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    policy = json.loads((Path(__file__).with_name('reference_calibration_policy.json')).read_text(encoding='utf-8'))
    overrides = bake.load_annual_reference_overrides()
    rows, inputs = [], {}
    sources = [Path(__file__), Path(__file__).with_name('velocity_estimation.py'),
               Path(__file__).with_name('synthetic_bake.py'), Path(__file__).with_name('record_calibration.py'),
               Path(__file__).with_name('source_backed_runtime_bake.py'),
               bake.DERIVATION_BALANCE_PATH, Path(__file__).with_name('reference_calibration_policy.json')]
    override_config = bake.DERIVATION_BALANCE['annualReferenceOverride']
    sources.extend(Path(__file__).with_name(c['relativePath']) for c in
                   [override_config] + override_config.get('additionalSources', []))
    for path in sources:
        inputs[str(path.resolve().relative_to(ROOT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    for year in range(1982, 2026):
        path = ROOT / f'Tools/KBOImporter/.cache/KBOImport/Normalized/{year}.json'
        raw = path.read_bytes()
        inputs[str(path.relative_to(ROOT))] = hashlib.sha256(raw).hexdigest()
        data = json.loads(raw.decode('utf-8'))
        availability = bake.derive_pitcher_role_availability(data['players'])
        for p in data['players']:
            sid = bake.pitch_source_identity.editor_source_season_id(str(p['sourcePlayerId']), year)
            card = overrides.get(sid)
            if not card or card['playerType'] != 'Pitcher' or 'Velocity' not in card['values']:
                continue
            person = 'PERSON_' + bake.stable_digest('editor-source-person-v1', str(p['sourcePlayerId']))
            evidence = {c['metric']: c for c in bake.pitcher_metric_evidence(p, availability)}
            rows.append(dict(id=sid, person=person, year=year, expected=card['values']['Velocity'],
                             split=split_person(person, policy), evidence=evidence))
    rows.sort(key=lambda r: (r['year'], r['id']))
    groups = {split: [r for r in rows if r['split']==split] for split in ('Train','Validation','Holdout')}
    if min(map(len, groups.values())) < 100:
        raise ValueError('선수 분리 검증 표본이 부족합니다.')
    candidates = []
    for ridge in (0.1, 1.0, 10.0, 100.0, 1000.0):
        model = fit(groups['Train'], ridge)
        candidates.append((scores(groups['Validation'], model)['mae'], ridge))
    ridge = min(candidates)[1]
    model = fit(groups['Train'] + groups['Validation'], ridge)
    baseline, holdout = scores(groups['Holdout']), scores(groups['Holdout'], model)
    temporal_train = [r for r in rows if r['year'] <= 2009]
    temporal_test = [r for r in rows if r['year'] > 2009]
    temporal = dict(baseline=scores(temporal_test), estimated=scores(temporal_test, fit(temporal_train, ridge)))
    if holdout['mae'] > baseline['mae']*0.95 or temporal['estimated']['mae'] > temporal['baseline']['mae']:
        raise ValueError('구속 추정이 선수 분리·시대 분리 검증을 통과하지 못했습니다.')
    training_people = {r['person'] for r in groups['Train']+groups['Validation']}
    if training_people & {r['person'] for r in groups['Holdout']}:
        raise ValueError('구속 추정의 보류 선수와 학습 선수가 겹칩니다.')
    report = dict(labelCount=len(rows), splitCounts={k:len(v) for k,v in groups.items()},
                  candidates=candidates, baseline=baseline, holdout=holdout, temporal=temporal,
                  personLeakage=False, specialCardsUsed=False, sourceInputs=inputs)
    model['validation'] = {k:v for k,v in report.items() if k!='sourceInputs'}
    args.output.mkdir(parents=True, exist_ok=True)
    for name, payload in (('velocity_estimation.json', model), ('fit-report.json', report)):
        (args.output/name).write_text(json.dumps(payload, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(json.dumps({k:v for k,v in report.items() if k!='sourceInputs'}, ensure_ascii=False, indent=2))


if __name__ == '__main__':
    main()
