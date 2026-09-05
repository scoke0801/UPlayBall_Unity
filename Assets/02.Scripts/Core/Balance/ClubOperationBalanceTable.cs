using System;
using System.Collections.Generic;
using Baseball.Core.Growth;
using Baseball.Core.Historical;

namespace Baseball.Core.Balance
{
    /// <summary>FanBase와 설명 변수들을 관중 수요로 변환하는 데이터 계약이다.</summary>
    public sealed class AttendanceBalanceDefinition
    {
        public AttendanceBalanceDefinition(
            double minimumBaseDemand,
            double maximumBaseDemand,
            double minimumPopularityFactor,
            double maximumPopularityFactor,
            double minimumRecentPerformanceFactor,
            double maximumRecentPerformanceFactor,
            double minimumOpponentAttractionFactor,
            double maximumOpponentAttractionFactor,
            double minimumSeasonImportanceFactor,
            double maximumSeasonImportanceFactor,
            double minimumMomentumFactor,
            double maximumMomentumFactor,
            double rivalryAttractionWeight,
            double varianceMinimum,
            double varianceMaximum)
        {
            ValidateNonNegativeFinite(minimumBaseDemand, nameof(minimumBaseDemand));
            ValidateNonNegativeFinite(maximumBaseDemand, nameof(maximumBaseDemand));
            if (maximumBaseDemand < minimumBaseDemand)
                throw new ArgumentOutOfRangeException(nameof(maximumBaseDemand));
            ValidateFactorRange(minimumPopularityFactor, maximumPopularityFactor, nameof(minimumPopularityFactor));
            ValidateFactorRange(
                minimumRecentPerformanceFactor,
                maximumRecentPerformanceFactor,
                nameof(minimumRecentPerformanceFactor));
            ValidateFactorRange(
                minimumOpponentAttractionFactor,
                maximumOpponentAttractionFactor,
                nameof(minimumOpponentAttractionFactor));
            ValidateFactorRange(
                minimumSeasonImportanceFactor,
                maximumSeasonImportanceFactor,
                nameof(minimumSeasonImportanceFactor));
            ValidateFactorRange(minimumMomentumFactor, maximumMomentumFactor, nameof(minimumMomentumFactor));
            ValidateNonNegativeFinite(rivalryAttractionWeight, nameof(rivalryAttractionWeight));
            if (rivalryAttractionWeight > 1d)
                throw new ArgumentOutOfRangeException(nameof(rivalryAttractionWeight));
            if (varianceMinimum <= 0d || varianceMaximum < varianceMinimum ||
                double.IsNaN(varianceMinimum) || double.IsNaN(varianceMaximum) ||
                double.IsInfinity(varianceMinimum) || double.IsInfinity(varianceMaximum))
                throw new ArgumentOutOfRangeException(nameof(varianceMinimum));

            MinimumBaseDemand = minimumBaseDemand;
            MaximumBaseDemand = maximumBaseDemand;
            MinimumPopularityFactor = minimumPopularityFactor;
            MaximumPopularityFactor = maximumPopularityFactor;
            MinimumRecentPerformanceFactor = minimumRecentPerformanceFactor;
            MaximumRecentPerformanceFactor = maximumRecentPerformanceFactor;
            MinimumOpponentAttractionFactor = minimumOpponentAttractionFactor;
            MaximumOpponentAttractionFactor = maximumOpponentAttractionFactor;
            MinimumSeasonImportanceFactor = minimumSeasonImportanceFactor;
            MaximumSeasonImportanceFactor = maximumSeasonImportanceFactor;
            MinimumMomentumFactor = minimumMomentumFactor;
            MaximumMomentumFactor = maximumMomentumFactor;
            RivalryAttractionWeight = rivalryAttractionWeight;
            VarianceMinimum = varianceMinimum;
            VarianceMaximum = varianceMaximum;
        }

        public double MinimumBaseDemand { get; }
        public double MaximumBaseDemand { get; }
        public double MinimumPopularityFactor { get; }
        public double MaximumPopularityFactor { get; }
        public double MinimumRecentPerformanceFactor { get; }
        public double MaximumRecentPerformanceFactor { get; }
        public double MinimumOpponentAttractionFactor { get; }
        public double MaximumOpponentAttractionFactor { get; }
        public double MinimumSeasonImportanceFactor { get; }
        public double MaximumSeasonImportanceFactor { get; }
        public double MinimumMomentumFactor { get; }
        public double MaximumMomentumFactor { get; }
        public double RivalryAttractionWeight { get; }
        public double VarianceMinimum { get; }
        public double VarianceMaximum { get; }

