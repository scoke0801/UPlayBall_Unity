namespace Baseball.Core.Players
{
    /// <summary>
    /// 새 게임 캐릭터 생성에서 능력치를 배분할 때 적용하는 상한 계수를 보관한다.
    /// </summary>
    public readonly struct CharacterCreationBalance
    {
        public const int AttributeCount = 6;

        /// <summary>
        /// 배분 기준값·추가 포인트·항목별 상한을 생성한다.
        /// </summary>
        public CharacterCreationBalance(int baseValue, int bonusPoints, int maxValue)
        {
            if (baseValue < AttributeRating.Minimum || baseValue > AttributeRating.Maximum)
                throw new System.ArgumentOutOfRangeException(nameof(baseValue));
            if (bonusPoints < 0)
                throw new System.ArgumentOutOfRangeException(nameof(bonusPoints));
            if (maxValue < baseValue || maxValue > AttributeRating.Maximum)
                throw new System.ArgumentOutOfRangeException(nameof(maxValue));

            BaseValue = baseValue;
            BonusPoints = bonusPoints;
            MaxValue = maxValue;
        }

        public int BaseValue { get; }
        public int BonusPoints { get; }
        public int MaxValue { get; }

        /// <summary>
        /// 전 능력치 50에서 시작해 60포인트를 분배하고 항목당 75를 넘지 못하게 한다.
        /// 균형형의 평균을 Rookie 기준 60에 맞추면서 특화와 약점의 교환 폭은 25로 유지한다.
        /// </summary>
        public static CharacterCreationBalance CreateDefault()
        {
            return new CharacterCreationBalance(baseValue: 50, bonusPoints: 60, maxValue: 75);
        }
    }
}
