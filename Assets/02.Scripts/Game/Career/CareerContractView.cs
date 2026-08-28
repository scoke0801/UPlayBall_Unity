using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Simulation.Career;

namespace Baseball.Game.Career
{
    public enum ContractNegotiationStatus
    {
        Active,
        ExpiringThisSeason,
        NegotiationAvailable,
        ExtensionOfferAvailable,
        CurrentTeamOfferAvailable,
        OffersAvailable
    }

    /// <summary>
    /// 계약 화면의 현재 계약 요약이다.
    /// </summary>
    public readonly struct CurrentContractView
    {
        public CurrentContractView(
            string teamName,
            int signedYear,
            int endYear,
            int contractYears,
            int remainingSeasons,
            long signingBonus,
            long annualSalary,
            long guaranteedValue,
            ExpectedRole expectedRole)
        {
            TeamName = teamName;
            SignedYear = signedYear;
            EndYear = endYear;
            ContractYears = contractYears;
            RemainingSeasons = remainingSeasons;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            GuaranteedValue = guaranteedValue;
            ExpectedRole = expectedRole;
        }

        public string TeamName { get; }
        public int SignedYear { get; }
        public int EndYear { get; }
        public int ContractYears { get; }
        public int RemainingSeasons { get; }
        public long SigningBonus { get; }
        public long AnnualSalary { get; }
        public long GuaranteedValue { get; }
        public ExpectedRole ExpectedRole { get; }
    }

    /// <summary>
    /// 계약 이력 표 한 줄이다.
    /// </summary>
    public readonly struct ContractHistoryView
    {
        public ContractHistoryView(
            string teamName,
            int signedYear,
            int endYear,
            int contractYears,
            long annualSalary,
            long guaranteedValue,
            ExpectedRole expectedRole,
            bool isCurrent)
        {
            TeamName = teamName;
            SignedYear = signedYear;
            EndYear = endYear;
            ContractYears = contractYears;
            AnnualSalary = annualSalary;
            GuaranteedValue = guaranteedValue;
            ExpectedRole = expectedRole;
            IsCurrent = isCurrent;
        }

        public string TeamName { get; }
        public int SignedYear { get; }
        public int EndYear { get; }
        public int ContractYears { get; }
        public long AnnualSalary { get; }
        public long GuaranteedValue { get; }
        public ExpectedRole ExpectedRole { get; }
        public bool IsCurrent { get; }
    }

    /// <summary>
    /// 계약 상여 조건의 화면용 달성 상태다.
    /// </summary>
    public readonly struct ContractBonusProgressView
    {
        public ContractBonusProgressView(ContractBonusProgress progress)
        {
            ClauseId = progress.Clause.ClauseId;
            Metric = progress.Clause.Metric;
            CurrentValue = progress.CurrentValue;
            TargetValue = progress.Clause.TargetValue;
            Reward = progress.Clause.Reward;
            NormalizedProgress = progress.NormalizedProgress;
            IsCompleted = progress.IsCompleted;
            HasSample = progress.HasSample;
        }

        public string ClauseId { get; }
        public ContractBonusMetric Metric { get; }
        public double CurrentValue { get; }
        public double TargetValue { get; }
        public long Reward { get; }
        public double NormalizedProgress { get; }
        public bool IsCompleted { get; }
        public bool HasSample { get; }
    }

    /// <summary>
    /// 계약 만료 후 선택 가능한 한 구단의 제안이다.
    /// </summary>
    public readonly struct RenewalContractOfferView
    {
        public RenewalContractOfferView(
            int teamId,
            string teamName,
            LeagueLevel leagueLevel,
            TeamColor teamColor,
            int positionNeed,
            int developmentRating,
            long signingBonus,
            long annualSalary,
            int contractYears,
            ExpectedRole expectedRole,
            ContractOfferChannel channel,
            double estimatedPlayingTime,
            bool isSelected)
        {
            TeamId = teamId;
            TeamName = teamName;
            LeagueLevel = leagueLevel;
            TeamColor = teamColor;
            PositionNeed = positionNeed;
            DevelopmentRating = developmentRating;
            SigningBonus = signingBonus;
            AnnualSalary = annualSalary;
            ContractYears = contractYears;
            ExpectedRole = expectedRole;
            Channel = channel;
            EstimatedPlayingTime = estimatedPlayingTime;
            IsSelected = isSelected;
        }

        public int TeamId { get; }
        public string TeamName { get; }
        public LeagueLevel LeagueLevel { get; }
        public TeamColor TeamColor { get; }
        public int PositionNeed { get; }
        public int DevelopmentRating { get; }
        public long SigningBonus { get; }
        public long AnnualSalary { get; }
        public int ContractYears { get; }
        public ExpectedRole ExpectedRole { get; }
        public ContractOfferChannel Channel { get; }
        public double EstimatedPlayingTime { get; }
        public bool IsSelected { get; }
        public long GuaranteedValue => SigningBonus + AnnualSalary * ContractYears;
    }

    /// <summary>
    /// 계약 탭이 한 번의 Render에서 소비할 읽기 전용 값 모음이다.
    /// </summary>
    public sealed class CareerContractView
    {
        public string PlayerName { get; internal set; }
        public int Age { get; internal set; }
        public PlayerPosition Position { get; internal set; }
        public int Overall { get; internal set; }
        public int SeasonYear { get; internal set; }
        public LeagueLevel LeagueLevel { get; internal set; }
        public SeasonPhase SeasonPhase { get; internal set; }
        public long AvailableMoney { get; internal set; }
        public CurrentContractView CurrentContract { get; internal set; }
        public ContractHistoryView[] ContractHistory { get; internal set; }
        public ContractBonusProgressView[] BonusProgress { get; internal set; }
        public long AchievedBonus { get; internal set; }
        public long MaximumBonus { get; internal set; }
        public long MarketSalaryMinimum { get; internal set; }
        public long MarketSalaryMaximum { get; internal set; }
        public int MarketOfferCount { get; internal set; }
        public ExpectedRole MarketExpectedRole { get; internal set; }
        public int CurrentTeamPositionNeed { get; internal set; }
        public ContractNegotiationStatus NegotiationStatus { get; internal set; }
        public RenewalContractOfferView[] RenewalOffers { get; internal set; }
        public RenewalContractOfferView? ExtensionOffer { get; internal set; }
        public bool CanBeginNegotiation { get; internal set; }
        public bool CanAcceptExtension { get; internal set; }
        public bool CanSignSelectedOffer { get; internal set; }
        public bool CanOpenMarket { get; internal set; }
        public bool IsCurrentTeamOfferHeld { get; internal set; }
        public string LastError { get; internal set; }
    }
}
