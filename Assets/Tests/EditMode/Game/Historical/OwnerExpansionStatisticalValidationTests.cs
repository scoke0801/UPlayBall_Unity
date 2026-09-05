using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Baseball.Core.Balance;
using Baseball.Core.Growth;
using Baseball.Core.Historical;
using Baseball.Core.Players;
using Baseball.Game.Historical;
using Baseball.Simulation.Historical;
using Baseball.Simulation.Random;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game.Historical
{
    /// <summary>구단 운영 확장 네 Lane의 장기 분포와 Production 경기 경로를 수치로 검증한다.</summary>
    public sealed class OwnerExpansionStatisticalValidationTests
    {
        private const int SeedCount = 8;
        private const int SeasonCountPerSeed = 10;
        private const int HomeGamesPerSeason = 72;
        private const int WeeksPerSeason = 52;
        private const double MinimumMeaningfulStaffSalaryRatio = 0.005d;
        private const double MaximumMeaningfulStaffSalaryRatio = 0.30d;

        private static readonly ulong[] Seeds =
        {
            101UL, 211UL, 307UL, 401UL, 503UL, 601UL, 701UL, 809UL
        };

        [Test]
        [Explicit("4종 확장 장기 통계 검증")]
        public void Economy_LongRunAcrossSeeds_IsDeterministicBoundedAndReportsCoreMetrics()
        {
            EconomyStatistics first = RunEconomyStatistics();
            EconomyStatistics second = RunEconomyStatistics();

            Console.WriteLine(first.Format());
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint),
                "동일 Seed 묶음의 장기 운영 지표가 재현되어야 한다.");
            Assert.That(first.SeedCount, Is.EqualTo(SeedCount));
            Assert.That(first.SeasonCount, Is.EqualTo(SeedCount * SeasonCountPerSeed));
            Assert.That(first.HomeGameCount,
                Is.EqualTo(SeedCount * SeasonCountPerSeed * HomeGamesPerSeason));
            Assert.That(first.MaximumAttendance, Is.LessThanOrEqualTo(first.MaximumCapacity));
            Assert.That(first.AverageAttendance, Is.GreaterThan(0d));
            Assert.That(first.AttendanceVariance, Is.GreaterThan(0d));
            Assert.That(first.AverageCapacityUsage, Is.InRange(0d, 1d));
            Assert.That(first.HomeGameRevenue, Is.GreaterThan(0L));
            Assert.That(first.MoneyIncome, Is.GreaterThan(0L));
            Assert.That(first.MoneyExpense, Is.GreaterThan(0L));
            Assert.That(first.ScoutingPointProduction, Is.GreaterThan(0L));
            Assert.That(first.DevelopmentPointProduction, Is.GreaterThan(0L));
                Assert.That(first.MaximumAnnualNetGrowthRatio, Is.LessThan(4d),
                    "팬 지표 상한 안에서 연간 순수익이 폭발적으로 증가하면 안 된다.");
        }

        [Test]
        [Explicit("4종 확장 장기 통계 검증")]
        public void FacilityRoi_TenSeasonFanShopStrategy_HasFiniteNonInstantPayback()
        {
            FacilityRoiStatistics statistics = RunFacilityRoiStatistics();

            Console.WriteLine(statistics.Format());
            Assert.That(statistics.IncrementalRevenue, Is.GreaterThan(0L));
            Assert.That(statistics.IncrementalOperatingCost, Is.GreaterThan(0L));
            Assert.That(statistics.UpgradeCost, Is.GreaterThan(0L));
            Assert.That(statistics.ReturnOnInvestment, Is.GreaterThan(0d));
            Assert.That(statistics.PaybackSeasons, Is.GreaterThanOrEqualTo(1d),
                "시설 투자비가 한 시즌 안에 회수되면 업그레이드 선택의 장기 무게가 약해진다.");
            Assert.That(statistics.PaybackSeasons, Is.LessThanOrEqualTo(SeasonCountPerSeed));
        }

        [Test]
        [Explicit("4종 확장 장기 통계 검증")]
        public void StaffSalaryRatio_MustRemainAMeaningfulSeasonTradeoff()
        {
            StaffSalaryStatistics statistics = RunStaffSalaryStatistics();

            Console.WriteLine(statistics.Format());
            Assert.That(
                statistics.AverageSalaryToHomeRevenueRatio,
                Is.InRange(MinimumMeaningfulStaffSalaryRatio, MaximumMeaningfulStaffSalaryRatio),
                "다섯 역할 연봉이 홈경기 매출의 0.5% 미만이면 Staff Market이 사실상 무료 선택이 된다.");
            Assert.That(statistics.EliteSalaryToHomeRevenueRatio,
                Is.GreaterThan(statistics.AverageSalaryToHomeRevenueRatio),
                "최고 Quality 5인의 비용이 평균 시장 5인보다 높아야 한다.");
            Assert.That(statistics.EliteSalaryToHomeRevenueRatio, Is.LessThanOrEqualTo(0.50d),
                "최고 Quality 5인의 연봉만으로 홈경기 매출 절반을 넘으면 선택지가 지나치게 좁아진다.");
        }

        [Test]
        [Explicit("4종 확장 장기 통계 검증")]
        public void TicketPolicy_LongRun_MustNotMakePremiumUniversallyOptimal()
        {
            TicketStrategyStatistics statistics = RunTicketStrategyStatistics();

            Console.WriteLine(statistics.Format());
            Assert.That(statistics.StrategyCaseCount, Is.GreaterThan(0));
            Assert.That(statistics.PremiumWinCount, Is.LessThan(statistics.StrategyCaseCount),
                "모든 FanBase·LeagueGrade·Seed에서 Premium이 최대 순수익이면 TicketPolicy 결정이 사라진다.");
            Assert.That(statistics.NonPremiumWinCount, Is.GreaterThan(0),
                "Cheap 또는 Standard가 유리한 구단 상황이 최소 하나는 있어야 한다.");
            Assert.That(statistics.PremiumWinRate, Is.LessThanOrEqualTo(0.75d),
                "Premium이 75%가 넘는 대표 구단 상황에서 우세하면 사실상의 지배 전략이다.");
        }

        [Test]
        [Explicit("4종 확장 장기 통계 검증")]
        public void ConditionIntelChemistry_MultiSeedDistributions_AreBoundedAndModifiersApplyOnce()
        {
            CrossSystemStatistics first = RunCrossSystemStatistics();
            CrossSystemStatistics second = RunCrossSystemStatistics();

            Console.WriteLine(first.Format());
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(first.ConditionSampleCount, Is.GreaterThan(0));
            Assert.That(first.AverageCondition, Is.InRange(1d, 99d),
                "장기 Condition이 항상 최저 또는 최고에 고착되면 안 된다.");
            Assert.That(first.ConditionAtFloorRate, Is.LessThan(0.80d));
            Assert.That(first.ConditionAtCeilingRate, Is.LessThan(0.80d));
            Assert.That(first.ResolvedRecovery, Is.EqualTo(first.ActualSingleRecovery));
            Assert.That(first.ResolvedRecovery, Is.Not.EqualTo(first.DoubledFacilityRecovery),
                "RecoveryCenter modifier를 두 번 적용한 값과 실제 회복량이 같아서는 안 된다.");
            Assert.That(first.IntelSampleCount, Is.GreaterThan(0));
            Assert.That(first.IntelUnknownCount, Is.GreaterThan(0));
            Assert.That(first.IntelLowCount, Is.GreaterThan(0));
            Assert.That(first.IntelEstimatedCount, Is.GreaterThan(0));
            Assert.That(first.IntelHighCount, Is.GreaterThan(0));
            Assert.That(first.IntelConfirmedCount, Is.GreaterThan(0));
            Assert.That(first.LineupNegativeCount, Is.GreaterThan(0));
            Assert.That(first.LineupNeutralCount, Is.GreaterThan(0));
            Assert.That(first.LineupPositiveCount, Is.GreaterThan(0));
            Assert.That(first.BatteryNegativeCount, Is.GreaterThan(0));
            Assert.That(first.BatteryNeutralCount, Is.GreaterThan(0));
            Assert.That(first.BatteryPositiveCount, Is.GreaterThan(0));
            Assert.That(first.MinimumChemistryModifier,
                Is.GreaterThanOrEqualTo(-first.ConditionLevelStep));
            Assert.That(first.MaximumChemistryModifier,
                Is.LessThanOrEqualTo(first.ConditionLevelStep));
        }

        [Test]
        [Explicit("4종 확장 장기 통계 검증")]
        public void ProductionServices_FullScheduledSeason_ReproducesPregameMatchAndWeeklyState()
        {
            ProductionPathStatistics first = RunProductionPathSeason();
            ProductionPathStatistics second = RunProductionPathSeason();

            Console.WriteLine(first.Format());
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(first.MatchCount, Is.GreaterThan(0));
            Assert.That(first.PreparedGameCount, Is.EqualTo(first.MatchCount));
            Assert.That(first.HomeGameCount, Is.GreaterThan(0));
            Assert.That(first.MaximumHomeAttendance, Is.LessThanOrEqualTo(first.StadiumCapacity));
            Assert.That(first.WeeklyAdvanceCount, Is.GreaterThan(0));
            Assert.That(first.TotalRecovery, Is.GreaterThan(0));
            Assert.That(first.FinalConditionAverage, Is.InRange(0d, 100d));
            Assert.That(first.FinalConditionMinimum, Is.InRange(0, 100));
            Assert.That(first.FinalConditionMaximum, Is.InRange(0, 100));
            Assert.That(first.FamiliarityPairCount, Is.GreaterThan(0));
        }

        private static EconomyStatistics RunEconomyStatistics()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            var attendance = new RunningStatistics();
            var capacityUsage = new RunningStatistics();
            long totalRevenue = 0L;
            long totalIncome = 0L;
            long totalExpense = 0L;
            long totalSp = 0L;
            long totalDp = 0L;
            int maximumAttendance = 0;
            int maximumCapacity = 0;
            double maximumGrowthRatio = 0d;

            for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                double fanBase = 25d + seedIndex * 7d;
                double popularity = 30d + seedIndex * 6d;
                double momentum = 45d;
                long previousAnnualNet = 0L;
                var random = new Pcg32Random(Seeds[seedIndex]);
                LeagueGrade grade = GetValidationGrade(seedIndex);

                for (int season = 0; season < SeasonCountPerSeed; season++)
                {
                    string seasonId = $"stat-economy:{seedIndex}:{season}";
                    ClubOperationState operation = CreateOperation(
                        balance,
                        seasonId,
                        fanBase,
                        popularity,
                        momentum,
                        TicketPriceTier.Standard,
                        facilityLevel: 1);
                    long currentMoney = 5_000_000_000L;

                    for (int week = 0; week < WeeksPerSeason; week++)
                    {
                        operation.BeginWeek(week);
                        WeeklyFacilityProductionResult production =
                            new WeeklyFacilityProductionResolver(balance).Resolve(
                                operation,
                                new WeeklyFacilityProductionContext(
                                    seasonId,
                                    week,
                                    grade,
                                    currentMoney,
                                    0,
                                    0));
                        Assert.That(operation.TryApplyWeeklyProduction(production), Is.True);
                        currentMoney = checked(currentMoney + production.Receipt.ResourceDelta.Money);
                        totalSp += production.ScoutingPointProduction;
                        totalDp += production.DevelopmentPointProduction;
                    }

                    var financeResolver = new HomeGameFinanceResolver(balance);
                    for (int game = 0; game < HomeGamesPerSeason; game++)
                    {
                        HomeGameContext context = CreateRandomGameContext(
                            $"stat-economy:{seedIndex}:{season}:{game}",
                            seasonId,
                            game * WeeksPerSeason / HomeGamesPerSeason,
                            grade,
                            random);
                        HomeGameFinanceResult result = financeResolver.Resolve(context, operation, random);
                        Assert.That(operation.TryApplyHomeGame(result), Is.True);
                        currentMoney = checked(currentMoney + result.NetGameIncome);
                        attendance.Add(result.Attendance);
                        capacityUsage.Add(result.CapacityRate);
                        totalRevenue = checked(totalRevenue + result.TicketRevenue + result.FanShopRevenue +
                            result.OtherGameRevenue);
                        maximumAttendance = Math.Max(maximumAttendance, result.Attendance);
                        maximumCapacity = Math.Max(maximumCapacity, result.Capacity);
                    }

                    long annualNet = operation.CurrentSeason.NetMoney;
                    if (season >= 8 && previousAnnualNet > 0L && annualNet > 0L)
                        maximumGrowthRatio = Math.Max(maximumGrowthRatio, annualNet / (double)previousAnnualNet);
                    previousAnnualNet = annualNet;
                    totalIncome = checked(totalIncome + operation.CurrentSeason.MoneyIncome);
                    totalExpense = checked(totalExpense + operation.CurrentSeason.MoneyExpense);
                    fanBase = operation.FanBase;
                    popularity = operation.Popularity;
                    momentum = operation.AttendanceMomentum;
                }
            }

            return new EconomyStatistics(
                Seeds.Length,
                Seeds.Length * SeasonCountPerSeed,
                attendance.Count,
                attendance.Mean,
                attendance.Variance,
                capacityUsage.Mean,
                maximumAttendance,
                maximumCapacity,
                totalRevenue,
                totalIncome,
                totalExpense,
                totalSp,
                totalDp,
                maximumGrowthRatio);
        }

        private static FacilityRoiStatistics RunFacilityRoiStatistics()
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            FacilityLevelDefinition fanShop = balance.GetFacilityLevel(FacilityType.FanShop, 1);
            long baselineRevenue = 0L;
            long strategyRevenue = 0L;
            long baselineOperatingCost = 0L;
            long strategyOperatingCost = 0L;

            for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                var baselineRandom = new Pcg32Random(Seeds[seedIndex]);
                var strategyRandom = new Pcg32Random(Seeds[seedIndex]);
                double baselineFan = 40d;
                double baselinePopularity = 40d;
                double baselineMomentum = 45d;
                double strategyFan = baselineFan;
                double strategyPopularity = baselinePopularity;
                double strategyMomentum = baselineMomentum;

                for (int season = 0; season < SeasonCountPerSeed; season++)
                {
                    string baselineSeasonId = $"roi-base:{seedIndex}:{season}";
                    string strategySeasonId = $"roi-shop:{seedIndex}:{season}";
                    ClubOperationState baseline = CreateOperation(
                        balance,
                        baselineSeasonId,
                        baselineFan,
                        baselinePopularity,
                        baselineMomentum,
                        TicketPriceTier.Standard,
                        facilityLevel: 0);
                    ClubOperationState strategy = CreateOperation(
                        balance,
                        strategySeasonId,
                        strategyFan,
                        strategyPopularity,
                        strategyMomentum,
                        TicketPriceTier.Standard,
                        facilityLevel: 0,
                        fanShopLevel: 1);
                    var resolver = new HomeGameFinanceResolver(balance);
                    var weeklyResolver = new WeeklyFacilityProductionResolver(balance);

                    for (int week = 0; week < WeeksPerSeason; week++)
                    {
                        baseline.BeginWeek(week);
                        strategy.BeginWeek(week);
                        WeeklyFacilityProductionResult baselineProduction = weeklyResolver.Resolve(
                            baseline,
                            new WeeklyFacilityProductionContext(
                                baselineSeasonId,
                                week,
                                LeagueGrade.Major,
                                long.MaxValue,
                                0,
                                0));
                        WeeklyFacilityProductionResult strategyProduction = weeklyResolver.Resolve(
                            strategy,
                            new WeeklyFacilityProductionContext(
                                strategySeasonId,
                                week,
                                LeagueGrade.Major,
                                long.MaxValue,
                                0,
                                0));
                        Assert.That(baseline.TryApplyWeeklyProduction(baselineProduction), Is.True);
                        Assert.That(strategy.TryApplyWeeklyProduction(strategyProduction), Is.True);
                        baselineOperatingCost = checked(baselineOperatingCost + baselineProduction.OperatingCost);
                        strategyOperatingCost = checked(strategyOperatingCost + strategyProduction.OperatingCost);
                    }

                    for (int game = 0; game < HomeGamesPerSeason; game++)
                    {
                        int week = game * WeeksPerSeason / HomeGamesPerSeason;
                        RandomGameInputs inputs = CreateRandomGameInputs(baselineRandom);
                        RandomGameInputs strategyInputs = CreateRandomGameInputs(strategyRandom);
                        Assert.That(strategyInputs, Is.EqualTo(inputs));
                        HomeGameFinanceResult baselineResult = resolver.Resolve(
                            CreateGameContext($"roi-base:{seedIndex}:{season}:{game}", baselineSeasonId, week,
                                LeagueGrade.Major, inputs),
                            baseline,
                            baselineRandom);
                        HomeGameFinanceResult strategyResult = resolver.Resolve(
                            CreateGameContext($"roi-shop:{seedIndex}:{season}:{game}", strategySeasonId, week,
                                LeagueGrade.Major, strategyInputs),
                            strategy,
                            strategyRandom);
                        Assert.That(baseline.TryApplyHomeGame(baselineResult), Is.True);
                        Assert.That(strategy.TryApplyHomeGame(strategyResult), Is.True);
                        baselineRevenue = checked(baselineRevenue + baselineResult.TicketRevenue +
                            baselineResult.FanShopRevenue + baselineResult.OtherGameRevenue);
                        strategyRevenue = checked(strategyRevenue + strategyResult.TicketRevenue +
                            strategyResult.FanShopRevenue + strategyResult.OtherGameRevenue);
                        baselineOperatingCost = checked(baselineOperatingCost + baselineResult.OperatingCost);
                        strategyOperatingCost = checked(strategyOperatingCost + strategyResult.OperatingCost);
                    }

                    baselineFan = baseline.FanBase;
                    baselinePopularity = baseline.Popularity;
                    baselineMomentum = baseline.AttendanceMomentum;
                    strategyFan = strategy.FanBase;
                    strategyPopularity = strategy.Popularity;
                    strategyMomentum = strategy.AttendanceMomentum;
                }
            }

            long upgradeCost = checked(fanShop.UpgradeMoneyCost * Seeds.Length);
            long incrementalRevenue = strategyRevenue - baselineRevenue;
            long incrementalOperatingCost = strategyOperatingCost - baselineOperatingCost;
            long netReturn = incrementalRevenue - incrementalOperatingCost;
            double roi = netReturn / (double)upgradeCost;
            double annualNetReturn = netReturn / (double)(Seeds.Length * SeasonCountPerSeed);
            double payback = annualNetReturn <= 0d
                ? double.PositiveInfinity
                : fanShop.UpgradeMoneyCost / annualNetReturn;
            return new FacilityRoiStatistics(
                incrementalRevenue,
                incrementalOperatingCost,
                upgradeCost,
                roi,
                payback);
        }

        private static StaffSalaryStatistics RunStaffSalaryStatistics()
        {
            StaffBalanceTable balance = StaffBalanceTable.CreateInitial();
            long salary = 0L;
            long eliteSalary = 0L;
            long homeRevenue = 0L;

            for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                StaffBundle bundle = CreateStaffBundle(Seeds[seedIndex], $"salary-team:{seedIndex}");
                salary = checked(salary + bundle.TotalAnnualSalary * SeasonCountPerSeed);
                eliteSalary = checked(eliteSalary +
                    CreateEliteStaffAnnualSalary(Seeds[seedIndex], $"elite-team:{seedIndex}") *
                    SeasonCountPerSeed);
                EconomyStatistics economy = RunSingleSeedEconomy(Seeds[seedIndex], seedIndex, facilityLevel: 0);
                homeRevenue = checked(homeRevenue + economy.HomeGameRevenue);
                Assert.That(bundle.Profile.ConditionRecoveryEfficiency,
                    Is.LessThanOrEqualTo(1d + balance.MaximumEffectBonus));
            }

            return new StaffSalaryStatistics(
                salary,
                eliteSalary,
                homeRevenue,
                salary / (double)homeRevenue,
                eliteSalary / (double)homeRevenue);
        }

        private static TicketStrategyStatistics RunTicketStrategyStatistics()
        {
            int cases = 0;
            int premiumWins = 0;
            int nonPremiumWins = 0;
            var wins = new int[Enum.GetValues(typeof(TicketPriceTier)).Length];
            double[] fanProfiles = { 20d, 50d, 80d };

            for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                for (int profileIndex = 0; profileIndex < fanProfiles.Length; profileIndex++)
                {
                    long bestNet = long.MinValue;
                    TicketPriceTier bestTier = TicketPriceTier.Standard;
                    for (int tierIndex = 0; tierIndex < wins.Length; tierIndex++)
                    {
                        TicketPriceTier tier = (TicketPriceTier)tierIndex;
                        long net = RunTicketStrategy(
                            Seeds[seedIndex],
                            fanProfiles[profileIndex],
                            GetValidationGrade(profileIndex + seedIndex),
                            tier);
                        if (net > bestNet)
                        {
                            bestNet = net;
                            bestTier = tier;
                        }
                    }
                    wins[(int)bestTier]++;
                    cases++;
                    if (bestTier == TicketPriceTier.Premium) premiumWins++;
                    else nonPremiumWins++;
                }
            }

            return new TicketStrategyStatistics(cases, premiumWins, nonPremiumWins, wins);
        }

        private static CrossSystemStatistics RunCrossSystemStatistics()
        {
            ConditionChemistryBalanceTable conditionBalance = ConditionChemistryBalanceTable.CreateDefault();
            ClubOperationBalanceTable clubBalance = ClubOperationBalanceTable.CreateInitial();
            StaffBundle staff = CreateStaffBundle(Seeds[0], "cross-team");
            ClubOperationState operation = CreateOperation(
                clubBalance,
                "cross-season",
                50d,
                50d,
                50d,
                TicketPriceTier.Standard,
                facilityLevel: 0,
                recoveryLevel: 3,
                dataAnalysisLevel: 3);
            ClubFacilityEffectProfile facility = new ClubFacilityEffectResolver(clubBalance).Resolve(operation);
            var recoveryContext = new ConditionRecoveryContext(
                conditionBalance.WeeklyBaseRecovery,
                1d + facility.ConditionRecoveryEfficiencyModifier,
                staff.Profile.ConditionRecoveryEfficiency);
            var recoveryResolver = new ConditionRecoveryResolver();
            int resolvedRecovery = recoveryResolver.ResolveRecovery(recoveryContext);
            var probe = new TeamSeasonPlayerStatusState(
                "recovery-probe",
                new[] { new TeamSeasonPlayerStatus("probe-player", 40) });
            int before = probe.Players[0].StoredBaseCondition;
            recoveryResolver.ApplyRecovery(probe, recoveryContext);
            int actualSingleRecovery = probe.Players[0].StoredBaseCondition - before;
            int doubledFacilityRecovery = (int)Math.Round(
                conditionBalance.WeeklyBaseRecovery *
                recoveryContext.FacilityEfficiencyMultiplier *
                recoveryContext.FacilityEfficiencyMultiplier *
                recoveryContext.StaffEfficiencyMultiplier,
                MidpointRounding.AwayFromZero);

            var conditionDistribution = new RunningStatistics();
            int conditionFloor = 0;
            int conditionCeiling = 0;
            int lineupNegative = 0;
            int lineupNeutral = 0;
            int lineupPositive = 0;
            int batteryNegative = 0;
            int batteryNeutral = 0;
            int batteryPositive = 0;
            int minimumModifier = int.MaxValue;
            int maximumModifier = int.MinValue;
            int[] intel = new int[Enum.GetValues(typeof(IntelState)).Length];
            var confidence = new RunningStatistics();
            var scoutingResolver = new ScoutingConfidenceResolver(ScoutingConfidenceDefinition.CreateInitial());
            double combinedConfidenceModifier = 1d + facility.ScoutingConfidenceModifier +
                staff.Profile.ScoutingConfidenceModifier;

            for (int seedIndex = 0; seedIndex < Seeds.Length; seedIndex++)
            {
                var random = new Pcg32Random(Seeds[seedIndex]);
                var statuses = new TeamSeasonPlayerStatus[25];
                for (int player = 0; player < statuses.Length; player++)
                    statuses[player] = new TeamSeasonPlayerStatus(
                        $"condition:{seedIndex}:{player}",
                        35 + NextInt(random, 56));
                var state = new TeamSeasonPlayerStatusState($"condition-team:{seedIndex}", statuses);

                for (int week = 0; week < WeeksPerSeason; week++)
                {
                    ApplyRepresentativeWeeklyWorkload(state, random, conditionBalance);
                    recoveryResolver.ApplyRecovery(state, recoveryContext);
                    for (int player = 0; player < state.Players.Count; player++)
                    {
                        int value = state.Players[player].StoredBaseCondition;
                        conditionDistribution.Add(value);
                        if (value == 0) conditionFloor++;
                        if (value == 100) conditionCeiling++;
                    }
                }

                for (int sample = 0; sample < 64; sample++)
                {
                    string teamKey = $"chemistry:{seedIndex}:{sample}";
                    var familiarity = new TeamChemistryFamiliarityState(teamKey);
                    var lineup = new LineupChemistryPlayer[9];
                    for (int player = 0; player < lineup.Length; player++)
                    {
                        string personId = $"lineup:{seedIndex}:{sample}:{player}";
                        lineup[player] = new LineupChemistryPlayer(
                            personId,
                            CreateStyleAttributes(sample % 16 == 0 ? 0 : NextInt(random, 3)));
                        if (player > 0)
                        {
                            int familiarityAmount = sample % 16 == 0 ? 0 : NextInt(random, 101);
                            familiarity.RecordLineupPair(
                                new PlayerPersonPairKey(lineup[player - 1].PlayerPersonId, personId),
                                familiarityAmount,
                                conditionBalance.FamiliarityCap);
                        }
                    }
                    LineupChemistryResult lineupResult =
                        new LineupChemistryResolver(conditionBalance).Resolve(teamKey, lineup, familiarity);
                    for (int player = 0; player < lineupResult.Players.Count; player++)
                    {
                        int modifier = lineupResult.Players[player].ConditionModifier;
                        CountModifier(modifier, ref lineupNegative, ref lineupNeutral, ref lineupPositive);
                        minimumModifier = Math.Min(minimumModifier, modifier);
                        maximumModifier = Math.Max(maximumModifier, modifier);
                    }

                    string pitcherId = $"pitcher:{seedIndex}:{sample}";
                    string catcherId = $"catcher:{seedIndex}:{sample}";
                    int batteryFamiliarity = NextInt(random, 101);
                    familiarity.RecordBatteryPair(
                        new PlayerPersonPairKey(pitcherId, catcherId),
                        batteryFamiliarity,
                        conditionBalance.FamiliarityCap);
                    BatteryChemistryResult battery = new BatteryChemistryResolver(conditionBalance).Resolve(
                        teamKey,
                        pitcherId,
                        CreatePitcherAttributes(random),
                        catcherId,
                        CreateCatcherAttributes(random),
                        familiarity);
                    CountModifier(
                        battery.PitcherConditionModifier,
                        ref batteryNegative,
                        ref batteryNeutral,
                        ref batteryPositive);
                    minimumModifier = Math.Min(minimumModifier, battery.PitcherConditionModifier);
                    maximumModifier = Math.Max(maximumModifier, battery.PitcherConditionModifier);

                    ScoutingEvidenceStrength strength = CreateEvidenceStrength(sample, random);
                    double value = scoutingResolver.CalculateConfidence(strength, combinedConfidenceModifier);
                    IntelState stateValue = scoutingResolver.ResolveState(value, strength.IsConfirmed);
                    intel[(int)stateValue]++;
                    confidence.Add(value);
                }
            }

            return new CrossSystemStatistics(
                conditionDistribution.Count,
                conditionDistribution.Mean,
                conditionFloor / (double)conditionDistribution.Count,
                conditionCeiling / (double)conditionDistribution.Count,
                resolvedRecovery,
                actualSingleRecovery,
                doubledFacilityRecovery,
                confidence.Count,
                confidence.Mean,
                intel[(int)IntelState.Unknown],
                intel[(int)IntelState.LowConfidence],
                intel[(int)IntelState.Estimated],
                intel[(int)IntelState.HighConfidence],
                intel[(int)IntelState.Confirmed],
                lineupNegative,
                lineupNeutral,
                lineupPositive,
                batteryNegative,
                batteryNeutral,
                batteryPositive,
                minimumModifier,
                maximumModifier,
                conditionBalance.ConditionLevelStep);
        }

        private static ProductionPathStatistics RunProductionPathSeason()
        {
            CreateRuntime(out ManagerHistoricalRuntimeState runtime, out IHistoricalContentProvider provider);
            BalanceTable balance = BalanceTable.CreateDefault();
            var pregame = new ManagerPregameService(balance, provider);
            var match = new ManagerModeMatchService(provider, balance);
            var coordinator = new ManagerModeCoordinator(balance);
            int matches = 0;
            int prepared = 0;
            int homeGames = 0;
            int maximumAttendance = 0;
            int weeklyAdvances = 0;
            int totalRecovery = 0;
            long homeRevenue = 0L;
            var confidence = new RunningStatistics();

            while (runtime.ManagerMode.LiveSeason.NextPlayerGame != null)
            {
                ManagerPregamePreparation preparation = pregame.PrepareNextGame(
                    runtime,
                    Array.Empty<string>(),
                    Array.Empty<string>());
                Assert.That(preparation.CanStartGame, Is.True);
                prepared++;
                confidence.Add(preparation.ScoutingReport.ReportConfidenceSummary.Confidence01);

                ManagerModeMatchResult result = match.PlayNextGame(runtime);
                matches++;
                if (result.HomeFinance.Status == HomeGameFinanceStatus.Applied)
                {
                    homeGames++;
                    maximumAttendance = Math.Max(maximumAttendance, result.HomeFinance.Attendance);
                    homeRevenue = checked(homeRevenue + result.HomeFinance.NetGameIncome);
                }

                if (matches % 6 == 0)
                {
                    ManagerWeeklyAdvanceResult weekly = coordinator.AdvanceWeek(runtime);
                    Assert.That(weekly.Status, Is.EqualTo(ManagerModeTransactionStatus.Applied));
                    weeklyAdvances++;
                    totalRecovery += weekly.ConditionRecovery;
                }
            }

            TeamSeasonPlayerStatusState playerStatus = runtime.ManagerMode.GetPlayerStatus(runtime.PlayerTeamSeasonKey);
            int minimum = 100;
            int maximum = 0;
            long totalCondition = 0L;
            for (int index = 0; index < playerStatus.Players.Count; index++)
            {
                int condition = playerStatus.Players[index].StoredBaseCondition;
                minimum = Math.Min(minimum, condition);
                maximum = Math.Max(maximum, condition);
                totalCondition += condition;
            }
            int pairCount = runtime.ManagerMode.GetFamiliarity(runtime.PlayerTeamSeasonKey).Entries.Count;
            return new ProductionPathStatistics(
                matches,
                prepared,
                homeGames,
                maximumAttendance,
                runtime.ManagerMode.ClubOperation.Stadium.Capacity,
                weeklyAdvances,
                totalRecovery,
                homeRevenue,
                totalCondition / (double)playerStatus.Players.Count,
                minimum,
                maximum,
                pairCount,
                confidence.Mean);
        }

        private static EconomyStatistics RunSingleSeedEconomy(ulong seed, int seedIndex, int facilityLevel)
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            var attendance = new RunningStatistics();
            var capacity = new RunningStatistics();
            int maximumAttendance = 0;
            int maximumCapacity = 0;
            long revenue = 0L;
            long income = 0L;
            long expense = 0L;
            long sp = 0L;
            long dp = 0L;
            double fan = 45d;
            double popularity = 45d;
            double momentum = 45d;
            var random = new Pcg32Random(seed);

            for (int season = 0; season < SeasonCountPerSeed; season++)
            {
                string seasonId = $"single:{seedIndex}:{season}";
                ClubOperationState operation = CreateOperation(
                    balance,
                    seasonId,
                    fan,
                    popularity,
                    momentum,
                    TicketPriceTier.Standard,
                    facilityLevel);
                var resolver = new HomeGameFinanceResolver(balance);
                for (int game = 0; game < HomeGamesPerSeason; game++)
                {
                    HomeGameFinanceResult result = resolver.Resolve(
                        CreateRandomGameContext(
                            $"single:{seedIndex}:{season}:{game}",
                            seasonId,
                            game * WeeksPerSeason / HomeGamesPerSeason,
                            GetValidationGrade(seedIndex),
                            random),
                        operation,
                        random);
                    operation.TryApplyHomeGame(result);
                    attendance.Add(result.Attendance);
                    capacity.Add(result.CapacityRate);
                    revenue = checked(revenue + result.TicketRevenue + result.FanShopRevenue + result.OtherGameRevenue);
                    maximumAttendance = Math.Max(maximumAttendance, result.Attendance);
                    maximumCapacity = Math.Max(maximumCapacity, result.Capacity);
                }
                income = checked(income + operation.CurrentSeason.MoneyIncome);
                expense = checked(expense + operation.CurrentSeason.MoneyExpense);
                fan = operation.FanBase;
                popularity = operation.Popularity;
                momentum = operation.AttendanceMomentum;
            }

            return new EconomyStatistics(
                1,
                SeasonCountPerSeed,
                attendance.Count,
                attendance.Mean,
                attendance.Variance,
                capacity.Mean,
                maximumAttendance,
                maximumCapacity,
                revenue,
                income,
                expense,
                sp,
                dp,
                0d);
        }

        private static long RunTicketStrategy(
            ulong seed,
            double initialFanBase,
            LeagueGrade grade,
            TicketPriceTier tier)
        {
            ClubOperationBalanceTable balance = ClubOperationBalanceTable.CreateInitial();
            var random = new Pcg32Random(seed);
            double fan = initialFanBase;
            double popularity = initialFanBase;
            double momentum = 50d;
            long net = 0L;

            for (int season = 0; season < 5; season++)
            {
                string seasonId = $"ticket:{seed}:{initialFanBase}:{season}:{(int)tier}";
                ClubOperationState operation = CreateOperation(
                    balance,
                    seasonId,
                    fan,
                    popularity,
                    momentum,
                    tier,
                    facilityLevel: 0);
                var resolver = new HomeGameFinanceResolver(balance);
                for (int game = 0; game < HomeGamesPerSeason; game++)
                {
                    HomeGameFinanceResult result = resolver.Resolve(
                        CreateRandomGameContext(
                            $"ticket:{seed}:{initialFanBase}:{season}:{game}:{(int)tier}",
                            seasonId,
                            game * WeeksPerSeason / HomeGamesPerSeason,
                            grade,
                            random),
                        operation,
                        random);
                    operation.TryApplyHomeGame(result);
                    net = checked(net + result.NetGameIncome);
                }
                fan = operation.FanBase;
                popularity = operation.Popularity;
                momentum = operation.AttendanceMomentum;
            }
            return net;
        }

        private static ClubOperationState CreateOperation(
            ClubOperationBalanceTable balance,
            string seasonId,
            double fanBase,
            double popularity,
            double momentum,
            TicketPriceTier ticketTier,
            int facilityLevel,
            int? fanShopLevel = null,
            int? recoveryLevel = null,
            int? dataAnalysisLevel = null)
        {
            StadiumLevelDefinition stadium = balance.GetStadiumLevel(1);
            var facilities = new FacilityState[Enum.GetValues(typeof(FacilityType)).Length];
            for (int index = 0; index < facilities.Length; index++)
            {
                var type = (FacilityType)index;
                int level = facilityLevel;
                if (type == FacilityType.FanShop && fanShopLevel.HasValue) level = fanShopLevel.Value;
                if (type == FacilityType.RecoveryCenter && recoveryLevel.HasValue) level = recoveryLevel.Value;
                if (type == FacilityType.DataAnalysisCenter && dataAnalysisLevel.HasValue)
                    level = dataAnalysisLevel.Value;
                facilities[index] = new FacilityState(type, level);
            }
            string teamKey = "owner-team";
            return new ClubOperationState(
                teamKey,
                fanBase,
                popularity,
                momentum,
                new StadiumState(stadium.Level, stadium.Capacity),
                facilities,
                new TicketPolicy(ticketTier),
                new WeeklyOperationLedger(seasonId, 0),
                new SeasonFinanceSummary(seasonId));
        }

        private static HomeGameContext CreateRandomGameContext(
            string gameId,
            string seasonId,
            int week,
            LeagueGrade grade,
            IRandomSource random)
        {
            return CreateGameContext(gameId, seasonId, week, grade, CreateRandomGameInputs(random));
        }

        private static HomeGameContext CreateGameContext(
            string gameId,
            string seasonId,
            int week,
            LeagueGrade grade,
            RandomGameInputs inputs)
        {
            return new HomeGameContext(
                gameId,
                seasonId,
                week,
                "owner-team",
                "opponent-team",
                GameVenue.Home,
                grade,
                inputs.Outcome,
                inputs.RecentPerformance,
                inputs.OpponentAttraction,
                inputs.SeasonImportance,
                inputs.RivalryStrength);
        }

        private static RandomGameInputs CreateRandomGameInputs(IRandomSource random)
        {
            double outcomeRoll = random.NextDouble();
            HomeGameOutcome outcome = outcomeRoll < 0.47d
                ? HomeGameOutcome.Win
                : outcomeRoll < 0.52d
                    ? HomeGameOutcome.Draw
                    : HomeGameOutcome.Loss;
            return new RandomGameInputs(
                outcome,
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble(),
                random.NextDouble());
        }

        private static StaffBundle CreateStaffBundle(ulong seed, string teamKey)
        {
            StaffBalanceTable balance = StaffBalanceTable.CreateInitial();
            var names = new string[25];
            for (int index = 0; index < names.Length; index++)
                names[index] = string.Concat("가상 스태프 ", (char)('가' + index));
            StaffCatalog catalog = new StaffCatalogGenerator().Generate(
                new StaffNameCatalog(names),
                5,
                seed,
                balance);
            IReadOnlyList<StaffMarketOffer> offers = new StaffMarketResolver().CreateOffers(
                catalog,
                Array.Empty<StaffContractState>(),
                teamKey,
                "offseason-one",
                StaffMarketKind.Offseason,
                LeagueGrade.Major,
                seed,
                balance);
            var contracts = new StaffContractState[5];
            var assignment = new TeamStaffAssignmentState(teamKey);
            long totalSalary = 0L;
            for (int index = 0; index < contracts.Length; index++)
            {
                StaffMarketOffer offer = offers[index];
                StaffDefinition definition = catalog.Get(offer.StaffId);
                contracts[index] = new StaffContractState(
                    $"staff-contract:{seed}:{index}",
                    definition.StaffId,
                    teamKey,
                    1,
                    10,
                    offer.AnnualSalary);
                assignment = assignment.WithAssignment(definition.Role, definition.StaffId);
                totalSalary = checked(totalSalary + offer.AnnualSalary);
            }
            TeamStaffEffectProfile profile = new TeamStaffEffectResolver().Resolve(
                catalog,
                contracts,
                assignment,
                balance);
            return new StaffBundle(profile, totalSalary);
        }

        private static long CreateEliteStaffAnnualSalary(ulong seed, string teamKey)
        {
            StaffBalanceTable balance = StaffBalanceTable.CreateInitial();
            var definitions = new StaffDefinition[Enum.GetValues(typeof(StaffRole)).Length];
            for (int index = 0; index < definitions.Length; index++)
            {
                var role = (StaffRole)index;
                definitions[index] = new StaffDefinition(
                    $"elite-staff:{seed}:{index}",
                    string.Concat("최상급 가상 스태프 ", (char)('가' + index)),
                    role,
                    StaffDefinition.MaximumQualityTier,
                    balance.GetQuality(StaffDefinition.MaximumQualityTier).SalaryBand,
                    StaffContractPreference.LongTerm,
                    new[] { balance.GetRole(role).Specialties[0] },
                    new[] { StaffPhilosophyTag.EvidenceBased });
            }
            IReadOnlyList<StaffMarketOffer> offers = new StaffMarketResolver().CreateOffers(
                new StaffCatalog(definitions),
                Array.Empty<StaffContractState>(),
                teamKey,
                "elite-offseason",
                StaffMarketKind.Offseason,
                LeagueGrade.Major,
                seed,
                balance);
            long salary = 0L;
            for (int index = 0; index < offers.Count; index++)
                salary = checked(salary + offers[index].AnnualSalary);
            return salary;
        }

        private static void ApplyRepresentativeWeeklyWorkload(
            TeamSeasonPlayerStatusState state,
            IRandomSource random,
            ConditionChemistryBalanceTable balance)
        {
            for (int index = 0; index < state.Players.Count; index++)
            {
                int cost;
                if (index < 9)
                {
                    int starts = 3 + NextInt(random, 4);
                    cost = checked(starts * balance.StartingHitterConditionCost);
                }
                else if (index < 14)
                {
                    cost = NextInt(random, 3) == 0 ? balance.StartingHitterConditionCost : 0;
                }
                else if (index < 19)
                {
                    int rotationUse = NextInt(random, 5) == index - 14 ? 3 + NextInt(random, 2) : 0;
                    cost = checked(rotationUse * balance.PitcherConditionCostPerThirtyPitches);
                }
                else
                {
                    cost = NextInt(random, 3) * balance.PitcherConditionCostPerThirtyPitches;
                }
                state.Players[index].ChangeCondition(-cost);
            }
        }

        private static BatterAttributes CreateStyleAttributes(int style)
        {
            switch (style)
            {
                case 0:
                    return new BatterAttributes(82, 38, 70, 78, 55, 55);
                case 1:
                    return new BatterAttributes(40, 84, 42, 40, 55, 55);
                default:
                    return new BatterAttributes(62, 62, 62, 62, 62, 62);
            }
        }

        private static PitcherAttributes CreatePitcherAttributes(IRandomSource random)
        {
            return new PitcherAttributes(
                30 + NextInt(random, 61),
                30 + NextInt(random, 61),
                30 + NextInt(random, 61),
                30 + NextInt(random, 61),
                30 + NextInt(random, 61),
                20 + NextInt(random, 81));
        }

        private static BatterAttributes CreateCatcherAttributes(IRandomSource random)
        {
            return new BatterAttributes(
                50,
                50,
                50,
                50,
                20 + NextInt(random, 81),
                20 + NextInt(random, 81));
        }

        private static ScoutingEvidenceStrength CreateEvidenceStrength(int sample, IRandomSource random)
        {
            switch (sample % 16)
            {
                case 0:
                    return new ScoutingEvidenceStrength(true, true, 1d, 1d, 1d);
                case 1:
                    return ScoutingEvidenceStrength.None;
                case 2:
                    return new ScoutingEvidenceStrength(true, false, 0.95d, 0.95d, 0.95d);
                case 3:
                    return new ScoutingEvidenceStrength(true, false, 0.75d, 0.80d, 0.80d);
                case 4:
                    return new ScoutingEvidenceStrength(true, false, 0.40d, 0.70d, 0.70d);
                default:
                    return new ScoutingEvidenceStrength(
                        true,
                        false,
                        0.10d + random.NextDouble() * 0.90d,
                        0.35d + random.NextDouble() * 0.65d,
                        0.35d + random.NextDouble() * 0.65d);
            }
        }

        private static void CountModifier(int modifier, ref int negative, ref int neutral, ref int positive)
        {
            if (modifier < 0) negative++;
            else if (modifier > 0) positive++;
            else neutral++;
        }

        private static int NextInt(IRandomSource random, int exclusiveMaximum)
        {
            return (int)(random.NextDouble() * exclusiveMaximum);
        }

        private static LeagueGrade GetValidationGrade(int index)
        {
            switch (index % 4)
            {
                case 0: return LeagueGrade.Rookie;
                case 1: return LeagueGrade.Major;
                case 2: return LeagueGrade.Classic;
                default: return LeagueGrade.Champion;
            }
        }

        private static void CreateRuntime(
            out ManagerHistoricalRuntimeState runtime,
            out IHistoricalContentProvider provider)
        {
            Type fixtureType = typeof(ManagerHistoricalSaveTests).GetNestedType(
                "Fixture",
                BindingFlags.NonPublic);
            MethodInfo create = fixtureType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public);
            object fixture = create.Invoke(null, new object[] { WorldRecordMode.SimulatedHistory, false });
            Type fixtureDataType = fixture.GetType();
            var state = (ManagerHistoricalRuntimeState)fixtureDataType
                .GetProperty("State", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            var adapter = (ManagerHistoricalSaveAdapter)fixtureDataType
                .GetMethod("CreateAdapter", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(fixture, null);
            provider = (IHistoricalContentProvider)fixtureDataType
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.Public)
                .GetValue(fixture);
            runtime = adapter.Restore(adapter.CreateSaveData(state));
        }

        private readonly struct RandomGameInputs : IEquatable<RandomGameInputs>
        {
            public RandomGameInputs(
                HomeGameOutcome outcome,
                double recentPerformance,
                double opponentAttraction,
                double seasonImportance,
                double rivalryStrength)
            {
                Outcome = outcome;
                RecentPerformance = recentPerformance;
                OpponentAttraction = opponentAttraction;
                SeasonImportance = seasonImportance;
                RivalryStrength = rivalryStrength;
            }

            public HomeGameOutcome Outcome { get; }
            public double RecentPerformance { get; }
            public double OpponentAttraction { get; }
            public double SeasonImportance { get; }
            public double RivalryStrength { get; }

            public bool Equals(RandomGameInputs other)
            {
                return Outcome == other.Outcome &&
                       RecentPerformance.Equals(other.RecentPerformance) &&
                       OpponentAttraction.Equals(other.OpponentAttraction) &&
                       SeasonImportance.Equals(other.SeasonImportance) &&
                       RivalryStrength.Equals(other.RivalryStrength);
            }

            public override bool Equals(object obj) => obj is RandomGameInputs other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (int)Outcome;
                    hashCode = (hashCode * 397) ^ RecentPerformance.GetHashCode();
                    hashCode = (hashCode * 397) ^ OpponentAttraction.GetHashCode();
                    hashCode = (hashCode * 397) ^ SeasonImportance.GetHashCode();
                    hashCode = (hashCode * 397) ^ RivalryStrength.GetHashCode();
                    return hashCode;
                }
            }
        }

        private sealed class RunningStatistics
        {
            private double _sumOfSquares;

            public int Count { get; private set; }
            public double Mean { get; private set; }
            public double Variance => Count < 2 ? 0d : _sumOfSquares / Count;

            public void Add(double value)
            {
                Count++;
                double delta = value - Mean;
                Mean += delta / Count;
                _sumOfSquares += delta * (value - Mean);
            }
        }

        private readonly struct StaffBundle
        {
            public StaffBundle(TeamStaffEffectProfile profile, long totalAnnualSalary)
            {
                Profile = profile;
                TotalAnnualSalary = totalAnnualSalary;
            }

            public TeamStaffEffectProfile Profile { get; }
            public long TotalAnnualSalary { get; }
        }

        private sealed class EconomyStatistics
        {
            public EconomyStatistics(
                int seedCount,
                int seasonCount,
                int homeGameCount,
                double averageAttendance,
                double attendanceVariance,
                double averageCapacityUsage,
                int maximumAttendance,
                int maximumCapacity,
                long homeGameRevenue,
                long moneyIncome,
                long moneyExpense,
                long scoutingPointProduction,
                long developmentPointProduction,
                double maximumAnnualNetGrowthRatio)
            {
                SeedCount = seedCount;
                SeasonCount = seasonCount;
                HomeGameCount = homeGameCount;
                AverageAttendance = averageAttendance;
                AttendanceVariance = attendanceVariance;
                AverageCapacityUsage = averageCapacityUsage;
                MaximumAttendance = maximumAttendance;
                MaximumCapacity = maximumCapacity;
                HomeGameRevenue = homeGameRevenue;
                MoneyIncome = moneyIncome;
                MoneyExpense = moneyExpense;
                ScoutingPointProduction = scoutingPointProduction;
                DevelopmentPointProduction = developmentPointProduction;
                MaximumAnnualNetGrowthRatio = maximumAnnualNetGrowthRatio;
            }

            public int SeedCount { get; }
            public int SeasonCount { get; }
            public int HomeGameCount { get; }
            public double AverageAttendance { get; }
            public double AttendanceVariance { get; }
            public double AverageCapacityUsage { get; }
            public int MaximumAttendance { get; }
            public int MaximumCapacity { get; }
            public long HomeGameRevenue { get; }
            public long MoneyIncome { get; }
            public long MoneyExpense { get; }
            public long ScoutingPointProduction { get; }
            public long DevelopmentPointProduction { get; }
            public double MaximumAnnualNetGrowthRatio { get; }
            public string Fingerprint => string.Join("|", SeedCount, SeasonCount, HomeGameCount,
                AverageAttendance.ToString("R"), AttendanceVariance.ToString("R"),
                AverageCapacityUsage.ToString("R"), MaximumAttendance, MaximumCapacity,
                HomeGameRevenue, MoneyIncome, MoneyExpense, ScoutingPointProduction,
                DevelopmentPointProduction, MaximumAnnualNetGrowthRatio.ToString("R"));

            public string Format()
            {
                return $"[Economy] Seed={SeedCount}, Season={SeasonCount}, HomeGame={HomeGameCount}, " +
                       $"AttendanceMean={AverageAttendance:F2}, AttendanceVariance={AttendanceVariance:F2}, " +
                       $"CapacityUsage={AverageCapacityUsage:P2}, HomeRevenue={HomeGameRevenue}, " +
                       $"MoneyIncome={MoneyIncome}, MoneyExpense={MoneyExpense}, " +
                       $"SP={ScoutingPointProduction}, DP={DevelopmentPointProduction}, " +
                       $"MaxAnnualNetGrowth={MaximumAnnualNetGrowthRatio:F3}x";
            }
        }

        private sealed class FacilityRoiStatistics
        {
            public FacilityRoiStatistics(
                long incrementalRevenue,
                long incrementalOperatingCost,
                long upgradeCost,
                double returnOnInvestment,
                double paybackSeasons)
            {
                IncrementalRevenue = incrementalRevenue;
                IncrementalOperatingCost = incrementalOperatingCost;
                UpgradeCost = upgradeCost;
                ReturnOnInvestment = returnOnInvestment;
                PaybackSeasons = paybackSeasons;
            }

            public long IncrementalRevenue { get; }
            public long IncrementalOperatingCost { get; }
            public long UpgradeCost { get; }
            public double ReturnOnInvestment { get; }
            public double PaybackSeasons { get; }

            public string Format() =>
                $"[FacilityROI] IncrementalRevenue={IncrementalRevenue}, " +
                $"IncrementalOperatingCost={IncrementalOperatingCost}, UpgradeCost={UpgradeCost}, " +
                $"ROI={ReturnOnInvestment:F3}, PaybackSeasons={PaybackSeasons:F2}";
        }

        private sealed class StaffSalaryStatistics
        {
            public StaffSalaryStatistics(
                long totalSalary,
                long eliteTotalSalary,
                long homeRevenue,
                double ratio,
                double eliteRatio)
            {
                TotalSalary = totalSalary;
                EliteTotalSalary = eliteTotalSalary;
                HomeRevenue = homeRevenue;
                AverageSalaryToHomeRevenueRatio = ratio;
                EliteSalaryToHomeRevenueRatio = eliteRatio;
            }

            public long TotalSalary { get; }
            public long EliteTotalSalary { get; }
            public long HomeRevenue { get; }
            public double AverageSalaryToHomeRevenueRatio { get; }
            public double EliteSalaryToHomeRevenueRatio { get; }

            public string Format() =>
                $"[StaffSalary] AverageSalary={TotalSalary}, EliteSalary={EliteTotalSalary}, " +
                $"HomeRevenue={HomeRevenue}, AverageRatio={AverageSalaryToHomeRevenueRatio:P2}, " +
                $"EliteRatio={EliteSalaryToHomeRevenueRatio:P2}, " +
                $"Gate={MinimumMeaningfulStaffSalaryRatio:P1}~{MaximumMeaningfulStaffSalaryRatio:P0}";
        }

        private sealed class TicketStrategyStatistics
        {
            private readonly int[] _wins;

            public TicketStrategyStatistics(int cases, int premiumWins, int nonPremiumWins, int[] wins)
            {
                StrategyCaseCount = cases;
                PremiumWinCount = premiumWins;
                NonPremiumWinCount = nonPremiumWins;
                _wins = wins;
            }

            public int StrategyCaseCount { get; }
            public int PremiumWinCount { get; }
            public int NonPremiumWinCount { get; }
            public double PremiumWinRate => PremiumWinCount / (double)StrategyCaseCount;

            public string Format() =>
                $"[TicketStrategy] Cases={StrategyCaseCount}, CheapWins={_wins[(int)TicketPriceTier.Cheap]}, " +
                $"StandardWins={_wins[(int)TicketPriceTier.Standard]}, " +
                $"PremiumWins={_wins[(int)TicketPriceTier.Premium]}";
        }

        private sealed class CrossSystemStatistics
        {
            public CrossSystemStatistics(
                int conditionSampleCount,
                double averageCondition,
                double conditionAtFloorRate,
                double conditionAtCeilingRate,
                int resolvedRecovery,
                int actualSingleRecovery,
                int doubledFacilityRecovery,
                int intelSampleCount,
                double averageIntelConfidence,
                int intelUnknownCount,
                int intelLowCount,
                int intelEstimatedCount,
                int intelHighCount,
                int intelConfirmedCount,
                int lineupNegativeCount,
                int lineupNeutralCount,
                int lineupPositiveCount,
                int batteryNegativeCount,
                int batteryNeutralCount,
                int batteryPositiveCount,
                int minimumChemistryModifier,
                int maximumChemistryModifier,
                int conditionLevelStep)
            {
                ConditionSampleCount = conditionSampleCount;
                AverageCondition = averageCondition;
                ConditionAtFloorRate = conditionAtFloorRate;
                ConditionAtCeilingRate = conditionAtCeilingRate;
                ResolvedRecovery = resolvedRecovery;
                ActualSingleRecovery = actualSingleRecovery;
                DoubledFacilityRecovery = doubledFacilityRecovery;
                IntelSampleCount = intelSampleCount;
                AverageIntelConfidence = averageIntelConfidence;
                IntelUnknownCount = intelUnknownCount;
                IntelLowCount = intelLowCount;
                IntelEstimatedCount = intelEstimatedCount;
                IntelHighCount = intelHighCount;
                IntelConfirmedCount = intelConfirmedCount;
                LineupNegativeCount = lineupNegativeCount;
                LineupNeutralCount = lineupNeutralCount;
                LineupPositiveCount = lineupPositiveCount;
                BatteryNegativeCount = batteryNegativeCount;
                BatteryNeutralCount = batteryNeutralCount;
                BatteryPositiveCount = batteryPositiveCount;
                MinimumChemistryModifier = minimumChemistryModifier;
                MaximumChemistryModifier = maximumChemistryModifier;
                ConditionLevelStep = conditionLevelStep;
            }

            public int ConditionSampleCount { get; }
            public double AverageCondition { get; }
            public double ConditionAtFloorRate { get; }
            public double ConditionAtCeilingRate { get; }
            public int ResolvedRecovery { get; }
            public int ActualSingleRecovery { get; }
            public int DoubledFacilityRecovery { get; }
            public int IntelSampleCount { get; }
            public double AverageIntelConfidence { get; }
            public int IntelUnknownCount { get; }
            public int IntelLowCount { get; }
            public int IntelEstimatedCount { get; }
            public int IntelHighCount { get; }
            public int IntelConfirmedCount { get; }
            public int LineupNegativeCount { get; }
            public int LineupNeutralCount { get; }
            public int LineupPositiveCount { get; }
            public int BatteryNegativeCount { get; }
            public int BatteryNeutralCount { get; }
            public int BatteryPositiveCount { get; }
            public int MinimumChemistryModifier { get; }
            public int MaximumChemistryModifier { get; }
            public int ConditionLevelStep { get; }
            public string Fingerprint => string.Join("|", ConditionSampleCount,
                AverageCondition.ToString("R"), ConditionAtFloorRate.ToString("R"),
                ConditionAtCeilingRate.ToString("R"), ResolvedRecovery, ActualSingleRecovery,
                DoubledFacilityRecovery, IntelSampleCount, AverageIntelConfidence.ToString("R"),
                IntelUnknownCount, IntelLowCount, IntelEstimatedCount, IntelHighCount,
                IntelConfirmedCount, LineupNegativeCount, LineupNeutralCount, LineupPositiveCount,
                BatteryNegativeCount, BatteryNeutralCount, BatteryPositiveCount,
                MinimumChemistryModifier, MaximumChemistryModifier, ConditionLevelStep);

            public string Format()
            {
                var builder = new StringBuilder();
                builder.Append("[Condition] Samples=").Append(ConditionSampleCount)
                    .Append(", Mean=").Append(AverageCondition.ToString("F2"))
                    .Append(", Floor=").Append(ConditionAtFloorRate.ToString("P2"))
                    .Append(", Ceiling=").Append(ConditionAtCeilingRate.ToString("P2"))
                    .Append(", Recovery=").Append(ResolvedRecovery)
                    .Append("\n[Intel] Samples=").Append(IntelSampleCount)
                    .Append(", Mean=").Append(AverageIntelConfidence.ToString("F3"))
                    .Append(", U/L/E/H/C=").Append(IntelUnknownCount).Append('/')
                    .Append(IntelLowCount).Append('/').Append(IntelEstimatedCount).Append('/')
                    .Append(IntelHighCount).Append('/').Append(IntelConfirmedCount)
                    .Append("\n[Chemistry] Lineup -/0/+=").Append(LineupNegativeCount).Append('/')
                    .Append(LineupNeutralCount).Append('/').Append(LineupPositiveCount)
                    .Append(", Battery -/0/+=").Append(BatteryNegativeCount).Append('/')
                    .Append(BatteryNeutralCount).Append('/').Append(BatteryPositiveCount)
                    .Append(", Modifier=").Append(MinimumChemistryModifier).Append("..").Append(MaximumChemistryModifier);
                return builder.ToString();
            }
        }

        private sealed class ProductionPathStatistics
        {
            public ProductionPathStatistics(
                int matchCount,
                int preparedGameCount,
                int homeGameCount,
                int maximumHomeAttendance,
                int stadiumCapacity,
                int weeklyAdvanceCount,
                int totalRecovery,
                long homeRevenue,
                double finalConditionAverage,
                int finalConditionMinimum,
                int finalConditionMaximum,
                int familiarityPairCount,
                double averageIntelConfidence)
            {
                MatchCount = matchCount;
                PreparedGameCount = preparedGameCount;
                HomeGameCount = homeGameCount;
                MaximumHomeAttendance = maximumHomeAttendance;
                StadiumCapacity = stadiumCapacity;
                WeeklyAdvanceCount = weeklyAdvanceCount;
                TotalRecovery = totalRecovery;
                HomeRevenue = homeRevenue;
                FinalConditionAverage = finalConditionAverage;
                FinalConditionMinimum = finalConditionMinimum;
                FinalConditionMaximum = finalConditionMaximum;
                FamiliarityPairCount = familiarityPairCount;
                AverageIntelConfidence = averageIntelConfidence;
            }

            public int MatchCount { get; }
            public int PreparedGameCount { get; }
            public int HomeGameCount { get; }
            public int MaximumHomeAttendance { get; }
            public int StadiumCapacity { get; }
            public int WeeklyAdvanceCount { get; }
            public int TotalRecovery { get; }
            public long HomeRevenue { get; }
            public double FinalConditionAverage { get; }
            public int FinalConditionMinimum { get; }
            public int FinalConditionMaximum { get; }
            public int FamiliarityPairCount { get; }
            public double AverageIntelConfidence { get; }
            public string Fingerprint => string.Join("|", MatchCount, PreparedGameCount, HomeGameCount,
                MaximumHomeAttendance, StadiumCapacity, WeeklyAdvanceCount, TotalRecovery, HomeRevenue,
                FinalConditionAverage.ToString("R"), FinalConditionMinimum, FinalConditionMaximum,
                FamiliarityPairCount, AverageIntelConfidence.ToString("R"));

            public string Format() =>
                $"[Production] Match={MatchCount}, Pregame={PreparedGameCount}, HomeGame={HomeGameCount}, " +
                $"MaxAttendance={MaximumHomeAttendance}/{StadiumCapacity}, Weekly={WeeklyAdvanceCount}, " +
                $"Recovery={TotalRecovery}, HomeNetRevenue={HomeRevenue}, " +
                $"ConditionMean/Min/Max={FinalConditionAverage:F2}/{FinalConditionMinimum}/{FinalConditionMaximum}, " +
                $"FamiliarityPairs={FamiliarityPairCount}, IntelMean={AverageIntelConfidence:F3}";
        }
    }
}
