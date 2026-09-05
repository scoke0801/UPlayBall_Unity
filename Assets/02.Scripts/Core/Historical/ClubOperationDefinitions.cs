using System;
using System.Collections.Generic;

namespace Baseball.Core.Historical
{
    /// <summary>구단 운영 시설의 고정된 여섯 역할을 식별한다.</summary>
    public enum FacilityType
    {
        ScoutingCenter,
        TrainingCenter,
        RecoveryCenter,
        DataAnalysisCenter,
        TacticLab,
        FanShop
    }

    /// <summary>관중 수요와 1인당 수익 사이의 선택을 나타낸다.</summary>
    public enum TicketPriceTier
    {
        Cheap,
        Standard,
        Premium
    }

    /// <summary>경기 일정에서 구단이 갖는 개최 지위를 구분한다.</summary>
    public enum GameVenue
    {
        Home,
        Away,
        Neutral
    }

    /// <summary>홈 경기 결과가 팬 지표에 주는 방향을 구분한다.</summary>
    public enum HomeGameOutcome
    {
        Loss,
        Draw,
        Win
    }

    /// <summary>재적용 방지 영수증의 운영 원인을 구분한다.</summary>
    public enum OperationReceiptKind
    {
        FacilityProduction,
        FacilityUpgrade,
        StadiumUpgrade,
        HomeGameFinance
    }

    /// <summary>홈 경기 경제 계산의 적용 여부를 나타낸다.</summary>
    public enum HomeGameFinanceStatus
    {
        Applied,
        AlreadyApplied,
        NotHomeGame
    }

    /// <summary>주간 시설 생산이 정상 실행되었는지 나타낸다.</summary>
    public enum WeeklyFacilityProductionStatus
    {
        Produced,
        SuspendedForInsufficientOperatingMoney,
        AlreadyApplied
    }

    /// <summary>시설·구장 업그레이드 판정 결과를 나타낸다.</summary>
    public enum ClubUpgradeStatus
    {
        Approved,
        AlreadyApplied,
        MaximumLevel,
        InsufficientMoney,
        LeagueGradeLocked,
        FanBaseLocked,
        SeasonAttendanceLocked,
        InvalidCurrentState
    }

    /// <summary>현재 홈 경기 티켓 가격 정책을 보관한다.</summary>
    public sealed class TicketPolicy
    {
        public TicketPolicy(TicketPriceTier priceTier)
        {
            if (!Enum.IsDefined(typeof(TicketPriceTier), priceTier))
                throw new ArgumentOutOfRangeException(nameof(priceTier));
            PriceTier = priceTier;
        }

        public TicketPriceTier PriceTier { get; }
    }

