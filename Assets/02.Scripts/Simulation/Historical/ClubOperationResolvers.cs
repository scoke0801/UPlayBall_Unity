using System;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Simulation.Random;

namespace Baseball.Simulation.Historical
{
    /// <summary>설명 가능한 수요 계수와 주입된 RNG로 홈 관중을 결정한다.</summary>
    public sealed class AttendanceResolver
    {
        private readonly ClubOperationBalanceTable _balance;

        public AttendanceResolver(ClubOperationBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public AttendanceResult Resolve(
            HomeGameContext context,
            ClubOperationState operation,
            IRandomSource random)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (context.Venue != GameVenue.Home)
                throw new ArgumentException("AttendanceResolver는 홈 경기만 계산합니다.", nameof(context));
            ValidateOperationContext(context, operation);

            AttendanceBalanceDefinition attendance = _balance.Attendance;
            TicketPolicyDefinition ticket = _balance.GetTicketPolicy(operation.TicketPolicy.PriceTier);
            LeagueOperationDefinition league = _balance.GetLeagueOperation(context.LeagueGrade);

            double normalizedFanBase = operation.FanBase / ClubOperationState.MaximumNormalizedScore;
            double normalizedPopularity = operation.Popularity / ClubOperationState.MaximumNormalizedScore;
            double normalizedMomentum = operation.AttendanceMomentum / ClubOperationState.MaximumNormalizedScore;
            double opponentAttraction = ClampUnit(
                context.OpponentAttraction + context.RivalryStoryStrength * attendance.RivalryAttractionWeight);

            double expectedDemand = Lerp(
                attendance.MinimumBaseDemand,
                attendance.MaximumBaseDemand,
                normalizedFanBase);
            expectedDemand *= Lerp(
                attendance.MinimumPopularityFactor,
                attendance.MaximumPopularityFactor,
                normalizedPopularity);
            expectedDemand *= Lerp(
                attendance.MinimumRecentPerformanceFactor,
                attendance.MaximumRecentPerformanceFactor,
                context.RecentPerformance);
            expectedDemand *= Lerp(
                attendance.MinimumOpponentAttractionFactor,
                attendance.MaximumOpponentAttractionFactor,
                opponentAttraction);
            expectedDemand *= league.DemandMultiplier;
            expectedDemand *= Lerp(
                attendance.MinimumSeasonImportanceFactor,
                attendance.MaximumSeasonImportanceFactor,
                context.SeasonImportance);
            expectedDemand *= ticket.DemandMultiplier;
            expectedDemand *= Lerp(
                attendance.MinimumMomentumFactor,
                attendance.MaximumMomentumFactor,
                normalizedMomentum);

            if (double.IsNaN(expectedDemand) || double.IsInfinity(expectedDemand) || expectedDemand < 0d)
                throw new InvalidOperationException("관중 기대 수요가 유효한 범위를 벗어났습니다.");
            double randomValue = random.NextDouble();
            if (randomValue < 0d || randomValue >= 1d || double.IsNaN(randomValue))
                throw new InvalidOperationException("IRandomSource는 0 이상 1 미만의 값을 반환해야 합니다.");
            double variance = attendance.VarianceMinimum +
                              (attendance.VarianceMaximum - attendance.VarianceMinimum) * randomValue;
            double variedDemand = expectedDemand * variance;
            int resolvedAttendance = ResolveAttendanceCount(variedDemand, operation.Stadium.Capacity);
            return new AttendanceResult(expectedDemand, resolvedAttendance, operation.Stadium.Capacity);
        }

        private static void ValidateOperationContext(HomeGameContext context, ClubOperationState operation)
        {
            if (!string.Equals(context.HomeTeamSeasonKey, operation.TeamSeasonKey, StringComparison.Ordinal))
                throw new ArgumentException("홈 구단과 ClubOperationState의 TeamSeasonKey가 일치하지 않습니다.");
            if (!string.Equals(context.SeasonId, operation.CurrentSeason.SeasonId, StringComparison.Ordinal))
                throw new ArgumentException("경기와 ClubOperationState의 SeasonId가 일치하지 않습니다.");
        }

        private static int ResolveAttendanceCount(double demand, int capacity)
        {
            if (demand <= 0d)
                return 0;
            if (demand >= capacity)
                return capacity;
            return (int)Math.Round(demand, MidpointRounding.AwayFromZero);
        }

        private static double Lerp(double minimum, double maximum, double normalized)
        {
            return minimum + (maximum - minimum) * normalized;
        }

