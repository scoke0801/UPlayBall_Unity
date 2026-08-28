using System;

namespace Baseball.Core.Players
{
    /// <summary>
    /// 새 게임 캐릭터 생성에서 배분한 능력치 6개가 규칙을 지키는지 검증한다.
    /// </summary>
    public static class AttributeAllocation
    {
        /// <summary>
        /// 가중치 비율에 맞춰 사용 가능한 추가 포인트를 모두 배분한 능력치 배열을 만든다.
        /// </summary>
        public static int[] CreateWeightedValues(
            CharacterCreationBalance balance,
            params int[] weights)
        {
            if (weights == null || weights.Length != CharacterCreationBalance.AttributeCount)
            {
                throw new ArgumentException(
                    $"가중치는 정확히 {CharacterCreationBalance.AttributeCount}개가 필요합니다.",
                    nameof(weights));
            }

            bool hasPositiveWeight = false;
            for (int index = 0; index < weights.Length; index++)
            {
                if (weights[index] < 0)
                    throw new ArgumentOutOfRangeException(nameof(weights), "가중치는 음수일 수 없습니다.");
                hasPositiveWeight |= weights[index] > 0;
            }

            if (!hasPositiveWeight)
                throw new ArgumentException("하나 이상의 가중치는 0보다 커야 합니다.", nameof(weights));

            var values = new int[CharacterCreationBalance.AttributeCount];
            var allocatedPoints = new int[CharacterCreationBalance.AttributeCount];
            for (int index = 0; index < values.Length; index++)
                values[index] = balance.BaseValue;

            int capacityPerAttribute = balance.MaxValue - balance.BaseValue;
            int pointCount = Math.Min(
                balance.BonusPoints,
                capacityPerAttribute * CharacterCreationBalance.AttributeCount);
            for (int point = 0; point < pointCount; point++)
            {
                int selectedIndex = FindNextWeightedIndex(weights, allocatedPoints, capacityPerAttribute);
                allocatedPoints[selectedIndex]++;
                values[selectedIndex]++;
            }

            return values;
        }

        /// <summary>
        /// 각 능력치가 [BaseValue, MaxValue] 범위 안에 있고 기준값 대비 추가분의 합이
        /// BonusPoints를 넘지 않는지 검증한다. 포지션 권장치와의 불일치는 검증하지 않는다 —
        /// 잘못된 빌드도 플레이어의 선택으로 허용하는 것이 기획 의도다.
        /// </summary>
        public static void Validate(CharacterCreationBalance balance, params int[] attributeValues)
        {
            if (attributeValues == null || attributeValues.Length != CharacterCreationBalance.AttributeCount)
            {
                throw new ArgumentException(
                    $"능력치는 정확히 {CharacterCreationBalance.AttributeCount}개를 배분해야 합니다.",
                    nameof(attributeValues));
            }

            int spentPoints = 0;
            for (int index = 0; index < attributeValues.Length; index++)
            {
                int value = attributeValues[index];
                if (value < balance.BaseValue || value > balance.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(attributeValues),
                        value,
                        $"능력치는 {balance.BaseValue}~{balance.MaxValue} 범위여야 합니다.");
                }

                spentPoints += value - balance.BaseValue;
            }

            if (spentPoints > balance.BonusPoints)
            {
                throw new ArgumentException(
                    $"배분 포인트 {spentPoints}가 허용치 {balance.BonusPoints}를 초과했습니다.",
                    nameof(attributeValues));
            }
        }

        private static int FindNextWeightedIndex(int[] weights, int[] allocatedPoints, int capacity)
        {
            int selectedIndex = -1;
            for (int index = 0; index < weights.Length; index++)
            {
                if (allocatedPoints[index] >= capacity)
                    continue;
                if (selectedIndex < 0 || IsHigherPriority(index, selectedIndex, weights, allocatedPoints))
                    selectedIndex = index;
            }

            return selectedIndex;
        }

        private static bool IsHigherPriority(
            int candidateIndex,
            int selectedIndex,
            int[] weights,
            int[] allocatedPoints)
        {
            long candidateScore = (long)weights[candidateIndex] * (allocatedPoints[selectedIndex] + 1);
            long selectedScore = (long)weights[selectedIndex] * (allocatedPoints[candidateIndex] + 1);
            return candidateScore > selectedScore;
        }
    }
}
