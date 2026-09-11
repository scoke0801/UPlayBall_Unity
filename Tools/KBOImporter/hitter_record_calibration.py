"""팀 성적 없이 동년 개인 타격 기록의 표본 신뢰도로 후보 능력치를 산출한다."""
import math


def estimate_ratings(records, profile):
    """선수 ID→타격 기록을 받아 결측은 제외하고 소표본을 동년 가중 평균으로 수축한다."""
    prior = float(profile['prior'])
    center = float(profile['center'])
    scale = float(profile['scale'])
    weights = profile['numerator']
    if (not all(math.isfinite(x) for x in (prior, center, scale)) or prior <= 0 or scale <= 0
            or not weights or any(not math.isfinite(w) or w < 0 for w in weights.values())):
        raise ValueError('수축 표본·중심·척도·분자 가중치가 유효하지 않습니다.')
    samples = {}
    for key in sorted(records):
        stats = records[key]
        denominator = stats.get(profile['denominator'])
        if denominator is None or any(stats.get(field) is None for field in weights):
            continue
        values = [denominator, *(stats[field] for field in weights)]
        if any(not math.isfinite(v) or v < 0 for v in values):
            raise ValueError('개인 타격 기록에 음수 또는 비유한 값이 있습니다.')
        if denominator == 0:
            continue
        samples[key] = (sum(stats[field] * weights[field] for field in sorted(weights)), denominator)
    if not samples:
        return {}
    total = sum(d for _, d in samples.values())
    mean = sum(n for n, _ in samples.values()) / total
    shrunk = {key: (n + prior * mean) / (d + prior) for key, (n, d) in samples.items()}
    weighted_mean = sum(shrunk[key] * d for key, (_, d) in samples.items()) / total
    deviation = math.sqrt(sum(d * (shrunk[key] - weighted_mean) ** 2
                              for key, (_, d) in samples.items()) / total)
    result = {}
    for key, (numerator, denominator) in samples.items():
        # 모두 같은 비율이면 강약 차이를 만들어 내지 않는다.
        z = (shrunk[key] - weighted_mean) / deviation if deviation > 1e-12 else 0.0
        result[key] = dict(rating=center + scale * z, numerator=numerator, denominator=denominator,
            leagueRate=mean, priorSample=prior, shrunkRate=shrunk[key], adjustedZ=z,
            populationMean=weighted_mean, populationStandardDeviation=deviation)
    return result


def rating_reliability(estimate, profile):
    """기록 비율의 평균 수축과 별개로, 지속 출전 표본에 도달할 때까지 기존 추정을 남긴다."""
    if not profile.get('blendWithExisting', False):
        return 1.0
    sample = estimate['denominator']
    full_sample = profile.get('fullSample')
    if full_sample is not None:
        if not math.isfinite(full_sample) or full_sample <= 0:
            raise ValueError('완전 반영 표본은 유한한 양수여야 합니다.')
        return min(1.0, sample / full_sample)
    return sample / (sample + estimate['priorSample'])


def resolve_rating(estimate, previous_rating, profile):
    """소표본은 기존 개인 기록 추정에 가깝게 남겨 평균급 능력을 자동 지급하지 않는다."""
    if not profile.get('blendWithExisting', False):
        return estimate['rating']
    reliability = rating_reliability(estimate, profile)
    return previous_rating + reliability * (estimate['rating'] - previous_rating)


def apply_to_seasons(seasons, records, profiles, clamp_rating):
    """로스터 선정 전 후보 능력치와 산출 근거를 함께 갱신한다. 관측 카드 덮어쓰기는 호출 뒤 적용한다."""
    names = {0: 'Contact', 1: 'Power', 5: 'BatterMental'}
    for profile in profiles:
        index = profile['index']
        if index not in (0, 1, 5):
            raise ValueError('타격 Contact·Power·Mental만 지원합니다.')
        estimates = estimate_ratings(records, profile)
        for season in seasons:
            identity = season['playerSeasonId']
            if season['playerType'] != 'Hitter' or identity not in estimates:
                continue
            estimate = estimates[identity]
            previous_rating = season['baseAttributes'][index]
            rating = resolve_rating(estimate, previous_rating, profile)
            value = clamp_rating(rating)
            season['baseAttributes'][index] = value
            trace = next(t for t in season['abilityDerivationTrace'] if t['attribute'] == names[index])
            sample = estimate['denominator']
            reliability = sample / (sample + estimate['priorSample'])
            z = estimate['adjustedZ']
            deviation = estimate['populationStandardDeviation']
            raw_rate = estimate['numerator'] / sample
            raw_z = (raw_rate - estimate['populationMean']) / deviation if deviation > 1e-12 else 0.0
            prior_z = (estimate['leagueRate'] - estimate['populationMean']) / deviation if deviation > 1e-12 else 0.0
            trace.update(evaluationMethod='ShrunkIndividualHittingRecord',
                combinedZ=z, ratingCenter=profile['center'], ratingBeforeClamp=rating,
                ratingAfterClamp=value, referenceCalibration=None,
                recordCalibration=dict(estimate, previousRating=previous_rating,
                    fullSample=profile.get('fullSample', 0),
                    ratingReliability=rating_reliability(estimate, profile)),
                components=[dict(metric=names[index]+'RecordRate', numerator=estimate['numerator'],
                    denominator=sample, rawValue=estimate['numerator']/sample, sampleSize=sample,
                    reliabilityConstant=estimate['priorSample'], referenceWeight=sample,
                    referenceGroupKey=trace.get('groupKey', ''), roleTier=trace.get('roleTier', 'Unknown'),
                    groupMean=estimate['populationMean'], groupStdDev=estimate['populationStandardDeviation'],
                    rawZ=raw_z, boundedZ=raw_z, priorZ=prior_z, reliability=reliability, adjustedZ=z, weight=1.0, configuredWeight=1.0,
                    contribution=z, observedContribution=reliability*raw_z,
                    priorContribution=(1.0-reliability)*prior_z, isAvailable=True)])
