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
        Rehabilitation,
        DevelopmentFallback,
        ContractContinuation,
        TryoutContract
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
                hasTradeProtection: false,
                hasUpperLeagueReleaseClause: true,
                upperLeagueReleaseCompensation: annualSalary)
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
                hasTradeProtection: false,
                hasUpperLeagueReleaseClause: true,
                upperLeagueReleaseCompensation: annualSalary)
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
            bool hasTradeProtection,
            bool hasUpperLeagueReleaseClause = false,
            long upperLeagueReleaseCompensation = 0L,
            bool hasRelegationTransferRequestClause = false)
        {
            if (team == null)
                throw new System.ArgumentNullException(nameof(team));
            if (contractYears <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(contractYears));
            if (estimatedPlayingTime < 0d || estimatedPlayingTime > 1d)
                throw new System.ArgumentOutOfRangeException(nameof(estimatedPlayingTime));
            if (upperLeagueReleaseCompensation < 0L)
                throw new System.ArgumentOutOfRangeException(nameof(upperLeagueReleaseCompensation));
            if (!hasUpperLeagueReleaseClause && upperLeagueReleaseCompensation > 0L)
                throw new System.ArgumentException("상위 리그 이적 조항 없이 보상금을 지정할 수 없습니다.");

            Team = team;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            ExpectedRole = expectedRole;
            OfferScore = offerScore;
            ContractYears = contractYears;
            Channel = channel;
            EstimatedPlayingTime = estimatedPlayingTime;
            HasTradeProtection = hasTradeProtection;
            HasUpperLeagueReleaseClause = hasUpperLeagueReleaseClause;
            UpperLeagueReleaseCompensation = upperLeagueReleaseCompensation;
            HasRelegationTransferRequestClause = hasRelegationTransferRequestClause;
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
        public bool HasUpperLeagueReleaseClause { get; }
        public long UpperLeagueReleaseCompensation { get; }
        public bool HasRelegationTransferRequestClause { get; }

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
                HasTradeProtection,
                HasUpperLeagueReleaseClause,
                UpperLeagueReleaseCompensation,
                HasRelegationTransferRequestClause);
        }

        /// <summary>금액·역할을 유지한 채 리그 이동 관련 계약 조항만 바꾼다.</summary>
        public ContractOffer WithMovementClauses(
            bool hasUpperLeagueReleaseClause,
            long upperLeagueReleaseCompensation,
            bool hasRelegationTransferRequestClause)
        {
            return new ContractOffer(
                Team,
                SigningBonus,
                AnnualSalary,
                ExpectedRole,
                OfferScore,
                ContractYears,
                Channel,
                EstimatedPlayingTime,
                HasTradeProtection,
                hasUpperLeagueReleaseClause,
                upperLeagueReleaseCompensation,
                hasRelegationTransferRequestClause);
        }
    }
}
