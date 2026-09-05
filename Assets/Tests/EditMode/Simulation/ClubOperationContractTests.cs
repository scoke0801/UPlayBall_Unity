using System;
using System.Reflection;
using Baseball.Core.Balance;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Simulation
{
    /// <summary>구단 운영 경제가 상한, 해금 조건, 영수증 멱등성과 시스템 경계를 지키는지 검증한다.</summary>
    public sealed class ClubOperationContractTests
    {
        private const string TeamSeasonKey = "economy-team-2026";
        private const string SeasonId = "season-2026";
        private const int WeekIndex = 7;

        [Test]
        public void Attendance_같은Seed는같은결과이고구장수용력을넘지않는다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(
                balance,
                fanBase: 100d,
                popularity: 100d,
                attendanceMomentum: 100d,
                ticketPriceTier: TicketPriceTier.Cheap);
            HomeGameContext context = CreateGameContext(
                "attendance-capacity",
                LeagueGrade.Galaxy,
                recentPerformance: 1d,
                opponentAttraction: 1d,
                seasonImportance: 1d,
                rivalryStoryStrength: 1d);
            var resolver = new AttendanceResolver(balance);

            AttendanceResult first = resolver.Resolve(context, operation, new Pcg32Random(932_771UL));
            AttendanceResult second = resolver.Resolve(context, operation, new Pcg32Random(932_771UL));

            Assert.That(first.ExpectedDemand, Is.EqualTo(second.ExpectedDemand));
            Assert.That(first.Attendance, Is.EqualTo(second.Attendance));
            Assert.That(first.Capacity, Is.EqualTo(second.Capacity));
            Assert.That(first.Attendance, Is.LessThanOrEqualTo(first.Capacity));
            Assert.That(first.Attendance, Is.EqualTo(operation.Stadium.Capacity),
                "최대 수요 입력은 최소 구장을 가득 채워 capacity clamp 경계를 실제로 통과해야 한다.");
        }

        [Test]
        public void TicketPolicy_가격이오르면수요는감소하고인당수익은증가한다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            var resolver = new HomeGameFinanceResolver(balance);
            HomeGameContext context = CreateGameContext("ticket-policy", LeagueGrade.Rookie);
            ClubOperationState cheapOperation = CreateOperation(
                balance,
                fanBase: 20d,
                popularity: 20d,
                attendanceMomentum: 20d,
                ticketPriceTier: TicketPriceTier.Cheap);
            ClubOperationState standardOperation = CreateOperation(
                balance,
                fanBase: 20d,
                popularity: 20d,
                attendanceMomentum: 20d,
                ticketPriceTier: TicketPriceTier.Standard);
            ClubOperationState premiumOperation = CreateOperation(
                balance,
                fanBase: 20d,
                popularity: 20d,
                attendanceMomentum: 20d,
                ticketPriceTier: TicketPriceTier.Premium);

            HomeGameFinanceResult cheap = resolver.Resolve(context, cheapOperation, new Pcg32Random(17UL));
            HomeGameFinanceResult standard = resolver.Resolve(context, standardOperation, new Pcg32Random(17UL));
            HomeGameFinanceResult premium = resolver.Resolve(context, premiumOperation, new Pcg32Random(17UL));

            Assert.That(cheap.AttendanceResult.ExpectedDemand, Is.GreaterThan(standard.AttendanceResult.ExpectedDemand));
            Assert.That(standard.AttendanceResult.ExpectedDemand, Is.GreaterThan(premium.AttendanceResult.ExpectedDemand));
            Assert.That(cheap.Attendance, Is.GreaterThan(standard.Attendance));
            Assert.That(standard.Attendance, Is.GreaterThan(premium.Attendance));
            Assert.That(
                cheap.TicketRevenue,
                Is.EqualTo((long)cheap.Attendance * balance.GetTicketPolicy(TicketPriceTier.Cheap).RevenuePerAttendee));
            Assert.That(
                premium.TicketRevenue,
                Is.EqualTo((long)premium.Attendance * balance.GetTicketPolicy(TicketPriceTier.Premium).RevenuePerAttendee));
            Assert.That(
                balance.GetTicketPolicy(TicketPriceTier.Premium).RevenuePerAttendee,
                Is.GreaterThan(balance.GetTicketPolicy(TicketPriceTier.Standard).RevenuePerAttendee));
        }

        [Test]
        public void FacilityUpgrade_리그팬관중MoneyGate를순서대로검증하고정확한경계에서승인한다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(balance, scoutingCenterLevel: 1);
            Assert.That(
                balance.TryGetNextFacilityLevel(FacilityType.ScoutingCenter, 1, out FacilityLevelDefinition next),
                Is.True);
            var resolver = new ClubUpgradeResolver(balance);

            FacilityUpgradeResult leagueLocked = resolver.ResolveFacilityUpgrade(
                operation,
                FacilityType.ScoutingCenter,
                CreateUpgradeContext("league-lock", LeagueGrade.Minor, 100d, long.MaxValue, long.MaxValue));
            FacilityUpgradeResult fanLocked = resolver.ResolveFacilityUpgrade(
                operation,
                FacilityType.ScoutingCenter,
                CreateUpgradeContext(
                    "fan-lock",
                    next.RequiredLeagueGrade.Value,
                    next.MinimumFanBase - 0.01d,
                    long.MaxValue,
                    long.MaxValue));
            FacilityUpgradeResult attendanceLocked = resolver.ResolveFacilityUpgrade(
                operation,
                FacilityType.ScoutingCenter,
                CreateUpgradeContext(
                    "attendance-lock",
                    next.RequiredLeagueGrade.Value,
                    next.MinimumFanBase,
                    next.MinimumSeasonAttendance - 1L,
                    long.MaxValue));
            FacilityUpgradeResult moneyLocked = resolver.ResolveFacilityUpgrade(
                operation,
                FacilityType.ScoutingCenter,
                CreateUpgradeContext(
                    "money-lock",
                    next.RequiredLeagueGrade.Value,
                    next.MinimumFanBase,
                    next.MinimumSeasonAttendance,
                    next.UpgradeMoneyCost - 1L));
            FacilityUpgradeResult approved = resolver.ResolveFacilityUpgrade(
                operation,
                FacilityType.ScoutingCenter,
                CreateUpgradeContext(
                    "approved",
                    next.RequiredLeagueGrade.Value,
                    next.MinimumFanBase,
                    next.MinimumSeasonAttendance,
                    next.UpgradeMoneyCost));

            Assert.That(leagueLocked.Status, Is.EqualTo(ClubUpgradeStatus.LeagueGradeLocked));
            Assert.That(fanLocked.Status, Is.EqualTo(ClubUpgradeStatus.FanBaseLocked));
            Assert.That(attendanceLocked.Status, Is.EqualTo(ClubUpgradeStatus.SeasonAttendanceLocked));
            Assert.That(moneyLocked.Status, Is.EqualTo(ClubUpgradeStatus.InsufficientMoney));
            Assert.That(approved.Status, Is.EqualTo(ClubUpgradeStatus.Approved));
            Assert.That(approved.MoneyCost, Is.EqualTo(next.UpgradeMoneyCost));
            Assert.That(approved.Receipt.ResourceDelta.Money, Is.EqualTo(-next.UpgradeMoneyCost));
        }

        [Test]
        public void FacilityUpgrade_승인영수증과상태변경은한번만반영된다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(balance, scoutingCenterLevel: 1);
            FacilityLevelDefinition next = GetNextFacility(balance, FacilityType.ScoutingCenter, 1);
            var resolver = new ClubUpgradeResolver(balance);
            ClubUpgradeContext context = CreateUpgradeContext(
                "upgrade-once",
                next.RequiredLeagueGrade.Value,
                next.MinimumFanBase,
                next.MinimumSeasonAttendance,
                next.UpgradeMoneyCost);
            FacilityUpgradeResult result = resolver.ResolveFacilityUpgrade(
                operation,
                FacilityType.ScoutingCenter,
                context);

            Assert.That(operation.TryApplyFacilityUpgrade(result), Is.True);
            Assert.That(operation.GetFacility(FacilityType.ScoutingCenter).Level, Is.EqualTo(2));
            Assert.That(operation.CurrentWeek.MoneyExpense, Is.EqualTo(next.UpgradeMoneyCost));
            Assert.That(operation.CurrentSeason.MoneyExpense, Is.EqualTo(next.UpgradeMoneyCost));
            Assert.That(operation.Receipts.Count, Is.EqualTo(1));

            Assert.That(operation.TryApplyFacilityUpgrade(result), Is.False);
            Assert.That(operation.CurrentWeek.MoneyExpense, Is.EqualTo(next.UpgradeMoneyCost));
            Assert.That(operation.CurrentSeason.MoneyExpense, Is.EqualTo(next.UpgradeMoneyCost));
            Assert.That(operation.Receipts.Count, Is.EqualTo(1));
            Assert.That(
                resolver.ResolveFacilityUpgrade(operation, FacilityType.ScoutingCenter, context).Status,
                Is.EqualTo(ClubUpgradeStatus.AlreadyApplied));
        }

        [Test]
        public void WeeklyProduction_SP와DP를생산하되각저장상한에서잘라낸다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(
                balance,
                scoutingCenterLevel: 1,
                trainingCenterLevel: 1);
            FacilityLevelDefinition scouting = balance.GetFacilityLevel(FacilityType.ScoutingCenter, 1);
            FacilityLevelDefinition training = balance.GetFacilityLevel(FacilityType.TrainingCenter, 1);
            var resolver = new WeeklyFacilityProductionResolver(balance);

            WeeklyFacilityProductionResult nearCapacity = resolver.Resolve(
                operation,
                new WeeklyFacilityProductionContext(
                    SeasonId,
                    WeekIndex,
                    LeagueGrade.Rookie,
                    long.MaxValue,
                    scouting.ScoutingPointStorageCapacity.Value - 10,
                    training.DevelopmentPointStorageCapacity.Value - 5));
            WeeklyFacilityProductionResult atCapacity = resolver.Resolve(
                operation,
                new WeeklyFacilityProductionContext(
                    SeasonId,
                    WeekIndex + 1,
                    LeagueGrade.Rookie,
                    long.MaxValue,
                    scouting.ScoutingPointStorageCapacity.Value,
                    training.DevelopmentPointStorageCapacity.Value));

            Assert.That(nearCapacity.Status, Is.EqualTo(WeeklyFacilityProductionStatus.Produced));
            Assert.That(nearCapacity.ScoutingPointProduction, Is.EqualTo(10));
            Assert.That(nearCapacity.DevelopmentPointProduction, Is.EqualTo(5));
            Assert.That(nearCapacity.Receipt.ResourceDelta.ScoutingPoints, Is.EqualTo(10));
            Assert.That(nearCapacity.Receipt.ResourceDelta.DevelopmentPoints, Is.EqualTo(5));
            Assert.That(atCapacity.ScoutingPointProduction, Is.Zero);
            Assert.That(atCapacity.DevelopmentPointProduction, Is.Zero);
        }

        [Test]
        public void WeeklyProduction_유지비가부족하면생산하지않고같은주재시도를막는다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(
                balance,
                scoutingCenterLevel: 1,
                trainingCenterLevel: 1);
            var resolver = new WeeklyFacilityProductionResolver(balance);
            var insufficient = new WeeklyFacilityProductionContext(
                SeasonId,
                WeekIndex,
                LeagueGrade.Rookie,
                0L,
                0,
                0);

            WeeklyFacilityProductionResult suspended = resolver.Resolve(operation, insufficient);

            Assert.That(suspended.Status,
                Is.EqualTo(WeeklyFacilityProductionStatus.SuspendedForInsufficientOperatingMoney));
            Assert.That(suspended.OperatingCost, Is.GreaterThan(0L));
            Assert.That(suspended.ScoutingPointProduction, Is.Zero);
            Assert.That(suspended.DevelopmentPointProduction, Is.Zero);
            Assert.That(suspended.Receipt.ResourceDelta.Money, Is.Zero);
            Assert.That(operation.TryApplyWeeklyProduction(suspended), Is.True);

            WeeklyFacilityProductionResult retry = resolver.Resolve(
                operation,
                new WeeklyFacilityProductionContext(
                    SeasonId,
                    WeekIndex,
                    LeagueGrade.Rookie,
                    long.MaxValue,
                    0,
                    0));
            Assert.That(retry.Status, Is.EqualTo(WeeklyFacilityProductionStatus.AlreadyApplied));
            Assert.That(operation.TryApplyWeeklyProduction(retry), Is.False);
            Assert.That(operation.CurrentWeek.ScoutingPointProduction, Is.Zero);
            Assert.That(operation.CurrentWeek.DevelopmentPointProduction, Is.Zero);
            Assert.That(operation.Receipts.Count, Is.EqualTo(1));
        }

        [Test]
        public void FanShop_같은관중에서인당부가수익과인기도유지를추가한다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            var resolver = new HomeGameFinanceResolver(balance);
            HomeGameContext context = CreateGameContext("fan-shop", LeagueGrade.Major);
            ClubOperationState withoutShop = CreateOperation(balance, fanShopLevel: 0);
            ClubOperationState withShop = CreateOperation(balance, fanShopLevel: 2);

            HomeGameFinanceResult baseline = resolver.Resolve(context, withoutShop, new Pcg32Random(3_901UL));
            HomeGameFinanceResult enhanced = resolver.Resolve(context, withShop, new Pcg32Random(3_901UL));
            ClubFacilityEffectProfile effect = new ClubFacilityEffectResolver(balance).Resolve(withShop);

            Assert.That(enhanced.Attendance, Is.EqualTo(baseline.Attendance));
            Assert.That(baseline.FanShopRevenue, Is.Zero);
            Assert.That(enhanced.FanShopRevenue,
                Is.EqualTo((long)enhanced.Attendance * effect.FanShopRevenuePerAttendee));
            Assert.That(enhanced.FanShopRevenue, Is.GreaterThan(0L));
            Assert.That(enhanced.FanPopularity.FanShopPopularityRetention,
                Is.EqualTo(effect.FanShopPopularityRetention));
            Assert.That(enhanced.PopularityDelta, Is.GreaterThan(baseline.PopularityDelta));
        }

        [Test]
        public void AwayGame_홈관중수익팬변화와영수증을전혀만들지않는다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(balance, fanShopLevel: 3);
            double fanBase = operation.FanBase;
            double popularity = operation.Popularity;
            double momentum = operation.AttendanceMomentum;
            HomeGameContext context = CreateGameContext(
                "away-no-revenue",
                LeagueGrade.Galaxy,
                venue: GameVenue.Away,
                recentPerformance: 1d,
                opponentAttraction: 1d,
                seasonImportance: 1d,
                rivalryStoryStrength: 1d);

            HomeGameFinanceResult result = new HomeGameFinanceResolver(balance).Resolve(
                context,
                operation,
                new ThrowingRandomSource());

            Assert.That(result.Status, Is.EqualTo(HomeGameFinanceStatus.NotHomeGame));
            Assert.That(result.Attendance, Is.Zero);
            Assert.That(result.TicketRevenue, Is.Zero);
            Assert.That(result.FanShopRevenue, Is.Zero);
            Assert.That(result.OtherGameRevenue, Is.Zero);
            Assert.That(result.OperatingCost, Is.Zero);
            Assert.That(result.Receipt, Is.Null);
            Assert.That(operation.TryApplyHomeGame(result), Is.False);
            Assert.That(operation.FanBase, Is.EqualTo(fanBase));
            Assert.That(operation.Popularity, Is.EqualTo(popularity));
            Assert.That(operation.AttendanceMomentum, Is.EqualTo(momentum));
            Assert.That(operation.Receipts, Is.Empty);
        }

        [Test]
        public void Receipt_동일결과를재적용해도Ledger와팬상태가중복변경되지않는다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(
                balance,
                scoutingCenterLevel: 1,
                trainingCenterLevel: 1,
                fanShopLevel: 1);
            WeeklyFacilityProductionResult production = new WeeklyFacilityProductionResolver(balance).Resolve(
                operation,
                new WeeklyFacilityProductionContext(
                    SeasonId,
                    WeekIndex,
                    LeagueGrade.Rookie,
                    long.MaxValue,
                    0,
                    0));
            HomeGameFinanceResult game = new HomeGameFinanceResolver(balance).Resolve(
                CreateGameContext("duplicate-game", LeagueGrade.Rookie),
                operation,
                new Pcg32Random(1_203UL));

            Assert.That(operation.TryApplyWeeklyProduction(production), Is.True);
            Assert.That(operation.TryApplyHomeGame(game), Is.True);
            int receiptCount = operation.Receipts.Count;
            long weeklyNet = operation.CurrentWeek.NetMoney;
            long seasonNet = operation.CurrentSeason.NetMoney;
            int seasonHomeGames = operation.CurrentSeason.HomeGames;
            double fanBase = operation.FanBase;
            double popularity = operation.Popularity;

            Assert.That(operation.TryApplyWeeklyProduction(production), Is.False);
            Assert.That(operation.TryApplyHomeGame(game), Is.False);
            Assert.That(operation.Receipts.Count, Is.EqualTo(receiptCount));
            Assert.That(operation.CurrentWeek.NetMoney, Is.EqualTo(weeklyNet));
            Assert.That(operation.CurrentSeason.NetMoney, Is.EqualTo(seasonNet));
            Assert.That(operation.CurrentSeason.HomeGames, Is.EqualTo(seasonHomeGames));
            Assert.That(operation.FanBase, Is.EqualTo(fanBase));
            Assert.That(operation.Popularity, Is.EqualTo(popularity));
        }

        [Test]
        public void Receipt_SaveLoad동등복원후같은주와경기를재계산하지않는다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState source = CreateOperation(
                balance,
                scoutingCenterLevel: 1,
                trainingCenterLevel: 1,
                fanShopLevel: 1);
            var productionResolver = new WeeklyFacilityProductionResolver(balance);
            var financeResolver = new HomeGameFinanceResolver(balance);
            var productionContext = new WeeklyFacilityProductionContext(
                SeasonId,
                WeekIndex,
                LeagueGrade.Rookie,
                long.MaxValue,
                0,
                0);
            HomeGameContext gameContext = CreateGameContext("load-game", LeagueGrade.Rookie);
            Assert.That(source.TryApplyWeeklyProduction(productionResolver.Resolve(source, productionContext)), Is.True);
            Assert.That(source.TryApplyHomeGame(
                financeResolver.Resolve(gameContext, source, new Pcg32Random(74UL))), Is.True);
            ClubOperationState loaded = ReloadOperation(source);
            int receiptCount = loaded.Receipts.Count;
            long weeklyNet = loaded.CurrentWeek.NetMoney;
            long seasonNet = loaded.CurrentSeason.NetMoney;
            int homeGames = loaded.CurrentSeason.HomeGames;

            WeeklyFacilityProductionResult duplicateProduction = productionResolver.Resolve(loaded, productionContext);
            HomeGameFinanceResult duplicateGame = financeResolver.Resolve(
                gameContext,
                loaded,
                new ThrowingRandomSource());

            Assert.That(duplicateProduction.Status, Is.EqualTo(WeeklyFacilityProductionStatus.AlreadyApplied));
            Assert.That(duplicateGame.Status, Is.EqualTo(HomeGameFinanceStatus.AlreadyApplied));
            Assert.That(loaded.TryApplyWeeklyProduction(duplicateProduction), Is.False);
            Assert.That(loaded.TryApplyHomeGame(duplicateGame), Is.False);
            Assert.That(loaded.Receipts.Count, Is.EqualTo(receiptCount));
            Assert.That(loaded.CurrentWeek.NetMoney, Is.EqualTo(weeklyNet));
            Assert.That(loaded.CurrentSeason.NetMoney, Is.EqualTo(seasonNet));
            Assert.That(loaded.CurrentSeason.HomeGames, Is.EqualTo(homeGames));
        }

        [Test]
        public void FacilityEffect_선수BaseStat을바꾸지않고명시적ContextModifier만반환한다()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            ClubOperationState operation = CreateOperation(
                balance,
                scoutingCenterLevel: 3,
                trainingCenterLevel: 3,
                recoveryCenterLevel: 3,
                dataAnalysisCenterLevel: 3,
                tacticLabLevel: 3,
                fanShopLevel: 3);
            var batterAttributes = new BatterAttributes(91, 82, 73, 64, 55, 46);
            var pitcherAttributes = new PitcherAttributes(87, 78, 69, 60, 51, 42);

            ClubFacilityEffectProfile effect = new ClubFacilityEffectResolver(balance).Resolve(operation);

            Assert.That(effect.ConditionRecoveryEfficiencyModifier, Is.GreaterThan(0d));
            Assert.That(effect.ScoutingConfidenceModifier, Is.GreaterThan(0d));
            Assert.That(effect.TacticResearchEfficiencyModifier, Is.GreaterThan(0d));
            Assert.That(effect.FanShopRevenuePerAttendee, Is.GreaterThan(0L));
            Assert.That(effect.FanShopPopularityRetention, Is.GreaterThan(0d));
            Assert.That(batterAttributes.Contact, Is.EqualTo(91));
            Assert.That(batterAttributes.Power, Is.EqualTo(82));
            Assert.That(batterAttributes.Speed, Is.EqualTo(73));
            Assert.That(pitcherAttributes.Stamina, Is.EqualTo(87));
            Assert.That(pitcherAttributes.Velocity, Is.EqualTo(78));
            Assert.That(pitcherAttributes.Control, Is.EqualTo(51));

            PropertyInfo[] properties = typeof(ClubFacilityEffectProfile).GetProperties();
            for (int index = 0; index < properties.Length; index++)
            {
                string name = properties[index].Name;
                Assert.That(name, Does.Not.Contain("Contact"));
                Assert.That(name, Does.Not.Contain("Power"));
                Assert.That(name, Does.Not.Contain("Velocity"));
                Assert.That(name, Does.Not.Contain("Control"));
                Assert.That(name, Does.Not.Contain("BaseStat"));
            }
        }

        private static ClubOperationState CreateOperation(
            ClubOperationBalanceTable balance,
            double fanBase = 50d,
            double popularity = 50d,
            double attendanceMomentum = 50d,
            TicketPriceTier ticketPriceTier = TicketPriceTier.Standard,
            int scoutingCenterLevel = 0,
            int trainingCenterLevel = 0,
            int recoveryCenterLevel = 0,
            int dataAnalysisCenterLevel = 0,
            int tacticLabLevel = 0,
            int fanShopLevel = 0)
        {
            StadiumLevelDefinition stadium = balance.GetStadiumLevel(1);
            return new ClubOperationState(
                TeamSeasonKey,
                fanBase,
                popularity,
                attendanceMomentum,
                new StadiumState(stadium.Level, stadium.Capacity),
                new[]
                {
                    new FacilityState(FacilityType.ScoutingCenter, scoutingCenterLevel),
                    new FacilityState(FacilityType.TrainingCenter, trainingCenterLevel),
                    new FacilityState(FacilityType.RecoveryCenter, recoveryCenterLevel),
                    new FacilityState(FacilityType.DataAnalysisCenter, dataAnalysisCenterLevel),
                    new FacilityState(FacilityType.TacticLab, tacticLabLevel),
                    new FacilityState(FacilityType.FanShop, fanShopLevel)
                },
                new TicketPolicy(ticketPriceTier),
                new WeeklyOperationLedger(SeasonId, WeekIndex),
                new SeasonFinanceSummary(SeasonId));
        }

        private static ClubOperationState ReloadOperation(ClubOperationState source)
        {
            WeeklyOperationLedger week = source.CurrentWeek;
            SeasonFinanceSummary season = source.CurrentSeason;
            return new ClubOperationState(
                source.TeamSeasonKey,
                source.FanBase,
                source.Popularity,
                source.AttendanceMomentum,
                source.Stadium,
                source.Facilities,
                source.TicketPolicy,
                new WeeklyOperationLedger(
                    week.SeasonId,
                    week.WeekIndex,
                    week.MoneyIncome,
                    week.MoneyExpense,
                    week.ScoutingPointProduction,
                    week.DevelopmentPointProduction,
                    week.HomeGames,
                    week.Attendance,
                    week.ReceiptCount),
                new SeasonFinanceSummary(
                    season.SeasonId,
                    season.HomeGames,
                    season.Attendance,
                    season.TicketRevenue,
                    season.FanShopRevenue,
                    season.OtherGameRevenue,
                    season.GameOperatingCost,
                    season.MoneyIncome,
                    season.MoneyExpense,
                    season.ScoutingPointProduction,
                    season.DevelopmentPointProduction),
                source.Receipts);
        }

        private static HomeGameContext CreateGameContext(
            string gameId,
            LeagueGrade leagueGrade,
            GameVenue venue = GameVenue.Home,
            double recentPerformance = 0.5d,
            double opponentAttraction = 0.5d,
            double seasonImportance = 0.5d,
            double rivalryStoryStrength = 0d)
        {
            return new HomeGameContext(
                gameId,
                SeasonId,
                WeekIndex,
                TeamSeasonKey,
                "opponent-team-2026",
                venue,
                leagueGrade,
                HomeGameOutcome.Win,
                recentPerformance,
                opponentAttraction,
                seasonImportance,
                rivalryStoryStrength);
        }

        private static ClubUpgradeContext CreateUpgradeContext(
            string operationId,
            LeagueGrade leagueGrade,
            double fanBase,
            long seasonAttendance,
            long currentMoney)
        {
            return new ClubUpgradeContext(
                operationId,
                SeasonId,
                WeekIndex,
                leagueGrade,
                fanBase,
                seasonAttendance,
                currentMoney);
        }

        private static FacilityLevelDefinition GetNextFacility(
            ClubOperationBalanceTable balance,
            FacilityType type,
            int level)
        {
            if (!balance.TryGetNextFacilityLevel(type, level, out FacilityLevelDefinition definition))
                throw new InvalidOperationException("테스트용 다음 시설 레벨이 없습니다.");
            return definition;
        }

        private sealed class ThrowingRandomSource : IRandomSource
        {
            public double NextDouble()
            {
                throw new InvalidOperationException("No-op 경로가 RNG를 소비했습니다.");
            }
        }
    }
}
