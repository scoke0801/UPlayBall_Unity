using System;
using System.Collections.Generic;
using Baseball.Core.Historical;

namespace Baseball.Presentation.Owner
{
    /// <summary>주간 또는 시즌 구단 재무 원본을 UI 계산 없이 전달한다.</summary>
    public sealed class OwnerFinanceSnapshot
    {
        public OwnerFinanceSnapshot(
            long moneyIncome,
            long moneyExpense,
            int scoutingPointProduction,
            int developmentPointProduction,
            int homeGames,
            long attendance)
        {
            if (moneyIncome < 0L || moneyExpense < 0L || scoutingPointProduction < 0 ||
                developmentPointProduction < 0 || homeGames < 0 || attendance < 0L)
                throw new ArgumentOutOfRangeException(nameof(moneyIncome));
            MoneyIncome = moneyIncome;
            MoneyExpense = moneyExpense;
            ScoutingPointProduction = scoutingPointProduction;
            DevelopmentPointProduction = developmentPointProduction;
            HomeGames = homeGames;
            Attendance = attendance;
        }

        public long MoneyIncome { get; }
        public long MoneyExpense { get; }
        public int ScoutingPointProduction { get; }
        public int DevelopmentPointProduction { get; }
        public int HomeGames { get; }
        public long Attendance { get; }
    }

    /// <summary>Resolver와 BalanceTable에서 확정된 시설 한 칸의 표시 입력이다.</summary>
    public sealed class OwnerFacilitySnapshot
    {
        public OwnerFacilitySnapshot(
            FacilityType facilityType,
            int level,
            int maximumLevel,
            long? nextUpgradeMoneyCost,
            bool canUpgrade,
            string upgradeDisabledReason,
            int weeklyScoutingPointProduction = 0,
            int? scoutingPointStorageCapacity = null,
            int weeklyDevelopmentPointProduction = 0,
            int? developmentPointStorageCapacity = null,
            double conditionRecoveryEfficiencyModifier = 0d,
            double scoutingConfidenceModifier = 0d,
            double tacticResearchEfficiencyModifier = 0d,
            long fanShopRevenuePerAttendee = 0L,
            double fanShopPopularityRetention = 0d)
        {
            if (!Enum.IsDefined(typeof(FacilityType), facilityType))
                throw new ArgumentOutOfRangeException(nameof(facilityType));
            if (level < 0 || maximumLevel < level || nextUpgradeMoneyCost < 0L ||
                weeklyScoutingPointProduction < 0 || weeklyDevelopmentPointProduction < 0 ||
                scoutingPointStorageCapacity < 0 || developmentPointStorageCapacity < 0 ||
                conditionRecoveryEfficiencyModifier < 0d || scoutingConfidenceModifier < 0d ||
                tacticResearchEfficiencyModifier < 0d || fanShopRevenuePerAttendee < 0L ||
                fanShopPopularityRetention < 0d ||
                double.IsNaN(conditionRecoveryEfficiencyModifier) ||
                double.IsInfinity(conditionRecoveryEfficiencyModifier) ||
                double.IsNaN(scoutingConfidenceModifier) ||
                double.IsInfinity(scoutingConfidenceModifier) ||
                double.IsNaN(tacticResearchEfficiencyModifier) ||
                double.IsInfinity(tacticResearchEfficiencyModifier) ||
                double.IsNaN(fanShopPopularityRetention) ||
                double.IsInfinity(fanShopPopularityRetention))
                throw new ArgumentOutOfRangeException(nameof(level));
            if (level == maximumLevel && nextUpgradeMoneyCost.HasValue)
                throw new ArgumentException("최대 레벨 시설에는 다음 업그레이드 비용을 표시할 수 없습니다.");
            if (canUpgrade && !nextUpgradeMoneyCost.HasValue)
                throw new ArgumentException("업그레이드 가능한 시설에는 다음 비용이 필요합니다.");
            if (!canUpgrade && string.IsNullOrWhiteSpace(upgradeDisabledReason))
                throw new ArgumentException("업그레이드 불가 상태에는 Resolver 사유가 필요합니다.", nameof(upgradeDisabledReason));

            FacilityType = facilityType;
            Level = level;
            MaximumLevel = maximumLevel;
            NextUpgradeMoneyCost = nextUpgradeMoneyCost;
            CanUpgrade = canUpgrade;
            UpgradeDisabledReason = upgradeDisabledReason ?? string.Empty;
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

        public FacilityType FacilityType { get; }
        public int Level { get; }
        public int MaximumLevel { get; }
        public long? NextUpgradeMoneyCost { get; }
        public bool CanUpgrade { get; }
        public string UpgradeDisabledReason { get; }
        public int WeeklyScoutingPointProduction { get; }
        public int? ScoutingPointStorageCapacity { get; }
        public int WeeklyDevelopmentPointProduction { get; }
        public int? DevelopmentPointStorageCapacity { get; }
        public double ConditionRecoveryEfficiencyModifier { get; }
        public double ScoutingConfidenceModifier { get; }
        public double TacticResearchEfficiencyModifier { get; }
        public long FanShopRevenuePerAttendee { get; }
        public double FanShopPopularityRetention { get; }
    }

