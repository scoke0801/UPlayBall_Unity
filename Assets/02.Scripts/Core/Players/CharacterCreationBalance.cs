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
        /// 전 능력치 40에서 시작해 72포인트를 분배하고 항목당 65를 넘지 못하게 한
        /// 최초 검증용 기본값을 만든다. 생성 시점부터 극단적인 신인이 나오지 않게 하려는 목적이다.
        /// </summary>
        public static CharacterCreationBalance CreateDefault()
        {
            return new CharacterCreationBalance(baseValue: 40, bonusPoints: 72, maxValue: 65);
        }
    }
}
