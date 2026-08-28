using Baseball.Core.Players;
using Baseball.Core.Teams;
using Baseball.Game.Career;
using Baseball.Simulation.Career;
using NUnit.Framework;

namespace Baseball.Tests.EditMode.Game
{
    public sealed class CareerMovementServiceTests
    {
        [Test]
        public void ExecuteTrade_계약을승계하고상대구단경쟁자를원소속구단으로보낸다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(configuration, 91234UL);
            int previousTeamId = career.MyPlayer.CurrentTeamId;
            int targetTeamId = FindOtherTeam(career, previousTeamId).TeamId;
            int previousRosterCount = GetTeam(career, previousTeamId).RosterCompetitors.Count;
            int targetRosterCount = GetTeam(career, targetTeamId).RosterCompetitors.Count;
            long salary = career.CurrentContract.AnnualSalary;
            ExpectedRole promisedRole = career.CurrentContract.ExpectedRole;

            TradeExecutionResult result = new PlayerMovementService(career, configuration.Balance)
                .ExecuteTrade(targetTeamId, ExpectedRole.StartingCompetition, gameIndex: 12);

            Assert.That(career.MyPlayer.CurrentTeamId, Is.EqualTo(targetTeamId));
            Assert.That(career.CurrentContract.TeamId, Is.EqualTo(targetTeamId));
            Assert.That(career.CurrentContract.SigningTeamId, Is.EqualTo(previousTeamId));
            Assert.That(career.CurrentContract.AnnualSalary, Is.EqualTo(salary));
            Assert.That(career.CurrentContract.ExpectedRole, Is.EqualTo(promisedRole));
            Assert.That(career.CurrentExpectedRole, Is.EqualTo(ExpectedRole.StartingCompetition));
            Assert.That(GetTeam(career, previousTeamId).RosterCompetitors.Count,
                Is.EqualTo(previousRosterCount + 1));
            Assert.That(GetTeam(career, targetTeamId).RosterCompetitors.Count,
                Is.EqualTo(targetRosterCount - 1));
            Assert.That(career.TradeState.History.Count, Is.EqualTo(1));
            Assert.That(result.ExchangedPlayerId, Is.GreaterThan(0));
            PlayerContractState exchangedContract = FindActiveContract(career, result.ExchangedPlayerId);
            Assert.That(exchangedContract, Is.Not.Null);
            Assert.That(exchangedContract.TeamId, Is.EqualTo(previousTeamId));
            Assert.That(career.CurrentLeague.CurrentSeason.LeagueStatistics.RegularSeason
                .GetPlayer(result.ExchangedPlayerId).TeamId, Is.EqualTo(previousTeamId));
        }

        private static PlayerContractState FindActiveContract(CareerState career, int playerId)
        {
            for (int index = 0; index < career.World.Contracts.Count; index++)
            {
                PlayerContractState contract = career.World.Contracts[index];
                if (contract.PlayerId == playerId && contract.IsActive)
                    return contract;
            }
            return null;
        }

        [Test]
        public void ExecuteTrade_시즌합계와구단별분할기록을동시에유지한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(configuration, 92345UL);
            int previousTeamId = career.MyPlayer.CurrentTeamId;
            var firstSeasonService = new CareerSeasonService(career, configuration.Balance);
            firstSeasonService.AdvanceNextRound();
            int targetTeamId = FindOtherTeam(career, previousTeamId).TeamId;
            new PlayerMovementService(career, configuration.Balance)
                .ExecuteTrade(targetTeamId, ExpectedRole.StartingCompetition, gameIndex: 1);
            new CareerSeasonService(career, configuration.Balance).AdvanceNextRound();

            PlayerCompetitionStatisticsState statistics = career.CurrentLeague.CurrentSeason
                .LeagueStatistics.RegularSeason.GetPlayer(career.MyPlayer.PlayerId);