        private static void ValidateFactorRange(double minimum, double maximum, string parameterName)
        {
            if (minimum <= 0d || maximum < minimum || double.IsNaN(minimum) || double.IsNaN(maximum) ||
                double.IsInfinity(minimum) || double.IsInfinity(maximum))
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static void ValidateNonNegativeFinite(double value, string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>티켓 단계별 수요 배율과 관중 1인당 수익을 데이터로 보관한다.</summary>
    public sealed class TicketPolicyDefinition
    {
        public TicketPolicyDefinition(TicketPriceTier priceTier, double demandMultiplier, long revenuePerAttendee)
        {
            if (!Enum.IsDefined(typeof(TicketPriceTier), priceTier))
                throw new ArgumentOutOfRangeException(nameof(priceTier));
            if (demandMultiplier <= 0d || double.IsNaN(demandMultiplier) || double.IsInfinity(demandMultiplier))
                throw new ArgumentOutOfRangeException(nameof(demandMultiplier));
            if (revenuePerAttendee < 0L)
                throw new ArgumentOutOfRangeException(nameof(revenuePerAttendee));
            PriceTier = priceTier;
            DemandMultiplier = demandMultiplier;
            RevenuePerAttendee = revenuePerAttendee;
        }

        public TicketPriceTier PriceTier { get; }
        public double DemandMultiplier { get; }
        public long RevenuePerAttendee { get; }
    }

    /// <summary>리그별 관중 잠재력과 운영비 규모를 데이터로 보관한다.</summary>
    public sealed class LeagueOperationDefinition
    {
        public LeagueOperationDefinition(
            LeagueGrade leagueGrade,
            double demandMultiplier,
            double operatingCostMultiplier)
        {
            if (!Enum.IsDefined(typeof(LeagueGrade), leagueGrade))
                throw new ArgumentOutOfRangeException(nameof(leagueGrade));
            if (demandMultiplier <= 0d || operatingCostMultiplier <= 0d ||
                double.IsNaN(demandMultiplier) || double.IsInfinity(demandMultiplier) ||
                double.IsNaN(operatingCostMultiplier) || double.IsInfinity(operatingCostMultiplier))
                throw new ArgumentOutOfRangeException(nameof(demandMultiplier));
            LeagueGrade = leagueGrade;
            DemandMultiplier = demandMultiplier;
            OperatingCostMultiplier = operatingCostMultiplier;
        }

        public LeagueGrade LeagueGrade { get; }
        public double DemandMultiplier { get; }
        public double OperatingCostMultiplier { get; }
    }

    /// <summary>홈 경기의 공통 부대수익과 경기일 운영비를 보관한다.</summary>
    public sealed class HomeGameFinanceBalanceDefinition
    {
        public HomeGameFinanceBalanceDefinition(long otherRevenuePerAttendee, long baseGameDayOperatingCost)
        {
            if (otherRevenuePerAttendee < 0L || baseGameDayOperatingCost < 0L)
                throw new ArgumentOutOfRangeException(nameof(otherRevenuePerAttendee));
            OtherRevenuePerAttendee = otherRevenuePerAttendee;
            BaseGameDayOperatingCost = baseGameDayOperatingCost;
        }

        public long OtherRevenuePerAttendee { get; }
        public long BaseGameDayOperatingCost { get; }
    }

    /// <summary>한 경기 결과와 점유율을 팬 지표 변화량으로 변환하는 계수를 보관한다.</summary>
    public sealed class FanPopularityBalanceDefinition
    {
        public FanPopularityBalanceDefinition(
            double winFanBaseDelta,
            double drawFanBaseDelta,
            double lossFanBaseDelta,
            double winPopularityDelta,
            double drawPopularityDelta,
            double lossPopularityDelta,
            double seasonImportanceOutcomeScale,
            double attendanceFanBaseDeltaAtEmpty,
            double attendanceFanBaseDeltaAtFull,
            double popularityDecayPerHomeGame,
            double momentumTargetCapacityRate,
            double momentumDeltaScale)
        {
            ValidateFinite(winFanBaseDelta, nameof(winFanBaseDelta));
            ValidateFinite(drawFanBaseDelta, nameof(drawFanBaseDelta));
            ValidateFinite(lossFanBaseDelta, nameof(lossFanBaseDelta));
            ValidateFinite(winPopularityDelta, nameof(winPopularityDelta));
            ValidateFinite(drawPopularityDelta, nameof(drawPopularityDelta));
            ValidateFinite(lossPopularityDelta, nameof(lossPopularityDelta));
            ValidateFinite(attendanceFanBaseDeltaAtEmpty, nameof(attendanceFanBaseDeltaAtEmpty));
            ValidateFinite(attendanceFanBaseDeltaAtFull, nameof(attendanceFanBaseDeltaAtFull));
            if (seasonImportanceOutcomeScale < 0d || popularityDecayPerHomeGame < 0d ||
                momentumDeltaScale < 0d ||
                double.IsNaN(seasonImportanceOutcomeScale) || double.IsInfinity(seasonImportanceOutcomeScale) ||
                double.IsNaN(popularityDecayPerHomeGame) || double.IsInfinity(popularityDecayPerHomeGame) ||
                double.IsNaN(momentumDeltaScale) || double.IsInfinity(momentumDeltaScale))
                throw new ArgumentOutOfRangeException(nameof(seasonImportanceOutcomeScale));
            if (momentumTargetCapacityRate < 0d || momentumTargetCapacityRate > 1d ||
                double.IsNaN(momentumTargetCapacityRate))
                throw new ArgumentOutOfRangeException(nameof(momentumTargetCapacityRate));

            WinFanBaseDelta = winFanBaseDelta;
            DrawFanBaseDelta = drawFanBaseDelta;
            LossFanBaseDelta = lossFanBaseDelta;
            WinPopularityDelta = winPopularityDelta;
            DrawPopularityDelta = drawPopularityDelta;
            LossPopularityDelta = lossPopularityDelta;
            SeasonImportanceOutcomeScale = seasonImportanceOutcomeScale;
            AttendanceFanBaseDeltaAtEmpty = attendanceFanBaseDeltaAtEmpty;
            AttendanceFanBaseDeltaAtFull = attendanceFanBaseDeltaAtFull;
            PopularityDecayPerHomeGame = popularityDecayPerHomeGame;
            MomentumTargetCapacityRate = momentumTargetCapacityRate;
            MomentumDeltaScale = momentumDeltaScale;
        }

        public double WinFanBaseDelta { get; }
        public double DrawFanBaseDelta { get; }
        public double LossFanBaseDelta { get; }
        public double WinPopularityDelta { get; }
        public double DrawPopularityDelta { get; }
        public double LossPopularityDelta { get; }
        public double SeasonImportanceOutcomeScale { get; }
        public double AttendanceFanBaseDeltaAtEmpty { get; }
        public double AttendanceFanBaseDeltaAtFull { get; }
        public double PopularityDecayPerHomeGame { get; }
        public double MomentumTargetCapacityRate { get; }
        public double MomentumDeltaScale { get; }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>시설 한 레벨의 비용·생산·연결 시스템 Modifier를 데이터로 보관한다.</summary>
    public sealed class FacilityLevelDefinition
    {
        public FacilityLevelDefinition(
            FacilityType type,
            int level,
            long upgradeMoneyCost,
            long weeklyOperatingCost,
            long homeGameOperatingCost,
            LeagueGrade? requiredLeagueGrade,
            double minimumFanBase,
            long minimumSeasonAttendance,
            int weeklyScoutingPointProduction,
            int? scoutingPointStorageCapacity,
            int weeklyDevelopmentPointProduction,
            int? developmentPointStorageCapacity,
            double conditionRecoveryEfficiencyModifier,
            double scoutingConfidenceModifier,
            double tacticResearchEfficiencyModifier,
            long fanShopRevenuePerAttendee,
            double fanShopPopularityRetention)
        {
            if (!Enum.IsDefined(typeof(FacilityType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (level < 0 || upgradeMoneyCost < 0L || weeklyOperatingCost < 0L ||
                homeGameOperatingCost < 0L || minimumSeasonAttendance < 0L ||
                weeklyScoutingPointProduction < 0 || weeklyDevelopmentPointProduction < 0)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (requiredLeagueGrade.HasValue &&
                !Enum.IsDefined(typeof(LeagueGrade), requiredLeagueGrade.Value))
                throw new ArgumentOutOfRangeException(nameof(requiredLeagueGrade));
            if (minimumFanBase < 0d || minimumFanBase > 100d || double.IsNaN(minimumFanBase))
                throw new ArgumentOutOfRangeException(nameof(minimumFanBase));
            if (scoutingPointStorageCapacity < 0 || developmentPointStorageCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPointStorageCapacity));
            ValidateNonNegativeFinite(
                conditionRecoveryEfficiencyModifier,
                nameof(conditionRecoveryEfficiencyModifier));
            ValidateNonNegativeFinite(scoutingConfidenceModifier, nameof(scoutingConfidenceModifier));
            ValidateNonNegativeFinite(tacticResearchEfficiencyModifier, nameof(tacticResearchEfficiencyModifier));
            ValidateNonNegativeFinite(fanShopPopularityRetention, nameof(fanShopPopularityRetention));
            if (fanShopRevenuePerAttendee < 0L)
                throw new ArgumentOutOfRangeException(nameof(fanShopRevenuePerAttendee));
            if (level == 0 && upgradeMoneyCost != 0L)
                throw new ArgumentException("레벨 0 시설에는 업그레이드 취득 비용을 둘 수 없습니다.");

            ValidateRoleIsolation(
                type,
                weeklyScoutingPointProduction,
                scoutingPointStorageCapacity,
                weeklyDevelopmentPointProduction,
                developmentPointStorageCapacity,
                conditionRecoveryEfficiencyModifier,
                scoutingConfidenceModifier,
                tacticResearchEfficiencyModifier,
                fanShopRevenuePerAttendee,
                fanShopPopularityRetention);

            Type = type;
            Level = level;
            UpgradeMoneyCost = upgradeMoneyCost;
            WeeklyOperatingCost = weeklyOperatingCost;
            HomeGameOperatingCost = homeGameOperatingCost;
            RequiredLeagueGrade = requiredLeagueGrade;
            MinimumFanBase = minimumFanBase;
            MinimumSeasonAttendance = minimumSeasonAttendance;
            WeeklyScoutingPointProduction = weeklyScoutingPointProduction;
            ScoutingPointStorageCapacity = scoutingPointStorageCapacity;
            WeeklyDevelopmentPointProduction = weeklyDevelopmentPointProduction;
            DevelopmentPointStorageCapacity = developmentPointStorageCapacity;
            ConditionRecoveryEfficiencyModifier = conditionRecoveryEfficiencyModifier;
            ScoutingConfidenceModifier = scoutingConfidenceModifier;
            TacticResearchEfficiencyModifier = tacticResearchEfficiencyModifier;
            FanShopRevenuePerAttendee = fanShopRevenuePerAttendee;
            FanShopPopularityRetention = fanShopPopularityRetention;
        }

        public FacilityType Type { get; }
        public int Level { get; }
        public long UpgradeMoneyCost { get; }
        public long WeeklyOperatingCost { get; }
        public long HomeGameOperatingCost { get; }
        public LeagueGrade? RequiredLeagueGrade { get; }
        public double MinimumFanBase { get; }
        public long MinimumSeasonAttendance { get; }
        public int WeeklyScoutingPointProduction { get; }
        public int? ScoutingPointStorageCapacity { get; }
        public int WeeklyDevelopmentPointProduction { get; }
        public int? DevelopmentPointStorageCapacity { get; }
        public double ConditionRecoveryEfficiencyModifier { get; }
        public double ScoutingConfidenceModifier { get; }
        public double TacticResearchEfficiencyModifier { get; }
        public long FanShopRevenuePerAttendee { get; }
        public double FanShopPopularityRetention { get; }

        private static void ValidateRoleIsolation(
            FacilityType type,
            int scoutingProduction,
            int? scoutingCapacity,
            int developmentProduction,
            int? developmentCapacity,
            double recoveryModifier,
            double confidenceModifier,
            double tacticModifier,
            long fanShopRevenue,
            double popularityRetention)
        {
            bool hasScouting = scoutingProduction != 0 || scoutingCapacity.HasValue;
            bool hasDevelopment = developmentProduction != 0 || developmentCapacity.HasValue;
            bool hasRecovery = recoveryModifier != 0d;
            bool hasConfidence = confidenceModifier != 0d;
            bool hasTactic = tacticModifier != 0d;
            bool hasFanShop = fanShopRevenue != 0L || popularityRetention != 0d;

            if (type != FacilityType.ScoutingCenter && hasScouting ||
                type != FacilityType.TrainingCenter && hasDevelopment ||
                type != FacilityType.RecoveryCenter && hasRecovery ||
                type != FacilityType.DataAnalysisCenter && hasConfidence ||
                type != FacilityType.TacticLab && hasTactic ||
                type != FacilityType.FanShop && hasFanShop)
                throw new ArgumentException("시설 효과는 해당 FacilityType의 기존 시스템 경계에만 둘 수 있습니다.");
        }

        private static void ValidateNonNegativeFinite(double value, string parameterName)
        {
            if (value < 0d || double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>구장 한 레벨의 수용력·비용·해금 요구조건을 데이터로 보관한다.</summary>
    public sealed class StadiumLevelDefinition
    {
        public StadiumLevelDefinition(
            int level,
            int capacity,
            long upgradeMoneyCost,
            long homeGameOperatingCost,
            LeagueGrade? requiredLeagueGrade,
            double minimumFanBase,
            long minimumSeasonAttendance)
        {
            if (level <= 0 || capacity <= 0 || upgradeMoneyCost < 0L ||
                homeGameOperatingCost < 0L || minimumSeasonAttendance < 0L)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (requiredLeagueGrade.HasValue &&
                !Enum.IsDefined(typeof(LeagueGrade), requiredLeagueGrade.Value))
                throw new ArgumentOutOfRangeException(nameof(requiredLeagueGrade));
            if (minimumFanBase < 0d || minimumFanBase > 100d || double.IsNaN(minimumFanBase))
                throw new ArgumentOutOfRangeException(nameof(minimumFanBase));
            if (level == 1 && upgradeMoneyCost != 0L)
                throw new ArgumentException("구장 레벨 1에는 증축 취득 비용을 둘 수 없습니다.");
            Level = level;
            Capacity = capacity;
            UpgradeMoneyCost = upgradeMoneyCost;
            HomeGameOperatingCost = homeGameOperatingCost;
            RequiredLeagueGrade = requiredLeagueGrade;
            MinimumFanBase = minimumFanBase;
            MinimumSeasonAttendance = minimumSeasonAttendance;
        }

        public int Level { get; }
        public int Capacity { get; }
        public long UpgradeMoneyCost { get; }
        public long HomeGameOperatingCost { get; }
        public LeagueGrade? RequiredLeagueGrade { get; }
        public double MinimumFanBase { get; }
        public long MinimumSeasonAttendance { get; }
    }

    /// <summary>구단 운영 Resolver가 소비하는 모든 조정 가능 수치를 한곳에 보관한다.</summary>
    public sealed class ClubOperationBalanceTable
    {
        private readonly TicketPolicyDefinition[] _ticketPolicies;
        private readonly LeagueOperationDefinition[] _leagueOperations;
        private readonly FacilityLevelDefinition[] _facilityLevels;
        private readonly StadiumLevelDefinition[] _stadiumLevels;

        public ClubOperationBalanceTable(
            AttendanceBalanceDefinition attendance,
            HomeGameFinanceBalanceDefinition homeGameFinance,
            FanPopularityBalanceDefinition fanPopularity,
            IReadOnlyList<TicketPolicyDefinition> ticketPolicies,
            IReadOnlyList<LeagueOperationDefinition> leagueOperations,
            IReadOnlyList<FacilityLevelDefinition> facilityLevels,
            IReadOnlyList<StadiumLevelDefinition> stadiumLevels)
        {
            Attendance = attendance ?? throw new ArgumentNullException(nameof(attendance));
            HomeGameFinance = homeGameFinance ?? throw new ArgumentNullException(nameof(homeGameFinance));
            FanPopularity = fanPopularity ?? throw new ArgumentNullException(nameof(fanPopularity));
            _ticketPolicies = CopyTicketPolicies(ticketPolicies);
            _leagueOperations = CopyLeagueOperations(leagueOperations);
            _facilityLevels = CopyFacilityLevels(facilityLevels);
            _stadiumLevels = CopyStadiumLevels(stadiumLevels);
        }

        public AttendanceBalanceDefinition Attendance { get; }
        public HomeGameFinanceBalanceDefinition HomeGameFinance { get; }
        public FanPopularityBalanceDefinition FanPopularity { get; }
        public IReadOnlyList<TicketPolicyDefinition> TicketPolicies => _ticketPolicies;
        public IReadOnlyList<LeagueOperationDefinition> LeagueOperations => _leagueOperations;
        public IReadOnlyList<FacilityLevelDefinition> FacilityLevels => _facilityLevels;
        public IReadOnlyList<StadiumLevelDefinition> StadiumLevels => _stadiumLevels;

        /// <summary>Production 데이터 에셋과 통계 Harness가 공유할 최초 운영 밸런스 기준선을 만든다.</summary>
        public static ClubOperationBalanceTable CreateInitial()
        {
            return new ClubOperationBalanceTable(
                new AttendanceBalanceDefinition(
                    2_000d,
                    18_000d,
                    0.75d,
                    1.25d,
                    0.85d,
                    1.15d,
                    0.90d,
                    1.20d,
                    0.90d,
                    1.20d,
                    0.90d,
                    1.10d,
                    0.30d,
                    0.94d,
                    1.06d),
                new HomeGameFinanceBalanceDefinition(
                    2_500L,
                    MoneyAmount.FromTenThousandWon(3_000L)),
                new FanPopularityBalanceDefinition(
                    0.04d,
                    0.01d,
                    -0.02d,
                    0.80d,
                    0.05d,
                    -0.55d,
                    0.50d,
                    -0.04d,
                    0.04d,
                    0.18d,
                    0.65d,
                    2.50d),
                CreateInitialTicketPolicies(),
                CreateInitialLeagueOperations(),
                CreateInitialFacilityLevels(),
                CreateInitialStadiumLevels());
        }

        public TicketPolicyDefinition GetTicketPolicy(TicketPriceTier priceTier)
        {
            if (!Enum.IsDefined(typeof(TicketPriceTier), priceTier))
                throw new ArgumentOutOfRangeException(nameof(priceTier));
            return _ticketPolicies[(int)priceTier];
        }

        public LeagueOperationDefinition GetLeagueOperation(LeagueGrade leagueGrade)
        {
            if (!Enum.IsDefined(typeof(LeagueGrade), leagueGrade))
                throw new ArgumentOutOfRangeException(nameof(leagueGrade));
            return _leagueOperations[(int)leagueGrade];
        }

        public FacilityLevelDefinition GetFacilityLevel(FacilityType type, int level)
        {
            if (TryGetFacilityLevel(type, level, out FacilityLevelDefinition definition))
                return definition;
            throw new ArgumentException("BalanceTable에 해당 시설 레벨이 없습니다.", nameof(level));
        }

        public bool TryGetFacilityLevel(
            FacilityType type,
            int level,
            out FacilityLevelDefinition definition)
        {
            for (int index = 0; index < _facilityLevels.Length; index++)
            {
                FacilityLevelDefinition candidate = _facilityLevels[index];
                if (candidate.Type == type && candidate.Level == level)
                {
                    definition = candidate;
                    return true;
                }
            }
            definition = null;
            return false;
        }

        public bool TryGetNextFacilityLevel(
            FacilityType type,
            int currentLevel,
            out FacilityLevelDefinition definition)
        {
            for (int index = 0; index < _facilityLevels.Length; index++)
            {
                FacilityLevelDefinition candidate = _facilityLevels[index];
                if (candidate.Type == type && candidate.Level == currentLevel + 1)
                {
                    definition = candidate;
                    return true;
                }
            }
            definition = null;
            return false;
        }

        public StadiumLevelDefinition GetStadiumLevel(int level)
        {
            if (TryGetStadiumLevel(level, out StadiumLevelDefinition definition))
                return definition;
            throw new ArgumentException("BalanceTable에 해당 구장 레벨이 없습니다.", nameof(level));
        }

        public bool TryGetStadiumLevel(int level, out StadiumLevelDefinition definition)
        {
            for (int index = 0; index < _stadiumLevels.Length; index++)
            {
                if (_stadiumLevels[index].Level == level)
                {
                    definition = _stadiumLevels[index];
                    return true;
                }
            }
            definition = null;
            return false;
        }

        public bool TryGetNextStadiumLevel(int currentLevel, out StadiumLevelDefinition definition)
        {
            for (int index = 0; index < _stadiumLevels.Length; index++)
            {
                if (_stadiumLevels[index].Level == currentLevel + 1)
                {
                    definition = _stadiumLevels[index];
                    return true;
                }
            }
            definition = null;
            return false;
        }

        private static TicketPolicyDefinition[] CopyTicketPolicies(
            IReadOnlyList<TicketPolicyDefinition> definitions)
        {
            int count = Enum.GetValues(typeof(TicketPriceTier)).Length;
            if (definitions == null || definitions.Count != count)
                throw new ArgumentException("모든 TicketPriceTier의 Definition이 필요합니다.", nameof(definitions));
            var result = new TicketPolicyDefinition[count];
            var found = new bool[count];
            for (int index = 0; index < definitions.Count; index++)
            {
                TicketPolicyDefinition definition = definitions[index]
                    ?? throw new ArgumentException("null TicketPolicyDefinition이 있습니다.", nameof(definitions));
                int tierIndex = (int)definition.PriceTier;
                if (found[tierIndex])
                    throw new ArgumentException("TicketPriceTier는 중복될 수 없습니다.", nameof(definitions));
                found[tierIndex] = true;
                result[tierIndex] = definition;
            }
            return result;
        }

        private static LeagueOperationDefinition[] CopyLeagueOperations(
            IReadOnlyList<LeagueOperationDefinition> definitions)
        {
            int count = Enum.GetValues(typeof(LeagueGrade)).Length;
            if (definitions == null || definitions.Count != count)
                throw new ArgumentException("모든 LeagueGrade의 운영 Definition이 필요합니다.", nameof(definitions));
            var result = new LeagueOperationDefinition[count];
            var found = new bool[count];
            for (int index = 0; index < definitions.Count; index++)
            {
                LeagueOperationDefinition definition = definitions[index]
                    ?? throw new ArgumentException("null LeagueOperationDefinition이 있습니다.", nameof(definitions));
                int gradeIndex = (int)definition.LeagueGrade;
                if (found[gradeIndex])
                    throw new ArgumentException("LeagueGrade는 중복될 수 없습니다.", nameof(definitions));
                found[gradeIndex] = true;
                result[gradeIndex] = definition;
            }
            return result;
        }

        private static FacilityLevelDefinition[] CopyFacilityLevels(
            IReadOnlyList<FacilityLevelDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                throw new ArgumentException("FacilityLevelDefinition이 필요합니다.", nameof(definitions));
            var result = new FacilityLevelDefinition[definitions.Count];
            for (int index = 0; index < definitions.Count; index++)
            {
                FacilityLevelDefinition definition = definitions[index]
                    ?? throw new ArgumentException("null FacilityLevelDefinition이 있습니다.", nameof(definitions));
                for (int previous = 0; previous < index; previous++)
                {
                    if (result[previous].Type == definition.Type && result[previous].Level == definition.Level)
                        throw new ArgumentException("같은 시설 타입과 레벨을 중복 정의할 수 없습니다.", nameof(definitions));
                }
                result[index] = definition;
            }

            int typeCount = Enum.GetValues(typeof(FacilityType)).Length;
            for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
            {
                int maximumLevel = -1;
                int levelCount = 0;
                for (int index = 0; index < result.Length; index++)
                {
                    if ((int)result[index].Type != typeIndex)
                        continue;
                    maximumLevel = Math.Max(maximumLevel, result[index].Level);
                    levelCount++;
                }
                if (maximumLevel < 0 || levelCount != maximumLevel + 1)
                    throw new ArgumentException("각 FacilityType은 레벨 0부터 연속해서 정의해야 합니다.", nameof(definitions));
            }
            return result;
        }

        private static StadiumLevelDefinition[] CopyStadiumLevels(
            IReadOnlyList<StadiumLevelDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0)
                throw new ArgumentException("StadiumLevelDefinition이 필요합니다.", nameof(definitions));
            var result = new StadiumLevelDefinition[definitions.Count];
            int maximumLevel = 0;
            for (int index = 0; index < definitions.Count; index++)
            {
                StadiumLevelDefinition definition = definitions[index]
                    ?? throw new ArgumentException("null StadiumLevelDefinition이 있습니다.", nameof(definitions));
                for (int previous = 0; previous < index; previous++)
                {
                    if (result[previous].Level == definition.Level)
                        throw new ArgumentException("같은 구장 레벨을 중복 정의할 수 없습니다.", nameof(definitions));
                }
                maximumLevel = Math.Max(maximumLevel, definition.Level);
                result[index] = definition;
            }
            if (maximumLevel != result.Length)
                throw new ArgumentException("구장 레벨은 1부터 연속해서 정의해야 합니다.", nameof(definitions));
            return result;
        }

        private static TicketPolicyDefinition[] CreateInitialTicketPolicies()
        {
            return new[]
            {
                new TicketPolicyDefinition(TicketPriceTier.Cheap, 1.12d, 9_000L),
                new TicketPolicyDefinition(TicketPriceTier.Standard, 1.00d, 13_000L),
                new TicketPolicyDefinition(TicketPriceTier.Premium, 0.72d, 18_000L)
            };
        }

        private static LeagueOperationDefinition[] CreateInitialLeagueOperations()
        {
            return new[]
            {
                new LeagueOperationDefinition(LeagueGrade.Rookie, 0.55d, 0.70d),
                new LeagueOperationDefinition(LeagueGrade.Minor, 0.65d, 0.78d),
                new LeagueOperationDefinition(LeagueGrade.Major, 0.75d, 0.87d),
                new LeagueOperationDefinition(LeagueGrade.World, 0.85d, 0.96d),
                new LeagueOperationDefinition(LeagueGrade.AllStar, 0.95d, 1.05d),
                new LeagueOperationDefinition(LeagueGrade.Classic, 1.05d, 1.15d),
                new LeagueOperationDefinition(LeagueGrade.Winners, 1.15d, 1.25d),
                new LeagueOperationDefinition(LeagueGrade.Champion, 1.25d, 1.36d),
                new LeagueOperationDefinition(LeagueGrade.Master, 1.38d, 1.48d),
                new LeagueOperationDefinition(LeagueGrade.Galaxy, 1.52d, 1.60d)
            };
        }

        private static FacilityLevelDefinition[] CreateInitialFacilityLevels()
        {
            return new[]
            {
                CreateScoutingCenterLevel(0, 0L, 0L, 0L, null, 0d, 0L, 0, null),
                CreateScoutingCenterLevel(1, MoneyAmount.FromTenThousandWon(50_000L), MoneyAmount.FromTenThousandWon(500L), MoneyAmount.FromTenThousandWon(40L), LeagueGrade.Rookie, 0d, 0L, 25, 250),
                CreateScoutingCenterLevel(2, MoneyAmount.FromTenThousandWon(120_000L), MoneyAmount.FromTenThousandWon(900L), MoneyAmount.FromTenThousandWon(70L), LeagueGrade.Major, 35d, 200_000L, 40, 400),
                CreateScoutingCenterLevel(3, MoneyAmount.FromTenThousandWon(240_000L), MoneyAmount.FromTenThousandWon(1_500L), MoneyAmount.FromTenThousandWon(120L), LeagueGrade.Classic, 60d, 500_000L, 60, 600),

                CreateTrainingCenterLevel(0, 0L, 0L, 0L, null, 0d, 0L, 0, null),
                CreateTrainingCenterLevel(1, MoneyAmount.FromTenThousandWon(50_000L), MoneyAmount.FromTenThousandWon(500L), MoneyAmount.FromTenThousandWon(40L), LeagueGrade.Rookie, 0d, 0L, 12, 120),
                CreateTrainingCenterLevel(2, MoneyAmount.FromTenThousandWon(120_000L), MoneyAmount.FromTenThousandWon(900L), MoneyAmount.FromTenThousandWon(70L), LeagueGrade.Major, 35d, 200_000L, 20, 200),
                CreateTrainingCenterLevel(3, MoneyAmount.FromTenThousandWon(240_000L), MoneyAmount.FromTenThousandWon(1_500L), MoneyAmount.FromTenThousandWon(120L), LeagueGrade.Classic, 60d, 500_000L, 32, 320),

                CreateModifierFacilityLevel(FacilityType.RecoveryCenter, 0, 0L, 0L, 0L, null, 0d, 0L, 0d, 0d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.RecoveryCenter, 1, MoneyAmount.FromTenThousandWon(45_000L), MoneyAmount.FromTenThousandWon(450L), MoneyAmount.FromTenThousandWon(35L), LeagueGrade.Rookie, 0d, 0L, 0.05d, 0d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.RecoveryCenter, 2, MoneyAmount.FromTenThousandWon(110_000L), MoneyAmount.FromTenThousandWon(800L), MoneyAmount.FromTenThousandWon(60L), LeagueGrade.Major, 35d, 200_000L, 0.10d, 0d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.RecoveryCenter, 3, MoneyAmount.FromTenThousandWon(220_000L), MoneyAmount.FromTenThousandWon(1_300L), MoneyAmount.FromTenThousandWon(100L), LeagueGrade.Classic, 60d, 500_000L, 0.16d, 0d, 0d, 0L, 0d),

                CreateModifierFacilityLevel(FacilityType.DataAnalysisCenter, 0, 0L, 0L, 0L, null, 0d, 0L, 0d, 0d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.DataAnalysisCenter, 1, MoneyAmount.FromTenThousandWon(45_000L), MoneyAmount.FromTenThousandWon(450L), MoneyAmount.FromTenThousandWon(35L), LeagueGrade.Rookie, 0d, 0L, 0d, 0.03d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.DataAnalysisCenter, 2, MoneyAmount.FromTenThousandWon(110_000L), MoneyAmount.FromTenThousandWon(800L), MoneyAmount.FromTenThousandWon(60L), LeagueGrade.Major, 35d, 200_000L, 0d, 0.06d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.DataAnalysisCenter, 3, MoneyAmount.FromTenThousandWon(220_000L), MoneyAmount.FromTenThousandWon(1_300L), MoneyAmount.FromTenThousandWon(100L), LeagueGrade.Classic, 60d, 500_000L, 0d, 0.10d, 0d, 0L, 0d),

                CreateModifierFacilityLevel(FacilityType.TacticLab, 0, 0L, 0L, 0L, null, 0d, 0L, 0d, 0d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.TacticLab, 1, MoneyAmount.FromTenThousandWon(40_000L), MoneyAmount.FromTenThousandWon(400L), MoneyAmount.FromTenThousandWon(30L), LeagueGrade.Rookie, 0d, 0L, 0d, 0d, 0.05d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.TacticLab, 2, MoneyAmount.FromTenThousandWon(100_000L), MoneyAmount.FromTenThousandWon(700L), MoneyAmount.FromTenThousandWon(55L), LeagueGrade.Major, 35d, 200_000L, 0d, 0d, 0.10d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.TacticLab, 3, MoneyAmount.FromTenThousandWon(200_000L), MoneyAmount.FromTenThousandWon(1_200L), MoneyAmount.FromTenThousandWon(90L), LeagueGrade.Classic, 60d, 500_000L, 0d, 0d, 0.15d, 0L, 0d),

                CreateModifierFacilityLevel(FacilityType.FanShop, 0, 0L, 0L, 0L, null, 0d, 0L, 0d, 0d, 0d, 0L, 0d),
                CreateModifierFacilityLevel(FacilityType.FanShop, 1, MoneyAmount.FromTenThousandWon(80_000L), MoneyAmount.FromTenThousandWon(350L), MoneyAmount.FromTenThousandWon(25L), LeagueGrade.Rookie, 0d, 0L, 0d, 0d, 0d, 700L, 0.03d),
                CreateModifierFacilityLevel(FacilityType.FanShop, 2, MoneyAmount.FromTenThousandWon(85_000L), MoneyAmount.FromTenThousandWon(650L), MoneyAmount.FromTenThousandWon(45L), LeagueGrade.Major, 35d, 200_000L, 0d, 0d, 0d, 1_400L, 0.06d),
                CreateModifierFacilityLevel(FacilityType.FanShop, 3, MoneyAmount.FromTenThousandWon(170_000L), MoneyAmount.FromTenThousandWon(1_000L), MoneyAmount.FromTenThousandWon(75L), LeagueGrade.Classic, 60d, 500_000L, 0d, 0d, 0d, 2_200L, 0.10d)
            };
        }

        private static StadiumLevelDefinition[] CreateInitialStadiumLevels()
        {
            return new[]
            {
                new StadiumLevelDefinition(1, 10_000, 0L, MoneyAmount.FromTenThousandWon(2_000L), null, 0d, 0L),
                new StadiumLevelDefinition(2, 15_000, MoneyAmount.FromTenThousandWon(300_000L), MoneyAmount.FromTenThousandWon(2_800L), LeagueGrade.Rookie, 25d, 100_000L),
                new StadiumLevelDefinition(3, 22_000, MoneyAmount.FromTenThousandWon(700_000L), MoneyAmount.FromTenThousandWon(4_000L), LeagueGrade.Major, 40d, 250_000L),
                new StadiumLevelDefinition(4, 30_000, MoneyAmount.FromTenThousandWon(1_400_000L), MoneyAmount.FromTenThousandWon(5_800L), LeagueGrade.AllStar, 58d, 500_000L),
                new StadiumLevelDefinition(5, 40_000, MoneyAmount.FromTenThousandWon(2_500_000L), MoneyAmount.FromTenThousandWon(8_000L), LeagueGrade.Champion, 75d, 900_000L)
            };
        }

        private static FacilityLevelDefinition CreateScoutingCenterLevel(
            int level,
            long upgradeCost,
            long weeklyCost,
            long homeGameCost,
            LeagueGrade? requiredGrade,
            double minimumFanBase,
            long minimumAttendance,
            int production,
            int? capacity)
        {
            return new FacilityLevelDefinition(
                FacilityType.ScoutingCenter,
                level,
                upgradeCost,
                weeklyCost,
                homeGameCost,
                requiredGrade,
                minimumFanBase,
                minimumAttendance,
                production,
                capacity,
                0,
                null,
                0d,
                0d,
                0d,
                0L,
                0d);
        }

        private static FacilityLevelDefinition CreateTrainingCenterLevel(
            int level,
            long upgradeCost,
            long weeklyCost,
            long homeGameCost,
            LeagueGrade? requiredGrade,
            double minimumFanBase,
            long minimumAttendance,
            int production,
            int? capacity)
        {
            return new FacilityLevelDefinition(
                FacilityType.TrainingCenter,
                level,
                upgradeCost,
                weeklyCost,
                homeGameCost,
                requiredGrade,
                minimumFanBase,
                minimumAttendance,
                0,
                null,
                production,
                capacity,
                0d,
                0d,
                0d,
                0L,
                0d);
        }

        private static FacilityLevelDefinition CreateModifierFacilityLevel(
            FacilityType type,
            int level,
            long upgradeCost,
            long weeklyCost,
            long homeGameCost,
            LeagueGrade? requiredGrade,
            double minimumFanBase,
            long minimumAttendance,
            double recoveryModifier,
            double confidenceModifier,
            double tacticModifier,
            long fanShopRevenue,
            double fanShopRetention)
        {
            return new FacilityLevelDefinition(
                type,
                level,
                upgradeCost,
                weeklyCost,
                homeGameCost,
                requiredGrade,
                minimumFanBase,
                minimumAttendance,
                0,
                null,
                0,
                null,
                recoveryModifier,
                confidenceModifier,
                tacticModifier,
                fanShopRevenue,
                fanShopRetention);
        }
    }
}
