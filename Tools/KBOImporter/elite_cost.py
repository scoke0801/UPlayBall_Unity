"""최종 발급 전력·연도 성과·불펜 역할 가치를 일반 카드 가격에 반영한다."""
import json
import math
from pathlib import Path

POLICY = json.loads(Path(__file__).with_name('elite_cost_policy.json').read_text(encoding='utf-8'))
ABILITY_NAMES = ('Contact', 'Power', 'Speed', 'Bunt', 'Defense', 'BatterMental',
                 'Stamina', 'Velocity', 'Stuff', 'Breaking', 'Control', 'PitcherMental')


def issued_strength(season):
    """능력치 보정 전 Trace의 rating 대신 최종 발급 능력치로 역할 전력을 평가한다."""
    weights = season['costDerivationTrace']['roleWeights']
    total = sum(row['weight'] for row in weights)
    if not math.isfinite(total) or total <= 0 or any(row['weight'] < 0 for row in weights):
        raise ValueError('발급 전력 가중치가 유효하지 않습니다.')
    # 기존 발급 가격은 당시 송구 입력을 보존한다. 새 발급의 Bunt를 과거 Arm 가격으로 재해석하지 않는다.
    def rating(ability):
        if ability == 'Arm':
            return season['costDerivationTrace'].get('legacyThrowingRating', season['baseAttributes'][3])
        return season['baseAttributes'][ABILITY_NAMES.index(ability)]
    value = sum(rating(row['ability']) * row['weight']
                for row in weights) / total
    if not math.isfinite(value):
        raise ValueError('최종 발급 전력이 유한하지 않습니다.')
    return value


def meets_gate(components, gate):
    """시즌 성과와 역할별 출장량을 함께 확인하여 소표본의 승격을 막는다."""
    return (components['reliability'] >= gate['minimumReliability']
            and components['workload']['ratio'] >= gate['minimumWorkloadRatio']
            and components['quality'] >= gate.get('minimumQuality', float('-inf')))


def build_cost_floors(seasons):
    """Edition·이름·참조 카드 가격과 독립적으로 일반 카드의 성과 하한을 산출한다."""
    floors, populations = {}, {}
    for season in seasons:
        trace = season.get('costDerivationTrace', {})
        components = trace.get('componentScores')
        if not components or season.get('sourceDataKind') == 'ResearchCardSupplement':
            continue
        ability_policy = POLICY['issuedAbility']
        if meets_gate(components, ability_policy):
            strength = issued_strength(season)
            for tier in ability_policy['tiers'][season['playerType']]:
                if strength >= tier['minimumStrength']:
                    floors[season['playerSeasonId']] = (tier['cost'], 'FinalIssuedAbility')
        if meets_gate(components, POLICY['annualLeaders']):
            populations.setdefault((season['originYear'], season['playerType']), []).append(season)
        if season['playerType'] != 'Pitcher' or components['workload']['starterShare'] >= POLICY['reliefStarterShareMaximum']:
            continue
        for gate in POLICY['reliefTiers']:
            if meets_gate(components, gate) and gate['cost'] > floors.get(season['playerSeasonId'], (1, ''))[0]:
                floors[season['playerSeasonId']] = (gate['cost'], 'ReliefQualityAndWorkload')
    for population in populations.values():
        leader = min(population, key=lambda s: (
            -s['costDerivationTrace']['continuousValue'],
            -s['costDerivationTrace']['componentScores']['reliability'],
            -s['costDerivationTrace']['componentScores']['workload']['sample'], s['playerSeasonId']))
        floors[leader['playerSeasonId']] = (POLICY['annualLeaders']['cost'], 'AnnualPerformanceLeader')
    # 관측된 일반 카드 가격은 평가 결과가 아니라 기준 데이터다. 후보군에는 남겨
    # 관측 선두를 제외했다는 이유로 차순위까지 연도 최고 하한을 받지 않게 한다.
    for season in seasons:
        if season.get('annualReferenceOverride') is not None:
            floors.pop(season['playerSeasonId'], None)
    return floors


def apply_cost_floors(seasons):
    """원래 가격·출처를 보존하고 확정된 성과 하한만 최종 Cost에 적용한다."""
    floors = build_cost_floors(seasons)
    for season in seasons:
        reference = season.get('annualReferenceOverride')
        if reference is not None:
            cost = reference['values']['Cost']
            if isinstance(cost, bool) or not isinstance(cost, int) or not 1 <= cost <= 10:
                raise ValueError('관측 일반 카드 Cost는 1~10 정수여야 합니다.')
            season['cost'] = season['costDerivationTrace']['cost'] = cost
            season['costDerivationTrace']['costMethod'] = 'AnnualReferenceOverride'
            season['costDerivationTrace'].pop('eliteCostAdjustment', None)
            continue
        floor, reason = floors.get(season['playerSeasonId'], (1, 'Unchanged'))
        if floor <= season['cost']:
            continue
        trace = season['costDerivationTrace']
        trace['eliteCostAdjustment'] = dict(version=POLICY['version'], previousCost=season['cost'],
            previousMethod=trace['costMethod'], floor=floor, reason=reason)
        season['cost'] = trace['cost'] = floor
        trace['costMethod'] = 'EliteSeasonFloor'
