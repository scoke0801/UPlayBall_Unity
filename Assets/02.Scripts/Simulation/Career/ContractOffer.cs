using Baseball.Core.Teams;

namespace Baseball.Simulation.Career
{
    /// <summary>
    /// 같은 계약 조건이라도 제안이 도착한 커리어 경로를 구분한다.
    /// </summary>
    public enum ContractOfferChannel
    {
        RookieEntry,
        CurrentTeamRenewal,
        CurrentTeamExtension,
        OpenMarket,
        Promotion,
        DevelopmentFallback
    }

    /// <summary>
    /// 새 게임 입단부터 재계약·공개 시장까지 한 구단의 계약 조건을 보관한다.
    /// </summary>
    public readonly struct ContractOffer
    {
        /// <summary>
        /// 오퍼 결과를 생성한다.
        /// </summary>
        public ContractOffer(
            GeneratedTeam team,
            long signingBonus,
            long annualSalary,
            ExpectedRole expectedRole,
            double offerScore)
            : this(
                team,
                signingBonus,
                annualSalary,
                expectedRole,
                offerScore,
                contractYears: 3,
                ContractOfferChannel.RookieEntry,
                estimatedPlayingTime: 0d,
                hasTradeProtection: false)
        {
        }

        /// <summary>
        /// 계약 기간까지 포함한 오퍼 결과를 생성한다.
        /// </summary>
        public ContractOffer(
            GeneratedTeam team,
            long signingBonus,
            long annualSalary,
            ExpectedRole expectedRole,
            double offerScore,
            int contractYears)
            : this(
                team,
                signingBonus,
                annualSalary,
                expectedRole,
                offerScore,
                contractYears,
                ContractOfferChannel.RookieEntry,
                estimatedPlayingTime: 0d,
                hasTradeProtection: false)
        {
        }

        /// <summary>
        /// 제안 경로와 예상 출장 비율까지 포함한 계약 결과를 생성한다.
        /// </summary>
        public ContractOffer(
            GeneratedTeam team,
            long signingBonus,
            long annualSalary,
            ExpectedRole expectedRole,
            double offerScore,
            int contractYears,
            ContractOfferChannel channel,
            double estimatedPlayingTime,
            bool hasTradeProtection)
        {
            if (team == null)
                throw new System.ArgumentNullException(nameof(team));
            if (contractYears <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(contractYears));
            if (estimatedPlayingTime < 0d || estimatedPlayingTime > 1d)
                throw new System.ArgumentOutOfRangeException(nameof(estimatedPlayingTime));

            Team = team;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            ExpectedRole = expectedRole;
            OfferScore = offerScore;
            ContractYears = contractYears;
            Channel = channel;
            EstimatedPlayingTime = estimatedPlayingTime;
            HasTradeProtection = hasTradeProtection;
        }

        public GeneratedTeam Team { get; }
        public long SigningBonus { get; }
        public long AnnualSalary { get; }
        public ExpectedRole ExpectedRole { get; }
        public double OfferScore { get; }
        public int ContractYears { get; }
        public ContractOfferChannel Channel { get; }
        public double EstimatedPlayingTime { get; }
        public bool HasTradeProtection { get; }

        /// <summary>
        /// 같은 조건을 유지한 채 계약 시장 경로만 명시적으로 바꾼다.
        /// </summary>
        public ContractOffer WithChannel(ContractOfferChannel channel)
        {
            return new ContractOffer(
                Team,
                SigningBonus,
                AnnualSalary,
                ExpectedRole,
                OfferScore,
                ContractYears,
                channel,
                EstimatedPlayingTime,
                HasTradeProtection);
        }
    }
}