            Assert.That(statistics.GetTeamSplit(previousTeamId), Is.Not.Null);
            Assert.That(statistics.GetTeamSplit(targetTeamId), Is.Not.Null);
            int splitPlateAppearances = statistics.GetTeamSplit(previousTeamId).Batting.PlateAppearances +
                                        statistics.GetTeamSplit(targetTeamId).Batting.PlateAppearances;
            Assert.That(splitPlateAppearances, Is.EqualTo(statistics.Batting.PlateAppearances));
        }

        [Test]
        public void CareerRecords_트레이드이력과구단별분할기록을함께표시한다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(configuration, 92567UL);
            int previousTeamId = career.MyPlayer.CurrentTeamId;
            new CareerSeasonService(career, configuration.Balance).AdvanceNextRound();
            int targetTeamId = FindOtherTeam(career, previousTeamId).TeamId;
            new PlayerMovementService(career, configuration.Balance)
                .ExecuteTrade(targetTeamId, ExpectedRole.StartingCompetition, gameIndex: 1);
            new CareerSeasonService(career, configuration.Balance).AdvanceNextRound();

            CareerRecordsView view = new CareerRecordsService()
                .Build(career, CareerRecordCategory.Batting);

            Assert.That(view.TradeHistory.Length, Is.EqualTo(1));
            Assert.That(view.TradeHistory[0].PreviousTeamName, Is.Not.Empty);
            Assert.That(view.TradeHistory[0].NewTeamName, Is.Not.Empty);
            Assert.That(view.TeamSplits.Length, Is.EqualTo(2));
            Assert.That(view.TeamSplits[0].Metrics.Length, Is.EqualTo(view.LeaderboardColumns.Length));
        }

        [Test]
        public void ContractExtension_중반기핵심선수는기존계약뒤에연장기간을붙인다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState generated = CreateCareer(configuration, 92876UL);
            int seasonYear = generated.CurrentLeague.CurrentSeason.Year;
            var expiringContract = new PlayerContractState(
                NewGameFlow.CurrentSaveVersion,
                generated.MyPlayer.CurrentTeamId,
                seasonYear,
                contractYears: 1,
                signingBonus: 0L,
                annualSalary: 1L,
                ExpectedRole.StartingCompetition);
            var career = new CareerState(
                NewGameFlow.CurrentSaveVersion,
                generated.MyPlayer,
                generated.CurrentLeague,
                expiringContract,
                generated.AvailableMoney);
            var seasonService = new CareerSeasonService(career, configuration.Balance);
            for (int game = 0; game < configuration.Balance.ContractRenewal.ExtensionStartGame; game++)
                seasonService.AdvanceNextRound();
            career.MyPlayer.InitializeSeasonStatus(condition: 90, managerEvaluation: 80);
            var renewalService = new ContractRenewalService(career, configuration.Balance);

            ContractOffer offer = renewalService.BuildExtensionOffer().Value;
            int previousContractCount = career.ContractHistory.Count;
            long moneyBefore = career.AvailableMoney;
            PlayerContractState extended = renewalService.AcceptExtension();

            Assert.That(offer.Channel, Is.EqualTo(ContractOfferChannel.CurrentTeamExtension));
            Assert.That(extended.EndYear, Is.EqualTo(seasonYear + offer.ContractYears));
            Assert.That(career.ContractHistory.Count, Is.EqualTo(previousContractCount + 1));
            Assert.That(career.AvailableMoney, Is.EqualTo(moneyBefore + offer.SigningBonus));
            Assert.That(renewalService.BuildExtensionOffer().HasValue, Is.False);
        }

        [Test]
        [Timeout(120000)]
        public void TradeMarket_첫40경기에는관심과트레이드가발생하지않는다()
        {
            NewGameConfiguration configuration = NewGameConfiguration.CreateDefault();
            CareerState career = CreateCareer(configuration, 93456UL);
            career.TradeState.SetPreference(TradePreference.RequestTrade);
            var seasonService = new CareerSeasonService(career, configuration.Balance);

            for (int index = 0; index < 40; index++)
                seasonService.AdvanceNextRound();

            Assert.That(career.TradeState.Interests.Count, Is.Zero);
            Assert.That(career.TradeState.History.Count, Is.Zero);
            Assert.That(career.MyPlayer.CurrentTeamId, Is.EqualTo(career.CurrentContract.TeamId));
        }

        private static CareerState CreateCareer(NewGameConfiguration configuration, ulong seed)
        {
            var flow = new NewGameFlow(configuration, seed);
            flow.SubmitIdentity("이동 테스트", "대한민국");
            flow.SelectPlayerType(PlayerType.Batter);
            flow.SelectPosition(PlayerPosition.Shortstop);
            flow.SelectHandedness(Handedness.Left, Handedness.Right);
            flow.SubmitBatterAttributes(new BatterAttributes(55, 50, 52, 43, 60, 52));
            flow.GenerateOffers();
            flow.SelectOffer(flow.State.SetupResult.Offers[0].Team.TeamId);
            flow.SignSelectedOffer();
            flow.StartRookieSeason();
            return flow.Career;
        }

        private static TeamState FindOtherTeam(CareerState career, int currentTeamId)
        {
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                TeamState team = career.CurrentLeague.Teams[index];
                if (team.TeamId != currentTeamId)
                    return team;
            }
            throw new System.InvalidOperationException("다른 구단이 없습니다.");
        }

        private static TeamState GetTeam(CareerState career, int teamId)
        {
            for (int index = 0; index < career.CurrentLeague.Teams.Count; index++)
            {
                if (career.CurrentLeague.Teams[index].TeamId == teamId)
                    return career.CurrentLeague.Teams[index];
            }
            throw new System.InvalidOperationException("구단이 없습니다.");
        }
    }
}
