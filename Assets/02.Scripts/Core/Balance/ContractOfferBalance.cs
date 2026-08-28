namespace Baseball.Core.Balance
{
    /// <summary>
    /// 새 게임에서 구단이 선수에게 계약 오퍼를 낼지, 얼마를 제시할지 계산하는 계수를 보관한다.
    /// </summary>
    public readonly struct ContractOfferBalance
    {
        /// <summary>
        /// 오퍼 평가 공식(PlayerValue × PositionNeed × TeamBudget × TeamPreference × ScoutVariance)에
        /// 쓰이는 계수를 생성한다.
        /// </summary>
        public ContractOfferBalance(
            double offerScoreThreshold,
            double scoutVarianceMinimum,
            double scoutVarianceMaximum,
            double preferredPositionBonus,
            long baseSigningBonus,
            long baseSalary)
            : this(
                offerScoreThreshold,
                scoutVarianceMinimum,
                scoutVarianceMaximum,
                preferredPositionBonus,
                baseSigningBonus,
                baseSalary,
                minimumOfferCount: 3,
                maximumOfferCount: 5,
                startingCompetitionNeed: 55,
                rosterCompetitionNeed: 40,
                ratingBaseline: 50d,
                contractYears: 3)
        {
        }

        /// <summary>
        /// 오퍼 수·예상 역할·금액 산정까지 포함한 전체 계약 밸런스를 생성한다.
        /// </summary>
        public ContractOfferBalance(
            double offerScoreThreshold,
            double scoutVarianceMinimum,
            double scoutVarianceMaximum,
            double preferredPositionBonus,
            long baseSigningBonus,
            long baseSalary,
            int minimumOfferCount,
            int maximumOfferCount,
            int startingCompetitionNeed,
            int rosterCompetitionNeed,
            double ratingBaseline,
            int contractYears)
        {
            if (minimumOfferCount <= 0 || maximumOfferCount < minimumOfferCount)
                throw new System.ArgumentOutOfRangeException(nameof(minimumOfferCount));
            if (ratingBaseline <= 0d)
                throw new System.ArgumentOutOfRangeException(nameof(ratingBaseline));
            if (contractYears <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(contractYears));

            OfferScoreThreshold = offerScoreThreshold;
            ScoutVarianceMinimum = scoutVarianceMinimum;
            ScoutVarianceMaximum = scoutVarianceMaximum;
            PreferredPositionBonus = preferredPositionBonus;
            BaseSigningBonus = baseSigningBonus;
            BaseSalary = baseSalary;
            MinimumOfferCount = minimumOfferCount;
            MaximumOfferCount = maximumOfferCount;
            StartingCompetitionNeed = startingCompetitionNeed;
            RosterCompetitionNeed = rosterCompetitionNeed;
            RatingBaseline = ratingBaseline;
            ContractYears = contractYears;
        }

        public double OfferScoreThreshold { get; }
        public double ScoutVarianceMinimum { get; }
        public double ScoutVarianceMaximum { get; }
        public double PreferredPositionBonus { get; }
        public long BaseSigningBonus { get; }
        public long BaseSalary { get; }
        public int MinimumOfferCount { get; }
        public int MaximumOfferCount { get; }
        public int StartingCompetitionNeed { get; }
        public int RosterCompetitionNeed { get; }
        public double RatingBaseline { get; }
        public int ContractYears { get; }

        /// <summary>
        /// 8개 구단 중 3~5개 정도만 오퍼를 내도록 맞춘 최초 검증용 기본값을 만든다.
        /// </summary>
        public static ContractOfferBalance CreateDefault()
        {
            return new ContractOfferBalance(
                offerScoreThreshold: 1.0d,
                scoutVarianceMinimum: 0.85d,
                scoutVarianceMaximum: 1.15d,
                preferredPositionBonus: 0.15d,
                baseSigningBonus: 20_000_000L,
                baseSalary: 30_000_000L);
        }
    }
}