        private static double ClampUnit(double value)
        {
            if (value < 0d) return 0d;
            if (value > 1d) return 1d;
            return value;
        }
    }

    /// <summary>시설 상태를 타 시스템에 한 번 전달할 Modifier 스냅샷으로 합성한다.</summary>
    public sealed class ClubFacilityEffectResolver
    {
        private readonly ClubOperationBalanceTable _balance;

        public ClubFacilityEffectResolver(ClubOperationBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public ClubFacilityEffectProfile Resolve(ClubOperationState operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            double recovery = 0d;
            double confidence = 0d;
            double tactic = 0d;
            long fanShopRevenue = 0L;
            double fanShopRetention = 0d;
            for (int index = 0; index < operation.Facilities.Count; index++)
            {
                FacilityState facility = operation.Facilities[index];
                FacilityLevelDefinition definition = _balance.GetFacilityLevel(facility.Type, facility.Level);
                recovery += definition.ConditionRecoveryEfficiencyModifier;
                confidence += definition.ScoutingConfidenceModifier;
                tactic += definition.TacticResearchEfficiencyModifier;
                fanShopRevenue = checked(fanShopRevenue + definition.FanShopRevenuePerAttendee);
                fanShopRetention += definition.FanShopPopularityRetention;
            }
            return new ClubFacilityEffectProfile(
                recovery,
                confidence,
                tactic,
                fanShopRevenue,
                fanShopRetention);
        }
    }

    /// <summary>경기 결과·점유율·FanShop을 팬 지표 변화량으로 변환한다.</summary>
    public sealed class FanPopularityResolver
    {
        private readonly FanPopularityBalanceDefinition _balance;

        public FanPopularityResolver(FanPopularityBalanceDefinition balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public FanPopularityResult Resolve(
            HomeGameContext context,
            AttendanceResult attendance,
            ClubFacilityEffectProfile facilityEffects)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (context.Venue != GameVenue.Home)
                throw new ArgumentException("FanPopularityResolver는 홈 경기만 계산합니다.", nameof(context));

            double baseFanDelta;
            double basePopularityDelta;
            switch (context.Outcome)
            {
                case HomeGameOutcome.Win:
                    baseFanDelta = _balance.WinFanBaseDelta;
                    basePopularityDelta = _balance.WinPopularityDelta;
                    break;
                case HomeGameOutcome.Draw:
                    baseFanDelta = _balance.DrawFanBaseDelta;
                    basePopularityDelta = _balance.DrawPopularityDelta;
                    break;
                case HomeGameOutcome.Loss:
                    baseFanDelta = _balance.LossFanBaseDelta;
                    basePopularityDelta = _balance.LossPopularityDelta;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(context));
            }

            double importanceScale = 1d +
                                     context.SeasonImportance * _balance.SeasonImportanceOutcomeScale;
            double attendanceFanDelta = Lerp(
                _balance.AttendanceFanBaseDeltaAtEmpty,
                _balance.AttendanceFanBaseDeltaAtFull,
                attendance.CapacityRate);
            double momentumDelta = (attendance.CapacityRate - _balance.MomentumTargetCapacityRate) *
                                   _balance.MomentumDeltaScale;
            return new FanPopularityResult(
                baseFanDelta * importanceScale,
                attendanceFanDelta,
                basePopularityDelta * importanceScale,
                _balance.PopularityDecayPerHomeGame,
                facilityEffects.FanShopPopularityRetention,
                momentumDelta);
        }

        private static double Lerp(double minimum, double maximum, double normalized)
        {
            return minimum + (maximum - minimum) * normalized;
        }
    }

    /// <summary>홈 경기 관중을 티켓·FanShop·운영비와 팬 변화가 포함된 한 결과로 계산한다.</summary>
    public sealed class HomeGameFinanceResolver
    {
        private readonly ClubOperationBalanceTable _balance;
        private readonly AttendanceResolver _attendanceResolver;
        private readonly ClubFacilityEffectResolver _facilityEffectResolver;
        private readonly FanPopularityResolver _fanPopularityResolver;

        public HomeGameFinanceResolver(ClubOperationBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
            _attendanceResolver = new AttendanceResolver(balance);
            _facilityEffectResolver = new ClubFacilityEffectResolver(balance);
            _fanPopularityResolver = new FanPopularityResolver(balance.FanPopularity);
        }