    /// <summary>한 시설의 타입과 현재 레벨만 저장한다.</summary>
    public sealed class FacilityState
    {
        public FacilityState(FacilityType type, int level)
        {
            if (!Enum.IsDefined(typeof(FacilityType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            if (level < 0)
                throw new ArgumentOutOfRangeException(nameof(level));
            Type = type;
            Level = level;
        }

        public FacilityType Type { get; }
        public int Level { get; }
    }

    /// <summary>구장 레벨과 실제 관중 상한을 저장한다.</summary>
    public sealed class StadiumState
    {
        public StadiumState(int level, int capacity)
        {
            if (level <= 0)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Level = level;
            Capacity = capacity;
        }

        public int Level { get; }
        public int Capacity { get; }
    }

    /// <summary>기존 ManagerEconomyState에 한 번만 반영할 세 자원 변화량이다.</summary>
    public readonly struct OperationResourceDelta
    {
        public OperationResourceDelta(long money, int scoutingPoints, int developmentPoints)
        {
            if (scoutingPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(scoutingPoints));
            if (developmentPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(developmentPoints));
            Money = money;
            ScoutingPoints = scoutingPoints;
            DevelopmentPoints = developmentPoints;
        }

        public long Money { get; }
        public int ScoutingPoints { get; }
        public int DevelopmentPoints { get; }
    }

    /// <summary>운영 경제 적용의 멱등성과 외부 잔액 반영 명령을 함께 표현한다.</summary>
    public sealed class OperationReceipt
    {
        public OperationReceipt(
            string receiptId,
            OperationReceiptKind kind,
            string seasonId,
            int weekIndex,
            string sourceId,
            OperationResourceDelta resourceDelta)
        {
            if (string.IsNullOrWhiteSpace(receiptId))
                throw new ArgumentException("ReceiptId는 비어 있을 수 없습니다.", nameof(receiptId));
            if (!Enum.IsDefined(typeof(OperationReceiptKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (weekIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(weekIndex));
            if (string.IsNullOrWhiteSpace(sourceId))
                throw new ArgumentException("SourceId는 비어 있을 수 없습니다.", nameof(sourceId));

            ReceiptId = receiptId.Trim();
            Kind = kind;
            SeasonId = seasonId.Trim();
            WeekIndex = weekIndex;
            SourceId = sourceId.Trim();
            ResourceDelta = resourceDelta;
        }

        public string ReceiptId { get; }
        public OperationReceiptKind Kind { get; }
        public string SeasonId { get; }
        public int WeekIndex { get; }
        public string SourceId { get; }
        public OperationResourceDelta ResourceDelta { get; }

        public static string CreateWeeklyFacilityReceiptId(
            string teamSeasonKey,
            string seasonId,
            int weekIndex)
        {
            return CreateTimedReceiptId("facility-week", teamSeasonKey, seasonId, weekIndex);
        }

        public static string CreateHomeGameReceiptId(string scheduledGameId)
        {
            if (string.IsNullOrWhiteSpace(scheduledGameId))
                throw new ArgumentException("ScheduledGameId는 비어 있을 수 없습니다.", nameof(scheduledGameId));
            return string.Concat("home-game:", scheduledGameId.Trim());
        }

        public static string CreateUpgradeReceiptId(string operationId)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("OperationId는 비어 있을 수 없습니다.", nameof(operationId));
            return string.Concat("club-upgrade:", operationId.Trim());
        }

        private static string CreateTimedReceiptId(
            string prefix,
            string teamSeasonKey,
            string seasonId,
            int weekIndex)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (weekIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(weekIndex));
            return string.Concat(
                prefix,
                ":",
                teamSeasonKey.Trim(),
                ":",
                seasonId.Trim(),
                ":",
                weekIndex.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>한 주에 반영된 구단 운영 자원 변화와 홈 관중을 집계한다.</summary>
    public sealed class WeeklyOperationLedger
    {
        public WeeklyOperationLedger(string seasonId, int weekIndex)
            : this(seasonId, weekIndex, 0L, 0L, 0, 0, 0, 0L, 0)
        {
        }

        public WeeklyOperationLedger(
            string seasonId,
            int weekIndex,
            long moneyIncome,
            long moneyExpense,
            int scoutingPointProduction,
            int developmentPointProduction,
            int homeGames,
            long attendance,
            int receiptCount)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (weekIndex < 0 || moneyIncome < 0L || moneyExpense < 0L ||
                scoutingPointProduction < 0 || developmentPointProduction < 0 ||
                homeGames < 0 || attendance < 0L || receiptCount < 0)
                throw new ArgumentOutOfRangeException(nameof(weekIndex));
            SeasonId = seasonId.Trim();
            WeekIndex = weekIndex;
            MoneyIncome = moneyIncome;
            MoneyExpense = moneyExpense;
            ScoutingPointProduction = scoutingPointProduction;
            DevelopmentPointProduction = developmentPointProduction;
            HomeGames = homeGames;
            Attendance = attendance;
            ReceiptCount = receiptCount;
        }

        public string SeasonId { get; }
        public int WeekIndex { get; }
        public long MoneyIncome { get; private set; }
        public long MoneyExpense { get; private set; }
        public int ScoutingPointProduction { get; private set; }
        public int DevelopmentPointProduction { get; private set; }
        public int HomeGames { get; private set; }
        public long Attendance { get; private set; }
        public int ReceiptCount { get; private set; }
        public long NetMoney => MoneyIncome - MoneyExpense;

        internal void RecordReceipt(OperationReceipt receipt)
        {
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (!string.Equals(receipt.SeasonId, SeasonId, StringComparison.Ordinal) ||
                receipt.WeekIndex != WeekIndex)
                return;

            long money = receipt.ResourceDelta.Money;
            if (money >= 0L)
                MoneyIncome = checked(MoneyIncome + money);
            else
                MoneyExpense = checked(MoneyExpense + checked(-money));
            ScoutingPointProduction = checked(
                ScoutingPointProduction + receipt.ResourceDelta.ScoutingPoints);
            DevelopmentPointProduction = checked(
                DevelopmentPointProduction + receipt.ResourceDelta.DevelopmentPoints);
            ReceiptCount = checked(ReceiptCount + 1);
        }

        internal void RecordHomeGame(int attendance)
        {
            if (attendance < 0)
                throw new ArgumentOutOfRangeException(nameof(attendance));
            HomeGames = checked(HomeGames + 1);
            Attendance = checked(Attendance + attendance);
        }
    }

    /// <summary>한 시즌의 운영 수입·지출과 홈 경기 재무를 누적한다.</summary>
    public sealed class SeasonFinanceSummary
    {
        public SeasonFinanceSummary(string seasonId)
            : this(seasonId, 0, 0L, 0L, 0L, 0L, 0L, 0L, 0L, 0, 0)
        {
        }

        public SeasonFinanceSummary(
            string seasonId,
            int homeGames,
            long attendance,
            long ticketRevenue,
            long fanShopRevenue,
            long otherGameRevenue,
            long gameOperatingCost,
            long moneyIncome,
            long moneyExpense,
            int scoutingPointProduction,
            int developmentPointProduction)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (homeGames < 0 || attendance < 0L || ticketRevenue < 0L || fanShopRevenue < 0L ||
                otherGameRevenue < 0L || gameOperatingCost < 0L || moneyIncome < 0L ||
                moneyExpense < 0L || scoutingPointProduction < 0 || developmentPointProduction < 0)
                throw new ArgumentOutOfRangeException(nameof(homeGames));
            SeasonId = seasonId.Trim();
            HomeGames = homeGames;
            Attendance = attendance;
            TicketRevenue = ticketRevenue;
            FanShopRevenue = fanShopRevenue;
            OtherGameRevenue = otherGameRevenue;
            GameOperatingCost = gameOperatingCost;
            MoneyIncome = moneyIncome;
            MoneyExpense = moneyExpense;
            ScoutingPointProduction = scoutingPointProduction;
            DevelopmentPointProduction = developmentPointProduction;
        }

        public string SeasonId { get; }
        public int HomeGames { get; private set; }
        public long Attendance { get; private set; }
        public long TicketRevenue { get; private set; }
        public long FanShopRevenue { get; private set; }
        public long OtherGameRevenue { get; private set; }
        public long GameOperatingCost { get; private set; }
        public long MoneyIncome { get; private set; }
        public long MoneyExpense { get; private set; }
        public int ScoutingPointProduction { get; private set; }
        public int DevelopmentPointProduction { get; private set; }
        public long NetMoney => MoneyIncome - MoneyExpense;
        public double AverageAttendance => HomeGames == 0 ? 0d : (double)Attendance / HomeGames;

        internal void RecordReceipt(OperationReceipt receipt)
        {
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (!string.Equals(receipt.SeasonId, SeasonId, StringComparison.Ordinal))
                return;

            long money = receipt.ResourceDelta.Money;
            if (money >= 0L)
                MoneyIncome = checked(MoneyIncome + money);
            else
                MoneyExpense = checked(MoneyExpense + checked(-money));
            ScoutingPointProduction = checked(
                ScoutingPointProduction + receipt.ResourceDelta.ScoutingPoints);
            DevelopmentPointProduction = checked(
                DevelopmentPointProduction + receipt.ResourceDelta.DevelopmentPoints);
        }

        internal void RecordHomeGame(HomeGameFinanceResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            HomeGames = checked(HomeGames + 1);
            Attendance = checked(Attendance + result.Attendance);
            TicketRevenue = checked(TicketRevenue + result.TicketRevenue);
            FanShopRevenue = checked(FanShopRevenue + result.FanShopRevenue);
            OtherGameRevenue = checked(OtherGameRevenue + result.OtherGameRevenue);
            GameOperatingCost = checked(GameOperatingCost + result.OperatingCost);
        }
    }

    /// <summary>관중 수요 계산의 설명 가능한 중간값과 최종 관중을 보관한다.</summary>
    public readonly struct AttendanceResult
    {
        public AttendanceResult(double expectedDemand, int attendance, int capacity)
        {
            if (double.IsNaN(expectedDemand) || double.IsInfinity(expectedDemand) || expectedDemand < 0d)
                throw new ArgumentOutOfRangeException(nameof(expectedDemand));
            if (capacity <= 0 || attendance < 0 || attendance > capacity)
                throw new ArgumentOutOfRangeException(nameof(attendance));
            ExpectedDemand = expectedDemand;
            Attendance = attendance;
            Capacity = capacity;
        }

        public double ExpectedDemand { get; }
        public int Attendance { get; }
        public int Capacity { get; }
        public double CapacityRate => Capacity == 0 ? 0d : (double)Attendance / Capacity;
    }

    /// <summary>경기 결과와 관중으로 발생한 팬·인기도·관중 관성 변화를 설명한다.</summary>
    public readonly struct FanPopularityResult
    {
        public FanPopularityResult(
            double outcomeFanBaseDelta,
            double attendanceFanBaseDelta,
            double outcomePopularityDelta,
            double popularityDecay,
            double fanShopPopularityRetention,
            double momentumDelta)
        {
            ValidateFinite(outcomeFanBaseDelta, nameof(outcomeFanBaseDelta));
            ValidateFinite(attendanceFanBaseDelta, nameof(attendanceFanBaseDelta));
            ValidateFinite(outcomePopularityDelta, nameof(outcomePopularityDelta));
            ValidateFinite(popularityDecay, nameof(popularityDecay));
            ValidateFinite(fanShopPopularityRetention, nameof(fanShopPopularityRetention));
            ValidateFinite(momentumDelta, nameof(momentumDelta));
            OutcomeFanBaseDelta = outcomeFanBaseDelta;
            AttendanceFanBaseDelta = attendanceFanBaseDelta;
            OutcomePopularityDelta = outcomePopularityDelta;
            PopularityDecay = popularityDecay;
            FanShopPopularityRetention = fanShopPopularityRetention;
            MomentumDelta = momentumDelta;
        }

        public double OutcomeFanBaseDelta { get; }
        public double AttendanceFanBaseDelta { get; }
        public double OutcomePopularityDelta { get; }
        public double PopularityDecay { get; }
        public double FanShopPopularityRetention { get; }
        public double MomentumDelta { get; }
        public double FanBaseDelta => OutcomeFanBaseDelta + AttendanceFanBaseDelta;
        public double PopularityDelta => OutcomePopularityDelta - PopularityDecay + FanShopPopularityRetention;

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>홈 경기 한 경기의 관중·수익·팬 변화를 기존 경제에 반영할 명령이다.</summary>
    public sealed class HomeGameFinanceResult
    {
        private HomeGameFinanceResult(
            HomeGameFinanceStatus status,
            string scheduledGameId,
            AttendanceResult attendanceResult,
            long ticketRevenue,
            long fanShopRevenue,
            long otherGameRevenue,
            long operatingCost,
            FanPopularityResult fanPopularity,
            OperationReceipt receipt)
        {
            Status = status;
            ScheduledGameId = scheduledGameId;
            AttendanceResult = attendanceResult;
            TicketRevenue = ticketRevenue;
            FanShopRevenue = fanShopRevenue;
            OtherGameRevenue = otherGameRevenue;
            OperatingCost = operatingCost;
            FanPopularity = fanPopularity;
            Receipt = receipt;
        }

        public HomeGameFinanceStatus Status { get; }
        public string ScheduledGameId { get; }
        public AttendanceResult AttendanceResult { get; }
        public int Attendance => AttendanceResult.Attendance;
        public int Capacity => AttendanceResult.Capacity;
        public double CapacityRate => AttendanceResult.CapacityRate;
        public long TicketRevenue { get; }
        public long FanShopRevenue { get; }
        public long OtherGameRevenue { get; }
        public long OperatingCost { get; }
        public long NetGameIncome => checked(TicketRevenue + FanShopRevenue + OtherGameRevenue - OperatingCost);
        public FanPopularityResult FanPopularity { get; }
        public double FanBaseDelta => FanPopularity.FanBaseDelta;
        public double PopularityDelta => FanPopularity.PopularityDelta;
        public double MomentumDelta => FanPopularity.MomentumDelta;
        public OperationReceipt Receipt { get; }

        public static HomeGameFinanceResult CreateApplied(
            string scheduledGameId,
            AttendanceResult attendanceResult,
            long ticketRevenue,
            long fanShopRevenue,
            long otherGameRevenue,
            long operatingCost,
            FanPopularityResult fanPopularity,
            OperationReceipt receipt)
        {
            ValidateGameId(scheduledGameId);
            if (ticketRevenue < 0L || fanShopRevenue < 0L || otherGameRevenue < 0L || operatingCost < 0L)
                throw new ArgumentOutOfRangeException(nameof(ticketRevenue));
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (receipt.Kind != OperationReceiptKind.HomeGameFinance ||
                !string.Equals(receipt.SourceId, scheduledGameId.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("홈 경기 영수증의 종류 또는 SourceId가 일치하지 않습니다.", nameof(receipt));
            long netIncome = checked(ticketRevenue + fanShopRevenue + otherGameRevenue - operatingCost);
            if (receipt.ResourceDelta.Money != netIncome ||
                receipt.ResourceDelta.ScoutingPoints != 0 ||
                receipt.ResourceDelta.DevelopmentPoints != 0)
                throw new ArgumentException("홈 경기 영수증의 자원 변화량이 재무 결과와 일치하지 않습니다.", nameof(receipt));
            return new HomeGameFinanceResult(
                HomeGameFinanceStatus.Applied,
                scheduledGameId.Trim(),
                attendanceResult,
                ticketRevenue,
                fanShopRevenue,
                otherGameRevenue,
                operatingCost,
                fanPopularity,
                receipt);
        }

        public static HomeGameFinanceResult CreateNotHomeGame(string scheduledGameId)
        {
            ValidateGameId(scheduledGameId);
            return CreateNoOperation(HomeGameFinanceStatus.NotHomeGame, scheduledGameId);
        }

        public static HomeGameFinanceResult CreateAlreadyApplied(string scheduledGameId)
        {
            ValidateGameId(scheduledGameId);
            return CreateNoOperation(HomeGameFinanceStatus.AlreadyApplied, scheduledGameId);
        }

        private static HomeGameFinanceResult CreateNoOperation(
            HomeGameFinanceStatus status,
            string scheduledGameId)
        {
            return new HomeGameFinanceResult(
                status,
                scheduledGameId.Trim(),
                default,
                0L,
                0L,
                0L,
                0L,
                default,
                null);
        }

        private static void ValidateGameId(string scheduledGameId)
        {
            if (string.IsNullOrWhiteSpace(scheduledGameId))
                throw new ArgumentException("ScheduledGameId는 비어 있을 수 없습니다.", nameof(scheduledGameId));
        }
    }

    /// <summary>주간 시설 생산과 유지비를 한 번 반영하기 위한 결과다.</summary>
    public sealed class WeeklyFacilityProductionResult
    {
        public WeeklyFacilityProductionResult(
            WeeklyFacilityProductionStatus status,
            long operatingCost,
            int scoutingPointProduction,
            int developmentPointProduction,
            OperationReceipt receipt)
        {
            if (!Enum.IsDefined(typeof(WeeklyFacilityProductionStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (operatingCost < 0L || scoutingPointProduction < 0 || developmentPointProduction < 0)
                throw new ArgumentOutOfRangeException(nameof(operatingCost));
            if (status == WeeklyFacilityProductionStatus.AlreadyApplied)
            {
                if (operatingCost != 0L || scoutingPointProduction != 0 ||
                    developmentPointProduction != 0 || receipt != null)
                    throw new ArgumentException("이미 처리된 생산 결과에는 자원 변화나 영수증을 포함할 수 없습니다.");
                Status = status;
                OperatingCost = 0L;
                ScoutingPointProduction = 0;
                DevelopmentPointProduction = 0;
                Receipt = null;
                return;
            }
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (receipt.Kind != OperationReceiptKind.FacilityProduction)
                throw new ArgumentException("주간 시설 생산 영수증이 아닙니다.", nameof(receipt));
            long expectedMoney = status == WeeklyFacilityProductionStatus.Produced ? -operatingCost : 0L;
            int expectedSp = status == WeeklyFacilityProductionStatus.Produced ? scoutingPointProduction : 0;
            int expectedDp = status == WeeklyFacilityProductionStatus.Produced ? developmentPointProduction : 0;
            if (receipt.ResourceDelta.Money != expectedMoney ||
                receipt.ResourceDelta.ScoutingPoints != expectedSp ||
                receipt.ResourceDelta.DevelopmentPoints != expectedDp)
                throw new ArgumentException("주간 시설 생산 영수증의 자원 변화량이 결과와 일치하지 않습니다.", nameof(receipt));
            Status = status;
            OperatingCost = operatingCost;
            ScoutingPointProduction = scoutingPointProduction;
            DevelopmentPointProduction = developmentPointProduction;
            Receipt = receipt;
        }

        public WeeklyFacilityProductionStatus Status { get; }
        public long OperatingCost { get; }
        public int ScoutingPointProduction { get; }
        public int DevelopmentPointProduction { get; }
        public OperationReceipt Receipt { get; }
    }

    /// <summary>시설 업그레이드의 검증 결과와 승인 시 경제 명령을 보관한다.</summary>
    public sealed class FacilityUpgradeResult
    {
        public FacilityUpgradeResult(
            ClubUpgradeStatus status,
            FacilityState currentFacility,
            FacilityState upgradedFacility,
            long moneyCost,
            OperationReceipt receipt)
        {
            if (!Enum.IsDefined(typeof(ClubUpgradeStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            CurrentFacility = currentFacility ?? throw new ArgumentNullException(nameof(currentFacility));
            if (moneyCost < 0L)
                throw new ArgumentOutOfRangeException(nameof(moneyCost));
            if (status == ClubUpgradeStatus.Approved)
            {
                if (upgradedFacility == null || receipt == null)
                    throw new ArgumentException("승인된 업그레이드에는 변경 상태와 영수증이 필요합니다.");
                if (upgradedFacility.Type != currentFacility.Type ||
                    upgradedFacility.Level != currentFacility.Level + 1)
                    throw new ArgumentException("시설은 같은 타입의 다음 레벨로만 업그레이드할 수 있습니다.");
                if (receipt.Kind != OperationReceiptKind.FacilityUpgrade ||
                    receipt.ResourceDelta.Money != -moneyCost ||
                    receipt.ResourceDelta.ScoutingPoints != 0 ||
                    receipt.ResourceDelta.DevelopmentPoints != 0)
                    throw new ArgumentException("시설 업그레이드 영수증이 비용과 일치하지 않습니다.", nameof(receipt));
            }
            else if (upgradedFacility != null || receipt != null)
            {
                throw new ArgumentException("거부된 업그레이드에는 변경 상태나 영수증을 포함할 수 없습니다.");
            }
            Status = status;
            UpgradedFacility = upgradedFacility;
            MoneyCost = moneyCost;
            Receipt = receipt;
        }

        public ClubUpgradeStatus Status { get; }
        public FacilityState CurrentFacility { get; }
        public FacilityState UpgradedFacility { get; }
        public long MoneyCost { get; }
        public OperationReceipt Receipt { get; }
        public bool IsApproved => Status == ClubUpgradeStatus.Approved;
    }

    /// <summary>구장 증축의 검증 결과와 승인 시 경제 명령을 보관한다.</summary>
    public sealed class StadiumUpgradeResult
    {
        public StadiumUpgradeResult(
            ClubUpgradeStatus status,
            StadiumState currentStadium,
            StadiumState upgradedStadium,
            long moneyCost,
            OperationReceipt receipt)
        {
            if (!Enum.IsDefined(typeof(ClubUpgradeStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            CurrentStadium = currentStadium ?? throw new ArgumentNullException(nameof(currentStadium));
            if (moneyCost < 0L)
                throw new ArgumentOutOfRangeException(nameof(moneyCost));
            if (status == ClubUpgradeStatus.Approved)
            {
                if (upgradedStadium == null || receipt == null)
                    throw new ArgumentException("승인된 증축에는 변경 상태와 영수증이 필요합니다.");
                if (upgradedStadium.Level != currentStadium.Level + 1)
                    throw new ArgumentException("구장은 다음 레벨로만 증축할 수 있습니다.");
                if (receipt.Kind != OperationReceiptKind.StadiumUpgrade ||
                    receipt.ResourceDelta.Money != -moneyCost ||
                    receipt.ResourceDelta.ScoutingPoints != 0 ||
                    receipt.ResourceDelta.DevelopmentPoints != 0)
                    throw new ArgumentException("구장 증축 영수증이 비용과 일치하지 않습니다.", nameof(receipt));
            }
            else if (upgradedStadium != null || receipt != null)
            {
                throw new ArgumentException("거부된 증축에는 변경 상태나 영수증을 포함할 수 없습니다.");
            }
            Status = status;
            UpgradedStadium = upgradedStadium;
            MoneyCost = moneyCost;
            Receipt = receipt;
        }

        public ClubUpgradeStatus Status { get; }
        public StadiumState CurrentStadium { get; }
        public StadiumState UpgradedStadium { get; }
        public long MoneyCost { get; }
        public OperationReceipt Receipt { get; }
        public bool IsApproved => Status == ClubUpgradeStatus.Approved;
    }

    /// <summary>선수 능력치를 건드리지 않고 다른 시스템에 전달할 시설 효과 스냅샷이다.</summary>
    public readonly struct ClubFacilityEffectProfile
    {
        public ClubFacilityEffectProfile(
            double conditionRecoveryEfficiencyModifier,
            double scoutingConfidenceModifier,
            double tacticResearchEfficiencyModifier,
            long fanShopRevenuePerAttendee,
            double fanShopPopularityRetention)
        {
            if (conditionRecoveryEfficiencyModifier < 0d ||
                scoutingConfidenceModifier < 0d ||
                tacticResearchEfficiencyModifier < 0d ||
                fanShopRevenuePerAttendee < 0L ||
                fanShopPopularityRetention < 0d ||
                double.IsNaN(conditionRecoveryEfficiencyModifier) ||
                double.IsNaN(scoutingConfidenceModifier) ||
                double.IsNaN(tacticResearchEfficiencyModifier) ||
                double.IsNaN(fanShopPopularityRetention))
                throw new ArgumentOutOfRangeException(nameof(conditionRecoveryEfficiencyModifier));
            ConditionRecoveryEfficiencyModifier = conditionRecoveryEfficiencyModifier;
            ScoutingConfidenceModifier = scoutingConfidenceModifier;
            TacticResearchEfficiencyModifier = tacticResearchEfficiencyModifier;
            FanShopRevenuePerAttendee = fanShopRevenuePerAttendee;
            FanShopPopularityRetention = fanShopPopularityRetention;
        }

        public double ConditionRecoveryEfficiencyModifier { get; }
        public double ScoutingConfidenceModifier { get; }
        public double TacticResearchEfficiencyModifier { get; }
        public long FanShopRevenuePerAttendee { get; }
        public double FanShopPopularityRetention { get; }
    }

    /// <summary>플레이어 구단의 팬·구장·시설·정책·재무 원본을 소유한다.</summary>
    public sealed class ClubOperationState
    {
        public const double MinimumNormalizedScore = 0d;
        public const double MaximumNormalizedScore = 100d;

        private readonly FacilityState[] _facilities;
        private readonly List<OperationReceipt> _receipts;
        private readonly HashSet<string> _receiptIds;

        public ClubOperationState(
            string teamSeasonKey,
            double fanBase,
            double popularity,
            double attendanceMomentum,
            StadiumState stadium,
            IReadOnlyList<FacilityState> facilities,
            TicketPolicy ticketPolicy,
            WeeklyOperationLedger currentWeek,
            SeasonFinanceSummary currentSeason,
            IReadOnlyList<OperationReceipt> receipts = null)
        {
            if (string.IsNullOrWhiteSpace(teamSeasonKey))
                throw new ArgumentException("TeamSeasonKey는 비어 있을 수 없습니다.", nameof(teamSeasonKey));
            ValidateNormalizedScore(fanBase, nameof(fanBase));
            ValidateNormalizedScore(popularity, nameof(popularity));
            ValidateNormalizedScore(attendanceMomentum, nameof(attendanceMomentum));
            TeamSeasonKey = teamSeasonKey.Trim();
            FanBase = fanBase;
            Popularity = popularity;
            AttendanceMomentum = attendanceMomentum;
            Stadium = stadium ?? throw new ArgumentNullException(nameof(stadium));
            TicketPolicy = ticketPolicy ?? throw new ArgumentNullException(nameof(ticketPolicy));
            CurrentWeek = currentWeek ?? throw new ArgumentNullException(nameof(currentWeek));
            CurrentSeason = currentSeason ?? throw new ArgumentNullException(nameof(currentSeason));
            if (!string.Equals(CurrentWeek.SeasonId, CurrentSeason.SeasonId, StringComparison.Ordinal))
                throw new ArgumentException("주간 Ledger와 시즌 Summary의 SeasonId가 일치해야 합니다.");
            _facilities = CopyAndValidateFacilities(facilities);
            _receipts = new List<OperationReceipt>(receipts?.Count ?? 0);
            _receiptIds = new HashSet<string>(StringComparer.Ordinal);
            if (receipts != null)
            {
                for (int index = 0; index < receipts.Count; index++)
                {
                    OperationReceipt receipt = receipts[index]
                        ?? throw new ArgumentException("null 운영 영수증이 있습니다.", nameof(receipts));
                    if (!string.Equals(receipt.SeasonId, CurrentSeason.SeasonId, StringComparison.Ordinal))
                        throw new ArgumentException("현재 시즌과 다른 운영 영수증은 복원할 수 없습니다.", nameof(receipts));
                    if (!_receiptIds.Add(receipt.ReceiptId))
                        throw new ArgumentException("ReceiptId는 중복될 수 없습니다.", nameof(receipts));
                    _receipts.Add(receipt);
                }
            }
        }

        public string TeamSeasonKey { get; }
        public double FanBase { get; private set; }
        public double Popularity { get; private set; }
        public double AttendanceMomentum { get; private set; }
        public StadiumState Stadium { get; private set; }
        public IReadOnlyList<FacilityState> Facilities => _facilities;
        public TicketPolicy TicketPolicy { get; private set; }
        public WeeklyOperationLedger CurrentWeek { get; private set; }
        public SeasonFinanceSummary CurrentSeason { get; }
        public IReadOnlyList<OperationReceipt> Receipts => _receipts;

        public FacilityState GetFacility(FacilityType type)
        {
            if (!Enum.IsDefined(typeof(FacilityType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            return _facilities[(int)type];
        }

        public bool HasReceipt(string receiptId)
        {
            return !string.IsNullOrWhiteSpace(receiptId) && _receiptIds.Contains(receiptId.Trim());
        }

        public void SetTicketPolicy(TicketPolicy ticketPolicy)
        {
            TicketPolicy = ticketPolicy ?? throw new ArgumentNullException(nameof(ticketPolicy));
        }

        public void BeginWeek(int weekIndex)
        {
            if (weekIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(weekIndex));
            CurrentWeek = new WeeklyOperationLedger(CurrentSeason.SeasonId, weekIndex);
        }

        private bool TryRecordReceipt(OperationReceipt receipt)
        {
            if (receipt == null)
                throw new ArgumentNullException(nameof(receipt));
            if (!string.Equals(receipt.SeasonId, CurrentSeason.SeasonId, StringComparison.Ordinal))
                throw new ArgumentException("현재 시즌과 다른 영수증은 반영할 수 없습니다.", nameof(receipt));
            if (!_receiptIds.Add(receipt.ReceiptId))
                return false;
            _receipts.Add(receipt);
            CurrentWeek.RecordReceipt(receipt);
            CurrentSeason.RecordReceipt(receipt);
            return true;
        }

        public bool TryApplyWeeklyProduction(WeeklyFacilityProductionResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.Status == WeeklyFacilityProductionStatus.AlreadyApplied)
                return false;
            return TryRecordReceipt(result.Receipt);
        }

        public bool TryApplyHomeGame(HomeGameFinanceResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.Status != HomeGameFinanceStatus.Applied)
                return false;
            if (!TryRecordReceipt(result.Receipt))
                return false;

            FanBase = ClampNormalizedScore(FanBase + result.FanBaseDelta);
            Popularity = ClampNormalizedScore(Popularity + result.PopularityDelta);
            AttendanceMomentum = ClampNormalizedScore(AttendanceMomentum + result.MomentumDelta);
            CurrentWeek.RecordHomeGame(result.Attendance);
            CurrentSeason.RecordHomeGame(result);
            return true;
        }

        public bool TryApplyFacilityUpgrade(FacilityUpgradeResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.IsApproved)
                return false;
            FacilityState current = GetFacility(result.CurrentFacility.Type);
            if (current.Level != result.CurrentFacility.Level)
                return false;
            if (!TryRecordReceipt(result.Receipt))
                return false;
            _facilities[(int)current.Type] = result.UpgradedFacility;
            return true;
        }

        public bool TryApplyStadiumUpgrade(StadiumUpgradeResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.IsApproved || Stadium.Level != result.CurrentStadium.Level ||
                Stadium.Capacity != result.CurrentStadium.Capacity)
                return false;
            if (!TryRecordReceipt(result.Receipt))
                return false;
            Stadium = result.UpgradedStadium;
            return true;
        }

        private static FacilityState[] CopyAndValidateFacilities(IReadOnlyList<FacilityState> facilities)
        {
            int facilityCount = Enum.GetValues(typeof(FacilityType)).Length;
            if (facilities == null || facilities.Count != facilityCount)
                throw new ArgumentException("시설 여섯 타입의 상태가 모두 필요합니다.", nameof(facilities));
            var result = new FacilityState[facilityCount];
            var found = new bool[facilityCount];
            for (int index = 0; index < facilities.Count; index++)
            {
                FacilityState facility = facilities[index]
                    ?? throw new ArgumentException("null 시설 상태가 있습니다.", nameof(facilities));
                int typeIndex = (int)facility.Type;
                if (found[typeIndex])
                    throw new ArgumentException("같은 FacilityType을 중복 저장할 수 없습니다.", nameof(facilities));
                found[typeIndex] = true;
                result[typeIndex] = facility;
            }
            return result;
        }

        private static void ValidateNormalizedScore(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                value < MinimumNormalizedScore || value > MaximumNormalizedScore)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static double ClampNormalizedScore(double value)
        {
            if (value < MinimumNormalizedScore) return MinimumNormalizedScore;
            if (value > MaximumNormalizedScore) return MaximumNormalizedScore;
            return value;
        }
    }

    /// <summary>한 경기의 공개 가능한 운영·관중 입력만 전달한다.</summary>
    public sealed class HomeGameContext
    {
        public HomeGameContext(
            string scheduledGameId,
            string seasonId,
            int weekIndex,
            string homeTeamSeasonKey,
            string awayTeamSeasonKey,
            GameVenue venue,
            LeagueGrade leagueGrade,
            HomeGameOutcome outcome,
            double recentPerformance,
            double opponentAttraction,
            double seasonImportance,
            double rivalryStoryStrength)
        {
            if (string.IsNullOrWhiteSpace(scheduledGameId))
                throw new ArgumentException("ScheduledGameId는 비어 있을 수 없습니다.", nameof(scheduledGameId));
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (weekIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(weekIndex));
            if (string.IsNullOrWhiteSpace(homeTeamSeasonKey))
                throw new ArgumentException("HomeTeamSeasonKey는 비어 있을 수 없습니다.", nameof(homeTeamSeasonKey));
            if (string.IsNullOrWhiteSpace(awayTeamSeasonKey))
                throw new ArgumentException("AwayTeamSeasonKey는 비어 있을 수 없습니다.", nameof(awayTeamSeasonKey));
            if (!Enum.IsDefined(typeof(GameVenue), venue))
                throw new ArgumentOutOfRangeException(nameof(venue));
            if (!Enum.IsDefined(typeof(LeagueGrade), leagueGrade))
                throw new ArgumentOutOfRangeException(nameof(leagueGrade));
            if (!Enum.IsDefined(typeof(HomeGameOutcome), outcome))
                throw new ArgumentOutOfRangeException(nameof(outcome));
            ValidateUnit(recentPerformance, nameof(recentPerformance));
            ValidateUnit(opponentAttraction, nameof(opponentAttraction));
            ValidateUnit(seasonImportance, nameof(seasonImportance));
            ValidateUnit(rivalryStoryStrength, nameof(rivalryStoryStrength));
            ScheduledGameId = scheduledGameId.Trim();
            SeasonId = seasonId.Trim();
            WeekIndex = weekIndex;
            HomeTeamSeasonKey = homeTeamSeasonKey.Trim();
            AwayTeamSeasonKey = awayTeamSeasonKey.Trim();
            Venue = venue;
            LeagueGrade = leagueGrade;
            Outcome = outcome;
            RecentPerformance = recentPerformance;
            OpponentAttraction = opponentAttraction;
            SeasonImportance = seasonImportance;
            RivalryStoryStrength = rivalryStoryStrength;
        }

        public string ScheduledGameId { get; }
        public string SeasonId { get; }
        public int WeekIndex { get; }
        public string HomeTeamSeasonKey { get; }
        public string AwayTeamSeasonKey { get; }
        public GameVenue Venue { get; }
        public LeagueGrade LeagueGrade { get; }
        public HomeGameOutcome Outcome { get; }
        public double RecentPerformance { get; }
        public double OpponentAttraction { get; }
        public double SeasonImportance { get; }
        public double RivalryStoryStrength { get; }

        private static void ValidateUnit(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d || value > 1d)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>주간 시설 생산 시 현재 세 자원과 처리 시점을 제공한다.</summary>
    public sealed class WeeklyFacilityProductionContext
    {
        public WeeklyFacilityProductionContext(
            string seasonId,
            int weekIndex,
            LeagueGrade leagueGrade,
            long currentMoney,
            int currentScoutingPoints,
            int currentDevelopmentPoints)
        {
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (weekIndex < 0 || currentMoney < 0L || currentScoutingPoints < 0 || currentDevelopmentPoints < 0)
                throw new ArgumentOutOfRangeException(nameof(weekIndex));
            if (!Enum.IsDefined(typeof(LeagueGrade), leagueGrade))
                throw new ArgumentOutOfRangeException(nameof(leagueGrade));
            SeasonId = seasonId.Trim();
            WeekIndex = weekIndex;
            LeagueGrade = leagueGrade;
            CurrentMoney = currentMoney;
            CurrentScoutingPoints = currentScoutingPoints;
            CurrentDevelopmentPoints = currentDevelopmentPoints;
        }

        public string SeasonId { get; }
        public int WeekIndex { get; }
        public LeagueGrade LeagueGrade { get; }
        public long CurrentMoney { get; }
        public int CurrentScoutingPoints { get; }
        public int CurrentDevelopmentPoints { get; }
    }

    /// <summary>시설 또는 구장 업그레이드 시점의 요구조건 입력이다.</summary>
    public sealed class ClubUpgradeContext
    {
        public ClubUpgradeContext(
            string operationId,
            string seasonId,
            int weekIndex,
            LeagueGrade leagueGrade,
            double fanBase,
            long seasonAttendance,
            long currentMoney)
        {
            if (string.IsNullOrWhiteSpace(operationId))
                throw new ArgumentException("OperationId는 비어 있을 수 없습니다.", nameof(operationId));
            if (string.IsNullOrWhiteSpace(seasonId))
                throw new ArgumentException("SeasonId는 비어 있을 수 없습니다.", nameof(seasonId));
            if (weekIndex < 0 || seasonAttendance < 0L || currentMoney < 0L)
                throw new ArgumentOutOfRangeException(nameof(weekIndex));
            if (!Enum.IsDefined(typeof(LeagueGrade), leagueGrade))
                throw new ArgumentOutOfRangeException(nameof(leagueGrade));
            if (double.IsNaN(fanBase) || double.IsInfinity(fanBase) || fanBase < 0d || fanBase > 100d)
                throw new ArgumentOutOfRangeException(nameof(fanBase));
            OperationId = operationId.Trim();
            SeasonId = seasonId.Trim();
            WeekIndex = weekIndex;
            LeagueGrade = leagueGrade;
            FanBase = fanBase;
            SeasonAttendance = seasonAttendance;
            CurrentMoney = currentMoney;
        }

        public string OperationId { get; }
        public string SeasonId { get; }
        public int WeekIndex { get; }
        public LeagueGrade LeagueGrade { get; }
        public double FanBase { get; }
        public long SeasonAttendance { get; }
        public long CurrentMoney { get; }
    }
}
