using System;

namespace Baseball.Core.Players
{
    /// <summary>
    /// 선수 유형별 초기 능력치 개수와 배분 한도를 정의한다.
    /// </summary>
    public readonly struct CareerAttributeAllocationRule
    {
        public CareerAttributeAllocationRule(int attributeCount, int baseValue, int bonusPoints, int maxValue)
        {
            if (attributeCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(attributeCount));
            if (baseValue < AttributeRating.Minimum || baseValue > AttributeRating.Maximum)
                throw new ArgumentOutOfRangeException(nameof(baseValue));
            if (bonusPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(bonusPoints));
            if (maxValue < baseValue || maxValue > AttributeRating.Maximum)
                throw new ArgumentOutOfRangeException(nameof(maxValue));
            if (bonusPoints > (maxValue - baseValue) * attributeCount)
                throw new ArgumentOutOfRangeException(nameof(bonusPoints), "배분 포인트가 전체 능력치 수용량보다 많습니다.");

            AttributeCount = attributeCount;
            BaseValue = baseValue;
            BonusPoints = bonusPoints;
            MaxValue = maxValue;
        }

        public int AttributeCount { get; }
        public int BaseValue { get; }
        public int BonusPoints { get; }
        public int MaxValue { get; }

        /// <summary>가중치에 비례해 사용 가능한 포인트를 모두 배분한 배열을 만든다.</summary>
        public int[] CreateWeightedValues(params int[] weights)
        {
            if (weights == null || weights.Length != AttributeCount)
                throw new ArgumentException($"가중치는 정확히 {AttributeCount}개가 필요합니다.", nameof(weights));

            var values = new int[AttributeCount];
            var allocated = new int[AttributeCount];
            bool hasPositiveWeight = false;
            for (int index = 0; index < AttributeCount; index++)
            {
                if (weights[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(weights));
                hasPositiveWeight |= weights[index] > 0;
                values[index] = BaseValue;
            }
            if (!hasPositiveWeight)
                throw new ArgumentException("하나 이상의 가중치는 0보다 커야 합니다.", nameof(weights));

            int capacity = MaxValue - BaseValue;
            for (int point = 0; point < BonusPoints; point++)
            {
                int selected = -1;
                for (int index = 0; index < AttributeCount; index++)
                {
                    if (allocated[index] >= capacity)
                        continue;
                    if (selected < 0 ||
                        (long)weights[index] * (allocated[selected] + 1) >
                        (long)weights[selected] * (allocated[index] + 1))
                    {
                        selected = index;
                    }
                }
                allocated[selected]++;
                values[selected]++;
            }
            return values;
        }

        /// <summary>모든 포인트를 사용한 능력치 배열인지 검증한다.</summary>
        public void ValidateComplete(int[] values)
        {
            if (values == null || values.Length != AttributeCount)
                throw new ArgumentException($"능력치는 정확히 {AttributeCount}개가 필요합니다.", nameof(values));

            int spent = 0;
            for (int index = 0; index < values.Length; index++)
            {
                int value = values[index];
                if (value < BaseValue || value > MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(values), value, $"능력치는 {BaseValue}~{MaxValue} 범위여야 합니다.");
                }
                spent += value - BaseValue;
            }

            if (spent != BonusPoints)
                throw new ArgumentException($"배분 포인트 {BonusPoints}를 모두 사용해야 합니다. 현재 {spent}포인트를 사용했습니다.", nameof(values));
        }
    }

    /// <summary>
    /// 신규 선수의 공정한 출발선을 선수 유형별로 제공한다.
    /// </summary>
    public readonly struct CareerCreationRules
    {
        public CareerCreationRules(
            CareerAttributeAllocationRule batter,
            CareerAttributeAllocationRule pitcher)
        {
            Batter = batter;
            Pitcher = pitcher;
        }

        public CareerAttributeAllocationRule Batter { get; }
        public CareerAttributeAllocationRule Pitcher { get; }

        public CareerAttributeAllocationRule GetRule(PlayerType playerType)
        {
            if (!Enum.IsDefined(typeof(PlayerType), playerType))
                throw new ArgumentOutOfRangeException(nameof(playerType));
            return playerType == PlayerType.Pitcher ? Pitcher : Batter;
        }

        /// <summary>기획안의 35 기본값, 타자 60·투수 40포인트 규칙을 만든다.</summary>
        public static CareerCreationRules CreateDefault()
        {
            return new CareerCreationRules(
                new CareerAttributeAllocationRule(attributeCount: 6, baseValue: 35, bonusPoints: 60, maxValue: 60),
                new CareerAttributeAllocationRule(attributeCount: 4, baseValue: 35, bonusPoints: 40, maxValue: 60));
        }
    }
}