    /// <summary>구단 경영 화면에 필요한 확정 상태와 Resolver Preview만 묶는다.</summary>
    public sealed class OwnerClubOperationSnapshot
    {
        private readonly OwnerFacilitySnapshot[] _facilities;

        public OwnerClubOperationSnapshot(
            int stadiumLevel,
            int stadiumCapacity,
            long? nextStadiumUpgradeMoneyCost,
            bool canUpgradeStadium,
            string stadiumUpgradeDisabledReason,
            double fanBase,
            double popularity,
            int? expectedAttendance,
            int? recentAttendance,
            TicketPriceTier ticketPriceTier,
            IReadOnlyList<OwnerFacilitySnapshot> facilities,
            OwnerFinanceSnapshot weeklyFinance,
            OwnerFinanceSnapshot seasonFinance)
        {
            if (stadiumLevel <= 0 || stadiumCapacity <= 0 || nextStadiumUpgradeMoneyCost < 0L)
                throw new ArgumentOutOfRangeException(nameof(stadiumLevel));
            if (fanBase < 0d || fanBase > 100d || popularity < 0d || popularity > 100d ||
                double.IsNaN(fanBase) || double.IsNaN(popularity))
                throw new ArgumentOutOfRangeException(nameof(fanBase));
            if (expectedAttendance < 0 || recentAttendance < 0)
                throw new ArgumentOutOfRangeException(nameof(expectedAttendance));
            if (!Enum.IsDefined(typeof(TicketPriceTier), ticketPriceTier))
                throw new ArgumentOutOfRangeException(nameof(ticketPriceTier));
            if (canUpgradeStadium && !nextStadiumUpgradeMoneyCost.HasValue)
                throw new ArgumentException("증축 가능한 구장에는 다음 비용이 필요합니다.");
            if (!canUpgradeStadium && string.IsNullOrWhiteSpace(stadiumUpgradeDisabledReason))
                throw new ArgumentException("증축 불가 상태에는 Resolver 사유가 필요합니다.", nameof(stadiumUpgradeDisabledReason));

            StadiumLevel = stadiumLevel;
            StadiumCapacity = stadiumCapacity;
            NextStadiumUpgradeMoneyCost = nextStadiumUpgradeMoneyCost;
            CanUpgradeStadium = canUpgradeStadium;
            StadiumUpgradeDisabledReason = stadiumUpgradeDisabledReason ?? string.Empty;
            FanBase = fanBase;
            Popularity = popularity;
            ExpectedAttendance = expectedAttendance;
            RecentAttendance = recentAttendance;
            TicketPriceTier = ticketPriceTier;
            _facilities = CopyFacilities(facilities);
            WeeklyFinance = weeklyFinance ?? throw new ArgumentNullException(nameof(weeklyFinance));
            SeasonFinance = seasonFinance ?? throw new ArgumentNullException(nameof(seasonFinance));
        }

        public int StadiumLevel { get; }
        public int StadiumCapacity { get; }
        public long? NextStadiumUpgradeMoneyCost { get; }
        public bool CanUpgradeStadium { get; }
        public string StadiumUpgradeDisabledReason { get; }
        public double FanBase { get; }
        public double Popularity { get; }
        public int? ExpectedAttendance { get; }
        public int? RecentAttendance { get; }
        public TicketPriceTier TicketPriceTier { get; }
        public IReadOnlyList<OwnerFacilitySnapshot> Facilities => _facilities;
        public OwnerFinanceSnapshot WeeklyFinance { get; }
        public OwnerFinanceSnapshot SeasonFinance { get; }

        private static OwnerFacilitySnapshot[] CopyFacilities(IReadOnlyList<OwnerFacilitySnapshot> source)
        {
            int count = Enum.GetValues(typeof(FacilityType)).Length;
            if (source == null || source.Count != count)
                throw new ArgumentException("여섯 FacilityType의 표시 Snapshot이 모두 필요합니다.", nameof(source));
            var result = new OwnerFacilitySnapshot[count];
            var found = new bool[count];
            for (int index = 0; index < source.Count; index++)
            {
                OwnerFacilitySnapshot facility = source[index]
                    ?? throw new ArgumentException("null 시설 Snapshot이 있습니다.", nameof(source));
                int typeIndex = (int)facility.FacilityType;
                if (found[typeIndex])
                    throw new ArgumentException("FacilityType은 중복될 수 없습니다.", nameof(source));
                found[typeIndex] = true;
                result[typeIndex] = facility;
            }
            return result;
        }
    }