        public HomeGameFinanceResult Resolve(
            HomeGameContext context,
            ClubOperationState operation,
            IRandomSource random)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (context.Venue != GameVenue.Home)
                return HomeGameFinanceResult.CreateNotHomeGame(context.ScheduledGameId);
            if (operation.HasReceipt(OperationReceipt.CreateHomeGameReceiptId(context.ScheduledGameId)))
                return HomeGameFinanceResult.CreateAlreadyApplied(context.ScheduledGameId);

            AttendanceResult attendance = _attendanceResolver.Resolve(context, operation, random);
            TicketPolicyDefinition ticket = _balance.GetTicketPolicy(operation.TicketPolicy.PriceTier);
            LeagueOperationDefinition league = _balance.GetLeagueOperation(context.LeagueGrade);
            ClubFacilityEffectProfile facilityEffects = _facilityEffectResolver.Resolve(operation);
            FanPopularityResult fanPopularity = _fanPopularityResolver.Resolve(
                context,
                attendance,
                facilityEffects);

            long ticketRevenue = checked((long)attendance.Attendance * ticket.RevenuePerAttendee);
            long fanShopRevenue = checked(
                (long)attendance.Attendance * facilityEffects.FanShopRevenuePerAttendee);
            long otherRevenue = checked(
                (long)attendance.Attendance * _balance.HomeGameFinance.OtherRevenuePerAttendee);
            long baseOperatingCost = CalculateHomeGameOperatingCost(operation);
            long operatingCost = MultiplyMoney(baseOperatingCost, league.OperatingCostMultiplier);
            long netIncome = checked(ticketRevenue + fanShopRevenue + otherRevenue - operatingCost);
            var receipt = new OperationReceipt(
                OperationReceipt.CreateHomeGameReceiptId(context.ScheduledGameId),
                OperationReceiptKind.HomeGameFinance,
                context.SeasonId,
                context.WeekIndex,
                context.ScheduledGameId,
                new OperationResourceDelta(netIncome, 0, 0));
            return HomeGameFinanceResult.CreateApplied(
                context.ScheduledGameId,
                attendance,
                ticketRevenue,
                fanShopRevenue,
                otherRevenue,
                operatingCost,
                fanPopularity,
                receipt);
        }

        private long CalculateHomeGameOperatingCost(ClubOperationState operation)
        {
            if (!_balance.TryGetStadiumLevel(operation.Stadium.Level, out StadiumLevelDefinition stadium) ||
                stadium.Capacity != operation.Stadium.Capacity)
                throw new InvalidOperationException("구장 상태가 현재 BalanceTable과 일치하지 않습니다.");
            long cost = checked(_balance.HomeGameFinance.BaseGameDayOperatingCost + stadium.HomeGameOperatingCost);
            for (int index = 0; index < operation.Facilities.Count; index++)
            {
                FacilityState facility = operation.Facilities[index];
                FacilityLevelDefinition definition = _balance.GetFacilityLevel(facility.Type, facility.Level);
                cost = checked(cost + definition.HomeGameOperatingCost);
            }
            return cost;
        }

