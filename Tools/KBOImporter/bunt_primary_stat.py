"""번트 관측과 결측 추정을 구분해 독립 능력치로 발급한다."""
import csv
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
POLICY = json.loads(Path(__file__).with_name('bunt_primary_stat_policy.json').read_text(encoding='utf-8'))


def load_observations():
    """기존 출처 연결을 재사용하며 어깨 열을 번트 관측으로 해석하지 않는다."""
    observations = {}
    for relative in POLICY['archivePaths']:
        path = ROOT / relative
        if not path.exists():
            continue
        with path.open(encoding='utf-8-sig', newline='') as stream:
            for row in csv.DictReader(stream):
                value = row.get('번트', '').strip()
                if not value or row.get('CardType') != '일반' or row.get('CardTypeCss') != 'playerCard1':
                    continue
                rating = int(value)
                if not 1 <= rating <= 100:
                    raise ValueError('번트 관측은 1~100이어야 합니다.')
                key = f'ArchivedWebCard:{relative}:{row["CardId"]}'
                if key in observations and observations[key] != rating:
                    raise ValueError('같은 출처의 번트 관측값이 충돌합니다.')
                observations[key] = rating
    return observations


def resolve(ratings, reference, observations):
    """희생번트 횟수 대신 직접 관측을 우선하며 결측은 저작 시 한 번 추정한다."""
    reference = reference or {}
    if 'Bunt' in reference.get('values', {}):
        return int(reference['values']['Bunt']), 'Observed', [reference['sources']['Bunt']]
    sources = sorted(set(reference.get('sources', {}).values()) & observations.keys())
    values = {observations[source] for source in sources}
    if len(values) > 1:
        raise ValueError('동일 시즌의 번트 출처가 충돌합니다. 우선 출처 확인이 필요합니다.')
    if values:
        return values.pop(), 'Observed', sources
    weights = POLICY['missingWeights']
    total = weights['Contact'] + weights['BatterMental']
    value = (ratings[0] * weights['Contact'] + ratings[5] * weights['BatterMental'] + total // 2) // total
    return value, 'EstimatedWithoutBuntObservation', []


def apply(seasons, references, observations=None):
    """발급된 번트값과 관측 유무·추정 입력을 Editor 근거에 남긴다."""
    observations = load_observations() if observations is None else observations
    for season in seasons:
        if season['playerType'] != 'Hitter':
            continue
        ratings = season['baseAttributes']
        previous = ratings[3]
        value, status, sources = resolve(ratings, references.get(season['playerSeasonId']), observations)
        ratings[3] = value
        if len(season.get('trainingCeiling', [])) >= 4:
            season['trainingCeiling'][3] = min(100, value + max(0, season['trainingCeiling'][3] - previous))
        season['buntDerivation'] = dict(version=POLICY['version'], status=status, sources=sources,
            value=value, inputs={} if sources else dict(Contact=ratings[0], BatterMental=ratings[5]))
        traces = season.get('abilityDerivationTrace', [])
        previous = next((trace for trace in traces if trace['attribute'] in ('Arm', 'Bunt')), None)
        if previous is not None:
            previous.update(attribute='Bunt', components=[], combinedZ=0, ratingCenter=value,
                ratingBeforeClamp=value, ratingAfterClamp=value, referenceCalibration=None,
                evidenceStatus=status, buntDerivation=season['buntDerivation'])