    /// <summary>구단 화면 시설 한 줄의 완성된 표시 문구와 Command 가능 상태다.</summary>
    public sealed class OwnerFacilityPresentationRow
    {
        internal OwnerFacilityPresentationRow(
            FacilityType facilityType,
            string name,
            string levelText,
            string effectPreviewText,
            string upgradeCostText,
            bool canUpgrade,
            string upgradeDisabledReason)
        {
            FacilityType = facilityType;
            Name = name;
            LevelText = levelText;
            EffectPreviewText = effectPreviewText;
            UpgradeCostText = upgradeCostText;
            CanUpgrade = canUpgrade;
            UpgradeDisabledReason = upgradeDisabledReason;
        }

        public FacilityType FacilityType { get; }
        public string Name { get; }
        public string LevelText { get; }
        public string EffectPreviewText { get; }
        public string UpgradeCostText { get; }
        public bool CanUpgrade { get; }
        public string UpgradeDisabledReason { get; }
    }

    /// <summary>주간·시즌 재무를 같은 표기 규칙으로 표시한다.</summary>
    public sealed class OwnerFinancePresentationModel
    {
        internal OwnerFinancePresentationModel(string title, OwnerFinanceSnapshot snapshot)
        {
            Title = title;
            IncomeText = $"수입 {FormatMoney(snapshot.MoneyIncome)}";
            ExpenseText = $"지출 {FormatMoney(snapshot.MoneyExpense)}";
            NetText = $"순이익 {FormatSignedMoney(snapshot.MoneyIncome - snapshot.MoneyExpense)}";
            ProductionText = $"SP +{snapshot.ScoutingPointProduction:N0} · DP +{snapshot.DevelopmentPointProduction:N0}";
            AttendanceText = snapshot.HomeGames == 0
                ? "홈 경기 없음"
                : $"홈 {snapshot.HomeGames:N0}경기 · 관중 {snapshot.Attendance:N0}명";
        }

        public string Title { get; }
        public string IncomeText { get; }
        public string ExpenseText { get; }
        public string NetText { get; }
        public string ProductionText { get; }
        public string AttendanceText { get; }

        private static string FormatMoney(long value) => OwnerMoneyFormatter.Format(value);

        private static string FormatSignedMoney(long value) => OwnerMoneyFormatter.FormatSigned(value);
    }

    /// <summary>구단 경영 Runtime View가 그대로 바인딩하는 불변 모델이다.</summary>
    public sealed class OwnerClubOperationPresentationModel
    {
        internal OwnerClubOperationPresentationModel(
            OwnerClubOperationSnapshot snapshot,
            string stadiumText,
            string stadiumUpgradeText,
            string fanBaseText,
            string popularityText,
            string expectedAttendanceText,
            string recentAttendanceText,
            string ticketPolicyText,
            IReadOnlyList<OwnerFacilityPresentationRow> facilities,
            OwnerFinancePresentationModel weeklyFinance,
            OwnerFinancePresentationModel seasonFinance)
        {
            Snapshot = snapshot;
            StadiumText = stadiumText;
            StadiumUpgradeText = stadiumUpgradeText;
            FanBaseText = fanBaseText;
            PopularityText = popularityText;
            ExpectedAttendanceText = expectedAttendanceText;
            RecentAttendanceText = recentAttendanceText;
            TicketPolicyText = ticketPolicyText;
            Facilities = facilities;
            WeeklyFinance = weeklyFinance;
            SeasonFinance = seasonFinance;
        }

        public OwnerClubOperationSnapshot Snapshot { get; }
        public string StadiumText { get; }
        public string StadiumUpgradeText { get; }
        public string FanBaseText { get; }
        public string PopularityText { get; }
        public string ExpectedAttendanceText { get; }
        public string RecentAttendanceText { get; }
        public string TicketPolicyText { get; }
        public IReadOnlyList<OwnerFacilityPresentationRow> Facilities { get; }
        public OwnerFinancePresentationModel WeeklyFinance { get; }
        public OwnerFinancePresentationModel SeasonFinance { get; }
    }

