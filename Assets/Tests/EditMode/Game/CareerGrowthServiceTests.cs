using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 시즌 결산과 오프시즌 활동이 세이브 루트에서 한 번만 결정론적으로 진행되는지 검증한다.
    /// </summary>
    public sealed class CareerGrowthServiceTests
    {
        [Test]
        public void SettleSeason_성장노쇠수입과12주오프시즌을연결한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(424242UL);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            long moneyBefore = flow.Career.AvailableMoney;
            long salary = flow.Career.CurrentContract.AnnualSalary;
            var service = new CareerGrowthService(flow.Career, NewGameConfiguration.CreateDefault().Balance);

            SeasonGrowthSettlementResult settlement = service.SettleSeasonAndBeginOffseason(
                CreateBatterUsage(),
                bonusIncome: MoneyAmount.FromTenThousandWon(300L));

            Assert.That(flow.Career.SaveVersion, Is.EqualTo(NewGameFlow.CurrentSaveVersion));
            Assert.That(flow.Career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.Offseason));
            Assert.That(flow.Career.CurrentOffseason, Is.SameAs(settlement.Offseason));
            Assert.That(settlement.Offseason.TotalWeeks, Is.EqualTo(12));
            Assert.That(flow.Career.AvailableMoney,
                Is.EqualTo(moneyBefore + salary + MoneyAmount.FromTenThousandWon(300L)));
            Assert.That(settlement.NaturalDevelopment.SourceType, Is.EqualTo(GrowthSourceType.NaturalDevelopment));
            Assert.That(settlement.Aging.SourceType, Is.EqualTo(GrowthSourceType.Aging));
            Assert.That(flow.Career.MyPlayer.GrowthState.GrowthHistory.Count, Is.EqualTo(2));
            Assert.Throws<System.InvalidOperationException>(() =>
                service.SettleSeasonAndBeginOffseason(CreateBatterUsage()));
        }

        [Test]
        public void OffseasonActivity_커리어Seed로결과를확정하고상태를동기화한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(777UL);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            var service = new CareerGrowthService(flow.Career, NewGameConfiguration.CreateDefault().Balance);
            service.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            long moneyBefore = flow.Career.AvailableMoney;

            PlannedOffseasonActivity activity = service.PlanActivity("personal_batting", startWeek: 1);
            service.StartActivity(activity.ActivityId);
            GrowthResultRecord result = service.CompleteActivity(activity.ActivityId);

            Assert.That(activity.Status, Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(activity.RandomSeed, Is.Not.EqualTo(0UL));
            Assert.That(result.RandomSeed, Is.EqualTo(activity.RandomSeed));
            Assert.That(flow.Career.AvailableMoney,
                Is.EqualTo(moneyBefore - MoneyAmount.FromTenThousandWon(300L)));
            Assert.That(flow.Career.CurrentOffseason.CurrentWeek, Is.EqualTo(4));
            Assert.That(flow.Career.MyPlayer.Condition, Is.EqualTo(flow.Career.MyPlayer.GrowthState.Condition));
        }

        [Test]
        public void ExecuteActivity_한번호출로선택기간전체를진행한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(778UL);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            var service = new CareerGrowthService(flow.Career, NewGameConfiguration.CreateDefault().Balance);
            service.SettleSeasonAndBeginOffseason(CreateBatterUsage());

            GrowthResultRecord result = service.ExecuteActivity("personal_batting");

            PlannedOffseasonActivity activity = flow.Career.CurrentOffseason.Activities[0];
            Assert.That(activity.Status, Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(activity.DurationWeeks, Is.EqualTo(3));
            Assert.That(flow.Career.CurrentOffseason.CurrentWeek, Is.EqualTo(4));
            Assert.That(result.SourceType, Is.EqualTo(GrowthSourceType.PersonalTraining));
        }

        [Test]
        public void ExecutePlannedActivities_훈련회복유학을담은순서대로모두실행한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(779UL);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            var service = new CareerGrowthService(
                flow.Career,
                NewGameConfiguration.CreateDefault().Balance);
            service.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            long moneyBefore = flow.Career.AvailableMoney;

            service.PlanActivity("personal_batting", startWeek: 1);
            service.PlanActivity("rehab_general", startWeek: 4);
            service.PlanActivity("japan_batting_camp", startWeek: 6);

            GrowthResultRecord[] results = service.ExecutePlannedActivities();

            Assert.That(results, Has.Length.EqualTo(3));
            Assert.That(results[0].SourceId, Is.EqualTo("personal_batting"));
            Assert.That(results[1].SourceId, Is.EqualTo("rehab_general"));
            Assert.That(results[2].SourceId, Is.EqualTo("japan_batting_camp"));
            Assert.That(flow.Career.CurrentOffseason.CurrentWeek, Is.EqualTo(12));
            Assert.That(flow.Career.AvailableMoney,
                Is.EqualTo(moneyBefore - MoneyAmount.FromTenThousandWon(3700L)));
            Assert.That(flow.Career.MyPlayer.StudyState.StudyUsedThisOffseason, Is.True);
            Assert.That(flow.Career.CurrentOffseason.Activities,
                Has.All.Matches<PlannedOffseasonActivity>(
                    activity => activity.Status == OffseasonActivityStatus.Completed));
        }

        [Test]
        public void SettleSeason_새오프시즌은이전시즌유학사용상태를초기화한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(778UL);
            flow.Career.MyPlayer.StudyState.RecordVisit("japan_batting_camp", 2027);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            var service = new CareerGrowthService(flow.Career, NewGameConfiguration.CreateDefault().Balance);

            service.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            PlannedOffseasonActivity activity = service.PlanActivity("japan_batting_camp", startWeek: 1);
            service.StartActivity(activity.ActivityId);
            GrowthResultRecord result = service.CompleteActivity(activity.ActivityId);

            Assert.That(activity.Status, Is.EqualTo(OffseasonActivityStatus.Completed));
            Assert.That(result.SourceId, Is.EqualTo("japan_batting_camp"));
            Assert.That(flow.Career.CurrentOffseason.CurrentWeek, Is.EqualTo(7));
            Assert.That(flow.Career.MyPlayer.StudyState.StudyUsedThisOffseason, Is.True);
        }

        [Test]
        public void SettleSeason_같은입력과Seed면완전히같은결과를낸다()
        {
            NewGameFlow first = CreateRegularSeasonCareer(991UL);
            NewGameFlow second = CreateRegularSeasonCareer(991UL);
            first.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            second.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();

            var firstService = new CareerGrowthService(first.Career, NewGameConfiguration.CreateDefault().Balance);
            var secondService = new CareerGrowthService(second.Career, NewGameConfiguration.CreateDefault().Balance);
            SeasonGrowthSettlementResult firstResult = firstService.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            SeasonGrowthSettlementResult secondResult = secondService.SettleSeasonAndBeginOffseason(CreateBatterUsage());

            Assert.That(secondResult.NaturalDevelopment.RandomSeed,
                Is.EqualTo(firstResult.NaturalDevelopment.RandomSeed));
            Assert.That(secondResult.Aging.RandomSeed, Is.EqualTo(firstResult.Aging.RandomSeed));
            Assert.That(second.Career.MyPlayer.GrowthState.BaseAbilities.ToArray(),
                Is.EqualTo(first.Career.MyPlayer.GrowthState.BaseAbilities.ToArray()));
            Assert.That(second.Career.AvailableMoney, Is.EqualTo(first.Career.AvailableMoney));
        }

        [Test]
        public void UsageSummary_타자는실제출장경기비율과포지션가중치를사용한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var statistics = new PlayerSeasonStatisticsState();
            for (int game = 0; game < 80; game++)
                statistics.RecordTeamGame();
            for (int game = 0; game < 40; game++)
            {
                statistics.RecordBatting(
                    started: true,
                    plateAppearances: 4,
                    atBats: 4,
                    runs: 0,
                    hits: 1,
                    doubles: 0,
                    triples: 0,
                    homeRuns: 0,
                    runsBattedIn: 0,
                    walks: 0,
                    strikeouts: 1);
            }

            var builder = new CareerSeasonUsageSummaryBuilder(
                configuration.Balance.PlayerEvaluation,
                configuration.Balance.CareerSeason.StartingRotationSize);
            SeasonUsageSummary usage = builder.Build(PlayerPosition.Shortstop, statistics);

            Assert.That(usage.UsageRatio, Is.EqualTo(0.5d).Within(0.000001d));
            Assert.That(SumWeights(usage), Is.EqualTo(1d).Within(0.000001d));
            Assert.That(GetWeight(usage, PlayerAbility.Defense),
                Is.GreaterThan(GetWeight(usage, PlayerAbility.Power)));
        }

        [Test]
        public void UsageSummary_선발투수는5인로테이션기회를기준으로정상활용량을계산한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var statistics = new PlayerSeasonStatisticsState();
            for (int game = 0; game < 80; game++)
                statistics.RecordTeamGame();
            for (int game = 0; game < 16; game++)
            {
                statistics.RecordPitching(
                    started: true,
                    outsRecorded: 18,
                    hitsAllowed: 5,
                    earnedRuns: 2,
                    walksAllowed: 2,
                    strikeouts: 6);
            }

            var builder = new CareerSeasonUsageSummaryBuilder(
                configuration.Balance.PlayerEvaluation,
                configuration.Balance.CareerSeason.StartingRotationSize);
            SeasonUsageSummary usage = builder.Build(PlayerPosition.StartingPitcher, statistics);

            Assert.That(usage.UsageRatio, Is.EqualTo(1d).Within(0.000001d));
            Assert.That(SumWeights(usage), Is.EqualTo(1d).Within(0.000001d));
            Assert.That(GetWeight(usage, PlayerAbility.Stamina),
                Is.GreaterThan(GetWeight(usage, PlayerAbility.Velocity)));
        }

        [Test]
        public void ExecuteActivity_자금이없어도휴식으로남은주를모두소화할수있다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(1234UL);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            var service = new CareerGrowthService(flow.Career, NewGameConfiguration.CreateDefault().Balance);
            service.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            flow.Career.Economy.Spend(
                flow.Career.CurrentLeague.CurrentSeason.Year,
                MoneyTransactionType.TrainingExpense,
                "테스트 소진",
                flow.Career.AvailableMoney);

            OffseasonState offseason = flow.Career.CurrentOffseason;
            while (!offseason.IsCompleted)
                service.ExecuteActivity("rest");

            Assert.That(flow.Career.AvailableMoney, Is.EqualTo(0L));
            Assert.That(offseason.CurrentWeek, Is.EqualTo(offseason.TotalWeeks + 1));
            Assert.That(offseason.Activities.Count, Is.EqualTo(offseason.TotalWeeks));
        }

        [Test]
        public void TrainingAccess_상위리그진출에따라고가프로그램이단계적으로해금된다()
        {
            GrowthBalanceTable balance = GrowthBalanceTable.CreateDefault();
            TrainingProgramDefinition foundation = balance.FindProgram("bat_power_camp");
            TrainingProgramDefinition advanced = balance.FindProgram("bat_elite_hitting_lab");
            TrainingProgramDefinition elite = balance.FindProgram("usa_elite_batting_academy");

            Assert.That(CareerTrainingAccess.CanAccess(foundation, LeagueLevel.Rookie), Is.True);
            Assert.That(CareerTrainingAccess.CanAccess(advanced, LeagueLevel.Rookie), Is.False);
            Assert.That(CareerTrainingAccess.CanAccess(advanced, LeagueLevel.Minor), Is.True);
            Assert.That(CareerTrainingAccess.CanAccess(elite, LeagueLevel.Minor), Is.False);
            Assert.That(CareerTrainingAccess.CanAccess(elite, LeagueLevel.Major), Is.True);
        }

        [Test]
        public void PlanActivity_루키리그에서는상위리그프로그램을직접요청해도거부한다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(9922UL);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            var service = new CareerGrowthService(
                flow.Career,
                NewGameConfiguration.CreateDefault().Balance);
            service.SettleSeasonAndBeginOffseason(CreateBatterUsage());

            Assert.Throws<System.InvalidOperationException>(() =>
                service.PlanActivity("bat_elite_hitting_lab", startWeek: 1));
        }

        [Test]
        public void AdvanceToNextSeason_남은주가있어도다음시즌으로넘어간다()
        {
            NewGameFlow flow = CreateRegularSeasonCareer(4321UL);
            flow.Career.CurrentLeague.CurrentSeason.CompleteRegularSeason();
            BalanceTable balance = NewGameConfiguration.CreateDefault().Balance;
            var growthService = new CareerGrowthService(flow.Career, balance);
            growthService.SettleSeasonAndBeginOffseason(CreateBatterUsage());
            growthService.ExecuteActivity("rest");
            int completedYear = flow.Career.CurrentLeague.CurrentSeason.Year;

            Assert.That(flow.Career.CurrentOffseason.CurrentWeek, Is.EqualTo(2));
            new CareerSeasonTransitionService(flow.Career, balance).AdvanceToNextSeason();

            Assert.That(flow.Career.CurrentLeague.CurrentSeason.Year, Is.EqualTo(completedYear + 1));
            Assert.That(flow.Career.CurrentLeague.CurrentSeason.Phase, Is.EqualTo(SeasonPhase.RegularSeason));
        }

        private static NewGameFlow CreateRegularSeasonCareer(ulong seed)
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("성장 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow;
        }

        private static SeasonUsageSummary CreateBatterUsage()
        {
            return new SeasonUsageSummary(
                1d,
                new[]
                {
                    new AbilityWeight(PlayerAbility.Contact, 0.5d),
                    new AbilityWeight(PlayerAbility.Defense, 0.3d),
                    new AbilityWeight(PlayerAbility.BatterMental, 0.2d)
                });
        }

        private static double SumWeights(SeasonUsageSummary usage)
        {
            double total = 0d;
            for (int index = 0; index < usage.DevelopmentWeights.Length; index++)
                total += usage.DevelopmentWeights[index].Weight;
            return total;
        }

        private static double GetWeight(SeasonUsageSummary usage, PlayerAbility ability)
        {
            for (int index = 0; index < usage.DevelopmentWeights.Length; index++)
            {
                if (usage.DevelopmentWeights[index].Ability == ability)
                    return usage.DevelopmentWeights[index].Weight;
            }
            return 0d;
        }
    }
}