        private static long MultiplyMoney(long amount, double multiplier)
        {
            double result = amount * multiplier;
            if (double.IsNaN(result) || double.IsInfinity(result) || result > long.MaxValue)
                throw new OverflowException("운영비가 long 범위를 벗어났습니다.");
            return checked((long)Math.Round(result, MidpointRounding.AwayFromZero));
        }
    }

    /// <summary>주간 시설 유지비와 SP/DP 생산량을 저장 한도까지 계산한다.</summary>
    public sealed class WeeklyFacilityProductionResolver
    {
        private readonly ClubOperationBalanceTable _balance;

        public WeeklyFacilityProductionResolver(ClubOperationBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public WeeklyFacilityProductionResult Resolve(
            ClubOperationState operation,
            WeeklyFacilityProductionContext context)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!string.Equals(context.SeasonId, operation.CurrentSeason.SeasonId, StringComparison.Ordinal))
                throw new ArgumentException("생산 Context와 ClubOperationState의 SeasonId가 일치하지 않습니다.");

            string receiptId = OperationReceipt.CreateWeeklyFacilityReceiptId(
                operation.TeamSeasonKey,
                context.SeasonId,
                context.WeekIndex);
            if (operation.HasReceipt(receiptId))
                return new WeeklyFacilityProductionResult(
                    WeeklyFacilityProductionStatus.AlreadyApplied,
                    0L,
                    0,
                    0,
                    null);

            long rawOperatingCost = 0L;
            int scoutingProduction = 0;
            int developmentProduction = 0;
            int? scoutingCapacity = null;
            int? developmentCapacity = null;
            for (int index = 0; index < operation.Facilities.Count; index++)
            {
                FacilityState facility = operation.Facilities[index];
                FacilityLevelDefinition definition = _balance.GetFacilityLevel(facility.Type, facility.Level);
                rawOperatingCost = checked(rawOperatingCost + definition.WeeklyOperatingCost);
                scoutingProduction = checked(scoutingProduction + definition.WeeklyScoutingPointProduction);
                developmentProduction = checked(
                    developmentProduction + definition.WeeklyDevelopmentPointProduction);
                scoutingCapacity = CombineCapacity(scoutingCapacity, definition.ScoutingPointStorageCapacity);
                developmentCapacity = CombineCapacity(
                    developmentCapacity,
                    definition.DevelopmentPointStorageCapacity);
            }

            LeagueOperationDefinition league = _balance.GetLeagueOperation(context.LeagueGrade);
            long operatingCost = MultiplyMoney(rawOperatingCost, league.OperatingCostMultiplier);
            if (context.CurrentMoney < operatingCost)
            {
                var suspendedReceipt = new OperationReceipt(
                    receiptId,
                    OperationReceiptKind.FacilityProduction,
                    context.SeasonId,
                    context.WeekIndex,
                    operation.TeamSeasonKey,
                    new OperationResourceDelta(0L, 0, 0));
                return new WeeklyFacilityProductionResult(
                    WeeklyFacilityProductionStatus.SuspendedForInsufficientOperatingMoney,
                    operatingCost,
                    0,
                    0,
                    suspendedReceipt);
            }

            int grantedScoutingPoints = ApplyStorageCapacity(
                context.CurrentScoutingPoints,
                scoutingProduction,
                scoutingCapacity);
            int grantedDevelopmentPoints = ApplyStorageCapacity(
                context.CurrentDevelopmentPoints,
                developmentProduction,
                developmentCapacity);
            var receipt = new OperationReceipt(
                receiptId,
                OperationReceiptKind.FacilityProduction,
                context.SeasonId,
                context.WeekIndex,
                operation.TeamSeasonKey,
                new OperationResourceDelta(-operatingCost, grantedScoutingPoints, grantedDevelopmentPoints));
            return new WeeklyFacilityProductionResult(
                WeeklyFacilityProductionStatus.Produced,
                operatingCost,
                grantedScoutingPoints,
                grantedDevelopmentPoints,
                receipt);
        }

        private static int? CombineCapacity(int? current, int? candidate)
        {
            if (!candidate.HasValue)
                return current;
            if (!current.HasValue)
                return candidate;
            return checked(current.Value + candidate.Value);
        }

        private static int ApplyStorageCapacity(int current, int production, int? capacity)
        {
            if (!capacity.HasValue)
                return production;
            if (current >= capacity.Value)
                return 0;
            return Math.Min(production, capacity.Value - current);
        }

        private static long MultiplyMoney(long amount, double multiplier)
        {
            double result = amount * multiplier;
            if (double.IsNaN(result) || double.IsInfinity(result) || result > long.MaxValue)
                throw new OverflowException("시설 유지비가 long 범위를 벗어났습니다.");
            return checked((long)Math.Round(result, MidpointRounding.AwayFromZero));
        }
    }

    /// <summary>시설과 구장 업그레이드 요구조건을 평가하고 비용 영수증을 만든다.</summary>
    public sealed class ClubUpgradeResolver
    {
        private readonly ClubOperationBalanceTable _balance;

        public ClubUpgradeResolver(ClubOperationBalanceTable balance)
        {
            _balance = balance ?? throw new ArgumentNullException(nameof(balance));
        }

        public FacilityUpgradeResult ResolveFacilityUpgrade(
            ClubOperationState operation,
            FacilityType facilityType,
            ClubUpgradeContext context)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            ValidateContext(operation, context);
            FacilityState current = operation.GetFacility(facilityType);
            string receiptId = OperationReceipt.CreateUpgradeReceiptId(context.OperationId);
            if (operation.HasReceipt(receiptId))
                return FacilityFailure(ClubUpgradeStatus.AlreadyApplied, current, 0L);
            if (!_balance.TryGetFacilityLevel(facilityType, current.Level, out _))
                return FacilityFailure(ClubUpgradeStatus.InvalidCurrentState, current, 0L);
            if (!_balance.TryGetNextFacilityLevel(facilityType, current.Level, out FacilityLevelDefinition next))
                return FacilityFailure(ClubUpgradeStatus.MaximumLevel, current, 0L);

            ClubUpgradeStatus requirementStatus = EvaluateRequirements(
                next.RequiredLeagueGrade,
                next.MinimumFanBase,
                next.MinimumSeasonAttendance,
                next.UpgradeMoneyCost,
                context);
            if (requirementStatus != ClubUpgradeStatus.Approved)
                return FacilityFailure(requirementStatus, current, next.UpgradeMoneyCost);

            var upgraded = new FacilityState(facilityType, next.Level);
            var receipt = new OperationReceipt(
                receiptId,
                OperationReceiptKind.FacilityUpgrade,
                context.SeasonId,
                context.WeekIndex,
                string.Concat("facility:", facilityType.ToString(), ":", next.Level.ToString()),
                new OperationResourceDelta(-next.UpgradeMoneyCost, 0, 0));
            return new FacilityUpgradeResult(
                ClubUpgradeStatus.Approved,
                current,
                upgraded,
                next.UpgradeMoneyCost,
                receipt);
        }

        public StadiumUpgradeResult ResolveStadiumUpgrade(
            ClubOperationState operation,
            ClubUpgradeContext context)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            ValidateContext(operation, context);
            StadiumState current = operation.Stadium;
            string receiptId = OperationReceipt.CreateUpgradeReceiptId(context.OperationId);
            if (operation.HasReceipt(receiptId))
                return StadiumFailure(ClubUpgradeStatus.AlreadyApplied, current, 0L);
            if (!_balance.TryGetStadiumLevel(current.Level, out StadiumLevelDefinition currentDefinition) ||
                currentDefinition.Capacity != current.Capacity)
                return StadiumFailure(ClubUpgradeStatus.InvalidCurrentState, current, 0L);
            if (!_balance.TryGetNextStadiumLevel(current.Level, out StadiumLevelDefinition next))
                return StadiumFailure(ClubUpgradeStatus.MaximumLevel, current, 0L);

            ClubUpgradeStatus requirementStatus = EvaluateRequirements(
                next.RequiredLeagueGrade,
                next.MinimumFanBase,
                next.MinimumSeasonAttendance,
                next.UpgradeMoneyCost,
                context);
            if (requirementStatus != ClubUpgradeStatus.Approved)
                return StadiumFailure(requirementStatus, current, next.UpgradeMoneyCost);

            var upgraded = new StadiumState(next.Level, next.Capacity);
            var receipt = new OperationReceipt(
                receiptId,
                OperationReceiptKind.StadiumUpgrade,
                context.SeasonId,
                context.WeekIndex,
                string.Concat("stadium:", next.Level.ToString()),
                new OperationResourceDelta(-next.UpgradeMoneyCost, 0, 0));
            return new StadiumUpgradeResult(
                ClubUpgradeStatus.Approved,
                current,
                upgraded,
                next.UpgradeMoneyCost,
                receipt);
        }

        private static void ValidateContext(ClubOperationState operation, ClubUpgradeContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!string.Equals(context.SeasonId, operation.CurrentSeason.SeasonId, StringComparison.Ordinal))
                throw new ArgumentException("업그레이드 Context와 ClubOperationState의 SeasonId가 일치하지 않습니다.");
        }

        private static ClubUpgradeStatus EvaluateRequirements(
            LeagueGrade? requiredLeagueGrade,
            double minimumFanBase,
            long minimumSeasonAttendance,
            long moneyCost,
            ClubUpgradeContext context)
        {
            if (requiredLeagueGrade.HasValue && context.LeagueGrade < requiredLeagueGrade.Value)
                return ClubUpgradeStatus.LeagueGradeLocked;
            if (context.FanBase < minimumFanBase)
                return ClubUpgradeStatus.FanBaseLocked;
            if (context.SeasonAttendance < minimumSeasonAttendance)
                return ClubUpgradeStatus.SeasonAttendanceLocked;
            if (context.CurrentMoney < moneyCost)
                return ClubUpgradeStatus.InsufficientMoney;
            return ClubUpgradeStatus.Approved;
        }

        private static FacilityUpgradeResult FacilityFailure(
            ClubUpgradeStatus status,
            FacilityState current,
            long moneyCost)
        {
            return new FacilityUpgradeResult(status, current, null, moneyCost, null);
        }

        private static StadiumUpgradeResult StadiumFailure(
            ClubUpgradeStatus status,
            StadiumState current,
            long moneyCost)
        {
            return new StadiumUpgradeResult(status, current, null, moneyCost, null);
        }
    }
}