    /// <summary>확정된 구단 운영 Snapshot을 한국어 표시 문구로만 변환한다.</summary>
    public static class OwnerClubOperationPresentationBuilder
    {
        public static OwnerClubOperationPresentationModel Build(OwnerClubOperationSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var rows = new OwnerFacilityPresentationRow[snapshot.Facilities.Count];
            for (int index = 0; index < rows.Length; index++)
                rows[index] = BuildFacility(snapshot.Facilities[index]);

            string stadiumUpgrade = snapshot.NextStadiumUpgradeMoneyCost.HasValue
                ? $"다음 증축 {FormatMoney(snapshot.NextStadiumUpgradeMoneyCost.Value)}"
                : "최대 규모";
            if (!snapshot.CanUpgradeStadium)
                stadiumUpgrade = string.Concat(stadiumUpgrade, " · ", snapshot.StadiumUpgradeDisabledReason);

            return new OwnerClubOperationPresentationModel(
                snapshot,
                $"구장 Lv.{snapshot.StadiumLevel} · {snapshot.StadiumCapacity:N0}석",
                stadiumUpgrade,
                $"FanBase {snapshot.FanBase:0.0}",
                $"Popularity {snapshot.Popularity:0.0}",
                $"예상 관중 {FormatAttendance(snapshot.ExpectedAttendance)}",
                $"최근 관중 {FormatAttendance(snapshot.RecentAttendance)}",
                $"티켓 정책 · {FormatTicket(snapshot.TicketPriceTier)}",
                rows,
                new OwnerFinancePresentationModel("이번 주", snapshot.WeeklyFinance),
                new OwnerFinancePresentationModel("이번 시즌", snapshot.SeasonFinance));
        }

        private static OwnerFacilityPresentationRow BuildFacility(OwnerFacilitySnapshot source)
        {
            string upgradeCost = source.NextUpgradeMoneyCost.HasValue
                ? FormatMoney(source.NextUpgradeMoneyCost.Value)
                : "최대 레벨";
            return new OwnerFacilityPresentationRow(
                source.FacilityType,
                FormatFacilityName(source.FacilityType),
                $"Lv.{source.Level}/{source.MaximumLevel}",
                FormatFacilityEffect(source),
                upgradeCost,
                source.CanUpgrade,
                source.UpgradeDisabledReason);
        }

        private static string FormatFacilityEffect(OwnerFacilitySnapshot source)
        {
            switch (source.FacilityType)
            {
                case FacilityType.ScoutingCenter:
                    return source.ScoutingPointStorageCapacity.HasValue
                        ? $"주간 SP +{source.WeeklyScoutingPointProduction:N0} · 저장 {source.ScoutingPointStorageCapacity.Value:N0}"
                        : "SP 생산 없음";
                case FacilityType.TrainingCenter:
                    return source.DevelopmentPointStorageCapacity.HasValue
                        ? $"주간 DP +{source.WeeklyDevelopmentPointProduction:N0} · 저장 {source.DevelopmentPointStorageCapacity.Value:N0}"
                        : "DP 생산 없음";
                case FacilityType.RecoveryCenter:
                    return FormatPercentEffect("회복 효율", source.ConditionRecoveryEfficiencyModifier);
                case FacilityType.DataAnalysisCenter:
                    return FormatPercentEffect("상대 분석 신뢰도", source.ScoutingConfidenceModifier);
                case FacilityType.TacticLab:
                    return FormatPercentEffect("전술 연구 효율", source.TacticResearchEfficiencyModifier);
                case FacilityType.FanShop:
                    return source.FanShopRevenuePerAttendee == 0L
                        ? "부가 수익 없음"
                        : $"관중 1인당 +{FormatMoney(source.FanShopRevenuePerAttendee)} · 인기도 유지 +{source.FanShopPopularityRetention:P0}";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static string FormatPercentEffect(string label, double modifier) =>
            modifier == 0d ? string.Concat(label, " 효과 없음") : $"{label} +{modifier:P0}";

        private static string FormatFacilityName(FacilityType type)
        {
            switch (type)
            {
                case FacilityType.ScoutingCenter: return "스카우팅 센터";
                case FacilityType.TrainingCenter: return "트레이닝 센터";
                case FacilityType.RecoveryCenter: return "회복 센터";
                case FacilityType.DataAnalysisCenter: return "데이터 분석실";
                case FacilityType.TacticLab: return "전술 연구소";
                case FacilityType.FanShop: return "팬샵";
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private static string FormatTicket(TicketPriceTier tier)
        {
            switch (tier)
            {
                case TicketPriceTier.Cheap: return "할인";
                case TicketPriceTier.Standard: return "일반";
                case TicketPriceTier.Premium: return "프리미엄";
                default: throw new ArgumentOutOfRangeException(nameof(tier));
            }
        }

        private static string FormatAttendance(int? attendance) =>
            attendance.HasValue ? $"{attendance.Value:N0}명" : "정보 부족";

        private static string FormatMoney(long value) => OwnerMoneyFormatter.Format(value);
    }
}
