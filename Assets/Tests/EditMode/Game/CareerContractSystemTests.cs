using Baseball.Core.Growth;
using Baseball.Core.Players;
using Baseball.Game.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    /// <summary>
    /// 계약 화면의 상여 진행·실제 지급·시장 가치가 같은 커리어 원본을 사용하는지 검증한다.
    /// </summary>
    public sealed class CareerContractSystemTests
    {
        [Test]
        public void ContractBonus_80경기타자계약은30경기출장을목표로한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateBatterCareer(configuration, 10101UL);
            var service = new ContractBonusService(configuration.Balance.ContractBonus);

            ContractBonusClause[] clauses = service.BuildClauses(
                career.MyPlayer.PrimaryPosition,
                career.CurrentContract.AnnualSalary,
                configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam);

            Assert.That(clauses[0].Metric, Is.EqualTo(ContractBonusMetric.GamesPlayed));
            Assert.That(clauses[0].TargetValue, Is.EqualTo(30d));
            Assert.That(clauses.Length, Is.EqualTo(6));
        }

        [Test]
        public void ContractBonus_달성한조건만시즌결산Money에한번지급한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateBatterCareer(configuration, 20202UL);
            RecordQualifiedBatterSeason(career.League.CurrentSeason.PlayerStatistics);
            var bonusService = new ContractBonusService(configuration.Balance.ContractBonus);
            ContractBonusProgress[] beforeSettlement = bonusService.Evaluate(
                career,
                configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam);
            long expectedBonus = 0L;
            int completedCount = 0;
            for (int index = 0; index < beforeSettlement.Length; index++)
            {
                if (!beforeSettlement[index].IsCompleted)
                    continue;
                expectedBonus += beforeSettlement[index].Clause.Reward;
                completedCount++;
            }
            Assert.That(completedCount, Is.EqualTo(4));

            career.League.CurrentSeason.CompleteRegularSeason();
            long moneyBefore = career.AvailableMoney;
            long salary = career.CurrentContract.AnnualSalary;
            SeasonGrowthSettlementResult result = new CareerGrowthService(career, configuration.Balance)
                .SettleSeasonAndBeginOffseason(CreateBatterUsage());

            Assert.That(result.BonusIncome, Is.EqualTo(expectedBonus));
            Assert.That(career.AvailableMoney, Is.EqualTo(moneyBefore + salary + expectedBonus));
            Assert.That(career.League.CurrentSeason.Settlement.IsApplied, Is.True);
        }

        [Test]
        public void ContractBonus_무등판투수는Era조건을달성하지않는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreatePitcherCareer(configuration, 30303UL);
            ContractBonusProgress[] progress = new ContractBonusService(configuration.Balance.ContractBonus)
                .Evaluate(career, configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam);

            ContractBonusProgress era = default;
            for (int index = 0; index < progress.Length; index++)
            {
                if (progress[index].Clause.Metric == ContractBonusMetric.EarnedRunAverage)
                    era = progress[index];
            }
            Assert.That(era.HasSample, Is.False);
            Assert.That(era.IsCompleted, Is.False);
            Assert.That(era.NormalizedProgress, Is.Zero);
        }

        [Test]
        public void ContractBonus_Era진행률은최소이닝을채우기전에완료로표시하지않는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreatePitcherCareer(configuration, 31313UL);
            career.League.CurrentSeason.PlayerStatistics.RecordPitching(
                started: true,
                outsRecorded: 3,
                hitsAllowed: 0,
                earnedRuns: 0,
                walksAllowed: 0,
                strikeouts: 2);

            ContractBonusProgress[] progress = new ContractBonusService(configuration.Balance.ContractBonus)
                .Evaluate(career, configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam);
            ContractBonusProgress era = default;
            for (int index = 0; index < progress.Length; index++)
            {
                if (progress[index].Clause.Metric == ContractBonusMetric.EarnedRunAverage)
                    era = progress[index];
            }

            Assert.That(era.HasSample, Is.True);
            Assert.That(era.IsCompleted, Is.False);
            Assert.That(era.NormalizedProgress, Is.EqualTo(3d / 135d).Within(0.000001d));
        }

        [Test]
        public void ContractView_같은상태는같은시장범위와상여진행을보여준다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateBatterCareer(configuration, 40404UL);
            RecordQualifiedBatterSeason(career.League.CurrentSeason.PlayerStatistics);
            var builder = new CareerContractViewBuilder(career, configuration.Balance);

            CareerContractView first = builder.Build(null, string.Empty);
            CareerContractView second = builder.Build(null, string.Empty);

            Assert.That(second.MarketSalaryMinimum, Is.EqualTo(first.MarketSalaryMinimum));
            Assert.That(second.MarketSalaryMaximum, Is.EqualTo(first.MarketSalaryMaximum));
            Assert.That(second.MarketOfferCount, Is.EqualTo(first.MarketOfferCount));
            Assert.That(second.AchievedBonus, Is.EqualTo(first.AchievedBonus));
            Assert.That(first.CurrentContract.RemainingSeasons, Is.EqualTo(2));
            Assert.That(first.NegotiationStatus, Is.EqualTo(ContractNegotiationStatus.Active));
        }

        [Test]
        public void ContractRenewal_서명하면계약금이Money원장에한번지급된다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState generated = CreateBatterCareer(configuration, 41414UL);
            var expiringContract = new PlayerContractState(
                NewGameFlow.CurrentSaveVersion,
                generated.CurrentContract.TeamId,
                generated.League.CurrentSeason.Year,
                contractYears: 1,
                generated.CurrentContract.SigningBonus,
                generated.CurrentContract.AnnualSalary,
                generated.CurrentContract.ExpectedRole);
            var career = new CareerState(
                NewGameFlow.CurrentSaveVersion,
                generated.MyPlayer,
                generated.League,
                expiringContract,
                generated.AvailableMoney);
            career.League.CurrentSeason.CompleteRegularSeason();
            new CareerGrowthService(career, configuration.Balance)
                .SettleSeasonAndBeginOffseason(CreateBatterUsage());

            var transition = new CareerSeasonTransitionService(career, configuration.Balance);
            Assert.That(transition.BeginTransition(), Is.EqualTo(SeasonTransitionStep.ContractOffers));
            transition.SelectRenewalOffer(transition.RenewalOffers[0].Team.TeamId);
            long signingBonus = transition.SelectedOffer.Value.SigningBonus;
            long moneyBefore = career.AvailableMoney;

            transition.SignSelectedOffer();

            Assert.That(career.AvailableMoney, Is.EqualTo(moneyBefore + signingBonus));
            Assert.That(career.Economy.Transactions[^1].Type, Is.EqualTo(MoneyTransactionType.ContractIncome));
            Assert.That(career.Economy.Transactions[^1].Amount, Is.EqualTo(signingBonus));
        }

        [Test]
        [Timeout(120000)]
        public void ContractBonus_45개시즌에서역할별상여분포를집계한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            int batterCompleted = 0;
            int starterCompleted = 0;
            int relieverCompleted = 0;
            long totalRewards = 0L;
            long totalSalary = 0L;

            for (int index = 0; index < 15; index++)
            {
                SimulateBonusSeason(configuration, PlayerPosition.Shortstop, (ulong)(51_000 + index),
                    ref batterCompleted, ref totalRewards, ref totalSalary);
                SimulateBonusSeason(configuration, PlayerPosition.StartingPitcher, (ulong)(52_000 + index),
                    ref starterCompleted, ref totalRewards, ref totalSalary);
                SimulateBonusSeason(configuration, PlayerPosition.ReliefPitcher, (ulong)(53_000 + index),
                    ref relieverCompleted, ref totalRewards, ref totalSalary);
            }

            double rewardToSalary = totalRewards / (double)totalSalary;
            TestContext.WriteLine(
                $"45시즌 계약 상여: 타자 {batterCompleted}건 / SP {starterCompleted}건 / " +
                $"RP {relieverCompleted}건 / 연봉 대비 달성액 {rewardToSalary:P1}");
            Assert.That(batterCompleted + starterCompleted + relieverCompleted, Is.GreaterThan(0));
            Assert.That(rewardToSalary, Is.InRange(0.001d, 0.35d));
        }

        private static CareerState CreateBatterCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("계약 타자", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            CompleteNewGame(flow);
            return flow.Career;
        }

        private static CareerState CreatePitcherCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("계약 투수", "대한민국");
            flow.SelectPlayerType(PlayerType.Pitcher);
            flow.SelectPosition(PlayerPosition.StartingPitcher);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitPitcherAttributes(new PitcherAttributes(55, 52, 50, 50, 52, 48));
            CompleteNewGame(flow);
            return flow.Career;
        }

        private static void CompleteNewGame(NewGameFlow flow)
        {
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
        }

        private static void SimulateBonusSeason(
            NewGameConfiguration configuration,
            PlayerPosition position,
            ulong seed,
            ref int completedCount,
            ref long totalRewards,
            ref long totalSalary)
        {
            CareerState career = position is PlayerPosition.StartingPitcher or PlayerPosition.ReliefPitcher
                ? CreatePitcherCareer(configuration, seed, position)
                : CreateBatterCareer(configuration, seed);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            while (seasonService.NextPlayerGame != null)
                seasonService.AdvanceNextRound();

            ContractBonusProgress[] progress = new ContractBonusService(configuration.Balance.ContractBonus)
                .Evaluate(career, configuration.Balance.CareerSeason.RegularSeasonGamesPerTeam);
            for (int index = 0; index < progress.Length; index++)
            {
                if (!progress[index].IsCompleted)
                    continue;
                completedCount++;
                totalRewards += progress[index].Clause.Reward;
            }
            totalSalary += career.CurrentContract.AnnualSalary;
        }

        private static CareerState CreatePitcherCareer(
            NewGameConfiguration configuration,
            ulong seed,
            PlayerPosition position)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("계약 투수", "대한민국");
            flow.SelectPlayerType(PlayerType.Pitcher);
            flow.SelectPosition(position);
            flow.SelectHandedness(Handedness.Right, Handedness.Right);
            flow.SubmitPitcherAttributes(new PitcherAttributes(55, 52, 50, 50, 52, 48));
            CompleteNewGame(flow);
            return flow.Career;
        }

        private static void RecordQualifiedBatterSeason(PlayerSeasonStatisticsState statistics)
        {
            for (int game = 0; game < 48; game++)
            {
                bool isHomeRun = game < 12;
                bool isDouble = game >= 12 && game < 36;
                statistics.RecordTeamGame();
                statistics.RecordBatting(
                    started: true,
                    plateAppearances: 4,
                    atBats: 4,
                    runs: isHomeRun ? 1 : 0,
                    hits: 1,
                    doubles: isDouble ? 1 : 0,
                    triples: 0,
                    homeRuns: isHomeRun ? 1 : 0,
                    runsBattedIn: 1,
                    walks: 0,
                    strikeouts: 1);
            }
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
    }
}
